using System.Diagnostics.Metrics;

namespace Usuarios.Api;

/// <summary>Métricas de negócio da usuarios-api, exportadas em <c>/metrics</c>.</summary>
public sealed class MetricasDeUsuarios
{
    /// <summary>Nome do medidor.</summary>
    public const string NomeMedidor = "ConexaoSolidaria.Usuarios";

    private readonly Counter<long> _cadastros;
    private readonly Counter<long> _logins;

    /// <summary>Cria os instrumentos.</summary>
    public MetricasDeUsuarios(IMeterFactory fabrica)
    {
        var medidor = fabrica.Create(NomeMedidor);
        _cadastros = medidor.CreateCounter<long>("usuarios_cadastrados", description: "Doadores cadastrados.");
        _logins = medidor.CreateCounter<long>("usuarios_logins", description: "Tentativas de login por resultado e perfil.");

        // Séries publicadas com zero desde a subida: cada réplica expõe as métricas antes do primeiro uso.
        _cadastros.Add(0);
        _logins.Add(0, new KeyValuePair<string, object?>("resultado", "falha"), new KeyValuePair<string, object?>("perfil", "desconhecido"));
    }

    /// <summary>Doador cadastrado.</summary>
    public void Cadastrado() => _cadastros.Add(1);

    /// <summary>Login bem-sucedido.</summary>
    public void LoginAceito(string perfil) =>
        _logins.Add(1, new KeyValuePair<string, object?>("resultado", "sucesso"), new KeyValuePair<string, object?>("perfil", perfil));

    /// <summary>Login recusado (e-mail inexistente ou senha errada — indistinguíveis de propósito).</summary>
    public void LoginRecusado() =>
        _logins.Add(1, new KeyValuePair<string, object?>("resultado", "falha"), new KeyValuePair<string, object?>("perfil", "desconhecido"));
}
