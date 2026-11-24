using System.Diagnostics.Metrics;

namespace Campanhas.Api.Doacoes;

/// <summary>Métricas de negócio da intenção de doação, exportadas em <c>/metrics</c>.</summary>
public sealed class MetricasDeDoacoes
{
    /// <summary>Nome do medidor.</summary>
    public const string NomeMedidor = "ConexaoSolidaria.Campanhas";

    private readonly Counter<long> _recebidas;
    private readonly Counter<double> _valorRecebido;
    private readonly Counter<long> _recusadas;

    /// <summary>Cria os instrumentos.</summary>
    public MetricasDeDoacoes(IMeterFactory fabrica)
    {
        var medidor = fabrica.Create(NomeMedidor);
        _recebidas = medidor.CreateCounter<long>("doacoes_recebidas", description: "Intenções de doação aceitas pela API.");
        _valorRecebido = medidor.CreateCounter<double>("doacoes_valor_recebido", unit: "BRL", description: "Soma dos valores das intenções aceitas.");
        _recusadas = medidor.CreateCounter<long>("doacoes_recusadas", description: "Intenções recusadas por regra de negócio.");
    }

    /// <summary>Registra uma intenção aceita.</summary>
    public void Recebida(decimal valor)
    {
        _recebidas.Add(1);
        _valorRecebido.Add((double)valor);
    }

    /// <summary>Registra uma intenção recusada.</summary>
    public void Recusada(string motivo) => _recusadas.Add(1, new KeyValuePair<string, object?>("motivo", motivo));
}
