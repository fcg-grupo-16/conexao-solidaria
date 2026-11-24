using Microsoft.Extensions.Options;
using Usuarios.Domain;

namespace Usuarios.Infrastructure.Seguranca;

/// <summary>Opções do hash de senha.</summary>
public sealed class OpcoesDeSenha
{
    /// <summary>Seção de configuração.</summary>
    public const string Secao = "Senha";

    /// <summary>Custo do BCrypt (2^n iterações). 11 ≈ 100–200 ms por hash em hardware comum.</summary>
    public int FatorDeTrabalho { get; set; } = 11;
}

/// <summary>
/// BCrypt no modo "enhanced": a senha passa por SHA-384 antes, então senhas acima de 72 bytes não são
/// truncadas silenciosamente (limite do BCrypt puro).
/// </summary>
public sealed class BCryptHasherDeSenha(IOptions<OpcoesDeSenha> opcoes) : IHasherDeSenha
{
    /// <inheritdoc />
    public string Gerar(string senha) => BCrypt.Net.BCrypt.EnhancedHashPassword(senha, opcoes.Value.FatorDeTrabalho);

    /// <inheritdoc />
    public bool Verificar(string senha, string hash) => BCrypt.Net.BCrypt.EnhancedVerify(senha, hash);
}
