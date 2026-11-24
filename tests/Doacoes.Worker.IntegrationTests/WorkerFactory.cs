using System.Globalization;
using Campanhas.Domain;
using Campanhas.Infrastructure.Persistencia;
using ConexaoSolidaria.Contracts.Eventos;
using ConexaoSolidaria.Persistencia;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(Doacoes.Worker.IntegrationTests.WorkerFactory))]

namespace Doacoes.Worker.IntegrationTests;

/// <summary>
/// doacoes-worker real contra PostgreSQL e RabbitMQ descartáveis. As migrations são aplicadas pelo
/// teste, fazendo o papel da campanhas-api (o worker nunca migra).
/// </summary>
public sealed class WorkerFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("campanhasdb").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3.13-management-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7.4-alpine").Build();

    /// <summary>Novo contexto apontando para o container.</summary>
    public CampanhasDbContext NovoContexto()
    {
        var opcoes = new DbContextOptionsBuilder<CampanhasDbContext>();
        opcoes.UsarPostgreSql(_postgres.GetConnectionString());
        return new CampanhasDbContext(opcoes.Options);
    }

    /// <summary>
    /// Publica no RabbitMQ pelo barramento do próprio worker (mesma topologia da campanhas-api).
    /// Com prazo: sem conexão com o broker, o Publish do MassTransit esperaria indefinidamente.
    /// </summary>
    public async Task PublicarAsync(DoacaoRecebidaEvent evento)
    {
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        prazo.CancelAfter(TimeSpan.FromSeconds(30));
        await Services.GetRequiredService<IBus>().Publish(evento, prazo.Token);
    }

    /// <summary>Cria uma campanha e uma doação pendente, como a campanhas-api faria.</summary>
    public async Task<(Campanha Campanha, Doacao Doacao)> DoacaoPendenteAsync(decimal valor = 100m, Campanha? campanha = null)
    {
        var agora = DateTimeOffset.UtcNow;
        await using var banco = NovoContexto();
        if (campanha is null)
        {
            campanha = Campanha.Criar("Campanha", "Teste do worker.", agora.AddDays(-1), agora.AddDays(10), 1000m, StatusCampanha.Ativa, agora).Valor;
            banco.Campanhas.Add(campanha);
        }

        var doacao = campanha.ReceberDoacao(Guid.NewGuid(), valor, agora).Valor;
        banco.Doacoes.Add(doacao);
        await banco.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (campanha, doacao);
    }

    /// <summary>Espera até a condição sobre o banco ser verdadeira.</summary>
    public async Task<bool> EsperarAsync(Func<CampanhasDbContext, Task<bool>> condicao, TimeSpan? limite = null)
    {
        var prazo = DateTime.UtcNow + (limite ?? TimeSpan.FromSeconds(20));
        while (DateTime.UtcNow < prazo)
        {
            await using var banco = NovoContexto();
            if (await condicao(banco))
            {
                return true;
            }

            await Task.Delay(200);
        }

        return false;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync(), _redis.StartAsync());
        await using var banco = NovoContexto();
        await banco.Database.MigrateAsync();
        // Força a criação do host: o worker só consome depois de subir.
        _ = Services;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var rabbit = new Uri(_rabbit.GetConnectionString());
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:CampanhasDb", _postgres.GetConnectionString());
        builder.UseSetting("RabbitMq:Host", rabbit.Host);
        builder.UseSetting("RabbitMq:Porta", rabbit.Port.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("RabbitMq:Usuario", "rabbitmq");
        builder.UseSetting("RabbitMq:Senha", "rabbitmq");
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("Banco:SegundosParaAguardarEsquema", "30");
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
