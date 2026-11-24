using ConexaoSolidaria.Web;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Usuarios.Domain;
using Usuarios.Infrastructure.Persistencia;

namespace Usuarios.Api.Autenticacao;

/// <summary>Credenciais de login.</summary>
/// <param name="Email">E-mail cadastrado.</param>
/// <param name="Senha">Senha.</param>
public sealed record LoginRequest(string Email, string Senha);

/// <summary>Token de acesso. Use no header <c>Authorization: Bearer {token}</c>.</summary>
/// <param name="Token">JWT.</param>
/// <param name="Tipo">Sempre <c>Bearer</c>.</param>
/// <param name="ExpiraEm">Momento de expiração (UTC).</param>
/// <param name="Perfil">Perfil do usuário autenticado.</param>
public sealed record LoginResponse(string Token, string Tipo, DateTimeOffset ExpiraEm, Perfil Perfil);

/// <summary>Formato mínimo do login; credencial errada é 401, não 400.</summary>
public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Define as regras.</summary>
    public LoginValidator()
    {
        RuleFor(r => r.Email).NotEmpty().WithMessage("Informe o e-mail.");
        RuleFor(r => r.Senha).NotEmpty().WithMessage("Informe a senha.");
    }
}

/// <summary>Login e emissão de JWT.</summary>
public static class AutenticacaoEndpoints
{
    // Hash de uma senha qualquer, usado quando o e-mail não existe: o BCrypt roda do mesmo jeito e o
    // tempo de resposta não revela quais e-mails estão cadastrados.
    private static readonly Lazy<string> HashFicticio = new(() => BCrypt.Net.BCrypt.EnhancedHashPassword(Guid.NewGuid().ToString()));

    /// <summary>Mapeia <c>POST /api/auth/login</c>.</summary>
    public static IEndpointRouteBuilder MapAutenticacaoEndpoints(this IEndpointRouteBuilder rotas)
    {
        rotas.MapPost("/api/auth/login", LoginAsync)
            .WithName("Login")
            .WithTags("Autenticação")
            .WithSummary("Autentica com e-mail e senha e devolve um JWT.")
            .WithDescription("O token carrega a claim 'role' (GestorONG ou Doador), usada pela campanhas-api para autorizar.")
            .AllowAnonymous()
            .ValidarCom<LoginRequest>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return rotas;
    }

    private static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest requisicao,
        UsuariosDbContext banco,
        IHasherDeSenha hasher,
        EmissorDeToken emissor,
        MetricasDeUsuarios metricas,
        CancellationToken cancelamento)
    {
        var email = Usuario.NormalizarEmail(requisicao.Email);
        var usuario = await banco.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancelamento);

        var senhaConfere = hasher.Verificar(requisicao.Senha, usuario?.SenhaHash ?? HashFicticio.Value);
        if (usuario is null || !senhaConfere)
        {
            // Mesma resposta para e-mail inexistente e senha errada.
            metricas.LoginRecusado();
            return TypedResults.Problem(
                title: "Não autorizado",
                detail: "E-mail ou senha inválidos.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var token = emissor.Emitir(usuario);
        metricas.LoginAceito(usuario.Perfil.ToString());
        return TypedResults.Ok(new LoginResponse(token.Token, "Bearer", token.ExpiraEm, usuario.Perfil));
    }
}
