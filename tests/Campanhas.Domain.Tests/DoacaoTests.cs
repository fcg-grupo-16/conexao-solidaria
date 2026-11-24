namespace Campanhas.Domain.Tests;

public class DoacaoTests
{
    private static readonly Guid Doador = Guid.NewGuid();

    [Fact]
    public void Campanha_ativa_no_prazo_aceita_doacao_pendente()
    {
        var campanha = Cenario.Ativa();

        var resultado = campanha.ReceberDoacao(Doador, 150.75m, Cenario.Agora);

        resultado.Sucesso.Should().BeTrue();
        var doacao = resultado.Valor;
        doacao.Id.Should().NotBeEmpty();
        doacao.CampanhaId.Should().Be(campanha.Id);
        doacao.DoadorId.Should().Be(Doador);
        doacao.Valor.Should().Be(150.75m);
        doacao.Status.Should().Be(StatusDoacao.Pendente);
        doacao.CriadaEm.Should().Be(Cenario.Agora);
        doacao.ProcessadaEm.Should().BeNull();
    }

    [Fact]
    public void Receber_doacao_nao_altera_o_arrecadado()
    {
        var campanha = Cenario.Ativa();

        campanha.ReceberDoacao(Doador, 500m, Cenario.Agora);

        campanha.ValorArrecadado.Should().Be(0m, "quem soma é o doacoes-worker, ao processar o evento");
    }

    [Fact]
    public void Campanha_cancelada_recusa_doacao()
    {
        var resultado = Cenario.ComStatus(StatusCampanha.Cancelada).ReceberDoacao(Doador, 10m, Cenario.Agora);

        resultado.Sucesso.Should().BeFalse();
        resultado.ErrosPorCampo()["IdCampanha"].Single().Should().Contain("cancelada");
    }

    [Fact]
    public void Campanha_concluida_recusa_doacao()
    {
        var resultado = Cenario.ComStatus(StatusCampanha.Concluida).ReceberDoacao(Doador, 10m, Cenario.Agora);

        resultado.ErrosPorCampo()["IdCampanha"].Single().Should().Contain("concluída");
    }

    [Fact]
    public void Campanha_com_prazo_vencido_recusa_doacao_mesmo_ativa()
    {
        var campanha = Cenario.Ativa();

        var resultado = campanha.ReceberDoacao(Doador, 10m, campanha.DataFim.AddSeconds(1));

        resultado.ErrosPorCampo()["IdCampanha"].Single().Should().Contain("encerrada");
    }

    [Fact]
    public void Campanha_que_ainda_nao_comecou_recusa_doacao()
    {
        var campanha = Cenario.Criar(inicio: Cenario.Agora.AddDays(5), fim: Cenario.Agora.AddDays(10)).Valor;

        campanha.ReceberDoacao(Doador, 10m, Cenario.Agora).ErrosPorCampo()["IdCampanha"].Single().Should().Contain("não começou");
    }

    [Fact]
    public void Ultimo_instante_do_prazo_ainda_aceita_doacao()
    {
        var campanha = Cenario.Ativa();

        campanha.ReceberDoacao(Doador, 10m, campanha.DataFim).Sucesso.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Valor_menor_ou_igual_a_zero_e_recusado(decimal valor)
    {
        Cenario.Ativa().ReceberDoacao(Doador, valor, Cenario.Agora).ErrosPorCampo().Should().ContainKey("ValorDoacao");
    }

    [Fact]
    public void Valor_com_mais_de_duas_casas_e_recusado()
    {
        Cenario.Ativa().ReceberDoacao(Doador, 10.005m, Cenario.Agora).ErrosPorCampo().Should().ContainKey("ValorDoacao");
    }

    [Fact]
    public void Valor_acima_do_maximo_e_recusado()
    {
        Cenario.Ativa().ReceberDoacao(Doador, Doacao.ValorMaximo + 0.01m, Cenario.Agora).ErrosPorCampo().Should().ContainKey("ValorDoacao");
    }

    [Fact]
    public void Doador_nao_identificado_e_recusado()
    {
        Cenario.Ativa().ReceberDoacao(Guid.Empty, 10m, Cenario.Agora).ErrosPorCampo().Should().ContainKey("Doador");
    }
}
