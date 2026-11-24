using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Usuarios.Infrastructure.Persistencia;

[assembly: AssemblyFixture(typeof(Usuarios.Api.IntegrationTests.UsuariosApiFactory))]

namespace Usuarios.Api.IntegrationTests;

/// <summary>
/// Sobe a usuarios-api real contra um PostgreSQL descartável (Testcontainers), uma vez por assembly.
/// As migrations rodam no startup, como em produção.
/// </summary>
public sealed class UsuariosApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("usuariosdb")
        .Build();

    /// <inheritdoc />
    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    /// <summary>Executa uma ação com o DbContext da aplicação.</summary>
    public async Task<T> NoBancoAsync<T>(Func<UsuariosDbContext, Task<T>> acao)
    {
        await using var escopo = Services.CreateAsyncScope();
        return await acao(escopo.ServiceProvider.GetRequiredService<UsuariosDbContext>());
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:UsuariosDb", _postgres.GetConnectionString());
        // Custo mínimo do BCrypt: o teste verifica o hash, não a lentidão dele.
        builder.UseSetting("Senha:FatorDeTrabalho", "4");
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
