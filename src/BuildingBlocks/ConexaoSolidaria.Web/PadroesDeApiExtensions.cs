using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace ConexaoSolidaria.Web;

/// <summary>Configuração HTTP comum: erros em ProblemDetails (RFC 7807) e enums como texto no JSON.</summary>
public static class PadroesDeApiExtensions
{
    /// <summary>Registra ProblemDetails com o traceId e o JSON com enums por nome.</summary>
    public static WebApplicationBuilder AdicionarPadroesDeApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
            contexto.ProblemDetails.Extensions["traceId"] = contexto.HttpContext.TraceIdentifier);

        builder.Services.ConfigureHttpJsonOptions(opcoes =>
            opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return builder;
    }

    /// <summary>Exceção não tratada e status sem corpo viram ProblemDetails, sem stack trace na resposta.</summary>
    public static WebApplication UsarPadroesDeApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }

    /// <summary>Resposta 409 padronizada.</summary>
    public static ProblemHttpResult Conflito(string detalhe) =>
        TypedResults.Problem(title: "Conflito", detail: detalhe, statusCode: StatusCodes.Status409Conflict);
}
