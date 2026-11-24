using Campanhas.Infrastructure.Persistencia;
using ConexaoSolidaria.Persistencia;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Campanhas.IntegrationTests.BancoDeCampanhasFixture))]

namespace Campanhas.IntegrationTests;

/// <summary>PostgreSQL descartável com as migrations do contexto de Campanhas aplicadas.</summary>
public sealed class BancoDeCampanhasFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("campanhasdb")
        .Build();

    /// <summary>Connection string do container.</summary>
    public string ConnectionString => _postgres.GetConnectionString();

    /// <summary>Novo contexto apontando para o container.</summary>
    public CampanhasDbContext NovoContexto()
    {
        var opcoes = new DbContextOptionsBuilder<CampanhasDbContext>();
        opcoes.UsarPostgreSql(ConnectionString);
        return new CampanhasDbContext(opcoes.Options);
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var banco = NovoContexto();
        await banco.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _postgres.DisposeAsync();
}
