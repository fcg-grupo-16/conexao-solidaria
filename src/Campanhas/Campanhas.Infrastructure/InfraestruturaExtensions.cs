using Campanhas.Infrastructure.Painel;
using Campanhas.Infrastructure.Persistencia;
using ConexaoSolidaria.Observabilidade;
using ConexaoSolidaria.Persistencia;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

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

    /// <summary>Nome da connection string do Redis.</summary>
    public const string ConnectionStringRedis = "Redis";

    /// <summary>
    /// Cache do painel de transparência no Redis, o invalidador e o health check (Degraded: sem Redis o
    /// painel segue respondendo a partir do banco). A conexão não bloqueia a subida nem falha se o Redis
    /// estiver fora; os timeouts são curtos para a queda do cache não virar lentidão no painel.
    /// </summary>
    public static IServiceCollection AdicionarCacheDoPainel(this IServiceCollection services, IConfiguration configuracao)
    {
        var conexao = configuracao.GetConnectionString(ConnectionStringRedis)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringRedis} não configurada.");

        var opcoesRedis = ConfigurationOptions.Parse(conexao);
        opcoesRedis.AbortOnConnectFail = false;
        opcoesRedis.ConnectTimeout = 2000;
        opcoesRedis.SyncTimeout = 1000;
        opcoesRedis.AsyncTimeout = 1000;

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(opcoesRedis));
        // Uma conexão só, compartilhada pelo cache e pelo health check.
        services.AddStackExchangeRedisCache(_ => { });
        services.AddOptions<RedisCacheOptions>().Configure<IConnectionMultiplexer>(
            (cache, redis) => cache.ConnectionMultiplexerFactory = () => Task.FromResult(redis));
        services.AddOptions<OpcoesDoPainel>().Bind(configuracao.GetSection(OpcoesDoPainel.Secao));
        services.AddScoped<PainelDeTransparencia>();
        services.AddSingleton<InvalidadorDoPainel>();
        services.AddHealthChecks().AddCheck<RedisHealthCheck>(
            "redis", failureStatus: HealthStatus.Degraded, tags: [ObservabilidadeExtensions.TagProntidao]);

        return services;
    }

    /// <summary>
    /// Outbox transacional na publicação: <c>IPublishEndpoint.Publish</c> grava a mensagem no
    /// PostgreSQL junto com o <c>SaveChanges</c>, e um serviço em background a entrega ao RabbitMQ.
    /// </summary>
    public static void AdicionarOutboxDeCampanhas(this IBusRegistrationConfigurator barramento) =>
        barramento.AddEntityFrameworkOutbox<CampanhasDbContext>(outbox =>
        {
            outbox.UsePostgres();
            outbox.UseBusOutbox();
            outbox.QueryDelay = TimeSpan.FromSeconds(1);
        });

    /// <summary>Aplica as migrations pendentes. Só a campanhas-api chama; o worker nunca migra.</summary>
    public static Task AplicarMigrationsDeCampanhasAsync(this IServiceProvider services, CancellationToken cancelamento = default) =>
        services.AplicarMigrationsAsync<CampanhasDbContext>(cancelamento);

    /// <summary>
    /// Espera o esquema do <c>campanhasdb</c> estar completo (migrations aplicadas pela campanhas-api).
    /// O worker não migra; se subir antes da API, consumir mensagens contra um banco sem tabelas
    /// mandaria todas para a fila de erro.
    /// </summary>
    public static async Task AguardarEsquemaDeCampanhasAsync(
        this IServiceProvider services, TimeSpan limite, CancellationToken cancelamento = default)
    {
        var prazo = DateTimeOffset.UtcNow + limite;
        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InfraestruturaExtensions));

        while (true)
        {
            try
            {
                await using var escopo = services.CreateAsyncScope();
                var banco = escopo.ServiceProvider.GetRequiredService<CampanhasDbContext>();
                var pendentes = (await banco.Database.GetPendingMigrationsAsync(cancelamento)).Count();
                if (pendentes == 0)
                {
                    return;
                }

                log.LogInformation("Aguardando a campanhas-api aplicar {Quantidade} migration(s)", pendentes);
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                log.LogInformation("Aguardando o PostgreSQL: {Erro}", erro.GetType().Name);
            }

            if (DateTimeOffset.UtcNow > prazo)
            {
                throw new TimeoutException($"O esquema do campanhasdb não ficou pronto em {limite}.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancelamento);
        }
    }
}
