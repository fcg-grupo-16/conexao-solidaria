#!/usr/bin/env bash
# Expõe os serviços do cluster em localhost enquanto o script estiver rodando (Ctrl+C encerra tudo).
#
#   ./scripts/port-forward.sh
set -euo pipefail

NAMESPACE=conexao-solidaria
PIDS=()

encerrar() {
  for pid in "${PIDS[@]}"; do kill "$pid" 2>/dev/null || true; done
}
trap encerrar EXIT INT TERM

encaminhar() {
  local recurso=$1 portas=$2 descricao=$3
  if kubectl -n "$NAMESPACE" get "$recurso" >/dev/null 2>&1; then
    kubectl -n "$NAMESPACE" port-forward "$recurso" "$portas" >/dev/null 2>&1 &
    PIDS+=($!)
    printf '  %-28s http://localhost:%s\n' "$descricao" "${portas%%:*}"
  fi
}

echo "Encaminhando portas (Ctrl+C para encerrar):"
encaminhar svc/gateway 8000:80 "API Gateway"
encaminhar svc/usuarios-api 5081:80 "usuarios-api (/swagger)"
encaminhar svc/campanhas-api 5082:80 "campanhas-api (/swagger)"
encaminhar svc/doacoes-worker 5083:80 "doacoes-worker (/health)"
encaminhar svc/rabbitmq 15672:15672 "RabbitMQ Management"
encaminhar svc/grafana 3000:80 "Grafana"
encaminhar svc/prometheus 9090:9090 "Prometheus"

wait
