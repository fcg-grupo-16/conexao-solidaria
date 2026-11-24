using System.Diagnostics.Metrics;

namespace Doacoes.Worker.Processamento;

/// <summary>Métricas de negócio do processamento, exportadas em <c>/metrics</c>.</summary>
public sealed class MetricasDoWorker
{
    /// <summary>Nome do medidor.</summary>
    public const string NomeMedidor = "ConexaoSolidaria.Doacoes";

    private readonly Counter<long> _processadas;
    private readonly Counter<double> _valorProcessado;
    private readonly Counter<long> _rejeitadas;
    private readonly Counter<long> _duplicadas;
    private readonly Histogram<double> _atraso;

    /// <summary>Cria os instrumentos.</summary>
    public MetricasDoWorker(IMeterFactory fabrica)
    {
        var medidor = fabrica.Create(NomeMedidor);
        _processadas = medidor.CreateCounter<long>("doacoes_processadas", description: "Doações somadas ao arrecadado.");
        _valorProcessado = medidor.CreateCounter<double>("doacoes_valor_processado", unit: "BRL", description: "Soma dos valores processados.");
        _rejeitadas = medidor.CreateCounter<long>("doacoes_rejeitadas", description: "Doações recusadas no processamento.");
        _duplicadas = medidor.CreateCounter<long>("doacoes_reentregas_ignoradas", description: "Mensagens reentregues de doações já processadas.");
        _atraso = medidor.CreateHistogram<double>(
            "doacoes_atraso_processamento",
            unit: "s",
            description: "Tempo entre a intenção de doação e o processamento pelo worker.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 30, 60] });

        // Séries publicadas com zero desde a subida (ver MetricasDeDoacoes na campanhas-api).
        _processadas.Add(0);
        _valorProcessado.Add(0);
        _rejeitadas.Add(0);
        _duplicadas.Add(0);
    }

    /// <summary>Doação somada.</summary>
    public void Processada(decimal valor, TimeSpan atraso)
    {
        _processadas.Add(1);
        _valorProcessado.Add((double)valor);
        _atraso.Record(Math.Max(0, atraso.TotalSeconds));
    }

    /// <summary>Doação recusada.</summary>
    public void Rejeitada() => _rejeitadas.Add(1);

    /// <summary>Reentrega de uma doação que já tinha sido processada.</summary>
    public void ReentregaIgnorada() => _duplicadas.Add(1);
}
