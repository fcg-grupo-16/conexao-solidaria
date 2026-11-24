# ADR 0004 — Observabilidade com OpenTelemetry, Prometheus e Grafana

- **Status:** aceita
- **Data:** 2026-11-23

## Contexto

O PDF exige que a aplicação exponha métricas de saúde (`/health` ou `/metrics`) e que o Grafana
tenha pelo menos um dashboard com métricas reais da aplicação em execução (por exemplo CPU/memória
dos pods ou contagem de requisições HTTP). O título da seção menciona "Zabbix e Grafana".

## Decisão

- **Health checks** em todos os serviços: `/health/live` (o processo responde) e `/health/ready`
  (dependências: PostgreSQL, RabbitMQ e Redis, conforme o serviço). O Kubernetes usa os dois nas
  probes de liveness e readiness.
- **Métricas com OpenTelemetry**, expostas em `/metrics` no formato Prometheus:
  - HTTP (requisições por rota, status e latência) e runtime .NET (GC, threads, memória);
  - métricas de negócio: doações recebidas, processadas e rejeitadas, e valor processado.
- **Prometheus** no cluster coleta os serviços, o RabbitMQ (`rabbitmq_prometheus`) e o cAdvisor do
  kubelet (CPU e memória por pod).
- **Grafana** com datasource e dashboard **provisionados a partir de arquivos versionados** — o
  dashboard aparece pronto ao subir o cluster, sem configuração manual.
- **Logs estruturados** em JSON (Serilog), com correlação por requisição.
- A configuração comum vive em `src/BuildingBlocks/ConexaoSolidaria.Observabilidade`, para os três
  serviços não duplicarem o mesmo bloco.

### Zabbix

Os itens obrigatórios da seção citam apenas `/health`/`/metrics` e o Grafana. Estamos confirmando
com o professor se o Zabbix é obrigatório (issue #23). Se for, ele entra monitorando os endpoints
`/health/ready` e alimentando um painel no mesmo Grafana via plugin, sem substituir o Prometheus.

## Consequências

- **Positivas:** padrão aberto (OTel) e independente de fornecedor; dashboard reprodutível; as
  probes do Kubernetes refletem o estado real das dependências.
- **Negativas:** mais três componentes no cluster (Prometheus, Grafana e, se exigido, Zabbix).

## Alternativas descartadas

- **`prometheus-net`:** funciona, mas amarraria a instrumentação ao Prometheus; o OpenTelemetry
  permite trocar o backend sem mudar o código.
- **Configurar o dashboard manualmente pela interface:** não reproduzível pelos professores.
