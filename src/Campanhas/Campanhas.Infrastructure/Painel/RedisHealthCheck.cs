using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Campanhas.Infrastructure.Painel;

/// <summary>PING no Redis. Registrado como Degraded: o painel funciona sem cache.</summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latencia = await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"PING em {latencia.TotalMilliseconds:0.0} ms");
        }
        catch (Exception erro) when (erro is RedisException or TimeoutException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Redis indisponível; o painel lê direto do banco.");
        }
    }
}
