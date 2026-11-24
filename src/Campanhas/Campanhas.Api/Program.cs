using Campanhas.Api.Doacoes;
using Campanhas.Api.Gestao;
using Campanhas.Api.Transparencia;
using Campanhas.Infrastructure;
using ConexaoSolidaria.Mensageria;
using ConexaoSolidaria.Observabilidade;
using ConexaoSolidaria.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AdicionarObservabilidade("campanhas-api", MetricasDeDoacoes.NomeMedidor, MensageriaExtensions.MedidorDoMassTransit);
builder.AdicionarPadroesDeApi();
builder.AdicionarAutenticacaoJwt();
builder.AdicionarSwagger(
    "Conexão Solidária — campanhas-api",
    "Gestão de campanhas (GestorONG), painel de transparência público e intenção de doação (Doador).",
    typeof(Program).Assembly);
builder.Services.AdicionarBancoDeCampanhas(builder.Configuration);
builder.Services.AdicionarCacheDoPainel(builder.Configuration);
builder.Services.AdicionarMensageria(
    builder.Configuration,
    prefixoDasFilas: "campanhas-api",
    brokerEssencial: false,
    registrar: barramento => barramento.AdicionarOutboxDeCampanhas());
builder.Services.AddSingleton<MetricasDeDoacoes>();

var app = builder.Build();

app.UsarObservabilidade();

// Métricas de negócio criadas já na subida: o /metrics as expõe (com zero) antes do primeiro uso.
_ = app.Services.GetRequiredService<MetricasDeDoacoes>();
app.UsarPadroesDeApi();
app.UsarAutenticacaoJwt();
app.UsarSwagger();

if (app.Configuration.GetValue("Banco:AplicarMigrations", true))
{
    await app.Services.AplicarMigrationsDeCampanhasAsync();
}

app.MapGestaoEndpoints();
app.MapDoacoesEndpoints();
app.MapTransparenciaEndpoints();

await app.RunAsync();

/// <summary>Ponto de entrada; público para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
