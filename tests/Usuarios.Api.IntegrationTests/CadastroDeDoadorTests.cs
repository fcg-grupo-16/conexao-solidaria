using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Usuarios.Domain;

namespace Usuarios.Api.IntegrationTests;

public class CadastroDeDoadorTests(UsuariosApiFactory api)
{
    private const string Rota = "/api/usuarios";
    private const string SenhaValida = "Doar2026!";

    private readonly HttpClient _cliente = api.CreateClient();

    private static object Requisicao(string? email = null, string? cpf = null, string senha = SenhaValida, string nome = "Maria da Silva") =>
        new { nomeCompleto = nome, email = email ?? DadosDeTeste.EmailUnico(), cpf = cpf ?? DadosDeTeste.CpfUnico(), senha };

    [Fact]
    public async Task Cadastro_valido_retorna_201_com_perfil_doador()
    {
        var resposta = await _cliente.PostAsJsonAsync(Rota, Requisicao(), TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Location.Should().NotBeNull();
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        corpo.GetProperty("perfil").GetString().Should().Be("Doador");
        corpo.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Resposta_nao_expoe_senha_nem_hash_nem_cpf()
    {
        var resposta = await _cliente.PostAsJsonAsync(Rota, Requisicao(), TestContext.Current.CancellationToken);
        var corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        corpo.Should().NotContainAny("senha", "Senha", "hash", "cpf", SenhaValida);
    }

    [Fact]
    public async Task Senha_e_armazenada_com_bcrypt()
    {
        var email = DadosDeTeste.EmailUnico();
        await _cliente.PostAsJsonAsync(Rota, Requisicao(email: email), TestContext.Current.CancellationToken);

        var hash = await api.NoBancoAsync(banco => banco.Usuarios.Where(u => u.Email == email).Select(u => u.SenhaHash).SingleAsync());

        hash.Should().StartWith("$2").And.NotContain(SenhaValida);
        BCrypt.Net.BCrypt.EnhancedVerify(SenhaValida, hash).Should().BeTrue();
    }

    [Fact]
    public async Task Email_repetido_retorna_409_mesmo_com_maiusculas()
    {
        var email = DadosDeTeste.EmailUnico();
        await _cliente.PostAsJsonAsync(Rota, Requisicao(email: email), TestContext.Current.CancellationToken);

        var resposta = await _cliente.PostAsJsonAsync(Rota, Requisicao(email: email.ToUpperInvariant()), TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("e-mail");
    }

    [Fact]
    public async Task Cpf_repetido_retorna_409_mesmo_com_mascara_diferente()
    {
        var cpf = DadosDeTeste.CpfUnico();
        await _cliente.PostAsJsonAsync(Rota, Requisicao(cpf: cpf), TestContext.Current.CancellationToken);

        var resposta = await _cliente.PostAsJsonAsync(Rota, Requisicao(cpf: DadosDeTeste.ComMascara(cpf)), TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("CPF");
    }

    [Fact]
    public async Task Cpf_invalido_retorna_400_apontando_o_campo()
    {
        var resposta = await _cliente.PostAsJsonAsync(Rota, Requisicao(cpf: "123.456.789-00"), TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        corpo.GetProperty("errors").TryGetProperty("Cpf", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("curta1")]
    [InlineData("somenteletras")]
    [InlineData("1234567890")]
    public async Task Senha_fraca_retorna_400(string senha)
    {
        var resposta = await _cliente.PostAsJsonAsync(Rota, Requisicao(senha: senha), TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Maria")]
    [InlineData("")]
    public async Task Nome_sem_sobrenome_retorna_400(string nome)
    {
        var resposta = await _cliente.PostAsJsonAsync(Rota, Requisicao(nome: nome), TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Email_em_formato_invalido_retorna_400()
    {
        var resposta = await _cliente.PostAsJsonAsync(Rota, Requisicao(email: "nao-e-email"), TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cadastro_persiste_email_normalizado_e_perfil_doador()
    {
        var email = DadosDeTeste.EmailUnico();
        await _cliente.PostAsJsonAsync(Rota, Requisicao(email: "  " + email.ToUpperInvariant()), TestContext.Current.CancellationToken);

        var usuario = await api.NoBancoAsync(banco => banco.Usuarios.SingleAsync(u => u.Email == email));

        usuario.Perfil.Should().Be(Perfil.Doador);
    }

    [Fact]
    public async Task Prontidao_reporta_postgresql_saudavel()
    {
        var resposta = await _cliente.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("postgresql");
    }
}
