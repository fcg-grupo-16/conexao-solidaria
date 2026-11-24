using System.Diagnostics.Metrics;
using System.Net;
using ConexaoSolidaria.Observabilidade;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConexaoSolidaria.BuildingBlocks.Tests;

public sealed class ObservabilidadeTests : IAsyncDisposable
{
    private const string NomeMedidor = "ConexaoSolidaria.Testes";

    private readonly Meter _medidor = new(NomeMedidor);
    private WebApplication? _app;

    private async Task<HttpClient> SubirAsync(bool dependenciaSaudavel)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AdicionarObservabilidade("servico-de-teste", NomeMedidor);
        builder.Services.AddHealthChecks().AddCheck(
            "dependencia",
            () => dependenciaSaudavel ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("fora do ar"),
            tags: [ObservabilidadeExtensions.TagProntidao]);

        _app = builder.Build();
        _app.UsarObservabilidade();
        _app.MapGet("/ping", () => "pong");
        await _app.StartAsync(TestContext.Current.CancellationToken);
        return _app.GetTestClient();
    }

    [Fact]
    public async Task Vivo_responde_200_mesmo_com_dependencia_fora_do_ar()
    {
        var cliente = await SubirAsync(dependenciaSaudavel: false);

        var resposta = await cliente.GetAsync("/health/live", TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Prontidao_responde_503_quando_uma_dependencia_cai()
    {
        var cliente = await SubirAsync(dependenciaSaudavel: false);

        var resposta = await cliente.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        corpo.Should().Contain("\"dependencia\"").And.Contain("Unhealthy");
    }

    [Fact]
    public async Task Prontidao_responde_200_com_dependencias_saudaveis()
    {
        var cliente = await SubirAsync(dependenciaSaudavel: true);

        var resposta = await cliente.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Metrics_expoe_http_e_metricas_de_negocio_no_formato_prometheus()
    {
        var cliente = await SubirAsync(dependenciaSaudavel: true);
        _medidor.CreateCounter<long>("doacoes_teste").Add(3);
        await cliente.GetAsync("/ping", TestContext.Current.CancellationToken);

        var resposta = await cliente.GetAsync("/metrics", TestContext.Current.CancellationToken);
        var corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        corpo.Should().Contain("http_server_request_duration_seconds");
        corpo.Should().Contain("doacoes_teste_total");
    }

    public async ValueTask DisposeAsync()
    {
        _medidor.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
