using Campanhas.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Campanhas.IntegrationTests;

public class PersistenciaDeCampanhasTests(BancoDeCampanhasFixture banco)
{
    private static readonly DateTimeOffset Agora = DateTimeOffset.UtcNow;

    private static Campanha NovaCampanha(decimal meta = 12_345.67m) =>
        Campanha.Criar("Páscoa Feliz", "Chocolates para as crianças.", Agora.AddDays(-1), Agora.AddDays(20), meta, StatusCampanha.Ativa, Agora).Valor;

    [Fact]
    public async Task Campanha_e_doacao_fazem_ida_e_volta_sem_perder_precisao()
    {
        var campanha = NovaCampanha();
        var doacao = campanha.ReceberDoacao(Guid.NewGuid(), 99.99m, Agora).Valor;
        await using (var escrita = banco.NovoContexto())
        {
            escrita.Campanhas.Add(campanha);
            escrita.Doacoes.Add(doacao);
            await escrita.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var leitura = banco.NovoContexto();
        var lida = await leitura.Campanhas.SingleAsync(c => c.Id == campanha.Id, TestContext.Current.CancellationToken);
        var doacaoLida = await leitura.Doacoes.SingleAsync(d => d.Id == doacao.Id, TestContext.Current.CancellationToken);

        lida.MetaFinanceira.Should().Be(12_345.67m);
        lida.Status.Should().Be(StatusCampanha.Ativa);
        lida.ValorArrecadado.Should().Be(0m);
        lida.DataFim.Should().BeCloseTo(campanha.DataFim, TimeSpan.FromMilliseconds(1));
        doacaoLida.Valor.Should().Be(99.99m);
        doacaoLida.Status.Should().Be(StatusDoacao.Pendente);
    }

    [Fact]
    public async Task Status_e_gravado_como_texto()
    {
        var campanha = NovaCampanha();
        await using var contexto = banco.NovoContexto();
        contexto.Campanhas.Add(campanha);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        var status = await contexto.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM campanhas WHERE id = {campanha.Id}")
            .SingleAsync(TestContext.Current.CancellationToken);

        status.Should().Be("Ativa");
    }

    [Fact]
    public async Task Banco_recusa_meta_nao_positiva_mesmo_em_escrita_direta()
    {
        var campanha = NovaCampanha();
        await using var contexto = banco.NovoContexto();
        contexto.Campanhas.Add(campanha);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        var atualizar = () => contexto.Database.ExecuteSqlAsync(
            $"UPDATE campanhas SET meta_financeira = 0 WHERE id = {campanha.Id}", TestContext.Current.CancellationToken);

        (await atualizar.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task Doacao_para_campanha_inexistente_e_recusada_pela_chave_estrangeira()
    {
        var doacao = NovaCampanha().ReceberDoacao(Guid.NewGuid(), 10m, Agora).Valor;
        await using var contexto = banco.NovoContexto();
        contexto.Doacoes.Add(doacao);

        var salvar = () => contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        await salvar.Should().ThrowAsync<DbUpdateException>();
    }
}
