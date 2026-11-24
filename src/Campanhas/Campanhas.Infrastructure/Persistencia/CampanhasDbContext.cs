using Campanhas.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Campanhas.Infrastructure.Persistencia;

/// <summary>
/// Banco <c>campanhasdb</c> do contexto de Campanhas: usado pela campanhas-api (dona das migrations)
/// e pelo doacoes-worker (processador assíncrono do mesmo contexto). Ver ADR 0002.
/// </summary>
public sealed class CampanhasDbContext(DbContextOptions<CampanhasDbContext> options) : DbContext(options)
{
    /// <summary>Campanhas de arrecadação.</summary>
    public DbSet<Campanha> Campanhas => Set<Campanha>();

    /// <summary>Intenções de doação e seu processamento.</summary>
    public DbSet<Doacao> Doacoes => Set<Doacao>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var campanha = modelBuilder.Entity<Campanha>();
        campanha.ToTable("campanhas", tabela =>
        {
            // Defesa em profundidade: as regras do domínio também valem para qualquer escrita direta.
            tabela.HasCheckConstraint("ck_campanhas_meta_positiva", "meta_financeira > 0");
            tabela.HasCheckConstraint("ck_campanhas_arrecadado_nao_negativo", "valor_arrecadado >= 0");
            tabela.HasCheckConstraint("ck_campanhas_periodo", "data_fim > data_inicio");
        });
        campanha.HasKey(c => c.Id);
        campanha.Property(c => c.Id).ValueGeneratedNever();
        campanha.Property(c => c.Titulo).HasMaxLength(Campanha.TamanhoMaximoTitulo).IsRequired();
        campanha.Property(c => c.Descricao).HasMaxLength(Campanha.TamanhoMaximoDescricao).IsRequired();
        campanha.Property(c => c.MetaFinanceira).HasPrecision(18, 2);
        campanha.Property(c => c.ValorArrecadado).HasPrecision(18, 2).HasDefaultValue(0m);
        campanha.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        // O painel público filtra por status.
        campanha.HasIndex(c => c.Status);

        var doacao = modelBuilder.Entity<Doacao>();
        doacao.ToTable("doacoes", tabela => tabela.HasCheckConstraint("ck_doacoes_valor_positivo", "valor > 0"));
        doacao.HasKey(d => d.Id);
        doacao.Property(d => d.Id).ValueGeneratedNever();
        doacao.Property(d => d.Valor).HasPrecision(18, 2);
        doacao.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        doacao.Property(d => d.MotivoRejeicao).HasMaxLength(300);
        doacao.HasOne<Campanha>().WithMany().HasForeignKey(d => d.CampanhaId).OnDelete(DeleteBehavior.Restrict);
        doacao.HasIndex(d => d.CampanhaId);
        doacao.HasIndex(d => d.DoadorId);

        // Tabelas do outbox transacional do MassTransit: a doação e o DoacaoRecebidaEvent são gravados
        // no mesmo SaveChanges; o evento só vai para o RabbitMQ depois do commit (ADR 0003).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
