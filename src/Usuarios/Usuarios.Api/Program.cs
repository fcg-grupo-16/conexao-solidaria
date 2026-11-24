using ConexaoSolidaria.Observabilidade;
using ConexaoSolidaria.Web;
using FluentValidation;
using Usuarios.Api.Autenticacao;
using Usuarios.Api.Cadastro;
using Usuarios.Api.Consulta;
using Usuarios.Infrastructure;
using Usuarios.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.AdicionarObservabilidade("usuarios-api");
builder.AdicionarPadroesDeApi();
builder.AdicionarAutenticacaoJwt();
builder.Services.AdicionarInfraestruturaDeUsuarios(builder.Configuration);
builder.Services.AddSingleton<EmissorDeToken>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

app.UsarObservabilidade();
app.UsarPadroesDeApi();
app.UsarAutenticacaoJwt();

if (app.Configuration.GetValue("Banco:AplicarMigrations", true))
{
    await app.Services.AplicarMigrationsAsync();
}

await app.Services.GarantirGestorInicialAsync();

app.MapCadastroEndpoints();
app.MapAutenticacaoEndpoints();
app.MapConsultaEndpoints();

await app.RunAsync();

/// <summary>Ponto de entrada; público para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
