using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Campanhas.IntegrationTests;

/// <summary>
/// Fila temporária ligada ao exchange do DoacaoRecebidaEvent: recebe uma cópia de cada evento
/// publicado (fanout), como o doacoes-worker receberia.
/// </summary>
internal sealed class EscutaDoExchange : IAsyncDisposable
{
    private readonly IConnection _conexao;
    private readonly IChannel _canal;
    private readonly string _fila;

    private EscutaDoExchange(IConnection conexao, IChannel canal, string fila)
    {
        _conexao = conexao;
        _canal = canal;
        _fila = fila;
    }

    public static async Task<EscutaDoExchange> CriarAsync(Uri broker, string exchange)
    {
        var conexao = await ConectarAsync(broker);
        var canal = await conexao.CreateChannelAsync();
        // Mesmas propriedades que o MassTransit usa (fanout, durável): declarar de novo é idempotente.
        await canal.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false);
        var fila = await canal.QueueDeclareAsync(string.Empty, durable: false, exclusive: true, autoDelete: true);
        await canal.QueueBindAsync(fila.QueueName, exchange, string.Empty);
        return new EscutaDoExchange(conexao, canal, fila.QueueName);
    }

    // O container avisa que está pronto pelo log; sob carga (várias suítes subindo containers ao mesmo
    // tempo) o listener AMQP ainda pode recusar o handshake por alguns instantes.
    private static async Task<IConnection> ConectarAsync(Uri broker)
    {
        var fabrica = new ConnectionFactory { Uri = broker, RequestedConnectionTimeout = TimeSpan.FromSeconds(10) };
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                return await fabrica.CreateConnectionAsync();
            }
            catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException) when (tentativa < 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }

    /// <summary>Espera um evento cujo corpo satisfaça o filtro; devolve o envelope do MassTransit.</summary>
    public async Task<JsonElement?> EsperarAsync(Func<JsonElement, bool> filtro, TimeSpan limite)
    {
        var prazo = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < prazo)
        {
            var entrega = await _canal.BasicGetAsync(_fila, autoAck: true);
            if (entrega is null)
            {
                await Task.Delay(200);
                continue;
            }

            var envelope = JsonDocument.Parse(Encoding.UTF8.GetString(entrega.Body.Span)).RootElement.Clone();
            if (filtro(envelope))
            {
                return envelope;
            }
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await _canal.DisposeAsync();
        await _conexao.DisposeAsync();
    }
}
