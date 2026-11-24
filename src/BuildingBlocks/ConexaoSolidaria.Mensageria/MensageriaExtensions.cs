using System.ComponentModel.DataAnnotations;
using ConexaoSolidaria.Contracts.Eventos;
using ConexaoSolidaria.Observabilidade;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ConexaoSolidaria.Mensageria;

/// <summary>Conexão com o RabbitMQ.</summary>
public sealed class OpcoesRabbitMq
{
    /// <summary>Seção de configuração.</summary>
    public const string Secao = "RabbitMq";

    /// <summary>Host do broker (nome do Service no Kubernetes).</summary>
    [Required]
    public string Host { get; set; } = "localhost";

    /// <summary>Porta AMQP.</summary>
    [Range(1, 65535)]
    public ushort Porta { get; set; } = 5672;

    /// <summary>Virtual host.</summary>
    [Required]
    public string VirtualHost { get; set; } = "/";

    /// <summary>Usuário (Secret no Kubernetes).</summary>
    [Required]
    public string Usuario { get; set; } = "guest";

    /// <summary>Senha (Secret no Kubernetes).</summary>
    [Required]
    public string Senha { get; set; } = "guest";
}

/// <summary>
/// Topologia e conexão do RabbitMQ compartilhadas entre a campanhas-api (publica) e o doacoes-worker
/// (consome). Ficar num lugar só garante que os dois lados declaram o mesmo exchange.
/// </summary>
public static class MensageriaExtensions
{
    /// <summary>
    /// Exchange (fanout) do <see cref="DoacaoRecebidaEvent"/>. Nome curto em vez do padrão do MassTransit
    /// (<c>ConexaoSolidaria.Contracts.Eventos:DoacaoRecebidaEvent</c>), para ficar legível no RabbitMQ Management.
    /// </summary>
    public const string ExchangeDoacaoRecebida = "doacao-recebida";

    /// <summary>
    /// Registra o MassTransit sobre RabbitMQ. <paramref name="registrar"/> adiciona consumidores e/ou outbox.
    /// </summary>
    /// <param name="services">Serviços.</param>
    /// <param name="configuracao">Configuração (seção <see cref="OpcoesRabbitMq.Secao"/>).</param>
    /// <param name="prefixoDasFilas">Prefixo das filas deste serviço (ex.: <c>doacoes-worker</c>).</param>
    /// <param name="brokerEssencial">
    /// Se o serviço depende do broker para funcionar. Quando falso (API com outbox), a queda do broker
    /// aparece como Degraded na prontidão, sem tirar o pod do balanceamento: as doações seguem sendo
    /// aceitas e o outbox entrega quando o broker voltar.
    /// </param>
    /// <param name="registrar">Registro de consumidores, outbox etc.</param>
    public static IServiceCollection AdicionarMensageria(
        this IServiceCollection services,
        IConfiguration configuracao,
        string prefixoDasFilas,
        bool brokerEssencial,
        Action<IBusRegistrationConfigurator>? registrar = null)
    {
        services.AddOptions<OpcoesRabbitMq>()
            .Bind(configuracao.GetSection(OpcoesRabbitMq.Secao))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMassTransit(barramento =>
        {
            barramento.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(prefixoDasFilas, false));
            barramento.ConfigureHealthCheckOptions(saude =>
            {
                saude.Name = "rabbitmq";
                // Pior status reportado na falha: Unhealthy tira o pod do balanceamento; Degraded não.
                saude.MinimalFailureStatus = brokerEssencial ? HealthStatus.Unhealthy : HealthStatus.Degraded;
                saude.Tags.Clear();
                saude.Tags.Add(ObservabilidadeExtensions.TagProntidao);
            });

            registrar?.Invoke(barramento);

            barramento.UsingRabbitMq((contexto, rabbit) =>
            {
                var opcoes = contexto.GetRequiredService<IOptions<OpcoesRabbitMq>>().Value;
                rabbit.Host(opcoes.Host, opcoes.Porta, opcoes.VirtualHost, host =>
                {
                    host.Username(opcoes.Usuario);
                    host.Password(opcoes.Senha);
                });

                rabbit.Message<DoacaoRecebidaEvent>(mensagem => mensagem.SetEntityName(ExchangeDoacaoRecebida));
                rabbit.ConfigureEndpoints(contexto);
            });
        });

        return services;
    }
}
