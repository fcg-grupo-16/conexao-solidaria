using ConexaoSolidaria.Observabilidade;
using ConexaoSolidaria.Web;
using FluentValidation;
using Usuarios.Api;
using Usuarios.Api.Autenticacao;
using Usuarios.Api.Cadastro;
using Usuarios.Api.Consulta;
using Usuarios.Infrastructure;
using Usuarios.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.AdicionarObservabilidade("usuarios-api", MetricasDeUsuarios.NomeMedidor);
builder.AdicionarPadroesDeApi();
builder.AdicionarAutenticacaoJwt();
builder.AdicionarSwagger(
    "Conexão Solidária — usuarios-api",
    "Cadastro público de doadores, login e emissão do JWT (perfis GestorONG e Doador).",
    typeof(Program).Assembly);
builder.Services.AdicionarInfraestruturaDeUsuarios(builder.Configuration);
builder.Services.AddSingleton<EmissorDeToken>();
builder.Services.AddSingleton<MetricasDeUsuarios>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

app.UsarObservabilidade();

// Métricas de negócio criadas já na subida: o /metrics as expõe (com zero) antes do primeiro uso.
_ = app.Services.GetRequiredService<MetricasDeUsuarios>();
app.UsarPadroesDeApi();
app.UsarAutenticacaoJwt();
app.UsarSwagger();

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
