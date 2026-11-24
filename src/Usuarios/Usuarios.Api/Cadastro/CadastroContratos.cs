using Usuarios.Domain;

namespace Usuarios.Api.Cadastro;

/// <summary>Cadastro público de doador.</summary>
/// <param name="NomeCompleto">Nome e sobrenome.</param>
/// <param name="Email">E-mail único na plataforma.</param>
/// <param name="Cpf">CPF com ou sem máscara (000.000.000-00).</param>
/// <param name="Senha">Mínimo de 8 caracteres, com letras e números.</param>
public sealed record CadastrarDoadorRequest(string NomeCompleto, string Email, string Cpf, string Senha);

/// <summary>Dados públicos do usuário (sem CPF completo e sem senha).</summary>
/// <param name="Id">Identificador.</param>
/// <param name="NomeCompleto">Nome completo.</param>
/// <param name="Email">E-mail normalizado.</param>
/// <param name="Perfil">Perfil de acesso.</param>
/// <param name="CriadoEm">Momento do cadastro (UTC).</param>
public sealed record UsuarioResponse(Guid Id, string NomeCompleto, string Email, Perfil Perfil, DateTimeOffset CriadoEm)
{
    /// <summary>Projeção da entidade.</summary>
    public static UsuarioResponse De(Usuario usuario) =>
        new(usuario.Id, usuario.NomeCompleto, usuario.Email, usuario.Perfil, usuario.CriadoEm);
}
