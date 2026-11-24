using Campanhas.Infrastructure.Persistencia;
using ConexaoSolidaria.Observabilidade;
using ConexaoSolidaria.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Campanhas.Infrastructure;

/// <summary>Registro da infraestrutura do contexto de Campanhas (API e worker).</summary>
public static class InfraestruturaExtensions
{
    /// <summary>Nome da connection string.</summary>
    public const string ConnectionString = "CampanhasDb";

    /// <summary>Banco <c>campanhasdb</c>, relógio e health check do PostgreSQL.</summary>
    public static IServiceCollection AdicionarBancoDeCampanhas(this IServiceCollection services, IConfiguration configuracao)
    {
        var conexao = configuracao.GetConnectionString(ConnectionString)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionString} não configurada.");

        services.AddDbContext<CampanhasDbContext>(opcoes => opcoes.UsarPostgreSql(conexao));
        services.AddSingleton(TimeProvider.System);
        services.AddHealthChecks()
            .AddDbContextCheck<CampanhasDbContext>("postgresql", tags: [ObservabilidadeExtensions.TagProntidao]);

        return services;
    }

    /// <summary>Aplica as migrations pendentes. Só a campanhas-api chama; o worker nunca migra.</summary>
    public static Task AplicarMigrationsDeCampanhasAsync(this IServiceProvider services, CancellationToken cancelamento = default) =>
        services.AplicarMigrationsAsync<CampanhasDbContext>(cancelamento);
}
