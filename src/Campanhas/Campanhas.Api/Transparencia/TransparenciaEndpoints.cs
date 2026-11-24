using Campanhas.Infrastructure.Painel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Campanhas.Api.Transparencia;

/// <summary>Painel de transparência: público, sem autenticação.</summary>
public static class TransparenciaEndpoints
{
    /// <summary>Mapeia <c>GET /api/transparencia/campanhas</c>.</summary>
    public static IEndpointRouteBuilder MapTransparenciaEndpoints(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGet("/api/transparencia/campanhas", ListarAsync)
            .WithName("PainelDeTransparencia")
            .WithTags("Transparência")
            .WithSummary("Campanhas ativas com meta e valor arrecadado (acesso público).")
            .WithDescription(
                "Retorna apenas campanhas com status Ativa. O valor arrecadado é a soma das doações já processadas " +
                "pelo doacoes-worker. O header X-Cache indica se a resposta veio do Redis (HIT) ou do banco (MISS).")
            .AllowAnonymous();

        return rotas;
    }

    private static async Task<Ok<IReadOnlyList<CampanhaPublica>>> ListarAsync(
        PainelDeTransparencia painel, HttpResponse resposta, CancellationToken cancelamento)
    {
        var consulta = await painel.ObterAsync(cancelamento);
        resposta.Headers["X-Cache"] = consulta.DoCache ? "HIT" : "MISS";
        return TypedResults.Ok(consulta.Campanhas);
    }
}
