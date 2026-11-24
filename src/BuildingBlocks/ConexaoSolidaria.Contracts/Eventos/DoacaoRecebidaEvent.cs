namespace ConexaoSolidaria.Contracts.Eventos;

/// <summary>
/// Publicado pela campanhas-api quando um doador registra uma intenção de doação.
/// Consumido pelo doacoes-worker, que soma o valor ao arrecadado da campanha.
/// </summary>
/// <remarks>
/// Evolução só aditiva: campo novo deve ser opcional. Nunca renomear, mover ou remover campo em uso,
/// porque publicador e consumidor podem estar em versões diferentes durante um deploy.
/// </remarks>
/// <param name="DoacaoId">Identificador da doação; é a chave de idempotência do processamento.</param>
/// <param name="CampanhaId">Campanha que recebe a doação.</param>
/// <param name="DoadorId">Usuário (role Doador) que fez a doação.</param>
/// <param name="Valor">Valor doado, em reais.</param>
/// <param name="OcorridoEm">Momento em que a intenção foi registrada (UTC).</param>
public sealed record DoacaoRecebidaEvent(
    Guid DoacaoId,
    Guid CampanhaId,
    Guid DoadorId,
    decimal Valor,
    DateTimeOffset OcorridoEm);
