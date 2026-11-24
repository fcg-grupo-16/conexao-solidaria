namespace Campanhas.Domain.Tests;

/// <summary>Datas e campanhas de referência; o relógio é fixo para as regras de data serem determinísticas.</summary>
internal static class Cenario
{
    public static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Inicio = Agora.AddDays(-1);
    public static readonly DateTimeOffset Fim = Agora.AddDays(30);

    public static Resultado<Campanha> Criar(
        string? titulo = "Natal Solidário",
        string? descricao = "Brinquedos e cestas para as crianças acolhidas.",
        DateTimeOffset? inicio = null,
        DateTimeOffset? fim = null,
        decimal meta = 10_000m,
        StatusCampanha status = StatusCampanha.Ativa,
        DateTimeOffset? agora = null) =>
        Campanha.Criar(titulo, descricao, inicio ?? Inicio, fim ?? Fim, meta, status, agora ?? Agora);

    public static Campanha Ativa() => Criar().Valor;

    public static Campanha ComStatus(StatusCampanha status)
    {
        var campanha = Ativa();
        campanha.Editar(campanha.Titulo, campanha.Descricao, campanha.DataInicio, campanha.DataFim, campanha.MetaFinanceira, status, Agora)
            .Sucesso.Should().BeTrue();
        return campanha;
    }
}
