namespace Campanhas.Domain;

/// <summary>Situação de uma campanha de arrecadação.</summary>
public enum StatusCampanha
{
    /// <summary>Recebendo doações (dentro do prazo).</summary>
    Ativa = 1,

    /// <summary>Encerrada pela ONG; não recebe doações.</summary>
    Concluida = 2,

    /// <summary>Cancelada; estado final, não recebe doações nem pode ser editada.</summary>
    Cancelada = 3,
}

/// <summary>Situação de uma doação no processamento assíncrono.</summary>
public enum StatusDoacao
{
    /// <summary>Intenção registrada pela API; aguardando o doacoes-worker.</summary>
    Pendente = 1,

    /// <summary>Somada ao arrecadado da campanha pelo worker.</summary>
    Processada = 2,

    /// <summary>Recusada pelo worker (a campanha deixou de aceitar doações antes do processamento).</summary>
    Rejeitada = 3,
}
