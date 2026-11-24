using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Usuarios.Domain;
using Usuarios.Infrastructure.Persistencia;

namespace Usuarios.Infrastructure.Seed;

/// <summary>
/// Gestor da ONG criado na subida. O cadastro público só cria doadores, então é este o único caminho
/// para existir um GestorONG. A senha vem de Secret no Kubernetes.
/// </summary>
public sealed class OpcoesGestorInicial
{
    /// <summary>Seção de configuração.</summary>
    public const string Secao = "GestorInicial";

    /// <summary>Nome completo.</summary>
    public string? NomeCompleto { get; set; }

    /// <summary>E-mail de login. Vazio desliga o seed.</summary>
    public string? Email { get; set; }

    /// <summary>CPF válido.</summary>
    public string? Cpf { get; set; }

    /// <summary>Senha inicial.</summary>
    public string? Senha { get; set; }
}

/// <summary>Seed idempotente do gestor inicial.</summary>
public static class SeedDoGestor
{
    /// <summary>
    /// Cria o gestor se ainda não existir usuário com o e-mail configurado. Rodar várias vezes (cada
    /// réplica, cada reinício) não duplica: a checagem é pelo e-mail, e o índice único segura a corrida.
    /// </summary>
    public static async Task GarantirGestorInicialAsync(this IServiceProvider services, CancellationToken cancelamento = default)
    {
        await using var escopo = services.CreateAsyncScope();
        var provedor = escopo.ServiceProvider;
        var opcoes = provedor.GetRequiredService<IOptions<OpcoesGestorInicial>>().Value;
        var log = provedor.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeedDoGestor));

        if (string.IsNullOrWhiteSpace(opcoes.Email))
        {
            log.LogWarning("GestorInicial:Email não configurado; nenhum GestorONG foi criado.");
            return;
        }

        if (string.IsNullOrWhiteSpace(opcoes.NomeCompleto) || string.IsNullOrWhiteSpace(opcoes.Senha) || !Cpf.TentarCriar(opcoes.Cpf, out var cpf))
        {
            throw new InvalidOperationException("GestorInicial incompleto: informe NomeCompleto, Email, Cpf válido e Senha.");
        }

        var banco = provedor.GetRequiredService<UsuariosDbContext>();
        var email = Usuario.NormalizarEmail(opcoes.Email);
        if (await banco.Usuarios.AnyAsync(u => u.Email == email, cancelamento))
        {
            return;
        }

        var hasher = provedor.GetRequiredService<IHasherDeSenha>();
        var relogio = provedor.GetRequiredService<TimeProvider>();
        var gestor = Usuario.CriarGestor(opcoes.NomeCompleto, email, cpf, hasher.Gerar(opcoes.Senha), relogio.GetUtcNow());
        banco.Usuarios.Add(gestor);

        try
        {
            await banco.SaveChangesAsync(cancelamento);
            log.LogInformation("GestorONG inicial {UsuarioId} criado", gestor.Id);
        }
        catch (DbUpdateException erro) when (ErrosDeBanco.EhViolacaoDeUnicidade(erro))
        {
            log.LogInformation("GestorONG inicial já criado por outra réplica");
        }
    }
}
