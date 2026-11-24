using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ConexaoSolidaria.Persistencia;

/// <summary>Convenções e operações de banco comuns aos serviços.</summary>
public static class PersistenciaExtensions
{
    /// <summary>PostgreSQL com nomes em snake_case (tabelas, colunas, índices).</summary>
    public static DbContextOptionsBuilder UsarPostgreSql(this DbContextOptionsBuilder opcoes, string conexao) =>
        opcoes.UseNpgsql(conexao).UseSnakeCaseNamingConvention();

    /// <summary>
    /// Aplica as migrations pendentes do contexto, uma réplica por vez.
    /// Só a API dona do banco chama isto; o worker nunca migra.
    /// </summary>
    /// <remarks>
    /// Com duas réplicas subindo juntas no Kubernetes, as duas encontravam a mesma migration pendente e
    /// a segunda falhava com "relation already exists" (a trava do próprio EF Core não segurou). Aqui um
    /// advisory lock do PostgreSQL, na mesma sessão usada pela migração, serializa as réplicas: a
    /// segunda espera, e ao pegar o lock já não há nada pendente.
    /// </remarks>
    public static async Task AplicarMigrationsAsync<TContexto>(
        this IServiceProvider services, CancellationToken cancelamento = default, TimeSpan? esperaPeloBanco = null)
        where TContexto : DbContext
    {
        // No Kubernetes o banco pode subir depois da API (no minikube, o volume do PostgreSQL chega a
        // levar minutos para ser provisionado). Em vez de cair e reiniciar, a API espera o banco.
        var prazo = DateTimeOffset.UtcNow + (esperaPeloBanco ?? TimeSpan.FromMinutes(4));
        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PersistenciaExtensions));
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await MigrarComLockAsync<TContexto>(services, cancelamento);
                return;
            }
            catch (Exception erro) when (EhBancoIndisponivel(erro) && DateTimeOffset.UtcNow < prazo)
            {
                log.LogWarning("PostgreSQL indisponível (tentativa {Tentativa}); nova tentativa em 3 s", tentativa);
                await Task.Delay(TimeSpan.FromSeconds(3), cancelamento);
            }
        }
    }

    private static bool EhBancoIndisponivel(Exception erro) =>
        erro is NpgsqlException { IsTransient: true }
        || erro.InnerException is NpgsqlException { IsTransient: true }
        || erro is PostgresException { SqlState: PostgresErrorCodes.CannotConnectNow };

    private static async Task MigrarComLockAsync<TContexto>(IServiceProvider services, CancellationToken cancelamento)
        where TContexto : DbContext
    {
        await using var escopo = services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<TContexto>();
        var log = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PersistenciaExtensions));
        var chave = ChaveDoLock(typeof(TContexto).FullName!);

        await banco.Database.OpenConnectionAsync(cancelamento);
        try
        {
            await banco.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({chave})", cancelamento);
            try
            {
                var pendentes = (await banco.Database.GetPendingMigrationsAsync(cancelamento)).ToList();
                if (pendentes.Count > 0)
                {
                    log.LogInformation("Aplicando {Quantidade} migration(s) em {Contexto}: {Migrations}", pendentes.Count, typeof(TContexto).Name, pendentes);
                    await banco.Database.MigrateAsync(cancelamento);
                }
            }
            finally
            {
                await banco.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({chave})", CancellationToken.None);
            }
        }
        finally
        {
            await banco.Database.CloseConnectionAsync();
        }
    }

    // Chave estável entre processos (string.GetHashCode muda a cada execução): FNV-1a de 64 bits.
    private static long ChaveDoLock(string nome)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in nome)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        return unchecked((long)hash);
    }

    /// <summary>True quando a falha foi violação de índice único (SQLSTATE 23505).</summary>
    public static bool EhViolacaoDeUnicidade(this DbUpdateException erro) =>
        erro.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
