#!/usr/bin/env bash
# Remove a plataforma do minikube, inclusive os volumes (dados do PostgreSQL e do RabbitMQ).
#
#   ./scripts/undeploy-minikube.sh
set -euo pipefail

cd "$(dirname "$0")/.."

kubectl delete -k deploy/k8s --ignore-not-found --wait=true
rm -rf deploy/.overlay-local
echo "Plataforma removida do minikube."
