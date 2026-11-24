using ConexaoSolidaria.Persistencia;
using ConexaoSolidaria.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Usuarios.Domain;
using Usuarios.Infrastructure.Persistencia;

namespace Usuarios.Api.Cadastro;

/// <summary>Cadastro público de doadores.</summary>
public static class CadastroEndpoints
{
    private const string MensagemEmailEmUso = "Já existe um usuário cadastrado com este e-mail.";
    private const string MensagemCpfEmUso = "Já existe um usuário cadastrado com este CPF.";

    /// <summary>Mapeia <c>POST /api/usuarios</c>.</summary>
    public static IEndpointRouteBuilder MapCadastroEndpoints(this IEndpointRouteBuilder rotas)
    {
        rotas.MapPost("/api/usuarios", CadastrarAsync)
            .WithName("CadastrarDoador")
            .WithTags("Usuários")
            .WithSummary("Cadastra um doador (acesso público).")
            .WithDescription("Todo cadastro público recebe o perfil Doador. E-mail e CPF são únicos; a senha é armazenada com BCrypt.")
            .AllowAnonymous()
            .ValidarCom<CadastrarDoadorRequest>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return rotas;
    }

    private static async Task<Results<Created<UsuarioResponse>, ProblemHttpResult>> CadastrarAsync(
        CadastrarDoadorRequest requisicao,
        UsuariosDbContext banco,
        IHasherDeSenha hasher,
        TimeProvider relogio,
        ILogger<CadastrarDoadorRequest> log,
        CancellationToken cancelamento)
    {
        var cpf = Cpf.Criar(requisicao.Cpf);
        var email = Usuario.NormalizarEmail(requisicao.Email);

        // Checagem prévia só para dar a mensagem certa; quem garante a unicidade é o índice único.
        if (await banco.Usuarios.AnyAsync(u => u.Email == email, cancelamento))
        {
            return PadroesDeApiExtensions.Conflito(MensagemEmailEmUso);
        }

        if (await banco.Usuarios.AnyAsync(u => u.Cpf == cpf, cancelamento))
        {
            return PadroesDeApiExtensions.Conflito(MensagemCpfEmUso);
        }

        var usuario = Usuario.CriarDoador(
            requisicao.NomeCompleto, email, cpf, hasher.Gerar(requisicao.Senha), relogio.GetUtcNow());
        banco.Usuarios.Add(usuario);

        try
        {
            await banco.SaveChangesAsync(cancelamento);
        }
        catch (DbUpdateException erro) when (erro.EhViolacaoDeUnicidade())
        {
            // Corrida: outra requisição gravou o mesmo e-mail ou CPF entre a checagem e o insert.
            return PadroesDeApiExtensions.Conflito("Já existe um usuário cadastrado com este e-mail ou CPF.");
        }

        log.LogInformation("Doador {UsuarioId} cadastrado (CPF {Cpf})", usuario.Id, cpf.Mascarado);
        return TypedResults.Created($"/api/usuarios/{usuario.Id}", UsuarioResponse.De(usuario));
    }
}
