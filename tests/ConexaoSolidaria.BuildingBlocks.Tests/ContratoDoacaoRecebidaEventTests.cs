using ConexaoSolidaria.Contracts.Eventos;

namespace ConexaoSolidaria.BuildingBlocks.Tests;

/// <summary>
/// O MassTransit identifica a mensagem por "urn:message:{namespace}:{nome}". Se este teste falhar,
/// NÃO ajuste a expectativa: publicador e consumidor em versões diferentes deixariam de se enxergar.
/// </summary>
public class ContratoDoacaoRecebidaEventTests
{
    [Fact]
    public void Nome_completo_do_evento_e_estavel()
    {
        typeof(DoacaoRecebidaEvent).FullName
            .Should().Be("ConexaoSolidaria.Contracts.Eventos.DoacaoRecebidaEvent");
    }

    [Fact]
    public void Campos_do_evento_sao_estaveis()
    {
        var campos = typeof(DoacaoRecebidaEvent).GetProperties()
            .ToDictionary(p => p.Name, p => p.PropertyType);

        campos.Should().BeEquivalentTo(new Dictionary<string, Type>
        {
            ["DoacaoId"] = typeof(Guid),
            ["CampanhaId"] = typeof(Guid),
            ["DoadorId"] = typeof(Guid),
            ["Valor"] = typeof(decimal),
            ["OcorridoEm"] = typeof(DateTimeOffset),
        });
    }

    [Fact]
    public void Evento_tem_igualdade_por_valor()
    {
        var momento = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        var campanha = Guid.NewGuid();
        var doador = Guid.NewGuid();

        new DoacaoRecebidaEvent(id, campanha, doador, 50m, momento)
            .Should().Be(new DoacaoRecebidaEvent(id, campanha, doador, 50m, momento));
    }
}
