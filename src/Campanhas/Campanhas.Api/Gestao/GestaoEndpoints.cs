using Campanhas.Domain;
using Campanhas.Infrastructure.Persistencia;
using ConexaoSolidaria.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Campanhas.Api.Gestao;

/// <summary>Gestão de campanhas: exclusiva do perfil GestorONG.</summary>
public static class GestaoEndpoints
{
    /// <summary>Mapeia <c>/api/campanhas</c>.</summary>
    public static IEndpointRouteBuilder MapGestaoEndpoints(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/campanhas")
            .WithTags("Gestão de campanhas")
            .RequireAuthorization(Perfis.GestorONG)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapPost("/", CriarAsync)
            .WithName("CriarCampanha")
            .WithSummary("Cria uma campanha.")
            .WithDescription("Regras: a data de término não pode estar no passado e a meta financeira deve ser maior que zero.");

        grupo.MapPut("/{id:guid}", EditarAsync)
            .WithName("EditarCampanha")
            .WithSummary("Edita os dados e o status de uma campanha.")
            .WithDescription("Campanha cancelada não pode ser editada. Campanha ativa não pode ter término no passado.");

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarCampanhas")
            .WithSummary("Lista todas as campanhas, com filtro opcional por status.");

        grupo.MapGet("/{id:guid}", ObterAsync)
            .WithName("ObterCampanha")
            .WithSummary("Detalhes de uma campanha.");

        return rotas;
    }

    private static async Task<Results<Created<CampanhaResponse>, ValidationProblem>> CriarAsync(
        CriarCampanhaRequest requisicao, CampanhasDbContext banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        var resultado = Campanha.Criar(
            requisicao.Titulo,
            requisicao.Descricao,
            requisicao.DataInicio,
            requisicao.DataFim,
            requisicao.MetaFinanceira,
            requisicao.Status ?? StatusCampanha.Ativa,
            relogio.GetUtcNow());

        if (!resultado.Sucesso)
        {
            return Invalida(resultado);
        }

        banco.Campanhas.Add(resultado.Valor);
        await banco.SaveChangesAsync(cancelamento);
        return TypedResults.Created($"/api/campanhas/{resultado.Valor.Id}", CampanhaResponse.De(resultado.Valor));
    }

    private static async Task<Results<Ok<CampanhaResponse>, NotFound, ValidationProblem>> EditarAsync(
        Guid id, EditarCampanhaRequest requisicao, CampanhasDbContext banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        var campanha = await banco.Campanhas.SingleOrDefaultAsync(c => c.Id == id, cancelamento);
        if (campanha is null)
        {
            return TypedResults.NotFound();
        }

        var resultado = campanha.Editar(
            requisicao.Titulo,
            requisicao.Descricao,
            requisicao.DataInicio,
            requisicao.DataFim,
            requisicao.MetaFinanceira,
            requisicao.Status,
            relogio.GetUtcNow());

        if (!resultado.Sucesso)
        {
            return Invalida(resultado);
        }

        // Só as colunas alteradas entram no UPDATE: o valor arrecadado, que o worker incrementa em
        // paralelo, não é sobrescrito pela edição.
        await banco.SaveChangesAsync(cancelamento);
        return TypedResults.Ok(CampanhaResponse.De(campanha));
    }

    private static async Task<Ok<List<CampanhaResponse>>> ListarAsync(
        StatusCampanha? status, CampanhasDbContext banco, CancellationToken cancelamento)
    {
        var consulta = banco.Campanhas.AsNoTracking();
        if (status is not null)
        {
            consulta = consulta.Where(c => c.Status == status);
        }

        var campanhas = await consulta.OrderByDescending(c => c.CriadaEm).ToListAsync(cancelamento);
        return TypedResults.Ok(campanhas.Select(CampanhaResponse.De).ToList());
    }

    private static async Task<Results<Ok<CampanhaResponse>, NotFound>> ObterAsync(
        Guid id, CampanhasDbContext banco, CancellationToken cancelamento)
    {
        var campanha = await banco.Campanhas.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancelamento);
        return campanha is null ? TypedResults.NotFound() : TypedResults.Ok(CampanhaResponse.De(campanha));
    }

    private static ValidationProblem Invalida(Resultado resultado) =>
        TypedResults.ValidationProblem(resultado.ErrosPorCampo(), title: "A campanha viola regras de negócio.");
}
