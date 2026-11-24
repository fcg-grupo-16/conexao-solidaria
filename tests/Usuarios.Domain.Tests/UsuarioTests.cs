namespace Usuarios.Domain.Tests;

public class UsuarioTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly Cpf CpfValido = Cpf.Criar("52998224725");

    [Fact]
    public void Cadastro_publico_sempre_cria_doador()
    {
        var usuario = Usuario.CriarDoador("Maria Silva", "maria@exemplo.com", CpfValido, "hash", Agora);

        usuario.Perfil.Should().Be(Perfil.Doador);
    }

    [Fact]
    public void Gestor_recebe_perfil_gestor_ong()
    {
        var usuario = Usuario.CriarGestor("Gestora da ONG", "gestora@exemplo.com", CpfValido, "hash", Agora);

        usuario.Perfil.Should().Be(Perfil.GestorONG);
    }

    [Fact]
    public void Email_e_normalizado_para_minusculas_sem_espacos()
    {
        var usuario = Usuario.CriarDoador("Maria Silva", "  Maria.Silva@Exemplo.COM ", CpfValido, "hash", Agora);

        usuario.Email.Should().Be("maria.silva@exemplo.com");
    }

    [Fact]
    public void Espacos_repetidos_no_nome_sao_removidos()
    {
        var usuario = Usuario.CriarDoador("  Maria   da  Silva ", "maria@exemplo.com", CpfValido, "hash", Agora);

        usuario.NomeCompleto.Should().Be("Maria da Silva");
    }

    [Fact]
    public void Gera_identificador_e_registra_momento_do_cadastro()
    {
        var usuario = Usuario.CriarDoador("Maria Silva", "maria@exemplo.com", CpfValido, "hash", Agora);

        usuario.Id.Should().NotBeEmpty();
        usuario.CriadoEm.Should().Be(Agora);
    }

    [Theory]
    [InlineData("", "maria@exemplo.com", "hash")]
    [InlineData("Maria Silva", "", "hash")]
    [InlineData("Maria Silva", "maria@exemplo.com", "")]
    public void Campos_obrigatorios_vazios_lancam_excecao(string nome, string email, string hash)
    {
        var criar = () => Usuario.CriarDoador(nome, email, CpfValido, hash, Agora);

        criar.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Nome_acima_do_limite_lanca_excecao()
    {
        var nome = "Maria " + new string('a', Usuario.TamanhoMaximoNome);

        var criar = () => Usuario.CriarDoador(nome, "maria@exemplo.com", CpfValido, "hash", Agora);

        criar.Should().Throw<ArgumentException>();
    }
}
