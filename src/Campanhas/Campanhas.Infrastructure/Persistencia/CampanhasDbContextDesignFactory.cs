using ConexaoSolidaria.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Campanhas.Infrastructure.Persistencia;

/// <summary>Usada só pelo <c>dotnet ef</c> para gerar migrations; não abre conexão.</summary>
public sealed class CampanhasDbContextDesignFactory : IDesignTimeDbContextFactory<CampanhasDbContext>
{
    /// <inheritdoc />
    public CampanhasDbContext CreateDbContext(string[] args)
    {
        var opcoes = new DbContextOptionsBuilder<CampanhasDbContext>();
        opcoes.UsarPostgreSql("Host=localhost;Database=campanhasdb");
        return new CampanhasDbContext(opcoes.Options);
    }
}
