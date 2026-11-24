namespace Campanhas.Domain.Tests;

public class CriacaoDeCampanhaTests
{
    [Fact]
    public void Campanha_valida_e_criada_com_todos_os_campos()
    {
        var resultado = Cenario.Criar();

        resultado.Sucesso.Should().BeTrue();
        var campanha = resultado.Valor;
        campanha.Id.Should().NotBeEmpty();
        campanha.Titulo.Should().Be("Natal Solidário");
        campanha.Descricao.Should().NotBeNullOrWhiteSpace();
        campanha.DataInicio.Should().Be(Cenario.Inicio);
        campanha.DataFim.Should().Be(Cenario.Fim);
        campanha.MetaFinanceira.Should().Be(10_000m);
        campanha.Status.Should().Be(StatusCampanha.Ativa);
        campanha.CriadaEm.Should().Be(Cenario.Agora);
    }

    [Fact]
    public void Campanha_nasce_com_arrecadado_zero()
    {
        Cenario.Ativa().ValorArrecadado.Should().Be(0m);
    }

    [Fact]
    public void Data_de_termino_no_passado_e_recusada()
    {
        var resultado = Cenario.Criar(inicio: Cenario.Agora.AddDays(-10), fim: Cenario.Agora.AddMinutes(-1));

        resultado.Sucesso.Should().BeFalse();
        resultado.ErrosPorCampo().Should().ContainKey("DataFim");
    }

    [Fact]
    public void Data_de_termino_no_proprio_instante_atual_e_aceita()
    {
        Cenario.Criar(inicio: Cenario.Agora.AddDays(-1), fim: Cenario.Agora).Sucesso.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Meta_menor_ou_igual_a_zero_e_recusada(decimal meta)
    {
        var resultado = Cenario.Criar(meta: meta);

        resultado.Sucesso.Should().BeFalse();
        resultado.ErrosPorCampo()["MetaFinanceira"].Should().ContainSingle().Which.Should().Contain("maior que zero");
    }

    [Fact]
    public void Meta_com_mais_de_duas_casas_decimais_e_recusada()
    {
        Cenario.Criar(meta: 100.001m).ErrosPorCampo().Should().ContainKey("MetaFinanceira");
    }

    [Fact]
    public void Meta_acima_do_maximo_e_recusada()
    {
        Cenario.Criar(meta: Campanha.MetaMaxima + 1).ErrosPorCampo().Should().ContainKey("MetaFinanceira");
    }

    [Fact]
    public void Meta_de_um_centavo_e_aceita()
    {
        Cenario.Criar(meta: 0.01m).Sucesso.Should().BeTrue();
    }

    [Fact]
    public void Termino_antes_ou_igual_ao_inicio_e_recusado()
    {
        var resultado = Cenario.Criar(inicio: Cenario.Fim, fim: Cenario.Fim);

        resultado.ErrosPorCampo()["DataFim"].Should().Contain(m => m.Contains("posterior"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Titulo_obrigatorio(string? titulo)
    {
        Cenario.Criar(titulo: titulo).ErrosPorCampo().Should().ContainKey("Titulo");
    }

    [Fact]
    public void Titulo_acima_do_limite_e_recusado()
    {
        Cenario.Criar(titulo: new string('a', Campanha.TamanhoMaximoTitulo + 1)).ErrosPorCampo().Should().ContainKey("Titulo");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Descricao_obrigatoria(string? descricao)
    {
        Cenario.Criar(descricao: descricao).ErrosPorCampo().Should().ContainKey("Descricao");
    }

    [Fact]
    public void Status_fora_do_enum_e_recusado()
    {
        Cenario.Criar(status: (StatusCampanha)99).ErrosPorCampo().Should().ContainKey("Status");
    }

    [Fact]
    public void Todos_os_erros_sao_reportados_de_uma_vez()
    {
        var resultado = Cenario.Criar(titulo: "", descricao: "", meta: 0, fim: Cenario.Agora.AddDays(-1), inicio: Cenario.Agora.AddDays(-2));

        resultado.ErrosPorCampo().Keys.Should().BeEquivalentTo("Titulo", "Descricao", "MetaFinanceira", "DataFim");
    }

    [Fact]
    public void Espacos_nas_bordas_do_titulo_e_da_descricao_sao_removidos()
    {
        var campanha = Cenario.Criar(titulo: "  Inverno Quente  ", descricao: "  Agasalhos  ").Valor;

        campanha.Titulo.Should().Be("Inverno Quente");
        campanha.Descricao.Should().Be("Agasalhos");
    }

    [Fact]
    public void Datas_sao_guardadas_em_utc()
    {
        var inicio = new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.FromHours(-3));
        var campanha = Cenario.Criar(inicio: inicio, fim: inicio.AddDays(10)).Valor;

        campanha.DataInicio.Offset.Should().Be(TimeSpan.Zero);
        campanha.DataInicio.Should().Be(inicio);
    }

    [Fact]
    public void Valor_de_falha_nao_pode_ser_lido()
    {
        var ler = () => Cenario.Criar(meta: 0).Valor;

        ler.Should().Throw<InvalidOperationException>();
    }
}
