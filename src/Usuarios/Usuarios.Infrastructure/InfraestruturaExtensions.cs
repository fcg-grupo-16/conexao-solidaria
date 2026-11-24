using ConexaoSolidaria.Observabilidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Usuarios.Domain;
using Usuarios.Infrastructure.Persistencia;
using Usuarios.Infrastructure.Seguranca;

namespace Usuarios.Infrastructure;

/// <summary>Registro da infraestrutura da usuarios-api.</summary>
public static class InfraestruturaExtensions
{
    /// <summary>Nome da connection string.</summary>
    public const string ConnectionString = "UsuariosDb";

    /// <summary>Banco, hash de senha e health check do PostgreSQL.</summary>
    public static IServiceCollection AdicionarInfraestruturaDeUsuarios(this IServiceCollection services, IConfiguration configuracao)
    {
        var conexao = configuracao.GetConnectionString(ConnectionString)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionString} não configurada.");

        services.AddDbContext<UsuariosDbContext>(opcoes => ConfigurarBanco(opcoes, conexao));
        services.AddOptions<OpcoesDeSenha>().Bind(configuracao.GetSection(OpcoesDeSenha.Secao));
        services.AddSingleton<IHasherDeSenha, BCryptHasherDeSenha>();
        services.AddSingleton(TimeProvider.System);
        services.AddHealthChecks()
            .AddDbContextCheck<UsuariosDbContext>("postgresql", tags: [ObservabilidadeExtensions.TagProntidao]);

        return services;
    }

    /// <summary>Provider e convenção de nomes (snake_case) do banco.</summary>
    public static void ConfigurarBanco(DbContextOptionsBuilder opcoes, string conexao) =>
        opcoes.UseNpgsql(conexao).UseSnakeCaseNamingConvention();

    /// <summary>
    /// Aplica as migrations pendentes. O EF Core trava a tabela de histórico durante a aplicação,
    /// então réplicas subindo juntas não aplicam a mesma migration duas vezes.
    /// </summary>
    public static async Task AplicarMigrationsAsync(this IServiceProvider services, CancellationToken cancelamento = default)
    {
        await using var escopo = services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<UsuariosDbContext>();
        var log = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InfraestruturaExtensions));

        var pendentes = (await banco.Database.GetPendingMigrationsAsync(cancelamento)).ToList();
        if (pendentes.Count > 0)
        {
            log.LogInformation("Aplicando {Quantidade} migration(s): {Migrations}", pendentes.Count, pendentes);
            await banco.Database.MigrateAsync(cancelamento);
        }
    }
}
