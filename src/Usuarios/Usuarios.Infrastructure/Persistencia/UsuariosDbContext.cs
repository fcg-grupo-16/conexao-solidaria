using Microsoft.EntityFrameworkCore;
using Usuarios.Domain;

namespace Usuarios.Infrastructure.Persistencia;

/// <summary>Banco <c>usuariosdb</c>: exclusivo da usuarios-api.</summary>
public sealed class UsuariosDbContext(DbContextOptions<UsuariosDbContext> options) : DbContext(options)
{
    /// <summary>Usuários cadastrados.</summary>
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var usuario = modelBuilder.Entity<Usuario>();
        usuario.ToTable("usuarios");
        usuario.HasKey(u => u.Id);
        usuario.Property(u => u.Id).ValueGeneratedNever();
        usuario.Property(u => u.NomeCompleto).HasMaxLength(Usuario.TamanhoMaximoNome).IsRequired();
        usuario.Property(u => u.Email).HasMaxLength(Usuario.TamanhoMaximoEmail).IsRequired();
        usuario.Property(u => u.Cpf)
            .HasConversion(cpf => cpf.Numero, numero => Cpf.Criar(numero))
            .HasMaxLength(11)
            .IsFixedLength()
            .IsRequired();
        usuario.Property(u => u.SenhaHash).HasMaxLength(100).IsRequired();
        usuario.Property(u => u.Perfil).HasConversion<string>().HasMaxLength(20).IsRequired();
        usuario.Property(u => u.CriadoEm).IsRequired();

        // A unicidade é garantida pelo banco, não só pela checagem prévia da API: duas requisições
        // simultâneas com o mesmo e-mail passariam ambas pela checagem.
        usuario.HasIndex(u => u.Email).IsUnique();
        usuario.HasIndex(u => u.Cpf).IsUnique();
    }
}
