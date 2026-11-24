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
    /// Aplica as migrations pendentes do contexto. O EF Core trava a tabela de histórico durante a
    /// aplicação, então réplicas subindo juntas não aplicam a mesma migration duas vezes.
    /// Só a API dona do banco chama isto; o worker nunca migra.
    /// </summary>
    public static async Task AplicarMigrationsAsync<TContexto>(this IServiceProvider services, CancellationToken cancelamento = default)
        where TContexto : DbContext
    {
        await using var escopo = services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<TContexto>();
        var log = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PersistenciaExtensions));

        var pendentes = (await banco.Database.GetPendingMigrationsAsync(cancelamento)).ToList();
        if (pendentes.Count > 0)
        {
            log.LogInformation("Aplicando {Quantidade} migration(s) em {Contexto}: {Migrations}", pendentes.Count, typeof(TContexto).Name, pendentes);
            await banco.Database.MigrateAsync(cancelamento);
        }
    }

    /// <summary>True quando a falha foi violação de índice único (SQLSTATE 23505).</summary>
    public static bool EhViolacaoDeUnicidade(this DbUpdateException erro) =>
        erro.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
