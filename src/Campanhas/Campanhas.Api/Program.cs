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
app.UsarPadroesDeApi();
app.UsarAutenticacaoJwt();

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
