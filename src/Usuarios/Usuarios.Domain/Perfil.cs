namespace Usuarios.Domain;

/// <summary>
/// Perfis de acesso da plataforma. O nome do membro é o valor da claim <c>role</c> no JWT,
/// por isso não pode ser renomeado sem coordenar com a campanhas-api.
/// </summary>
public enum Perfil
{
    /// <summary>Usuário cadastrado publicamente; pode doar.</summary>
    Doador = 1,

    /// <summary>Gestor da ONG; pode criar e editar campanhas. Só existe por seed de configuração.</summary>
    GestorONG = 2,
}
