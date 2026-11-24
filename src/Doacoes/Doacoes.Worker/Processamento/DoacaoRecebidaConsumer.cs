using Campanhas.Domain;
using Campanhas.Infrastructure.Painel;
using Campanhas.Infrastructure.Persistencia;
using ConexaoSolidaria.Contracts.Eventos;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Doacoes.Worker.Processamento;

/// <summary>
/// Soma cada doação ao "Valor Total Arrecadado" da campanha, exatamente uma vez.
/// </summary>
/// <remarks>
/// O RabbitMQ entrega pelo menos uma vez, então a mesma mensagem pode chegar de novo. A guarda é a
/// transição <c>Pendente → Processada</c> feita por um UPDATE condicional: só a primeira entrega muda a
/// linha (o PostgreSQL bloqueia a linha e reavalia o WHERE nas concorrentes), e o incremento do
/// arrecadado só acontece, na mesma transação, se essa transição aconteceu.
/// </remarks>
public sealed class DoacaoRecebidaConsumer(
    CampanhasDbContext banco,
    TimeProvider relogio,
    InvalidadorDoPainel painel,
    MetricasDoWorker metricas,
    ILogger<DoacaoRecebidaConsumer> log) : IConsumer<DoacaoRecebidaEvent>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<DoacaoRecebidaEvent> contexto)
    {
        var evento = contexto.Message;
        var cancelamento = contexto.CancellationToken;

        var campanha = await banco.Campanhas.AsNoTracking().SingleOrDefaultAsync(c => c.Id == evento.CampanhaId, cancelamento);
        if (campanha is null)
        {
            // A FK impede doação sem campanha; chegar aqui é inconsistência. Lançar manda para a fila
            // _error depois das tentativas, para análise, em vez de perder a mensagem em silêncio.
            throw new InvalidOperationException($"Campanha {evento.CampanhaId} da doação {evento.DoacaoId} não existe.");
        }

        var agora = relogio.GetUtcNow();

        // Prazo avaliado no momento da INTENÇÃO: uma doação feita no último minuto não é recusada só
        // porque o worker a processou depois do fim. O status (cancelada/concluída) vale o atual.
        var aceita = campanha.VerificarSeAceitaDoacao(evento.OcorridoEm);

        await using var transacao = await banco.Database.BeginTransactionAsync(cancelamento);

        if (!aceita.Sucesso)
        {
            var motivo = aceita.Erros[0].Mensagem;
            var rejeitadas = await banco.Doacoes
                .Where(d => d.Id == evento.DoacaoId && d.Status == StatusDoacao.Pendente)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(d => d.Status, StatusDoacao.Rejeitada)
                        .SetProperty(d => d.ProcessadaEm, agora)
                        .SetProperty(d => d.MotivoRejeicao, motivo),
                    cancelamento);
            await transacao.CommitAsync(cancelamento);

            if (rejeitadas == 1)
            {
                metricas.Rejeitada();
                log.LogWarning("Doação {DoacaoId} rejeitada: {Motivo}", evento.DoacaoId, motivo);
            }
            else
            {
                metricas.ReentregaIgnorada();
            }

            return;
        }

        var processadas = await banco.Doacoes
            .Where(d => d.Id == evento.DoacaoId && d.Status == StatusDoacao.Pendente)
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.Status, StatusDoacao.Processada).SetProperty(d => d.ProcessadaEm, agora),
                cancelamento);

        if (processadas == 0)
        {
            await transacao.CommitAsync(cancelamento);
            metricas.ReentregaIgnorada();
            log.LogInformation("Doação {DoacaoId} já processada; reentrega ignorada", evento.DoacaoId);
            return;
        }

        // Incremento atômico no banco: réplicas do worker somando na mesma campanha não perdem valor.
        await banco.Campanhas
            .Where(c => c.Id == evento.CampanhaId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ValorArrecadado, c => c.ValorArrecadado + evento.Valor), cancelamento);

        await transacao.CommitAsync(cancelamento);

        // Depois do commit: o painel público passa a mostrar o valor novo na próxima leitura.
        await painel.InvalidarAsync(cancelamento);

        metricas.Processada(evento.Valor, agora - evento.OcorridoEm);
        log.LogInformation(
            "Doação {DoacaoId} de {Valor:N2} somada à campanha {CampanhaId}",
            evento.DoacaoId,
            evento.Valor,
            evento.CampanhaId);
    }
}

/// <summary>Retry e concorrência da fila do consumidor.</summary>
public sealed class DoacaoRecebidaConsumerDefinition : ConsumerDefinition<DoacaoRecebidaConsumer>
{
    /// <inheritdoc />
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpoint,
        IConsumerConfigurator<DoacaoRecebidaConsumer> consumidor,
        IRegistrationContext contexto)
    {
        endpoint.PrefetchCount = 16;
        endpoint.ConcurrentMessageLimit = 8;

        // Falha transitória (banco reiniciando, deadlock): tenta de novo com espera crescente.
        // Esgotadas as tentativas, a mensagem vai para a fila doacoes-worker-doacao-recebida_error.
        endpoint.UseMessageRetry(retry => retry.Intervals(
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10)));
    }
}
