using System.Net.Http.Headers;
using Campanhas.Infrastructure.Persistencia;
using ConexaoSolidaria.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(Campanhas.IntegrationTests.CampanhasApiFactory))]

namespace Campanhas.IntegrationTests;

/// <summary>
/// campanhas-api real contra um PostgreSQL descartável (Testcontainers), uma vez por assembly.
/// </summary>
public sealed class CampanhasApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("campanhasdb")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3.13-management-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7.4-alpine").Build();

    /// <summary>URI AMQP do broker de teste.</summary>
    public Uri RabbitMqUri => new(_rabbit.GetConnectionString());

    /// <summary>Connection string do container.</summary>
    public string ConnectionString => _postgres.GetConnectionString();

    /// <summary>Novo contexto apontando para o container (fora do container de DI da API).</summary>
    public CampanhasDbContext NovoContexto()
    {
        var opcoes = new DbContextOptionsBuilder<CampanhasDbContext>();
        opcoes.UsarPostgreSql(ConnectionString);
        return new CampanhasDbContext(opcoes.Options);
    }

    /// <summary>Cliente HTTP autenticado com um token do perfil informado.</summary>
    public HttpClient ClienteComo(string perfil, Guid? usuarioId = null)
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GeradorDeToken.Gerar(perfil, usuarioId ?? Guid.NewGuid()));
        return cliente;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync(), _redis.StartAsync());
        // Migra já aqui: testes de persistência usam o banco antes de a API ser criada.
        await using var banco = NovoContexto();
        await banco.Database.MigrateAsync();

        // Sobe a API principal já aqui, antes de qualquer réplica (WithWebHostBuilder). O MassTransit
        // cria os instrumentos de métrica uma única vez por processo, no IMeterFactory do primeiro host
        // que sobe; se esse host fosse uma réplica de vida curta, o Meter morreria com ela e as métricas
        // "masstransit" sumiriam do /metrics da API principal pelo resto da execução.
        _ = Server;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:CampanhasDb", ConnectionString);
        builder.UseSetting("Jwt:ChaveSecreta", GeradorDeToken.Chave);
        builder.UseSetting("RabbitMq:Host", RabbitMqUri.Host);
        builder.UseSetting("RabbitMq:Porta", RabbitMqUri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("RabbitMq:Usuario", "rabbitmq");
        builder.UseSetting("RabbitMq:Senha", "rabbitmq");
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
