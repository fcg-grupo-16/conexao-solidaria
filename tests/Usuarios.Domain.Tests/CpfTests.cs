namespace Usuarios.Domain.Tests;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData(" 529.982.247-25 ")]
    [InlineData("111.444.777-35")]
    public void Aceita_cpf_valido_com_ou_sem_mascara(string valor)
    {
        Cpf.EhValido(valor).Should().BeTrue();
    }

    [Theory]
    [InlineData("529.982.247-24")] // segundo dígito verificador errado
    [InlineData("529.982.247-15")] // primeiro dígito verificador errado
    [InlineData("000.000.000-00")] // sequência repetida passa no cálculo, mas não é CPF
    [InlineData("11111111111")]
    [InlineData("5299822472")] // 10 dígitos
    [InlineData("529982247250")] // 12 dígitos
    [InlineData("529.982.24725")] // máscara parcial
    [InlineData("529-982-247.25")] // separadores fora de lugar
    [InlineData("52998224a25")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Recusa_cpf_invalido(string? valor)
    {
        Cpf.EhValido(valor).Should().BeFalse();
    }

    [Fact]
    public void Guarda_so_os_digitos()
    {
        Cpf.Criar("529.982.247-25").Numero.Should().Be("52998224725");
    }

    [Fact]
    public void Mesmo_cpf_com_e_sem_mascara_sao_iguais()
    {
        Cpf.Criar("529.982.247-25").Should().Be(Cpf.Criar("52998224725"));
    }

    [Fact]
    public void Mascara_esconde_inicio_e_digitos_verificadores()
    {
        var cpf = Cpf.Criar("52998224725");

        cpf.Mascarado.Should().Be("***.982.247-**");
        cpf.ToString().Should().Be(cpf.Mascarado);
    }

    [Fact]
    public void Criar_com_cpf_invalido_lanca_excecao()
    {
        var criar = () => Cpf.Criar("123.456.789-00");

        criar.Should().Throw<ArgumentException>();
    }
}
