using System.Security.Claims;
using Campanhas.Infrastructure.Persistencia;
using ConexaoSolidaria.Contracts.Eventos;
using ConexaoSolidaria.Web;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Campanhas.Api.Doacoes;

/// <summary>Intenção de doação (perfil Doador).</summary>
public static class DoacoesEndpoints
{
    /// <summary>Mapeia <c>POST /api/doacoes</c> e <c>GET /api/doacoes/{id}</c>.</summary>
    public static IEndpointRouteBuilder MapDoacoesEndpoints(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/doacoes")
            .WithTags("Doações")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapPost("/", RegistrarAsync)
            .WithName("RegistrarDoacao")
            .WithSummary("Registra uma intenção de doação (doador logado).")
            .WithDescription(
                "A API não altera o valor arrecadado: grava a doação como Pendente e publica DoacaoRecebidaEvent " +
                "no RabbitMQ (outbox transacional). O doacoes-worker processa e soma o valor à campanha. " +
                "Campanhas concluídas, canceladas ou fora do prazo recusam a doação (422).")
            .RequireAuthorization(Perfis.Doador)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        grupo.MapGet("/{id:guid}", ObterAsync)
            .WithName("ObterDoacao")
            .WithSummary("Acompanha o processamento de uma doação.")
            .WithDescription("O doador vê só as próprias doações; o gestor vê qualquer uma.")
            .RequireAuthorization(politica => politica.RequireRole(Perfis.Doador, Perfis.GestorONG));

        return rotas;
    }

    private static async Task<Results<Accepted<DoacaoResponse>, ProblemHttpResult, ValidationProblem>> RegistrarAsync(
        RegistrarDoacaoRequest requisicao,
        ClaimsPrincipal usuario,
        CampanhasDbContext banco,
        IPublishEndpoint publicador,
        TimeProvider relogio,
        MetricasDeDoacoes metricas,
        CancellationToken cancelamento)
    {
        var campanha = await banco.Campanhas.AsNoTracking().SingleOrDefaultAsync(c => c.Id == requisicao.IdCampanha, cancelamento);
        if (campanha is null)
        {
            return TypedResults.Problem(
                title: "Campanha não encontrada",
                detail: $"Não existe campanha com o id {requisicao.IdCampanha}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var agora = relogio.GetUtcNow();
        var aceita = campanha.VerificarSeAceitaDoacao(agora);
        if (!aceita.Sucesso)
        {
            metricas.Recusada("campanha_nao_aceita");
            return TypedResults.Problem(
                title: "A campanha não aceita doações",
                detail: aceita.Erros[0].Mensagem,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var resultado = campanha.ReceberDoacao(usuario.IdDoUsuario(), requisicao.ValorDoacao, agora);
        if (!resultado.Sucesso)
        {
            metricas.Recusada("valor_invalido");
            return TypedResults.ValidationProblem(resultado.ErrosPorCampo(), title: "A doação viola regras de negócio.");
        }

        var doacao = resultado.Valor;
        banco.Doacoes.Add(doacao);

        // Com o outbox, o Publish não fala com o RabbitMQ: grava a mensagem na tabela outbox_message,
        // que entra no MESMO commit da doação. Sem doação sem evento, sem evento sem doação.
        await publicador.Publish(
            new DoacaoRecebidaEvent(doacao.Id, doacao.CampanhaId, doacao.DoadorId, doacao.Valor, doacao.CriadaEm),
            cancelamento);
        await banco.SaveChangesAsync(cancelamento);

        metricas.Recebida(doacao.Valor);
        return TypedResults.Accepted($"/api/doacoes/{doacao.Id}", DoacaoResponse.De(doacao));
    }

    private static async Task<Results<Ok<DoacaoResponse>, NotFound>> ObterAsync(
        Guid id, ClaimsPrincipal usuario, CampanhasDbContext banco, CancellationToken cancelamento)
    {
        var doacao = await banco.Doacoes.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, cancelamento);

        // Doação de outro doador responde 404, não 403: não confirma que o id existe.
        if (doacao is null || (!usuario.IsInRole(Perfis.GestorONG) && doacao.DoadorId != usuario.IdDoUsuario()))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(DoacaoResponse.De(doacao));
    }
}
