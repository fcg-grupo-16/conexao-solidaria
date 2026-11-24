#!/usr/bin/env bash
# Sobe a plataforma no minikube: constrói as imagens com a tag do COMMIT, aplica os manifestos e espera
# tudo ficar pronto.
#
#   ./scripts/deploy-minikube.sh
#
# Por que a tag do commit: com uma tag móvel (latest, dev), uma imagem nova com o mesmo nome não
# substitui a antiga no nó e o pod segue rodando o binário velho, sem nenhum erro visível.
set -euo pipefail

cd "$(dirname "$0")/.."

NAMESPACE=conexao-solidaria
SERVICOS=(usuarios-api campanhas-api doacoes-worker)

# Sem array associativo: o bash 3.2 que vem no macOS não tem.
dockerfile_de() {
  case "$1" in
    usuarios-api) echo src/Usuarios/Usuarios.Api/Dockerfile ;;
    campanhas-api) echo src/Campanhas/Campanhas.Api/Dockerfile ;;
    doacoes-worker) echo src/Doacoes/Doacoes.Worker/Dockerfile ;;
  esac
}

for comando in minikube kubectl docker git; do
  command -v "$comando" >/dev/null || { echo "ERRO: '$comando' não encontrado no PATH." >&2; exit 1; }
done

if ! minikube status >/dev/null 2>&1; then
  echo "==> Iniciando o minikube"
  minikube start --cpus=4 --memory=8g
fi

TAG=$(git rev-parse --short=12 HEAD)
if [[ -n "$(git status --porcelain -- src Directory.Build.props Directory.Packages.props global.json)" ]]; then
  # Código alterado e não commitado: tag única, para a imagem nova nunca ser confundida com a do commit.
  TAG="${TAG}-local-$(date +%Y%m%d%H%M%S)"
fi
echo "==> Tag das imagens: $TAG"

# Constrói direto no Docker do minikube: a imagem já nasce no nó, sem 'minikube image load'.
(
  eval "$(minikube docker-env --shell bash)"
  for servico in "${SERVICOS[@]}"; do
    echo "==> Construindo $servico"
    docker build --quiet -f "$(dockerfile_de "$servico")" -t "conexao-solidaria/$servico:$TAG" . >/dev/null
  done
)

OVERLAY=deploy/.overlay-local
mkdir -p "$OVERLAY"
{
  echo "apiVersion: kustomize.config.k8s.io/v1beta1"
  echo "kind: Kustomization"
  echo "resources:"
  echo "  - ../k8s"
  echo "images:"
  for servico in "${SERVICOS[@]}"; do
    echo "  - name: conexao-solidaria/$servico"
    echo "    newTag: \"$TAG\""
  done
} > "$OVERLAY/kustomization.yaml"

echo "==> Aplicando os manifestos"
kubectl apply -k "$OVERLAY"

echo "==> Aguardando a infraestrutura"
kubectl -n "$NAMESPACE" rollout status statefulset/postgres --timeout=300s
kubectl -n "$NAMESPACE" rollout status statefulset/rabbitmq --timeout=300s
kubectl -n "$NAMESPACE" rollout status deployment/redis --timeout=180s

echo "==> Aguardando os serviços"
for servico in "${SERVICOS[@]}"; do
  kubectl -n "$NAMESPACE" rollout status "deployment/$servico" --timeout=420s
done

echo "==> Aguardando o gateway"
kubectl -n "$NAMESPACE" rollout status deployment/gateway --timeout=120s

echo "==> Aguardando a observabilidade"
kubectl -n "$NAMESPACE" rollout status deployment/prometheus --timeout=180s
kubectl -n "$NAMESPACE" rollout status deployment/grafana --timeout=180s

kubectl -n "$NAMESPACE" get pods -o wide
cat <<FIM

Plataforma no ar (tag $TAG). Para acessar de fora do cluster:

  ./scripts/port-forward.sh

FIM
