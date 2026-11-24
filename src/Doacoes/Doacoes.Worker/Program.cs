using Campanhas.Infrastructure;
using ConexaoSolidaria.Mensageria;
using ConexaoSolidaria.Observabilidade;
using Doacoes.Worker.Processamento;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.AdicionarObservabilidade("doacoes-worker", MetricasDoWorker.NomeMedidor);
builder.Services.AdicionarBancoDeCampanhas(builder.Configuration);
builder.Services.AdicionarCacheDoPainel(builder.Configuration);
builder.Services.AdicionarMensageria(
    builder.Configuration,
    prefixoDasFilas: "doacoes-worker",
    brokerEssencial: true,
    registrar: barramento => barramento.AddConsumer<DoacaoRecebidaConsumer, DoacaoRecebidaConsumerDefinition>());
builder.Services.AddSingleton<MetricasDoWorker>();

var app = builder.Build();

app.UsarObservabilidade();
app.MapGet("/", () => Results.Ok(new { servico = "doacoes-worker", fila = "doacoes-worker-doacao-recebida" }))
    .ExcludeFromDescription();

// O barramento só começa a consumir no RunAsync, depois que o esquema existir.
await app.Services.AguardarEsquemaDeCampanhasAsync(
    TimeSpan.FromSeconds(app.Configuration.GetValue("Banco:SegundosParaAguardarEsquema", 300)));

await app.RunAsync();

/// <summary>Ponto de entrada; público para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
