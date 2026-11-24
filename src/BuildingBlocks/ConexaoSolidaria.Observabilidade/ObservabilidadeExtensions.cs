using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace ConexaoSolidaria.Observabilidade;

/// <summary>
/// Logs estruturados, métricas Prometheus e health checks, iguais nos três serviços.
/// </summary>
public static class ObservabilidadeExtensions
{
    /// <summary>
    /// Tag dos health checks que entram na prontidão (<c>/health/ready</c>): as dependências do serviço.
    /// </summary>
    public const string TagProntidao = "ready";

    private static readonly string[] RotasDeInfraestrutura = ["/health", "/metrics"];

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Registra Serilog (JSON no console), OpenTelemetry com exportador Prometheus e o serviço de
    /// health checks. Cada serviço adiciona os próprios checks com a tag <see cref="TagProntidao"/>.
    /// </summary>
    /// <param name="builder">Builder da aplicação.</param>
    /// <param name="nomeServico">Nome lógico do serviço (aparece nos logs e nas métricas).</param>
    /// <param name="medidores">Nomes dos <see cref="System.Diagnostics.Metrics.Meter"/> de negócio a exportar.</param>
    public static WebApplicationBuilder AdicionarObservabilidade(
        this WebApplicationBuilder builder,
        string nomeServico,
        params string[] medidores)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeServico);

        builder.Services.AddSerilog((services, configuracao) => configuracao
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Servico", nomeServico)
            .WriteTo.Console(new RenderedCompactJsonFormatter()));

        builder.Services.AddHealthChecks();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(recurso => recurso.AddService(nomeServico))
            .WithMetrics(metricas =>
            {
                metricas
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();

                foreach (var medidor in medidores)
                {
                    metricas.AddMeter(medidor);
                }
            });

        return builder;
    }

    /// <summary>
    /// Liga o log de requisições e mapeia <c>/health/live</c>, <c>/health/ready</c> e <c>/metrics</c>.
    /// Chamar logo depois do <c>Build()</c>, antes dos demais middlewares.
    /// </summary>
    public static WebApplication UsarObservabilidade(this WebApplication app)
    {
        app.UseSerilogRequestLogging(opcoes =>
        {
            opcoes.MessageTemplate = "HTTP {RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0} ms";
            // Probes e scrape rodam a cada poucos segundos; em Information elas afogariam o log útil.
            opcoes.GetLevel = (contexto, _, erro) =>
                erro is not null || contexto.Response.StatusCode >= 500 ? LogEventLevel.Error
                : EhRotaDeInfraestrutura(contexto.Request.Path) ? LogEventLevel.Verbose
                : LogEventLevel.Information;
        });

        // Vivo = o processo responde. Não consulta dependências: banco fora do ar não deve fazer o
        // Kubernetes reiniciar o pod, só tirá-lo do balanceamento (isso é papel da prontidão).
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = EscreverRelatorioAsync,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(TagProntidao),
            ResponseWriter = EscreverRelatorioAsync,
        });

        app.MapPrometheusScrapingEndpoint("/metrics");

        return app;
    }

    private static bool EhRotaDeInfraestrutura(PathString caminho) =>
        RotasDeInfraestrutura.Any(rota => caminho.StartsWithSegments(rota, StringComparison.OrdinalIgnoreCase));

    // Não inclui a exceção: a mensagem de erro de um driver costuma carregar host, porta e usuário.
    private static Task EscreverRelatorioAsync(HttpContext contexto, HealthReport relatorio)
    {
        contexto.Response.ContentType = "application/json; charset=utf-8";

        var corpo = new
        {
            status = relatorio.Status.ToString(),
            duracaoMs = Math.Round(relatorio.TotalDuration.TotalMilliseconds, 1),
            dependencias = relatorio.Entries.Select(item => new
            {
                nome = item.Key,
                status = item.Value.Status.ToString(),
                duracaoMs = Math.Round(item.Value.Duration.TotalMilliseconds, 1),
                descricao = item.Value.Description,
            }),
        };

        return contexto.Response.WriteAsync(JsonSerializer.Serialize(corpo, OpcoesJson));
    }
}
