using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ConexaoSolidaria.Web;

/// <summary>Validação do corpo das requisições com FluentValidation em minimal APIs.</summary>
public static class ValidacaoExtensions
{
    /// <summary>
    /// Valida o argumento do tipo <typeparamref name="T"/> antes do handler e responde
    /// 400 com <c>ValidationProblemDetails</c> se houver erro.
    /// </summary>
    public static RouteHandlerBuilder ValidarCom<T>(this RouteHandlerBuilder rota) where T : class =>
        rota.AddEndpointFilter(async (contexto, proximo) =>
        {
            var validador = contexto.HttpContext.RequestServices.GetRequiredService<IValidator<T>>();
            var argumento = contexto.Arguments.OfType<T>().FirstOrDefault();
            if (argumento is null)
            {
                return TypedResults.Problem(
                    title: "Requisição inválida",
                    detail: "O corpo da requisição é obrigatório.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var resultado = await validador.ValidateAsync(argumento, contexto.HttpContext.RequestAborted);
            if (!resultado.IsValid)
            {
                return TypedResults.ValidationProblem(
                    resultado.ToDictionary(),
                    title: "Um ou mais campos são inválidos.");
            }

            return await proximo(contexto);
        })
        .ProducesValidationProblem();
}
