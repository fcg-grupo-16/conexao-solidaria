namespace Usuarios.Domain;

/// <summary>Gera e confere hashes de senha. A senha em texto nunca é persistida.</summary>
public interface IHasherDeSenha
{
    /// <summary>Gera o hash da senha.</summary>
    string Gerar(string senha);

    /// <summary>Confere se a senha corresponde ao hash.</summary>
    bool Verificar(string senha, string hash);
}
