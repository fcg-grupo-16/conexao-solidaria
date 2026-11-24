using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Campanhas.IntegrationTests;

/// <summary>
/// Emite tokens no mesmo formato da usuarios-api (mesmas claims, emissor, público e algoritmo), para
/// testar a autorização da campanhas-api sem subir o outro serviço.
/// </summary>
internal static class GeradorDeToken
{
    public const string Chave = "chave-de-teste-com-pelo-menos-32-bytes-de-tamanho";

    public static string Gerar(
        string perfil,
        Guid usuarioId,
        string chave = Chave,
        string emissor = "ConexaoSolidaria",
        DateTime? expiraEm = null)
    {
        var agora = DateTime.UtcNow;
        var expiracao = expiraEm ?? agora.AddMinutes(30);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = emissor,
            Audience = "ConexaoSolidaria",
            IssuedAt = expiracao < agora ? expiracao.AddMinutes(-10) : agora,
            NotBefore = expiracao < agora ? expiracao.AddMinutes(-10) : agora,
            Expires = expiracao,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", usuarioId.ToString()),
                new Claim("name", "Usuário de Teste"),
                new Claim("email", "teste@exemplo.com"),
                new Claim("role", perfil),
            ]),
        });
    }
}
