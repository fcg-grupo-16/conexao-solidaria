using ConexaoSolidaria.Observabilidade;
using ConexaoSolidaria.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Usuarios.Domain;
using Usuarios.Infrastructure.Persistencia;
using Usuarios.Infrastructure.Seed;
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
        services.AddOptions<OpcoesGestorInicial>().Bind(configuracao.GetSection(OpcoesGestorInicial.Secao));
        services.AddSingleton(TimeProvider.System);
        services.AddHealthChecks()
            .AddDbContextCheck<UsuariosDbContext>("postgresql", tags: [ObservabilidadeExtensions.TagProntidao]);

        return services;
    }

    /// <summary>Provider e convenção de nomes (snake_case) do banco.</summary>
    public static void ConfigurarBanco(DbContextOptionsBuilder opcoes, string conexao) => opcoes.UsarPostgreSql(conexao);

    /// <summary>Aplica as migrations pendentes do <see cref="UsuariosDbContext"/>.</summary>
    public static Task AplicarMigrationsAsync(this IServiceProvider services, CancellationToken cancelamento = default) =>
        services.AplicarMigrationsAsync<UsuariosDbContext>(cancelamento);
}
