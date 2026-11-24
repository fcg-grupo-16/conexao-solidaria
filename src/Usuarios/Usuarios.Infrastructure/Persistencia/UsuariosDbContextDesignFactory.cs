using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Usuarios.Infrastructure.Persistencia;

/// <summary>
/// Usada só pelo <c>dotnet ef</c> para gerar migrations; não abre conexão.
/// </summary>
public sealed class UsuariosDbContextDesignFactory : IDesignTimeDbContextFactory<UsuariosDbContext>
{
    /// <inheritdoc />
    public UsuariosDbContext CreateDbContext(string[] args)
    {
        var opcoes = new DbContextOptionsBuilder<UsuariosDbContext>();
        InfraestruturaExtensions.ConfigurarBanco(opcoes, "Host=localhost;Database=usuariosdb");
        return new UsuariosDbContext(opcoes.Options);
    }
}
