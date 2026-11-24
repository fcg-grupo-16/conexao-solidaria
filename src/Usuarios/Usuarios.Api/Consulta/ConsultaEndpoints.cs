using ConexaoSolidaria.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Usuarios.Api.Cadastro;
using Usuarios.Infrastructure.Persistencia;

namespace Usuarios.Api.Consulta;

/// <summary>Consulta de usuários autenticados.</summary>
public static class ConsultaEndpoints
{
    /// <summary>Mapeia <c>GET /api/usuarios/me</c> e <c>GET /api/usuarios/{id}</c>.</summary>
    public static IEndpointRouteBuilder MapConsultaEndpoints(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/usuarios").WithTags("Usuários");

        grupo.MapGet("/me", ObterAutenticadoAsync)
            .WithName("ObterUsuarioAutenticado")
            .WithSummary("Dados do usuário dono do token.")
            .RequireAuthorization();

        grupo.MapGet("/{id:guid}", ObterPorIdAsync)
            .WithName("ObterUsuario")
            .WithSummary("Dados de um usuário (apenas GestorONG).")
            .RequireAuthorization(Perfis.GestorONG);

        return rotas;
    }

    private static async Task<Results<Ok<UsuarioResponse>, NotFound>> ObterAutenticadoAsync(
        HttpContext contexto, UsuariosDbContext banco, CancellationToken cancelamento) =>
        await ObterPorIdAsync(contexto.User.IdDoUsuario(), banco, cancelamento);

    private static async Task<Results<Ok<UsuarioResponse>, NotFound>> ObterPorIdAsync(
        Guid id, UsuariosDbContext banco, CancellationToken cancelamento)
    {
        var usuario = await banco.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancelamento);
        return usuario is null ? TypedResults.NotFound() : TypedResults.Ok(UsuarioResponse.De(usuario));
    }
}
