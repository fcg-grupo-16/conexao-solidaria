using System.Text.Json;
using Campanhas.Domain;
using Campanhas.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Campanhas.Infrastructure.Painel;

/// <summary>Linha do painel público: o que o PDF pede (título, meta e arrecadado) e o necessário para exibir.</summary>
/// <param name="Id">Identificador (usado pelo doador para doar).</param>
/// <param name="Titulo">Título.</param>
/// <param name="MetaFinanceira">Meta em reais.</param>
/// <param name="ValorArrecadado">Soma das doações processadas pelo worker.</param>
/// <param name="PercentualAtingido">Arrecadado ÷ meta, em %, com duas casas.</param>
/// <param name="DataFim">Fim da arrecadação (UTC).</param>
public sealed record CampanhaPublica(
    Guid Id,
    string Titulo,
    decimal MetaFinanceira,
    decimal ValorArrecadado,
    decimal PercentualAtingido,
    DateTimeOffset DataFim);

/// <summary>Resultado da consulta, com a origem (para o header de diagnóstico).</summary>
/// <param name="Campanhas">Campanhas ativas.</param>
/// <param name="DoCache">True se veio do Redis.</param>
public sealed record ConsultaDoPainel(IReadOnlyList<CampanhaPublica> Campanhas, bool DoCache);

/// <summary>Opções do cache do painel.</summary>
public sealed class OpcoesDoPainel
{
    /// <summary>Seção de configuração.</summary>
    public const string Secao = "Painel";

    /// <summary>
    /// Validade do cache. É só a rede de segurança: o worker invalida a chave a cada doação processada
    /// e a API a cada criação/edição de campanha.
    /// </summary>
    public int SegundosDeCache { get; set; } = 30;
}

/// <summary>
/// Painel de transparência: campanhas com status Ativa, lidas do Redis (cache-aside) ou do PostgreSQL.
/// Queda do Redis não derruba o painel: a consulta cai para o banco (ADR 0002).
/// </summary>
public sealed class PainelDeTransparencia(
    CampanhasDbContext banco,
    IDistributedCache cache,
    IOptions<OpcoesDoPainel> opcoes,
    ILogger<PainelDeTransparencia> log)
{
    /// <summary>Chave do painel no Redis. Compartilhada com o invalidador do worker.</summary>
    public const string ChaveDoCache = "conexao-solidaria:painel:campanhas-ativas:v1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Campanhas ativas para o painel público.</summary>
    public async Task<ConsultaDoPainel> ObterAsync(CancellationToken cancelamento)
    {
        var emCache = await LerDoCacheAsync(cancelamento);
        if (emCache is not null)
        {
            return new ConsultaDoPainel(emCache, DoCache: true);
        }

        var campanhas = await banco.Campanhas
            .AsNoTracking()
            .Where(c => c.Status == StatusCampanha.Ativa)
            .OrderBy(c => c.DataFim)
            .Select(c => new CampanhaPublica(
                c.Id,
                c.Titulo,
                c.MetaFinanceira,
                c.ValorArrecadado,
                Math.Round(c.ValorArrecadado / c.MetaFinanceira * 100, 2),
                c.DataFim))
            .ToListAsync(cancelamento);

        await GravarNoCacheAsync(campanhas, cancelamento);
        return new ConsultaDoPainel(campanhas, DoCache: false);
    }

    private async Task<List<CampanhaPublica>?> LerDoCacheAsync(CancellationToken cancelamento)
    {
        try
        {
            var bytes = await cache.GetAsync(ChaveDoCache, cancelamento);
            return bytes is null ? null : JsonSerializer.Deserialize<List<CampanhaPublica>>(bytes, Json);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            log.LogWarning("Cache do painel indisponível; lendo do banco ({Erro})", erro.GetType().Name);
            return null;
        }
    }

    private async Task GravarNoCacheAsync(List<CampanhaPublica> campanhas, CancellationToken cancelamento)
    {
        try
        {
            await cache.SetAsync(
                ChaveDoCache,
                JsonSerializer.SerializeToUtf8Bytes(campanhas, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(opcoes.Value.SegundosDeCache) },
                cancelamento);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            log.LogWarning("Não foi possível gravar o painel no cache ({Erro})", erro.GetType().Name);
        }
    }
}
