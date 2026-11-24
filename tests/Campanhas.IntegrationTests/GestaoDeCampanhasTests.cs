using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Campanhas.IntegrationTests;

public class GestaoDeCampanhasTests(CampanhasApiFactory api)
{
    private const string Rota = "/api/campanhas";

    private readonly HttpClient _gestor = api.ClienteComo("GestorONG");
    private readonly HttpClient _doador = api.ClienteComo("Doador");
    private readonly HttpClient _anonimo = api.CreateClient();

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private static object NovaCampanha(
        string titulo = "Natal Solidário",
        decimal meta = 5000m,
        DateTimeOffset? fim = null,
        string? status = null) => new
        {
            titulo,
            descricao = "Brinquedos para as crianças acolhidas.",
            dataInicio = DateTimeOffset.UtcNow.AddDays(-1),
            dataFim = fim ?? DateTimeOffset.UtcNow.AddDays(30),
            metaFinanceira = meta,
            status,
        };

    private async Task<JsonElement> CriarAsync(object? campanha = null)
    {
        var resposta = await _gestor.PostAsJsonAsync(Rota, campanha ?? NovaCampanha(), Cancelamento);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
    }

    private static object Edicao(JsonElement campanha, string? titulo = null, decimal? meta = null, string? status = null, DateTimeOffset? fim = null) => new
    {
        titulo = titulo ?? campanha.GetProperty("titulo").GetString(),
        descricao = campanha.GetProperty("descricao").GetString(),
        dataInicio = campanha.GetProperty("dataInicio").GetDateTimeOffset(),
        dataFim = fim ?? campanha.GetProperty("dataFim").GetDateTimeOffset(),
        metaFinanceira = meta ?? campanha.GetProperty("metaFinanceira").GetDecimal(),
        status = status ?? campanha.GetProperty("status").GetString(),
    };

    [Fact]
    public async Task Gestor_cria_campanha_ativa_por_padrao()
    {
        var resposta = await _gestor.PostAsJsonAsync(Rota, NovaCampanha(), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Location!.ToString().Should().StartWith("/api/campanhas/");
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        corpo.GetProperty("status").GetString().Should().Be("Ativa");
        corpo.GetProperty("valorArrecadado").GetDecimal().Should().Be(0m);
        corpo.GetProperty("metaFinanceira").GetDecimal().Should().Be(5000m);
    }

    [Fact]
    public async Task Doador_nao_pode_criar_campanha()
    {
        var resposta = await _doador.PostAsJsonAsync(Rota, NovaCampanha(), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonimo_nao_pode_criar_campanha()
    {
        var resposta = await _anonimo.PostAsJsonAsync(Rota, NovaCampanha(), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_assinado_com_outra_chave_e_recusado()
    {
        using var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer",
            GeradorDeToken.Gerar("GestorONG", Guid.NewGuid(), chave: "outra-chave-qualquer-com-mais-de-32-bytes!!"));

        var resposta = await cliente.PostAsJsonAsync(Rota, NovaCampanha(), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_expirado_e_recusado()
    {
        using var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer",
            GeradorDeToken.Gerar("GestorONG", Guid.NewGuid(), expiraEm: DateTime.UtcNow.AddMinutes(-5)));

        var resposta = await cliente.PostAsJsonAsync(Rota, NovaCampanha(), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Data_de_termino_no_passado_retorna_400()
    {
        var resposta = await _gestor.PostAsJsonAsync(Rota, NovaCampanha(fim: DateTimeOffset.UtcNow.AddHours(-1)), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        corpo.GetProperty("errors").TryGetProperty("DataFim", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Meta_menor_ou_igual_a_zero_retorna_400(decimal meta)
    {
        var resposta = await _gestor.PostAsJsonAsync(Rota, NovaCampanha(meta: meta), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        corpo.GetProperty("errors").TryGetProperty("MetaFinanceira", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Status_desconhecido_retorna_400()
    {
        var resposta = await _gestor.PostAsJsonAsync(Rota, NovaCampanha(status: "Pausada"), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Gestor_edita_campos_e_status()
    {
        var campanha = await CriarAsync();
        var id = campanha.GetProperty("id").GetGuid();

        var resposta = await _gestor.PutAsJsonAsync($"{Rota}/{id}", Edicao(campanha, titulo: "Natal Solidário 2026", meta: 8000m, status: "Concluida"), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        corpo.GetProperty("titulo").GetString().Should().Be("Natal Solidário 2026");
        corpo.GetProperty("metaFinanceira").GetDecimal().Should().Be(8000m);
        corpo.GetProperty("status").GetString().Should().Be("Concluida");
        corpo.GetProperty("atualizadaEm").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task Edicao_com_meta_zero_retorna_400()
    {
        var campanha = await CriarAsync();

        var resposta = await _gestor.PutAsJsonAsync($"{Rota}/{campanha.GetProperty("id").GetGuid()}", Edicao(campanha, meta: 0), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Campanha_cancelada_nao_pode_ser_editada()
    {
        var campanha = await CriarAsync();
        var id = campanha.GetProperty("id").GetGuid();
        (await _gestor.PutAsJsonAsync($"{Rota}/{id}", Edicao(campanha, status: "Cancelada"), Cancelamento)).EnsureSuccessStatusCode();

        var resposta = await _gestor.PutAsJsonAsync($"{Rota}/{id}", Edicao(campanha, status: "Ativa"), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Edicao_de_campanha_inexistente_retorna_404()
    {
        var campanha = await CriarAsync();

        var resposta = await _gestor.PutAsJsonAsync($"{Rota}/{Guid.NewGuid()}", Edicao(campanha), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Doador_nao_pode_editar_campanha()
    {
        var campanha = await CriarAsync();

        var resposta = await _doador.PutAsJsonAsync($"{Rota}/{campanha.GetProperty("id").GetGuid()}", Edicao(campanha), Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Gestor_lista_e_filtra_por_status()
    {
        var ativa = await CriarAsync(NovaCampanha(titulo: "Filtro ativa"));
        var cancelada = await CriarAsync(NovaCampanha(titulo: "Filtro cancelada", status: "Cancelada"));

        var todas = await _gestor.GetFromJsonAsync<JsonElement>(Rota, Cancelamento);
        var canceladas = await _gestor.GetFromJsonAsync<JsonElement>($"{Rota}?status=Cancelada", Cancelamento);

        var idsTodas = todas.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
        idsTodas.Should().Contain([ativa.GetProperty("id").GetGuid(), cancelada.GetProperty("id").GetGuid()]);
        canceladas.EnumerateArray().Should().OnlyContain(c => c.GetProperty("status").GetString() == "Cancelada")
            .And.Contain(c => c.GetProperty("id").GetGuid() == cancelada.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Gestor_obtem_campanha_por_id()
    {
        var campanha = await CriarAsync();
        var id = campanha.GetProperty("id").GetGuid();

        var resposta = await _gestor.GetAsync($"{Rota}/{id}", Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _gestor.GetAsync($"{Rota}/{Guid.NewGuid()}", Cancelamento)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Listagem_de_gestao_exige_gestor()
    {
        (await _doador.GetAsync(Rota, Cancelamento)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _anonimo.GetAsync(Rota, Cancelamento)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
