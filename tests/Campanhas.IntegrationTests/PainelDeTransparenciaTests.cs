using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Campanhas.Infrastructure.Painel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Campanhas.IntegrationTests;

/// <summary>Coleção sem paralelismo: os testes conferem HIT/MISS do cache, que outras classes invalidam.</summary>
[CollectionDefinition(nameof(PainelSemParalelismo), DisableParallelization = true)]
public sealed class PainelSemParalelismo;

[Collection(nameof(PainelSemParalelismo))]
public class PainelDeTransparenciaTests(CampanhasApiFactory api)
{
    private const string Rota = "/api/transparencia/campanhas";

    private readonly HttpClient _gestor = api.ClienteComo("GestorONG");
    private readonly HttpClient _publico = api.CreateClient();

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private async Task<Guid> CriarAsync(string titulo, string status = "Ativa", decimal meta = 1000m)
    {
        var resposta = await _gestor.PostAsJsonAsync("/api/campanhas", new
        {
            titulo,
            descricao = "Painel.",
            dataInicio = DateTimeOffset.UtcNow.AddDays(-1),
            dataFim = DateTimeOffset.UtcNow.AddDays(15),
            metaFinanceira = meta,
            status,
        }, Cancelamento);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("id").GetGuid();
    }

    private async Task<(JsonElement Corpo, string Cache)> PainelAsync()
    {
        var resposta = await _publico.GetAsync(Rota, Cancelamento);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento), resposta.Headers.GetValues("X-Cache").Single());
    }

    private static JsonElement? Campanha(JsonElement painel, Guid id) =>
        painel.EnumerateArray().Cast<JsonElement?>().FirstOrDefault(c => c!.Value.GetProperty("id").GetGuid() == id);

    [Fact]
    public async Task Painel_e_publico_e_mostra_so_campanhas_ativas()
    {
        var ativa = await CriarAsync("Painel ativa");
        var concluida = await CriarAsync("Painel concluída", "Concluida");
        var cancelada = await CriarAsync("Painel cancelada", "Cancelada");

        var (painel, _) = await PainelAsync();

        Campanha(painel, ativa).Should().NotBeNull();
        Campanha(painel, concluida).Should().BeNull();
        Campanha(painel, cancelada).Should().BeNull();
    }

    [Fact]
    public async Task Painel_exibe_titulo_meta_e_valor_arrecadado()
    {
        var id = await CriarAsync("Campanha do agasalho", meta: 2500m);

        var campanha = Campanha((await PainelAsync()).Corpo, id)!.Value;

        campanha.GetProperty("titulo").GetString().Should().Be("Campanha do agasalho");
        campanha.GetProperty("metaFinanceira").GetDecimal().Should().Be(2500m);
        campanha.GetProperty("valorArrecadado").GetDecimal().Should().Be(0m);
        campanha.GetProperty("percentualAtingido").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Segunda_leitura_vem_do_cache_e_criar_campanha_invalida()
    {
        await PainelAsync();
        (await PainelAsync()).Cache.Should().Be("HIT");

        var nova = await CriarAsync("Invalida o cache");
        var (painel, cache) = await PainelAsync();

        cache.Should().Be("MISS");
        Campanha(painel, nova).Should().NotBeNull();
    }

    [Fact]
    public async Task Valor_processado_aparece_no_painel_depois_da_invalidacao()
    {
        var id = await CriarAsync("Processada pelo worker", meta: 400m);
        await PainelAsync();
        await PainelAsync();

        // O que o doacoes-worker faz: incremento atômico, commit e invalidação do cache.
        await using (var banco = api.NovoContexto())
        {
            await banco.Campanhas.Where(c => c.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ValorArrecadado, c => c.ValorArrecadado + 100m), Cancelamento);
        }

        await api.Services.GetRequiredService<InvalidadorDoPainel>().InvalidarAsync(Cancelamento);

        var campanha = Campanha((await PainelAsync()).Corpo, id)!.Value;
        campanha.GetProperty("valorArrecadado").GetDecimal().Should().Be(100m);
        campanha.GetProperty("percentualAtingido").GetDecimal().Should().Be(25m);
    }

    [Fact]
    public async Task Painel_responde_do_banco_quando_o_cache_falha()
    {
        var id = await CriarAsync("Sem Redis");
        await using var banco = api.NovoContexto();
        var painel = new PainelDeTransparencia(
            banco, new CacheQueFalha(), Options.Create(new OpcoesDoPainel()), NullLogger<PainelDeTransparencia>.Instance);

        var consulta = await painel.ObterAsync(Cancelamento);

        consulta.DoCache.Should().BeFalse();
        consulta.Campanhas.Should().Contain(c => c.Id == id);
    }

    private sealed class CacheQueFalha : IDistributedCache
    {
        public byte[]? Get(string key) => throw new TimeoutException();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new TimeoutException();

        public void Refresh(string key) => throw new TimeoutException();

        public Task RefreshAsync(string key, CancellationToken token = default) => throw new TimeoutException();

        public void Remove(string key) => throw new TimeoutException();

        public Task RemoveAsync(string key, CancellationToken token = default) => throw new TimeoutException();

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new TimeoutException();

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw new TimeoutException();
    }
}
