using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Campanhas.Infrastructure.Painel;

/// <summary>
/// Remove o painel do cache depois de uma mudança que ele exibe (doação processada, campanha criada ou
/// editada). Chamado sempre DEPOIS do commit: invalidar antes deixaria uma janela em que outra leitura
/// repovoaria o cache com o valor antigo.
/// </summary>
public sealed class InvalidadorDoPainel(IDistributedCache cache, ILogger<InvalidadorDoPainel> log)
{
    /// <summary>Invalida o painel. Falha do Redis não propaga: o TTL curto é a rede de segurança.</summary>
    public async Task InvalidarAsync(CancellationToken cancelamento)
    {
        try
        {
            await cache.RemoveAsync(PainelDeTransparencia.ChaveDoCache, cancelamento);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            log.LogWarning("Não foi possível invalidar o cache do painel; ele expira pelo TTL ({Erro})", erro.GetType().Name);
        }
    }
}
