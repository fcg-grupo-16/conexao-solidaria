using ConexaoSolidaria.Observabilidade;
using ConexaoSolidaria.Web;
using FluentValidation;
using Usuarios.Api.Cadastro;
using Usuarios.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AdicionarObservabilidade("usuarios-api");
builder.AdicionarPadroesDeApi();
builder.Services.AdicionarInfraestruturaDeUsuarios(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

app.UsarObservabilidade();
app.UsarPadroesDeApi();

if (app.Configuration.GetValue("Banco:AplicarMigrations", true))
{
    await app.Services.AplicarMigrationsAsync();
}

app.MapCadastroEndpoints();

await app.RunAsync();

/// <summary>Ponto de entrada; público para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
