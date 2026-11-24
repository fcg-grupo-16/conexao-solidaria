using Campanhas.Domain;

namespace Campanhas.Api.Gestao;

/// <summary>Dados de criação de uma campanha.</summary>
/// <param name="Titulo">Título exibido no painel público.</param>
/// <param name="Descricao">Descrição da campanha.</param>
/// <param name="DataInicio">Início da arrecadação (ISO 8601, com fuso).</param>
/// <param name="DataFim">Fim da arrecadação; não pode estar no passado.</param>
/// <param name="MetaFinanceira">Meta em reais; maior que zero.</param>
/// <param name="Status">Ativa (padrão), Concluida ou Cancelada.</param>
public sealed record CriarCampanhaRequest(
    string? Titulo,
    string? Descricao,
    DateTimeOffset DataInicio,
    DateTimeOffset DataFim,
    decimal MetaFinanceira,
    StatusCampanha? Status);

/// <summary>Dados de edição de uma campanha (todos os campos, inclusive o status).</summary>
/// <param name="Titulo">Título.</param>
/// <param name="Descricao">Descrição.</param>
/// <param name="DataInicio">Início da arrecadação.</param>
/// <param name="DataFim">Fim da arrecadação.</param>
/// <param name="MetaFinanceira">Meta em reais; maior que zero.</param>
/// <param name="Status">Ativa, Concluida ou Cancelada.</param>
public sealed record EditarCampanhaRequest(
    string? Titulo,
    string? Descricao,
    DateTimeOffset DataInicio,
    DateTimeOffset DataFim,
    decimal MetaFinanceira,
    StatusCampanha Status);

/// <summary>Visão completa da campanha, para a gestão.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Titulo">Título.</param>
/// <param name="Descricao">Descrição.</param>
/// <param name="DataInicio">Início da arrecadação (UTC).</param>
/// <param name="DataFim">Fim da arrecadação (UTC).</param>
/// <param name="MetaFinanceira">Meta em reais.</param>
/// <param name="ValorArrecadado">Soma das doações processadas pelo worker.</param>
/// <param name="Status">Situação.</param>
/// <param name="CriadaEm">Criação (UTC).</param>
/// <param name="AtualizadaEm">Última edição (UTC).</param>
public sealed record CampanhaResponse(
    Guid Id,
    string Titulo,
    string Descricao,
    DateTimeOffset DataInicio,
    DateTimeOffset DataFim,
    decimal MetaFinanceira,
    decimal ValorArrecadado,
    StatusCampanha Status,
    DateTimeOffset CriadaEm,
    DateTimeOffset? AtualizadaEm)
{
    /// <summary>Projeção da entidade.</summary>
    public static CampanhaResponse De(Campanha c) =>
        new(c.Id, c.Titulo, c.Descricao, c.DataInicio, c.DataFim, c.MetaFinanceira, c.ValorArrecadado, c.Status, c.CriadaEm, c.AtualizadaEm);
}
