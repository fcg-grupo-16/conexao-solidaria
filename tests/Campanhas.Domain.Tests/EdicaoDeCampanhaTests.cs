namespace Campanhas.Domain.Tests;

public class EdicaoDeCampanhaTests
{
    private static Resultado Editar(
        Campanha campanha,
        string? titulo = "Natal Solidário 2026",
        decimal? meta = null,
        DateTimeOffset? fim = null,
        StatusCampanha? status = null,
        DateTimeOffset? agora = null) =>
        campanha.Editar(
            titulo,
            campanha.Descricao,
            campanha.DataInicio,
            fim ?? campanha.DataFim,
            meta ?? campanha.MetaFinanceira,
            status ?? campanha.Status,
            agora ?? Cenario.Agora);

    [Fact]
    public void Edicao_valida_altera_campos_e_registra_momento()
    {
        var campanha = Cenario.Ativa();

        var resultado = Editar(campanha, meta: 25_000m, agora: Cenario.Agora.AddHours(1));

        resultado.Sucesso.Should().BeTrue();
        campanha.Titulo.Should().Be("Natal Solidário 2026");
        campanha.MetaFinanceira.Should().Be(25_000m);
        campanha.AtualizadaEm.Should().Be(Cenario.Agora.AddHours(1));
    }

    [Fact]
    public void Edicao_invalida_nao_altera_nada()
    {
        var campanha = Cenario.Ativa();

        var resultado = Editar(campanha, titulo: "", meta: 0);

        resultado.Sucesso.Should().BeFalse();
        campanha.Titulo.Should().Be("Natal Solidário");
        campanha.MetaFinanceira.Should().Be(10_000m);
        campanha.AtualizadaEm.Should().BeNull();
    }

    [Fact]
    public void Meta_menor_ou_igual_a_zero_e_recusada_na_edicao()
    {
        Editar(Cenario.Ativa(), meta: 0).ErrosPorCampo().Should().ContainKey("MetaFinanceira");
    }

    [Fact]
    public void Campanha_ativa_nao_pode_terminar_no_passado()
    {
        var campanha = Cenario.Ativa();

        var resultado = Editar(campanha, fim: Cenario.Agora.AddMinutes(-1));

        resultado.ErrosPorCampo().Should().ContainKey("DataFim");
    }

    [Fact]
    public void Campanha_vencida_pode_ser_concluida()
    {
        var campanha = Cenario.Ativa();
        var depoisDoFim = campanha.DataFim.AddDays(1);

        Editar(campanha, status: StatusCampanha.Concluida, agora: depoisDoFim).Sucesso.Should().BeTrue();
        campanha.Status.Should().Be(StatusCampanha.Concluida);
    }

    [Fact]
    public void Campanha_concluida_pode_ser_reaberta_com_novo_prazo()
    {
        var campanha = Cenario.ComStatus(StatusCampanha.Concluida);

        Editar(campanha, status: StatusCampanha.Ativa, fim: Cenario.Agora.AddDays(60)).Sucesso.Should().BeTrue();
        campanha.Status.Should().Be(StatusCampanha.Ativa);
    }

    [Fact]
    public void Campanha_cancelada_e_estado_final()
    {
        var campanha = Cenario.ComStatus(StatusCampanha.Cancelada);

        var resultado = Editar(campanha, status: StatusCampanha.Ativa);

        resultado.ErrosPorCampo()["Status"].Should().ContainSingle().Which.Should().Contain("cancelada");
        campanha.Status.Should().Be(StatusCampanha.Cancelada);
    }

    [Fact]
    public void Edicao_nao_altera_o_arrecadado()
    {
        var campanha = Cenario.Ativa();

        Editar(campanha, meta: 1m);

        campanha.ValorArrecadado.Should().Be(0m);
    }
}
