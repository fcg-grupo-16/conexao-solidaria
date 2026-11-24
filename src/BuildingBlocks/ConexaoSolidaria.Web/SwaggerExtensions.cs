using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace ConexaoSolidaria.Web;

/// <summary>Swagger com o botão "Authorize" para o JWT (usado na demonstração e pelos professores).</summary>
public static class SwaggerExtensions
{
    private const string Esquema = "Bearer";

    /// <summary>Gera o documento OpenAPI com o esquema Bearer e os comentários XML dos contratos.</summary>
    /// <param name="builder">Builder.</param>
    /// <param name="titulo">Título exibido no Swagger.</param>
    /// <param name="descricao">Descrição do serviço.</param>
    /// <param name="assembly">Assembly da API (para os comentários XML dos contratos).</param>
    public static WebApplicationBuilder AdicionarSwagger(this WebApplicationBuilder builder, string titulo, string descricao, Assembly assembly)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(opcoes =>
        {
            opcoes.SwaggerDoc("v1", new OpenApiInfo { Title = titulo, Version = "v1", Description = descricao });
            opcoes.AddSecurityDefinition(Esquema, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Cole o token retornado por POST /api/auth/login (sem o prefixo 'Bearer').",
            });
            opcoes.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(Esquema, documento)] = [],
            });

            // Comentários XML da API e dos projetos que ela referencia (contratos e domínio).
            var prefixo = assembly.GetName().Name!.Split('.')[0];
            foreach (var xml in Directory.GetFiles(AppContext.BaseDirectory, "*.xml")
                         .Where(arquivo => Path.GetFileName(arquivo).StartsWith(prefixo, StringComparison.Ordinal)
                                           || Path.GetFileName(arquivo).StartsWith("ConexaoSolidaria", StringComparison.Ordinal)))
            {
                opcoes.IncludeXmlComments(xml);
            }
        });

        return builder;
    }

    /// <summary>
    /// Publica o Swagger em <c>/swagger</c>. Fica ligado em todos os ambientes: é a interface da
    /// demonstração e da correção (o PDF pede autenticação via Postman/Swagger).
    /// </summary>
    public static WebApplication UsarSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(ui =>
        {
            ui.DocumentTitle = app.Environment.ApplicationName;
            ui.EnablePersistAuthorization();
            ui.DisplayRequestDuration();
        });
        return app;
    }
}
