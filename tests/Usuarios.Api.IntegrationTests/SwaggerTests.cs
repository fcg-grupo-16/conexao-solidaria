using System.Net;
using System.Text.Json;

namespace Usuarios.Api.IntegrationTests;

public class SwaggerTests(UsuariosApiFactory api)
{
    [Fact]
    public async Task Documento_openapi_tem_esquema_bearer_e_as_rotas()
    {
        var resposta = await api.CreateClient().GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        documento.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString().Should().Be("bearer");
        var rotas = documento.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        rotas.Should().Contain(["/api/usuarios", "/api/auth/login", "/api/usuarios/me"]);
    }

    [Fact]
    public async Task Interface_do_swagger_responde()
    {
        var resposta = await api.CreateClient().GetAsync("/swagger/index.html", TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
