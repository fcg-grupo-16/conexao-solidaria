using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Usuarios.Infrastructure.Seed;

namespace Usuarios.Api.IntegrationTests;

public class AutenticacaoTests(UsuariosApiFactory api)
{
    private const string SenhaDoador = "Doar2026!";

    private readonly HttpClient _cliente = api.CreateClient();

    private async Task<(string Email, Guid Id)> CadastrarDoadorAsync()
    {
        var email = DadosDeTeste.EmailUnico();
        var resposta = await _cliente.PostAsJsonAsync(
            "/api/usuarios",
            new { nomeCompleto = "João Doador", email, cpf = DadosDeTeste.CpfUnico(), senha = SenhaDoador },
            TestContext.Current.CancellationToken);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (email, corpo.GetProperty("id").GetGuid());
    }

    private async Task<HttpResponseMessage> LoginAsync(string email, string senha) =>
        await _cliente.PostAsJsonAsync("/api/auth/login", new { email, senha }, TestContext.Current.CancellationToken);

    private async Task<string> TokenAsync(string email, string senha)
    {
        var corpo = await (await LoginAsync(email, senha)).Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return corpo.GetProperty("token").GetString()!;
    }

    private async Task<HttpResponseMessage> GetAutenticadoAsync(string rota, string token)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _cliente.SendAsync(requisicao, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Login_do_doador_emite_token_com_role_doador()
    {
        var (email, id) = await CadastrarDoadorAsync();

        var resposta = await LoginAsync(email, SenhaDoador);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        corpo.GetProperty("tipo").GetString().Should().Be("Bearer");
        corpo.GetProperty("perfil").GetString().Should().Be("Doador");

        var token = new JsonWebToken(corpo.GetProperty("token").GetString());
        token.GetClaim("role").Value.Should().Be("Doador");
        token.Subject.Should().Be(id.ToString());
        token.Issuer.Should().Be("ConexaoSolidaria");
        token.Audiences.Should().ContainSingle().Which.Should().Be("ConexaoSolidaria");
        token.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Gestor_do_seed_faz_login_com_role_gestor_ong()
    {
        var resposta = await LoginAsync(UsuariosApiFactory.EmailGestor, UsuariosApiFactory.SenhaGestor);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        new JsonWebToken(corpo.GetProperty("token").GetString()).GetClaim("role").Value.Should().Be("GestorONG");
    }

    [Fact]
    public async Task Login_aceita_email_com_maiusculas_e_espacos()
    {
        var (email, _) = await CadastrarDoadorAsync();

        var resposta = await LoginAsync($"  {email.ToUpperInvariant()} ", SenhaDoador);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Senha_errada_e_email_inexistente_recebem_o_mesmo_401()
    {
        var (email, _) = await CadastrarDoadorAsync();

        var senhaErrada = await LoginAsync(email, "SenhaErrada1");
        var emailInexistente = await LoginAsync(DadosDeTeste.EmailUnico(), SenhaDoador);

        senhaErrada.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        emailInexistente.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var detalhe1 = (await senhaErrada.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("detail").GetString();
        var detalhe2 = (await emailInexistente.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("detail").GetString();
        detalhe1.Should().Be(detalhe2);
    }

    [Fact]
    public async Task Login_sem_campos_retorna_400()
    {
        var resposta = await LoginAsync("", "");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Me_retorna_o_dono_do_token()
    {
        var (email, id) = await CadastrarDoadorAsync();
        var token = await TokenAsync(email, SenhaDoador);

        var resposta = await GetAutenticadoAsync("/api/usuarios/me", token);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        corpo.GetProperty("id").GetGuid().Should().Be(id);
        corpo.GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task Me_sem_token_retorna_401()
    {
        var resposta = await _cliente.GetAsync("/api/usuarios/me", TestContext.Current.CancellationToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_adulterado_retorna_401()
    {
        var (email, _) = await CadastrarDoadorAsync();
        var token = await TokenAsync(email, SenhaDoador);
        var adulterado = token[..^4] + (token[^4..] == "AAAA" ? "BBBB" : "AAAA");

        var resposta = await GetAutenticadoAsync("/api/usuarios/me", adulterado);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Consulta_por_id_e_exclusiva_do_gestor()
    {
        var (email, id) = await CadastrarDoadorAsync();
        var tokenDoador = await TokenAsync(email, SenhaDoador);
        var tokenGestor = await TokenAsync(UsuariosApiFactory.EmailGestor, UsuariosApiFactory.SenhaGestor);

        var comoDoador = await GetAutenticadoAsync($"/api/usuarios/{id}", tokenDoador);
        var comoGestor = await GetAutenticadoAsync($"/api/usuarios/{id}", tokenGestor);

        comoDoador.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        comoGestor.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Seed_do_gestor_nao_duplica_ao_rodar_de_novo()
    {
        await api.Services.GarantirGestorInicialAsync(TestContext.Current.CancellationToken);
        await api.Services.GarantirGestorInicialAsync(TestContext.Current.CancellationToken);

        var gestores = await api.NoBancoAsync(banco => banco.Usuarios.CountAsync(u => u.Email == UsuariosApiFactory.EmailGestor));

        gestores.Should().Be(1);
    }
}
