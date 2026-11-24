using Campanhas.Domain;

namespace Campanhas.Api.Doacoes;

/// <summary>Intenção de doação.</summary>
/// <param name="IdCampanha">Campanha que recebe a doação.</param>
/// <param name="ValorDoacao">Valor em reais (maior que zero, até duas casas decimais).</param>
public sealed record RegistrarDoacaoRequest(Guid IdCampanha, decimal ValorDoacao);

/// <summary>Situação de uma doação.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="IdCampanha">Campanha.</param>
/// <param name="ValorDoacao">Valor.</param>
/// <param name="Status">Pendente até o worker processar; depois Processada ou Rejeitada.</param>
/// <param name="CriadaEm">Registro da intenção (UTC).</param>
/// <param name="ProcessadaEm">Processamento pelo worker (UTC).</param>
/// <param name="MotivoRejeicao">Motivo, se rejeitada.</param>
public sealed record DoacaoResponse(
    Guid Id,
    Guid IdCampanha,
    decimal ValorDoacao,
    StatusDoacao Status,
    DateTimeOffset CriadaEm,
    DateTimeOffset? ProcessadaEm,
    string? MotivoRejeicao)
{
    /// <summary>Projeção da entidade.</summary>
    public static DoacaoResponse De(Doacao d) =>
        new(d.Id, d.CampanhaId, d.Valor, d.Status, d.CriadaEm, d.ProcessadaEm, d.MotivoRejeicao);
}
