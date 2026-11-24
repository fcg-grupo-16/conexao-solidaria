using Campanhas.Api.Gestao;
using Campanhas.Infrastructure;
using ConexaoSolidaria.Observabilidade;
using ConexaoSolidaria.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AdicionarObservabilidade("campanhas-api");
builder.AdicionarPadroesDeApi();
builder.AdicionarAutenticacaoJwt();
builder.Services.AdicionarBancoDeCampanhas(builder.Configuration);

var app = builder.Build();

app.UsarObservabilidade();
app.UsarPadroesDeApi();
app.UsarAutenticacaoJwt();

if (app.Configuration.GetValue("Banco:AplicarMigrations", true))
{
    await app.Services.AplicarMigrationsDeCampanhasAsync();
}

app.MapGestaoEndpoints();

await app.RunAsync();

/// <summary>Ponto de entrada; público para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
