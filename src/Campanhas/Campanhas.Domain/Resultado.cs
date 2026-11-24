namespace Campanhas.Domain;

/// <summary>Violação de regra de negócio, associada ao campo que a causou.</summary>
/// <param name="Campo">Campo da requisição, no mesmo formato das chaves de erro da validação (PascalCase).</param>
/// <param name="Mensagem">Mensagem para o usuário, em português.</param>
public sealed record ErroDeDominio(string Campo, string Mensagem);

/// <summary>Resultado de uma operação de domínio que pode violar regras de negócio.</summary>
public class Resultado
{
    /// <summary>Cria um resultado.</summary>
    protected Resultado(IReadOnlyList<ErroDeDominio> erros) => Erros = erros;

    /// <summary>Erros encontrados; vazio quando a operação foi aceita.</summary>
    public IReadOnlyList<ErroDeDominio> Erros { get; }

    /// <summary>True quando nenhuma regra foi violada.</summary>
    public bool Sucesso => Erros.Count == 0;

    /// <summary>Operação aceita.</summary>
    public static Resultado Ok() => new([]);

    /// <summary>Operação recusada.</summary>
    public static Resultado Falha(IReadOnlyList<ErroDeDominio> erros) => new(erros);

    /// <summary>Operação recusada por um único motivo.</summary>
    public static Resultado Falha(string campo, string mensagem) => new([new ErroDeDominio(campo, mensagem)]);

    /// <summary>Erros agrupados por campo, no formato do <c>ValidationProblemDetails</c>.</summary>
    public IDictionary<string, string[]> ErrosPorCampo() =>
        Erros.GroupBy(e => e.Campo).ToDictionary(g => g.Key, g => g.Select(e => e.Mensagem).ToArray());
}

/// <summary>Resultado com valor.</summary>
public sealed class Resultado<T> : Resultado
{
    private readonly T? _valor;

    private Resultado(T? valor, IReadOnlyList<ErroDeDominio> erros) : base(erros) => _valor = valor;

    /// <summary>Valor produzido. Só existe em caso de sucesso.</summary>
    public T Valor => Sucesso ? _valor! : throw new InvalidOperationException("Resultado com falha não tem valor.");

    /// <summary>Operação aceita.</summary>
    public static Resultado<T> Ok(T valor) => new(valor, []);

    /// <summary>Operação recusada.</summary>
    public static new Resultado<T> Falha(IReadOnlyList<ErroDeDominio> erros) => new(default, erros);
}
