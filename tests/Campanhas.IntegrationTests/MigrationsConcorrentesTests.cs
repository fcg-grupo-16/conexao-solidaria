using Campanhas.Infrastructure.Persistencia;
using ConexaoSolidaria.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Campanhas.IntegrationTests;

public class MigrationsConcorrentesTests(CampanhasApiFactory api)
{
    [Fact]
    public async Task Replicas_subindo_juntas_aplicam_as_migrations_uma_vez_sem_erro()
    {
        // Banco vazio próprio, para as migrations estarem todas pendentes.
        var nome = $"migracoes_{Guid.NewGuid():N}";
        await using (var conexao = new NpgsqlConnection(api.ConnectionString))
        {
            await conexao.OpenAsync(TestContext.Current.CancellationToken);
            await using var comando = new NpgsqlCommand($"CREATE DATABASE {nome}", conexao);
            await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var conexaoDoBanco = new NpgsqlConnectionStringBuilder(api.ConnectionString) { Database = nome }.ConnectionString;
        ServiceProvider Replica() => new ServiceCollection()
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILoggerFactory), NullLoggerFactory.Instance)
            .AddDbContext<CampanhasDbContext>(opcoes => opcoes.UsarPostgreSql(conexaoDoBanco))
            .BuildServiceProvider();

        var replicas = Enumerable.Range(0, 4).Select(_ => Replica()).ToList();

        var migrar = () => Task.WhenAll(replicas.Select(r => r.AplicarMigrationsAsync<CampanhasDbContext>(TestContext.Current.CancellationToken)));

        await migrar.Should().NotThrowAsync();
        await using var escopo = replicas[0].CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<CampanhasDbContext>();
        (await banco.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        foreach (var replica in replicas)
        {
            await replica.DisposeAsync();
        }
    }
}
