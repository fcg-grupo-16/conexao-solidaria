namespace Campanhas.Domain;

/// <summary>Campanha de arrecadação da ONG.</summary>
/// <remarks>
/// <see cref="ValorArrecadado"/> não tem método de escrita no domínio: só o doacoes-worker o altera,
/// com um incremento atômico no banco, ao processar cada doação.
/// </remarks>
public sealed class Campanha
{
    /// <summary>Tamanho máximo do título.</summary>
    public const int TamanhoMaximoTitulo = 150;

    /// <summary>Tamanho máximo da descrição.</summary>
    public const int TamanhoMaximoDescricao = 2000;

    /// <summary>Maior meta aceita (cabe com folga em <c>numeric(18,2)</c>).</summary>
    public const decimal MetaMaxima = 1_000_000_000m;

    // Construtor do EF Core.
    private Campanha()
    {
        Titulo = null!;
        Descricao = null!;
    }

    private Campanha(Guid id, DateTimeOffset criadaEm)
    {
        Id = id;
        CriadaEm = criadaEm;
        Titulo = null!;
        Descricao = null!;
    }

    /// <summary>Identificador.</summary>
    public Guid Id { get; private set; }

    /// <summary>Título exibido no painel de transparência.</summary>
    public string Titulo { get; private set; }

    /// <summary>Descrição da campanha.</summary>
    public string Descricao { get; private set; }

    /// <summary>Início do período de arrecadação.</summary>
    public DateTimeOffset DataInicio { get; private set; }

    /// <summary>Fim do período de arrecadação.</summary>
    public DateTimeOffset DataFim { get; private set; }

    /// <summary>Meta financeira, em reais. Sempre maior que zero.</summary>
    public decimal MetaFinanceira { get; private set; }

    /// <summary>Situação da campanha.</summary>
    public StatusCampanha Status { get; private set; }

    /// <summary>Soma das doações processadas pelo worker.</summary>
    public decimal ValorArrecadado { get; private set; }

    /// <summary>Momento da criação.</summary>
    public DateTimeOffset CriadaEm { get; private set; }

    /// <summary>Momento da última edição pelo gestor.</summary>
    public DateTimeOffset? AtualizadaEm { get; private set; }

    /// <summary>
    /// Cria uma campanha. Regras do PDF: a data de término não pode estar no passado e a meta
    /// financeira deve ser maior que zero.
    /// </summary>
    public static Resultado<Campanha> Criar(
        string? titulo,
        string? descricao,
        DateTimeOffset dataInicio,
        DateTimeOffset dataFim,
        decimal metaFinanceira,
        StatusCampanha status,
        DateTimeOffset agora)
    {
        var erros = ValidarCampos(titulo, descricao, dataInicio, dataFim, metaFinanceira, status);
        if (dataFim < agora)
        {
            erros.Add(new ErroDeDominio(nameof(DataFim), "A data de término não pode estar no passado."));
        }

        if (erros.Count > 0)
        {
            return Resultado<Campanha>.Falha(erros);
        }

        var campanha = new Campanha(Guid.CreateVersion7(agora), agora.ToUniversalTime());
        campanha.Aplicar(titulo!, descricao!, dataInicio, dataFim, metaFinanceira, status);
        return Resultado<Campanha>.Ok(campanha);
    }

    /// <summary>
    /// Edita os dados e o status. Campanha cancelada é estado final. Uma campanha que continua
    /// <see cref="StatusCampanha.Ativa"/> não pode ficar com término no passado; para encerrar uma
    /// campanha vencida, o gestor muda o status para <see cref="StatusCampanha.Concluida"/>.
    /// </summary>
    public Resultado Editar(
        string? titulo,
        string? descricao,
        DateTimeOffset dataInicio,
        DateTimeOffset dataFim,
        decimal metaFinanceira,
        StatusCampanha status,
        DateTimeOffset agora)
    {
        if (Status == StatusCampanha.Cancelada)
        {
            return Resultado.Falha(nameof(Status), "Campanha cancelada não pode ser editada.");
        }

        var erros = ValidarCampos(titulo, descricao, dataInicio, dataFim, metaFinanceira, status);
        if (status == StatusCampanha.Ativa && dataFim < agora)
        {
            erros.Add(new ErroDeDominio(nameof(DataFim), "Uma campanha ativa não pode ter a data de término no passado."));
        }

        if (erros.Count > 0)
        {
            return Resultado.Falha(erros);
        }

        Aplicar(titulo!, descricao!, dataInicio, dataFim, metaFinanceira, status);
        AtualizadaEm = agora.ToUniversalTime();
        return Resultado.Ok();
    }

