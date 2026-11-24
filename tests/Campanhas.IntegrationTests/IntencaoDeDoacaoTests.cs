using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Campanhas.Domain;
using ConexaoSolidaria.Mensageria;
using Microsoft.EntityFrameworkCore;

namespace Campanhas.IntegrationTests;

public class IntencaoDeDoacaoTests(CampanhasApiFactory api)
{
    private const string Rota = "/api/doacoes";

    private readonly Guid _doadorId = Guid.NewGuid();
    private readonly HttpClient _gestor = api.ClienteComo("GestorONG");

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private HttpClient Doador() => api.ClienteComo("Doador", _doadorId);

    private async Task<Guid> CriarCampanhaAsync(string status = "Ativa")
    {
        var resposta = await _gestor.PostAsJsonAsync("/api/campanhas", new
        {
            titulo = "Campanha de doação",
            descricao = "Teste de doação.",
            dataInicio = DateTimeOffset.UtcNow.AddDays(-1),
            dataFim = DateTimeOffset.UtcNow.AddDays(10),
            metaFinanceira = 1000m,
            status,
        }, Cancelamento);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CriarCampanhaVencidaAsync()
    {
        var antes = DateTimeOffset.UtcNow.AddDays(-30);
        var campanha = Campanha.Criar("Vencida", "Prazo terminou.", antes, antes.AddDays(10), 100m, StatusCampanha.Ativa, antes).Valor;
        await using var banco = api.NovoContexto();
        banco.Campanhas.Add(campanha);
        await banco.SaveChangesAsync(Cancelamento);
        return campanha.Id;
    }

    [Fact]
    public async Task Doacao_valida_retorna_202_pendente_e_publica_o_evento()
    {
        var campanhaId = await CriarCampanhaAsync();
        await using var escuta = await EscutaDoExchange.CriarAsync(api.RabbitMqUri, MensageriaExtensions.ExchangeDoacaoRecebida);

        var resposta = await Doador().PostAsJsonAsync(Rota, new { idCampanha = campanhaId, valorDoacao = 150.50m }, Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        var doacaoId = corpo.GetProperty("id").GetGuid();
        corpo.GetProperty("status").GetString().Should().Be("Pendente");
        resposta.Headers.Location!.ToString().Should().Be($"/api/doacoes/{doacaoId}");

        var envelope = await escuta.EsperarAsync(
            e => e.GetProperty("message").GetProperty("doacaoId").GetGuid() == doacaoId,
            TimeSpan.FromSeconds(20));

        envelope.Should().NotBeNull("o outbox deve entregar o DoacaoRecebidaEvent ao RabbitMQ");
        var mensagem = envelope!.Value.GetProperty("message");
        mensagem.GetProperty("campanhaId").GetGuid().Should().Be(campanhaId);
        mensagem.GetProperty("doadorId").GetGuid().Should().Be(_doadorId);
        // O serializador do MassTransit grava decimal como texto, para não perder precisão no JSON.
        var valor = mensagem.GetProperty("valor");
        (valor.ValueKind == JsonValueKind.String
            ? decimal.Parse(valor.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            : valor.GetDecimal()).Should().Be(150.50m);
        envelope.Value.GetProperty("messageType")[0].GetString()
            .Should().Be("urn:message:ConexaoSolidaria.Contracts.Eventos:DoacaoRecebidaEvent");
    }

    [Fact]
    public async Task Metrics_expoe_doacoes_recebidas_e_metricas_do_masstransit()
    {
        var campanhaId = await CriarCampanhaAsync();
        await using var escuta = await EscutaDoExchange.CriarAsync(api.RabbitMqUri, MensageriaExtensions.ExchangeDoacaoRecebida);
        var resposta = await Doador().PostAsJsonAsync(Rota, new { idCampanha = campanhaId, valorDoacao = 30m }, Cancelamento);
        var doacaoId = (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("id").GetGuid();
        await escuta.EsperarAsync(e => e.GetProperty("message").GetProperty("doacaoId").GetGuid() == doacaoId, TimeSpan.FromSeconds(20));

        var metricas = await api.CreateClient().GetStringAsync("/metrics", Cancelamento);

        metricas.Should().Contain("doacoes_recebidas_total").And.Contain("doacoes_valor_recebido");
        metricas.Should().Contain("masstransit");
    }

    [Fact]
    public async Task Api_nao_altera_o_valor_arrecadado()
    {
        var campanhaId = await CriarCampanhaAsync();

        var resposta = await Doador().PostAsJsonAsync(Rota, new { idCampanha = campanhaId, valorDoacao = 80m }, Cancelamento);
        var doacaoId = (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("id").GetGuid();

        await using var banco = api.NovoContexto();
        var campanha = await banco.Campanhas.SingleAsync(c => c.Id == campanhaId, Cancelamento);
        var doacao = await banco.Doacoes.SingleAsync(d => d.Id == doacaoId, Cancelamento);
        campanha.ValorArrecadado.Should().Be(0m, "quem soma é o doacoes-worker, nunca a API");
        doacao.Status.Should().Be(StatusDoacao.Pendente);
        doacao.DoadorId.Should().Be(_doadorId);
    }

    [Theory]
    [InlineData("Cancelada", "cancelada")]
    [InlineData("Concluida", "concluída")]
    public async Task Campanha_cancelada_ou_concluida_retorna_422(string status, string trecho)
    {
        var campanhaId = await CriarCampanhaAsync(status);

        var resposta = await Doador().PostAsJsonAsync(Rota, new { idCampanha = campanhaId, valorDoacao = 10m }, Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("detail").GetString().Should().Contain(trecho);
    }

    [Fact]
    public async Task Campanha_com_prazo_vencido_retorna_422()
    {
        var campanhaId = await CriarCampanhaVencidaAsync();

        var resposta = await Doador().PostAsJsonAsync(Rota, new { idCampanha = campanhaId, valorDoacao = 10m }, Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Doacao_recusada_nao_grava_nem_publica()
    {
        var campanhaId = await CriarCampanhaAsync("Cancelada");

        await Doador().PostAsJsonAsync(Rota, new { idCampanha = campanhaId, valorDoacao = 10m }, Cancelamento);

        await using var banco = api.NovoContexto();
        (await banco.Doacoes.CountAsync(d => d.CampanhaId == campanhaId, Cancelamento)).Should().Be(0);
    }

    [Fact]
    public async Task Campanha_inexistente_retorna_404()
    {
        var resposta = await Doador().PostAsJsonAsync(Rota, new { idCampanha = Guid.NewGuid(), valorDoacao = 10m }, Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(10.001)]
    public async Task Valor_invalido_retorna_400(decimal valor)
    {
        var campanhaId = await CriarCampanhaAsync();

        var resposta = await Doador().PostAsJsonAsync(Rota, new { idCampanha = campanhaId, valorDoacao = valor }, Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("errors").TryGetProperty("ValorDoacao", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Gestor_nao_pode_doar()
    {
        var campanhaId = await CriarCampanhaAsync();

        var resposta = await _gestor.PostAsJsonAsync(Rota, new { idCampanha = campanhaId, valorDoacao = 10m }, Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonimo_nao_pode_doar()
    {
        var resposta = await api.CreateClient().PostAsJsonAsync(Rota, new { idCampanha = Guid.NewGuid(), valorDoacao = 10m }, Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Doador_acompanha_a_propria_doacao_mas_nao_a_de_outro()
    {
        var campanhaId = await CriarCampanhaAsync();
        var resposta = await Doador().PostAsJsonAsync(Rota, new { idCampanha = campanhaId, valorDoacao = 25m }, Cancelamento);
        var local = resposta.Headers.Location!.ToString();

        var dono = await Doador().GetAsync(local, Cancelamento);
        var outroDoador = await api.ClienteComo("Doador").GetAsync(local, Cancelamento);
        var gestor = await _gestor.GetAsync(local, Cancelamento);

        dono.StatusCode.Should().Be(HttpStatusCode.OK);
        outroDoador.StatusCode.Should().Be(HttpStatusCode.NotFound);
        gestor.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
