#!/usr/bin/env bash
# Confere a entrega do hackathon contra o cluster (minikube) e o repositório, item a item do PDF.
# Abre seus próprios port-forwards em portas altas (não conflita com o port-forward.sh) e roda o
# smoke test pelo API Gateway no final.
#
#   ./scripts/verify-entrega.sh
#
# Requer kubectl, curl e jq. O gh é opcional (confere o CI e as imagens publicadas).
set -euo pipefail

cd "$(dirname "$0")/.."

NS=conexao-solidaria
OK=0
FALHAS=0
AVISOS=0
PIDS=()

passou() { OK=$((OK + 1)); printf '  \342\234\224 %s\n' "$1"; }
falhou() { FALHAS=$((FALHAS + 1)); printf '  \342\234\230 %s\n' "$1"; }
aviso() { AVISOS=$((AVISOS + 1)); printf '  ! %s\n' "$1"; }
checar() { if eval "$2" >/dev/null 2>&1; then passou "$1"; else falhou "$1"; fi; }

encerrar() { for pid in "${PIDS[@]}"; do kill "$pid" 2>/dev/null || true; done; }
trap encerrar EXIT INT TERM

encaminhar() { # recurso porta_local porta_remota
  kubectl -n "$NS" port-forward "$1" "$2:$3" >/dev/null 2>&1 &
  PIDS+=($!)
}

esperar_porta() {
  for _ in $(seq 1 30); do
    curl -s -o /dev/null "http://localhost:$1" && return 0
    sleep 0.5
  done
  return 1
}

for comando in kubectl curl jq; do
  command -v "$comando" >/dev/null || { echo "ERRO: '$comando' não encontrado no PATH." >&2; exit 1; }
done

echo "Kubernetes (namespace $NS)"
checar "namespace existe" "kubectl get namespace $NS"
TOTAL=$(kubectl -n "$NS" get pods --no-headers 2>/dev/null | wc -l | tr -d ' ')
PRONTOS=$(kubectl -n "$NS" get pods -o json | jq '[.items[] | select(.status.conditions[]? | select(.type == "Ready" and .status == "True"))] | length')
if [[ "$TOTAL" -gt 0 && "$TOTAL" == "$PRONTOS" ]]; then passou "pods prontos: $PRONTOS/$TOTAL"; else falhou "pods prontos: $PRONTOS/$TOTAL"; fi
for d in usuarios-api campanhas-api doacoes-worker gateway redis prometheus grafana; do
  checar "Deployment $d disponível" "[[ \$(kubectl -n $NS get deployment $d -o jsonpath='{.status.availableReplicas}') -ge 1 ]]"
done
for s in postgres rabbitmq; do
  checar "StatefulSet $s pronto" "[[ \$(kubectl -n $NS get statefulset $s -o jsonpath='{.status.readyReplicas}') -ge 1 ]]"
done
CONFIGMAPS=$(kubectl -n "$NS" get configmaps --no-headers | grep -vc kube-root-ca)
SERVICES=$(kubectl -n "$NS" get services --no-headers | wc -l | tr -d ' ')
SECRETS=$(kubectl -n "$NS" get secrets --no-headers | wc -l | tr -d ' ')
passou "manifestos aplicados: $CONFIGMAPS ConfigMaps, $SERVICES Services, $SECRETS Secrets"
for d in usuarios-api campanhas-api doacoes-worker; do
  IMAGEM=$(kubectl -n "$NS" get deployment "$d" -o jsonpath='{.spec.template.spec.containers[0].image}')
  if [[ "$IMAGEM" =~ :(dev|latest)$ ]]; then aviso "$d usa tag móvel ($IMAGEM); use ./scripts/deploy-minikube.sh"; else passou "$d com tag de commit ($IMAGEM)"; fi
done

encaminhar svc/gateway 18088 80
encaminhar svc/usuarios-api 18081 8080
encaminhar svc/campanhas-api 18082 8080
encaminhar svc/doacoes-worker 18083 8080
encaminhar svc/rabbitmq 25672 15672
encaminhar svc/prometheus 19090 9090
encaminhar svc/grafana 13000 80
for porta in 18088 18081 18082 18083 25672 19090 13000; do esperar_porta "$porta" || true; done

