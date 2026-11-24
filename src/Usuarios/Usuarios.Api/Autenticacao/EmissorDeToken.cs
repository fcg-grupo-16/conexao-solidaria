using System.Security.Claims;
using ConexaoSolidaria.Web;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Usuarios.Domain;

namespace Usuarios.Api.Autenticacao;

/// <summary>Token emitido no login.</summary>
/// <param name="Token">JWT assinado com HS256.</param>
/// <param name="ExpiraEm">Momento de expiração (UTC).</param>
public sealed record TokenEmitido(string Token, DateTimeOffset ExpiraEm);

/// <summary>Emite o JWT com as claims que a campanhas-api usa para autorizar.</summary>
public sealed class EmissorDeToken(IOptions<OpcoesJwt> opcoes, TimeProvider relogio)
{
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>Gera o token do usuário autenticado.</summary>
    public TokenEmitido Emitir(Usuario usuario)
    {
        var jwt = opcoes.Value;
        var agora = relogio.GetUtcNow();
        var expiraEm = agora.AddMinutes(jwt.ExpiracaoEmMinutos);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.ChaveDeAssinatura(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(Claims.Id, usuario.Id.ToString()),
                new Claim(Claims.Nome, usuario.NomeCompleto),
                new Claim(Claims.Email, usuario.Email),
                new Claim(Claims.Perfil, usuario.Perfil.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
        });

        return new TokenEmitido(token, expiraEm);
    }
}
