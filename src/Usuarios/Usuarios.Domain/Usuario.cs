namespace Usuarios.Domain;

/// <summary>Usuário da plataforma (doador ou gestor da ONG).</summary>
public sealed class Usuario
{
    /// <summary>Tamanho máximo do nome completo.</summary>
    public const int TamanhoMaximoNome = 150;

    /// <summary>Tamanho máximo do e-mail (RFC 5321).</summary>
    public const int TamanhoMaximoEmail = 254;

    // Construtor do EF Core.
    private Usuario()
    {
        NomeCompleto = null!;
        Email = null!;
        Cpf = null!;
        SenhaHash = null!;
    }

    private Usuario(Guid id, string nomeCompleto, string email, Cpf cpf, string senhaHash, Perfil perfil, DateTimeOffset criadoEm)
    {
        Id = id;
        NomeCompleto = nomeCompleto;
        Email = email;
        Cpf = cpf;
        SenhaHash = senhaHash;
        Perfil = perfil;
        CriadoEm = criadoEm;
    }

    /// <summary>Identificador; vira a claim <c>sub</c> do JWT.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome completo.</summary>
    public string NomeCompleto { get; private set; }

    /// <summary>E-mail normalizado (sem espaços, minúsculo); único na plataforma.</summary>
    public string Email { get; private set; }

    /// <summary>CPF válido; único na plataforma.</summary>
    public Cpf Cpf { get; private set; }

    /// <summary>Hash da senha.</summary>
    public string SenhaHash { get; private set; }

    /// <summary>Perfil de acesso.</summary>
    public Perfil Perfil { get; private set; }

    /// <summary>Momento do cadastro (UTC).</summary>
    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>
    /// Cadastro público: sempre com o perfil <see cref="Perfil.Doador"/>. Não há caminho público para
    /// criar um gestor.
    /// </summary>
    public static Usuario CriarDoador(string nomeCompleto, string email, Cpf cpf, string senhaHash, DateTimeOffset criadoEm) =>
        Criar(nomeCompleto, email, cpf, senhaHash, Perfil.Doador, criadoEm);

    /// <summary>Gestor da ONG, criado apenas pelo seed de configuração.</summary>
    public static Usuario CriarGestor(string nomeCompleto, string email, Cpf cpf, string senhaHash, DateTimeOffset criadoEm) =>
        Criar(nomeCompleto, email, cpf, senhaHash, Perfil.GestorONG, criadoEm);

    /// <summary>Normalização usada no cadastro e no login, para a comparação de e-mail ser a mesma.</summary>
    public static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();

    private static Usuario Criar(string nomeCompleto, string email, Cpf cpf, string senhaHash, Perfil perfil, DateTimeOffset criadoEm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeCompleto);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNull(cpf);
        ArgumentException.ThrowIfNullOrWhiteSpace(senhaHash);

        var nome = string.Join(' ', nomeCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (nome.Length > TamanhoMaximoNome)
        {
            throw new ArgumentException($"O nome deve ter no máximo {TamanhoMaximoNome} caracteres.", nameof(nomeCompleto));
        }

        var emailNormalizado = NormalizarEmail(email);
        if (emailNormalizado.Length > TamanhoMaximoEmail)
        {
            throw new ArgumentException($"O e-mail deve ter no máximo {TamanhoMaximoEmail} caracteres.", nameof(email));
        }

        return new Usuario(Guid.CreateVersion7(criadoEm), nome, emailNormalizado, cpf, senhaHash, perfil, criadoEm.ToUniversalTime());
    }
}