    /// <summary>
    /// Regra do PDF: não se doa para campanha encerrada ou cancelada. Encerrada = concluída pela ONG
    /// ou fora do período de arrecadação.
    /// </summary>
    public Resultado VerificarSeAceitaDoacao(DateTimeOffset agora) => Status switch
    {
        StatusCampanha.Cancelada => Resultado.Falha("IdCampanha", "A campanha foi cancelada e não aceita doações."),
        StatusCampanha.Concluida => Resultado.Falha("IdCampanha", "A campanha foi concluída e não aceita doações."),
        _ when agora > DataFim => Resultado.Falha("IdCampanha", "A campanha está encerrada: o prazo de arrecadação terminou."),
        _ when agora < DataInicio => Resultado.Falha("IdCampanha", "A campanha ainda não começou a receber doações."),
        _ => Resultado.Ok(),
    };

    /// <summary>
    /// Registra a intenção de doação como <see cref="StatusDoacao.Pendente"/>. O valor só entra no
    /// arrecadado quando o doacoes-worker processar o evento.
    /// </summary>
    public Resultado<Doacao> ReceberDoacao(Guid doadorId, decimal valor, DateTimeOffset agora)
    {
        var aceita = VerificarSeAceitaDoacao(agora);
        if (!aceita.Sucesso)
        {
            return Resultado<Doacao>.Falha(aceita.Erros);
        }

        return Doacao.Criar(Id, doadorId, valor, agora);
    }

    private void Aplicar(string titulo, string descricao, DateTimeOffset dataInicio, DateTimeOffset dataFim, decimal meta, StatusCampanha status)
    {
        Titulo = titulo.Trim();
        Descricao = descricao.Trim();
        DataInicio = dataInicio.ToUniversalTime();
        DataFim = dataFim.ToUniversalTime();
        MetaFinanceira = meta;
        Status = status;
    }

    private static List<ErroDeDominio> ValidarCampos(
        string? titulo, string? descricao, DateTimeOffset dataInicio, DateTimeOffset dataFim, decimal meta, StatusCampanha status)
    {
        var erros = new List<ErroDeDominio>();

        if (string.IsNullOrWhiteSpace(titulo))
        {
            erros.Add(new(nameof(Titulo), "Informe o título."));
        }
        else if (titulo.Trim().Length > TamanhoMaximoTitulo)
        {
            erros.Add(new(nameof(Titulo), $"O título deve ter no máximo {TamanhoMaximoTitulo} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            erros.Add(new(nameof(Descricao), "Informe a descrição."));
        }
        else if (descricao.Trim().Length > TamanhoMaximoDescricao)
        {
            erros.Add(new(nameof(Descricao), $"A descrição deve ter no máximo {TamanhoMaximoDescricao} caracteres."));
        }

        if (dataFim <= dataInicio)
        {
            erros.Add(new(nameof(DataFim), "A data de término deve ser posterior à data de início."));
        }

        if (meta <= 0)
        {
            erros.Add(new(nameof(MetaFinanceira), "A meta financeira deve ser maior que zero."));
        }
        else if (meta > MetaMaxima)
        {
            erros.Add(new(nameof(MetaFinanceira), $"A meta financeira deve ser de no máximo {MetaMaxima:N2}."));
        }
        else if (decimal.Round(meta, 2) != meta)
        {
            erros.Add(new(nameof(MetaFinanceira), "A meta financeira deve ter no máximo duas casas decimais."));
        }

        if (!Enum.IsDefined(status))
        {
            erros.Add(new(nameof(Status), "Status inválido. Use Ativa, Concluida ou Cancelada."));
        }

        return erros;
    }
}
