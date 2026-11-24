namespace Usuarios.Api.IntegrationTests;

/// <summary>Gera e-mails e CPFs válidos e únicos, para os testes não colidirem no banco compartilhado.</summary>
internal static class DadosDeTeste
{
    private static int _sequencia = Random.Shared.Next(100_000, 900_000);

    public static string EmailUnico() => $"doador.{Guid.NewGuid():N}@exemplo.com";

    public static string CpfUnico()
    {
        var baseNumerica = Interlocked.Increment(ref _sequencia).ToString("D9");
        var d1 = Digito(baseNumerica, 10);
        var d2 = Digito(baseNumerica + d1, 11);
        return $"{baseNumerica}{d1}{d2}";
    }

    public static string ComMascara(string cpf) => $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";

    private static int Digito(string digitos, int peso)
    {
        var soma = digitos.Select((c, i) => (c - '0') * (peso - i)).Sum();
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