echo "Saúde e métricas (/health e /metrics)"
for par in usuarios-api:18081 campanhas-api:18082 doacoes-worker:18083; do
  servico=${par%%:*}; porta=${par##*:}
  checar "$servico /health/ready Healthy" "[[ \$(curl -s http://localhost:$porta/health/ready | jq -r .status) == Healthy ]]"
done
# Lê o /metrics inteiro antes de procurar: com pipefail, "curl | grep -q" falha de forma intermitente,
# porque o grep encerra no primeiro acerto e o curl morre com SIGPIPE.
expoe_metrica() { local corpo; corpo=$(curl -s "http://localhost:$1/metrics"); [[ "$corpo" == *$'\n'"$2"* ]]; }
checar "usuarios-api expõe usuarios_cadastrados_total" "expoe_metrica 18081 usuarios_cadastrados_total"
checar "campanhas-api expõe doacoes_recebidas_total" "expoe_metrica 18082 doacoes_recebidas_total"
checar "doacoes-worker expõe doacoes_processadas_total" "expoe_metrica 18083 doacoes_processadas_total"

echo "Observabilidade (Prometheus e Grafana)"
ALVOS=$(curl -s http://localhost:19090/api/v1/targets | jq -r '[.data.activeTargets[] | .health] | "\(map(select(. == "up")) | length)/\(length)"')
if [[ "${ALVOS%%/*}" == "${ALVOS##*/}" && "${ALVOS##*/}" -gt 0 ]]; then passou "alvos do Prometheus up: $ALVOS"; else falhou "alvos do Prometheus up: $ALVOS"; fi
checar "Grafana saudável" "[[ \$(curl -s http://localhost:13000/api/health | jq -r .database) == ok ]]"
checar "dashboard 'Conexão Solidária — Visão geral' provisionado" "curl -s 'http://localhost:13000/api/search?query=Conex' | jq -e 'map(.uid) | index(\"conexao-solidaria\")'"
checar "dashboard com dados reais (CPU dos pods)" "curl -s http://localhost:19090/api/v1/query --data-urlencode 'query=sum(rate(container_cpu_usage_seconds_total{namespace=\"$NS\",container=\"\",pod!=\"\"}[1m]))' | jq -e '.data.result | length > 0'"

echo "Mensageria (RabbitMQ)"
rabbit() { curl -s -u conexao:conexao-demo "http://localhost:25672/api/$1"; }
checar "exchange doacao-recebida (fanout)" "[[ \$(rabbit exchanges/%2F/doacao-recebida | jq -r .type) == fanout ]]"
CONSUMIDORES=$(rabbit queues/%2F/doacoes-worker-doacao-recebida | jq -r '.consumers // 0')
if [[ "$CONSUMIDORES" -ge 1 ]]; then passou "fila doacoes-worker-doacao-recebida com $CONSUMIDORES consumidor(es)"; else falhou "fila doacoes-worker-doacao-recebida sem consumidores"; fi

echo "Fluxo do PDF pelo API Gateway"
if GATEWAY_URL=http://localhost:18088 RABBITMQ_URL=http://localhost:25672 ./scripts/smoke-test.sh > /tmp/verify-smoke.$$ 2>&1; then
  passou "smoke test: $(tail -1 /tmp/verify-smoke.$$)"
else
  falhou "smoke test falhou:"; sed 's/^/      /' /tmp/verify-smoke.$$ | grep -E '✘|Resultado' || true
fi
rm -f /tmp/verify-smoke.$$

echo "Repositório e entregáveis"
checar "README.md com passo a passo" "grep -q 'deploy-minikube.sh' README.md"
checar "ADRs (decisões de arquitetura)" "ls docs/adr/0002-bancos-de-dados.md"
if ls docs/arquitetura/*.svg docs/arquitetura/*.png >/dev/null 2>&1; then passou "diagrama de arquitetura"; else aviso "diagrama de arquitetura ainda não está em docs/arquitetura/"; fi
if ls docs/*.pdf >/dev/null 2>&1; then passou "PDF de justificativa dos bancos"; else aviso "PDF de justificativa dos bancos ainda não está em docs/"; fi
if ls docs/roteiro-video.md >/dev/null 2>&1; then passou "roteiro do vídeo"; else aviso "roteiro do vídeo ainda não está em docs/"; fi
if command -v gh >/dev/null && gh auth status >/dev/null 2>&1; then
  CI=$(gh run list -R fcg-grupo-16/conexao-solidaria -b main -e push -L 1 --json status,conclusion --jq '.[0] | "\(.status) \(.conclusion)"' 2>/dev/null || echo "? ?")
  case "$CI" in
    "completed success") passou "último CI da main verde (build, testes e imagens no GHCR)" ;;
    completed*) falhou "último CI da main: ${CI#completed }" ;;
    *) aviso "último CI da main ainda em andamento (${CI%% *})" ;;
  esac
else
  aviso "gh indisponível: CI e imagens não conferidos"
fi

echo
echo "Resultado: $OK ok, $FALHAS falha(s), $AVISOS aviso(s)"
[[ "$FALHAS" -eq 0 ]]
