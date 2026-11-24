namespace Usuarios.Domain;

/// <summary>
/// CPF válido (formato e dígitos verificadores), guardado só com os 11 dígitos.
/// Aceita a entrada com ou sem máscara: <c>123.456.789-09</c> ou <c>12345678909</c>.
/// </summary>
public sealed record Cpf
{
    private const int Tamanho = 11;

    private Cpf(string numero) => Numero = numero;

    /// <summary>Os 11 dígitos, sem máscara.</summary>
    public string Numero { get; }

    /// <summary>Para logs: só os dígitos do meio ficam visíveis (<c>***.456.789-**</c>).</summary>
    public string Mascarado => $"***.{Numero[3..6]}.{Numero[6..9]}-**";

    /// <summary>Cria um CPF ou lança <see cref="ArgumentException"/> se for inválido.</summary>
    public static Cpf Criar(string? valor) =>
        TentarCriar(valor, out var cpf) ? cpf : throw new ArgumentException("CPF inválido.", nameof(valor));

    /// <summary>Valida formato e dígitos verificadores.</summary>
    public static bool EhValido(string? valor) => TentarCriar(valor, out _);

    /// <summary>Tenta criar um CPF a partir da entrada com ou sem máscara.</summary>
    public static bool TentarCriar(string? valor, out Cpf cpf)
    {
        cpf = null!;
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var texto = valor.Trim();
        // Só aceita dígitos e os separadores da máscara; letras ou outros símbolos invalidam.
        if (texto.Any(c => !char.IsAsciiDigit(c) && c is not '.' and not '-'))
        {
            return false;
        }

        var digitos = new string([.. texto.Where(char.IsAsciiDigit)]);
        if (digitos.Length != Tamanho || !FormatoComMascaraValido(texto, digitos))
        {
            return false;
        }

        // 000.000.000-00, 111.111.111-11... passam no cálculo dos dígitos, mas não são CPFs.
        if (digitos.All(c => c == digitos[0]))
        {
            return false;
        }

        if (CalcularDigito(digitos, 9) != digitos[9] - '0' || CalcularDigito(digitos, 10) != digitos[10] - '0')
        {
            return false;
        }

        cpf = new Cpf(digitos);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Mascarado;

    // Sem máscara (11 dígitos) ou com a máscara completa (000.000.000-00); máscara parcial é erro de digitação.
    private static bool FormatoComMascaraValido(string texto, string digitos) =>
        texto.Length == Tamanho
        || (texto.Length == 14 && texto[3] == '.' && texto[7] == '.' && texto[11] == '-'
            && texto.Remove(11, 1).Remove(7, 1).Remove(3, 1) == digitos);

    private static int CalcularDigito(string digitos, int quantidade)
    {
        var soma = 0;
        for (var i = 0; i < quantidade; i++)
        {
            soma += (digitos[i] - '0') * (quantidade + 1 - i);
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
