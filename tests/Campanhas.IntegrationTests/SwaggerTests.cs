using System.Net;
using System.Text.Json;

namespace Campanhas.IntegrationTests;

public class SwaggerTests(CampanhasApiFactory api)
{
    [Fact]
    public async Task Documento_openapi_tem_esquema_bearer_e_as_rotas()
    {
        var resposta = await api.CreateClient().GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        documento.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _).Should().BeTrue();
        var rotas = documento.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        rotas.Should().Contain(["/api/campanhas", "/api/campanhas/{id}", "/api/doacoes", "/api/transparencia/campanhas"]);
    }
}
