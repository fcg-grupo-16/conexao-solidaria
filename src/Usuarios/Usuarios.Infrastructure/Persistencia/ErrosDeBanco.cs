using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Usuarios.Infrastructure.Persistencia;

/// <summary>Interpretação de erros do PostgreSQL.</summary>
public static class ErrosDeBanco
{
    /// <summary>True quando a falha foi violação de índice único (SQLSTATE 23505).</summary>
    public static bool EhViolacaoDeUnicidade(DbUpdateException erro) =>
        erro.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
