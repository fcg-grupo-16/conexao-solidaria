namespace Campanhas.Domain;

/// <summary>Intenção de doação de um doador para uma campanha.</summary>
public sealed class Doacao
{
    /// <summary>Maior doação aceita numa única intenção.</summary>
    public const decimal ValorMaximo = 1_000_000m;

    // Construtor do EF Core.
    private Doacao()
    {
    }

    private Doacao(Guid id, Guid campanhaId, Guid doadorId, decimal valor, DateTimeOffset criadaEm)
    {
        Id = id;
        CampanhaId = campanhaId;
        DoadorId = doadorId;
        Valor = valor;
        Status = StatusDoacao.Pendente;
        CriadaEm = criadaEm;
    }

    /// <summary>Identificador; chave de idempotência do processamento.</summary>
    public Guid Id { get; private set; }

    /// <summary>Campanha que recebe a doação.</summary>
    public Guid CampanhaId { get; private set; }

    /// <summary>Doador (claim <c>sub</c> do token).</summary>
    public Guid DoadorId { get; private set; }

    /// <summary>Valor doado, em reais.</summary>
    public decimal Valor { get; private set; }

    /// <summary>Situação no processamento assíncrono.</summary>
    public StatusDoacao Status { get; private set; }

    /// <summary>Momento em que a intenção foi registrada.</summary>
    public DateTimeOffset CriadaEm { get; private set; }

    /// <summary>Momento em que o worker processou (ou rejeitou) a doação.</summary>
    public DateTimeOffset? ProcessadaEm { get; private set; }

    /// <summary>Motivo da rejeição, quando houver.</summary>
    public string? MotivoRejeicao { get; private set; }

    internal static Resultado<Doacao> Criar(Guid campanhaId, Guid doadorId, decimal valor, DateTimeOffset agora)
    {
        var erros = new List<ErroDeDominio>();
        if (valor <= 0)
        {
            erros.Add(new("ValorDoacao", "O valor da doação deve ser maior que zero."));
        }
        else if (valor > ValorMaximo)
        {
            erros.Add(new("ValorDoacao", $"O valor da doação deve ser de no máximo {ValorMaximo:N2}."));
        }
        else if (decimal.Round(valor, 2) != valor)
        {
            erros.Add(new("ValorDoacao", "O valor da doação deve ter no máximo duas casas decimais."));
        }

        if (doadorId == Guid.Empty)
        {
            erros.Add(new("Doador", "Doador não identificado."));
        }

        return erros.Count > 0
            ? Resultado<Doacao>.Falha(erros)
            : Resultado<Doacao>.Ok(new Doacao(Guid.CreateVersion7(agora), campanhaId, doadorId, valor, agora.ToUniversalTime()));
    }
}
