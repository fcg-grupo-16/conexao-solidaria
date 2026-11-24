using System.Net.Http.Json;

namespace Usuarios.Api.IntegrationTests;

public class MetricasTests(UsuariosApiFactory api)
{
    [Fact]
    public async Task Metrics_expoe_cadastros_e_logins_por_resultado()
    {
        var cliente = api.CreateClient();
        var email = DadosDeTeste.EmailUnico();
        await cliente.PostAsJsonAsync("/api/usuarios",
            new { nomeCompleto = "Ana Métrica", email, cpf = DadosDeTeste.CpfUnico(), senha = "Doar2026!" },
            TestContext.Current.CancellationToken);
        await cliente.PostAsJsonAsync("/api/auth/login", new { email, senha = "Doar2026!" }, TestContext.Current.CancellationToken);
        await cliente.PostAsJsonAsync("/api/auth/login", new { email, senha = "Errada2026" }, TestContext.Current.CancellationToken);

        var metricas = await cliente.GetStringAsync("/metrics", TestContext.Current.CancellationToken);

        metricas.Should().Contain("usuarios_cadastrados_total");
        metricas.Should().Contain("usuarios_logins_total{").And.Contain("resultado=\"sucesso\"").And.Contain("resultado=\"falha\"");
        metricas.Should().Contain("http_server_request_duration_seconds");
    }
}
