# ADR 0003 — RabbitMQ + MassTransit 8 com outbox e consumidor idempotente

- **Status:** aceita
- **Data:** 2026-11-23

## Contexto

Requisito do PDF: ao receber uma doação, a API **não** atualiza o valor arrecadado; ela publica um
evento (`DoacaoRecebidaEvent`) num broker, e um segundo serviço consome o evento e atualiza o
"Valor Total Arrecadado". O vídeo precisa mostrar a mensagem passando pela fila.

Dois problemas clássicos aparecem nesse desenho:

1. **Escrita dupla** — gravar a doação no banco e publicar no broker são duas operações. Se a
   aplicação cair entre elas, ou a doação existe sem evento (nunca é somada) ou o evento existe sem
   doação.
2. **Entrega pelo menos uma vez** — o broker pode reentregar uma mensagem (falha antes do ack,
   reinício do consumidor). Sem cuidado, a mesma doação é somada duas vezes.

## Decisão

- **Broker: RabbitMQ 3.13** com o plugin de management (interface usada no vídeo) e o plugin
  `rabbitmq_prometheus` (métricas de fila no Grafana).
- **Biblioteca: MassTransit 8.5.x.** Cuida da topologia (exchange por tipo de mensagem, fila por
  consumidor), serialização, retry, fila de erro e outbox. A linha 9 exige licença comercial, por
  isso a versão fica travada em 8.x no `Directory.Packages.props`.
- **Outbox transacional (EF Core + PostgreSQL) na `campanhas-api`:** a doação `Pendente` e a
  mensagem são gravadas na mesma transação; um serviço em background entrega a mensagem ao broker
  depois do commit. Resolve a escrita dupla.
- **Consumidor idempotente no `doacoes-worker`:** a transição `Pendente → Processada` é a guarda; o
  incremento do arrecadado só acontece se a transição acontecer, na mesma transação (ADR 0002).
  Reentrega não soma duas vezes.
- **Retry com backoff** para falhas transitórias (banco indisponível); falha persistente vai para a
  fila `_error` do consumidor para análise. Violação de regra de negócio (campanha cancelada entre a
  intenção e o processamento) não é retentada: a doação vira `Rejeitada`.
- O contrato vive em um único projeto (`ConexaoSolidaria.Contracts`), porque o MassTransit casa
  mensagens pelo nome completo do tipo. Um teste trava esse nome.

## Consequências

- **Positivas:** nenhuma doação se perde nem é somada duas vezes; a API responde `202 Accepted`
  rápido e não depende do broker estar no ar no momento da requisição (o outbox entrega depois).
- **Negativas:** consistência eventual — o painel reflete a doação alguns instantes depois do
  `202`. É o comportamento pedido pelo PDF e é o que o vídeo demonstra.

## Alternativas descartadas

- **Kafka:** modelo de log particionado é mais do que o problema pede, operação mais pesada no
  minikube, e a visualização "mensagem passando pela fila" é mais direta no RabbitMQ.
- **Cliente RabbitMQ puro:** exigiria reimplementar retry, fila de erro, topologia e outbox.
- **Publicar direto após o `SaveChanges`:** sujeito à escrita dupla descrita acima.
