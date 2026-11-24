using System.Net;
using Campanhas.Domain;
using ConexaoSolidaria.Contracts.Eventos;
using Campanhas.Infrastructure.Painel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Doacoes.Worker.IntegrationTests;

public class ProcessamentoDeDoacoesTests(WorkerFactory worker)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private static DoacaoRecebidaEvent EventoDe(Doacao doacao) =>
        new(doacao.Id, doacao.CampanhaId, doacao.DoadorId, doacao.Valor, doacao.CriadaEm);

    private async Task<decimal> ArrecadadoAsync(Guid campanhaId)
    {
        await using var banco = worker.NovoContexto();
        return await banco.Campanhas.Where(c => c.Id == campanhaId).Select(c => c.ValorArrecadado).SingleAsync(Cancelamento);
    }

    private async Task<Doacao> DoacaoAsync(Guid id)
    {
        await using var banco = worker.NovoContexto();
        return await banco.Doacoes.AsNoTracking().SingleAsync(d => d.Id == id, Cancelamento);
    }

    [Fact]
    public async Task Evento_soma_a_doacao_ao_arrecadado_e_marca_processada()
    {
        var (campanha, doacao) = await worker.DoacaoPendenteAsync(150.25m);

        await worker.PublicarAsync(EventoDe(doacao));

        (await worker.EsperarAsync(async b => (await b.Doacoes.AsNoTracking().SingleAsync(d => d.Id == doacao.Id)).Status == StatusDoacao.Processada))
            .Should().BeTrue();
        (await ArrecadadoAsync(campanha.Id)).Should().Be(150.25m);
        (await DoacaoAsync(doacao.Id)).ProcessadaEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Mesma_mensagem_entregue_duas_vezes_soma_uma_vez()
    {
        var (campanha, doacao) = await worker.DoacaoPendenteAsync(40m);
        var evento = EventoDe(doacao);

        await worker.PublicarAsync(evento);
        await worker.PublicarAsync(evento);
        await worker.EsperarAsync(async b => (await b.Doacoes.AsNoTracking().SingleAsync(d => d.Id == doacao.Id)).Status == StatusDoacao.Processada);
        // Dá tempo para a segunda entrega ser consumida antes de conferir.
        await Task.Delay(TimeSpan.FromSeconds(2), Cancelamento);

        (await ArrecadadoAsync(campanha.Id)).Should().Be(40m);
    }

    [Fact]
    public async Task Doacoes_concorrentes_na_mesma_campanha_nao_perdem_valor()
    {
        var (campanha, primeira) = await worker.DoacaoPendenteAsync(10m);
        var doacoes = new List<Doacao> { primeira };
        for (var i = 0; i < 19; i++)
        {
            doacoes.Add((await worker.DoacaoPendenteAsync(10m, campanha)).Doacao);
        }

        await Task.WhenAll(doacoes.Select(d => worker.PublicarAsync(EventoDe(d))));

        (await worker.EsperarAsync(async b => await b.Doacoes.CountAsync(d => d.CampanhaId == campanha.Id && d.Status == StatusDoacao.Processada) == 20, TimeSpan.FromSeconds(30)))
            .Should().BeTrue();
        (await ArrecadadoAsync(campanha.Id)).Should().Be(200m);
    }

    [Fact]
    public async Task Doacao_para_campanha_cancelada_depois_da_intencao_e_rejeitada()
    {
        var (campanha, doacao) = await worker.DoacaoPendenteAsync(70m);
        await using (var banco = worker.NovoContexto())
        {
            var paraCancelar = await banco.Campanhas.SingleAsync(c => c.Id == campanha.Id, Cancelamento);
            paraCancelar.Editar(paraCancelar.Titulo, paraCancelar.Descricao, paraCancelar.DataInicio, paraCancelar.DataFim,
                paraCancelar.MetaFinanceira, StatusCampanha.Cancelada, DateTimeOffset.UtcNow).Sucesso.Should().BeTrue();
            await banco.SaveChangesAsync(Cancelamento);
        }

        await worker.PublicarAsync(EventoDe(doacao));

        (await worker.EsperarAsync(async b => (await b.Doacoes.AsNoTracking().SingleAsync(d => d.Id == doacao.Id)).Status == StatusDoacao.Rejeitada))
            .Should().BeTrue();
        (await DoacaoAsync(doacao.Id)).MotivoRejeicao.Should().Contain("cancelada");
        (await ArrecadadoAsync(campanha.Id)).Should().Be(0m);
    }

    [Fact]
    public async Task Prazo_e_avaliado_no_momento_da_intencao_e_nao_do_processamento()
    {
        // Intenção feita dentro do prazo; o worker processa depois do fim da campanha.
        var agora = DateTimeOffset.UtcNow;
        var campanha = Campanha.Criar("Última hora", "Doação no último minuto.", agora.AddDays(-5), agora.AddSeconds(2), 500m, StatusCampanha.Ativa, agora).Valor;
        var (_, doacao) = await worker.DoacaoPendenteAsync(25m, await SalvarAsync(campanha));
        await Task.Delay(TimeSpan.FromSeconds(3), Cancelamento);

        await worker.PublicarAsync(EventoDe(doacao));

        (await worker.EsperarAsync(async b => (await b.Doacoes.AsNoTracking().SingleAsync(d => d.Id == doacao.Id)).Status != StatusDoacao.Pendente))
            .Should().BeTrue();
        (await DoacaoAsync(doacao.Id)).Status.Should().Be(StatusDoacao.Processada);
        (await ArrecadadoAsync(campanha.Id)).Should().Be(25m);
    }

    [Fact]
    public async Task Processar_doacao_invalida_o_cache_do_painel()
    {
        var cache = worker.Services.GetRequiredService<IDistributedCache>();
        await cache.SetStringAsync(PainelDeTransparencia.ChaveDoCache, "[]", Cancelamento);
        var (_, doacao) = await worker.DoacaoPendenteAsync(12m);

        await worker.PublicarAsync(EventoDe(doacao));
        await worker.EsperarAsync(async b => (await b.Doacoes.AsNoTracking().SingleAsync(d => d.Id == doacao.Id)).Status == StatusDoacao.Processada);

        (await worker.EsperarAsync(async _ => await cache.GetAsync(PainelDeTransparencia.ChaveDoCache) is null, TimeSpan.FromSeconds(5)))
            .Should().BeTrue("o worker invalida o painel depois do commit");
    }

    [Fact]
    public async Task Prontidao_reporta_postgresql_e_rabbitmq()
    {
        var resposta = await worker.CreateClient().GetAsync("/health/ready", Cancelamento);
        var corpo = await resposta.Content.ReadAsStringAsync(Cancelamento);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        corpo.Should().Contain("postgresql").And.Contain("rabbitmq").And.Contain("redis");
    }

    [Fact]
    public async Task Metrics_expoe_as_metricas_de_processamento()
    {
        var (_, doacao) = await worker.DoacaoPendenteAsync(5m);
        await worker.PublicarAsync(EventoDe(doacao));
        await worker.EsperarAsync(async b => (await b.Doacoes.AsNoTracking().SingleAsync(d => d.Id == doacao.Id)).Status == StatusDoacao.Processada);

        var metricas = await worker.CreateClient().GetStringAsync("/metrics", Cancelamento);

        metricas.Should().Contain("doacoes_processadas_total").And.Contain("doacoes_atraso_processamento");
        metricas.Should().Contain("masstransit");
    }

    private async Task<Campanha> SalvarAsync(Campanha campanha)
    {
        await using var banco = worker.NovoContexto();
        banco.Campanhas.Add(campanha);
        await banco.SaveChangesAsync(Cancelamento);
        return campanha;
    }
}
