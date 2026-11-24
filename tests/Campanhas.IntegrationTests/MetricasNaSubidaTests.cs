namespace Campanhas.IntegrationTests;

public class MetricasNaSubidaTests(CampanhasApiFactory api)
{
    [Fact]
    public async Task Metricas_de_negocio_aparecem_antes_da_primeira_doacao()
    {
        // Réplica recém-criada (factory nova): nenhuma doação passou por ela.
        await using var replicaNova = api.WithWebHostBuilder(_ => { });

        var metricas = await replicaNova.CreateClient().GetStringAsync("/metrics", TestContext.Current.CancellationToken);

        metricas.Should().Contain("doacoes_recebidas_total").And.Contain("doacoes_recusadas_total");
    }
}
