using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ConexaoSolidaria.Web;

/// <summary>
/// Configuração do JWT, compartilhada entre quem emite (usuarios-api) e quem valida (campanhas-api).
/// Os três valores precisam ser idênticos nos dois serviços.
/// </summary>
public sealed class OpcoesJwt
{
    /// <summary>Seção de configuração.</summary>
    public const string Secao = "Jwt";

    /// <summary>Tamanho mínimo da chave do HS256, em bytes.</summary>
    public const int TamanhoMinimoChave = 32;

    /// <summary>Emissor do token.</summary>
    [Required]
    public string Issuer { get; set; } = "ConexaoSolidaria";

    /// <summary>Público do token.</summary>
    [Required]
    public string Audience { get; set; } = "ConexaoSolidaria";

    /// <summary>Chave simétrica do HS256 (Secret no Kubernetes). Mínimo de 32 bytes.</summary>
    [Required]
    public string ChaveSecreta { get; set; } = string.Empty;

    /// <summary>Validade do token emitido.</summary>
    [Range(1, 1440)]
    public int ExpiracaoEmMinutos { get; set; } = 60;

    /// <summary>Chave de assinatura derivada da chave secreta.</summary>
    public SymmetricSecurityKey ChaveDeAssinatura() => new(Encoding.UTF8.GetBytes(ChaveSecreta));
}

/// <summary>Perfis (roles) da plataforma, como aparecem na claim <c>role</c> do token.</summary>
public static class Perfis
{
    /// <summary>Gestor da ONG: gestão de campanhas.</summary>
    public const string GestorONG = "GestorONG";

    /// <summary>Doador: registra doações.</summary>
    public const string Doador = "Doador";
}

/// <summary>Nomes das claims usadas no token (sem o mapeamento legado para URIs do WS-Federation).</summary>
public static class Claims
{
    /// <summary>Identificador do usuário.</summary>
    public const string Id = JwtRegisteredClaimNames.Sub;

    /// <summary>Nome completo.</summary>
    public const string Nome = JwtRegisteredClaimNames.Name;

    /// <summary>E-mail.</summary>
    public const string Email = JwtRegisteredClaimNames.Email;

    /// <summary>Perfil.</summary>
    public const string Perfil = "role";

    /// <summary>Lê o identificador do usuário autenticado.</summary>
    public static Guid IdDoUsuario(this ClaimsPrincipal usuario) =>
        Guid.TryParse(usuario.FindFirstValue(Id), out var id)
            ? id
            : throw new InvalidOperationException("Token sem a claim 'sub'.");
}

/// <summary>Autenticação JWT Bearer e políticas por perfil.</summary>
public static class AutenticacaoExtensions
{
    /// <summary>
    /// Valida o JWT (assinatura, emissor, público e validade) e registra as políticas
    /// <see cref="Perfis.GestorONG"/> e <see cref="Perfis.Doador"/>.
    /// A configuração é conferida na subida: chave curta ou ausente impede o serviço de iniciar.
    /// </summary>
    public static WebApplicationBuilder AdicionarAutenticacaoJwt(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<OpcoesJwt>()
            .Bind(builder.Configuration.GetSection(OpcoesJwt.Secao))
            .ValidateDataAnnotations()
            .Validate(
                opcoes => Encoding.UTF8.GetByteCount(opcoes.ChaveSecreta) >= OpcoesJwt.TamanhoMinimoChave,
                $"Jwt:ChaveSecreta deve ter pelo menos {OpcoesJwt.TamanhoMinimoChave} bytes.")
            .ValidateOnStart();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<OpcoesJwt>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.Value.ChaveDeAssinatura(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = Claims.Nome,
                    RoleClaimType = Claims.Perfil,
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Perfis.GestorONG, politica => politica.RequireRole(Perfis.GestorONG))
            .AddPolicy(Perfis.Doador, politica => politica.RequireRole(Perfis.Doador));

        return builder;
    }

    /// <summary>Liga autenticação e autorização no pipeline.</summary>
    public static WebApplication UsarAutenticacaoJwt(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
