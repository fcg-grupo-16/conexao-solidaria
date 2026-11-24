#!/usr/bin/env bash
# Prova automatizada do fluxo exigido no PDF, pelo API Gateway:
# cadastro → login (JWT) → campanha → doação → mensagem no RabbitMQ → worker → painel atualizado,
# além das regras de negócio e de autorização.
#
#   ./scripts/smoke-test.sh
#   GATEWAY_URL=http://localhost:8088 RABBITMQ_URL=http://localhost:15672 ./scripts/smoke-test.sh
#
# Funciona contra o docker compose ou o minikube (com ./scripts/port-forward.sh rodando).
# Requer curl e jq.
set -euo pipefail

GATEWAY_URL=${GATEWAY_URL:-http://localhost:8088}
RABBITMQ_URL=${RABBITMQ_URL:-http://localhost:15672}
RABBITMQ_USUARIO=${RABBITMQ_USUARIO:-conexao}
RABBITMQ_SENHA=${RABBITMQ_SENHA:-conexao-demo}
GESTOR_EMAIL=${GESTOR_EMAIL:-gestor@esperancasolidaria.org}
GESTOR_SENHA=${GESTOR_SENHA:-Gestor@2026}
FILA=doacoes-worker-doacao-recebida

for comando in curl jq; do
  command -v "$comando" >/dev/null || { echo "ERRO: '$comando' não encontrado no PATH." >&2; exit 1; }
done

OK=0
FALHAS=0
CORPO=""
STATUS=""

passou() { OK=$((OK + 1)); printf '  \342\234\224 %s\n' "$1"; }
falhou() { FALHAS=$((FALHAS + 1)); printf '  \342\234\230 %s\n' "$1"; }
conferir() { # conferir "descrição" esperado obtido
  if [[ "$2" == "$3" ]]; then passou "$1"; else falhou "$1 (esperado: $2, obtido: $3)"; fi
}

# requisicao MÉTODO ROTA [TOKEN] [CORPO_JSON] → define STATUS e CORPO
requisicao() {
  local metodo=$1 rota=$2 token=${3:-} dados=${4:-}
  local argumentos=(-s -o /tmp/smoke-corpo.$$ -w '%{http_code}' -X "$metodo" "$GATEWAY_URL$rota" -H 'Content-Type: application/json')
  [[ -n "$token" ]] && argumentos+=(-H "Authorization: Bearer $token")
  [[ -n "$dados" ]] && argumentos+=(-d "$dados")
  STATUS=$(curl "${argumentos[@]}" || echo 000)
  CORPO=$(cat /tmp/smoke-corpo.$$ 2>/dev/null || true)
  rm -f /tmp/smoke-corpo.$$
}

# Data ISO 8601 em UTC deslocada em dias (GNU date ou BSD date do macOS).
data_utc() {
  date -u -d "$1 days" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -v"$1"d +%Y-%m-%dT%H:%M:%SZ
}

# CPF válido aleatório (formato 000.000.000-00).
cpf_aleatorio() {
  local base="" i soma resto d1 d2
  for i in 1 2 3 4 5 6 7 8 9; do base="$base$((RANDOM % 10))"; done
  soma=0; for i in 0 1 2 3 4 5 6 7 8; do soma=$((soma + ${base:i:1} * (10 - i))); done
  resto=$((soma % 11)); d1=$(( resto < 2 ? 0 : 11 - resto ))
  soma=0; for i in 0 1 2 3 4 5 6 7 8; do soma=$((soma + ${base:i:1} * (11 - i))); done
  soma=$((soma + d1 * 2))
  resto=$((soma % 11)); d2=$(( resto < 2 ? 0 : 11 - resto ))
  echo "${base:0:3}.${base:3:3}.${base:6:3}-$d1$d2"
}

mensagens_confirmadas_na_fila() {
  curl -s -u "$RABBITMQ_USUARIO:$RABBITMQ_SENHA" "$RABBITMQ_URL/api/queues/%2F/$FILA" \
    | jq -r '.message_stats.ack // 0'
}

echo "Smoke test em $GATEWAY_URL"

echo "Cadastro de doador (público)"
EMAIL="smoke.$(date +%s).$RANDOM@exemplo.com"
CPF=$(cpf_aleatorio)
SENHA_DOADOR="Doar2026!"
requisicao POST /api/usuarios "" "{\"nomeCompleto\":\"Doadora Smoke\",\"email\":\"$EMAIL\",\"cpf\":\"$CPF\",\"senha\":\"$SENHA_DOADOR\"}"
conferir "cadastro válido → 201" 201 "$STATUS"
conferir "perfil Doador" Doador "$(jq -r .perfil <<<"$CORPO")"
requisicao POST /api/usuarios "" "{\"nomeCompleto\":\"Doadora Smoke\",\"email\":\"$EMAIL\",\"cpf\":\"$(cpf_aleatorio)\",\"senha\":\"$SENHA_DOADOR\"}"
conferir "e-mail repetido → 409" 409 "$STATUS"
requisicao POST /api/usuarios "" "{\"nomeCompleto\":\"Doadora Smoke\",\"email\":\"x$EMAIL\",\"cpf\":\"123.456.789-00\",\"senha\":\"$SENHA_DOADOR\"}"
conferir "CPF inválido → 400" 400 "$STATUS"

echo "Autenticação (JWT)"
requisicao POST /api/auth/login "" "{\"email\":\"$GESTOR_EMAIL\",\"senha\":\"$GESTOR_SENHA\"}"
conferir "login do gestor → 200" 200 "$STATUS"
TOKEN_GESTOR=$(jq -r '.token // empty' <<<"$CORPO")
conferir "token do gestor com perfil GestorONG" GestorONG "$(jq -r .perfil <<<"$CORPO")"
requisicao POST /api/auth/login "" "{\"email\":\"$EMAIL\",\"senha\":\"$SENHA_DOADOR\"}"
conferir "login do doador → 200" 200 "$STATUS"
TOKEN_DOADOR=$(jq -r '.token // empty' <<<"$CORPO")
requisicao POST /api/auth/login "" "{\"email\":\"$EMAIL\",\"senha\":\"SenhaErrada1\"}"
conferir "senha errada → 401" 401 "$STATUS"

echo "Gestão de campanhas (GestorONG)"
INICIO=$(data_utc -1)
FIM=$(data_utc +30)
CAMPANHA="{\"titulo\":\"Smoke $(date +%s)\",\"descricao\":\"Campanha do smoke test.\",\"dataInicio\":\"$INICIO\",\"dataFim\":\"$FIM\",\"metaFinanceira\":1000}"
requisicao POST /api/campanhas "" "$CAMPANHA"
conferir "sem token → 401" 401 "$STATUS"
requisicao POST /api/campanhas "$TOKEN_DOADOR" "$CAMPANHA"
conferir "token de doador → 403" 403 "$STATUS"
requisicao POST /api/campanhas "$TOKEN_GESTOR" "{\"titulo\":\"Inválida\",\"descricao\":\"x\",\"dataInicio\":\"$(data_utc -10)\",\"dataFim\":\"$(data_utc -1)\",\"metaFinanceira\":0}"
conferir "término no passado e meta zero → 400" 400 "$STATUS"
conferir "erro aponta DataFim e MetaFinanceira" "true" "$(jq -r '(.errors | has("DataFim")) and (.errors | has("MetaFinanceira"))' <<<"$CORPO")"
requisicao POST /api/campanhas "$TOKEN_GESTOR" "$CAMPANHA"
conferir "campanha válida → 201" 201 "$STATUS"
CAMPANHA_ID=$(jq -r '.id // empty' <<<"$CORPO")

echo "Doação assíncrona (API → RabbitMQ → worker)"
ACKS_ANTES=$(mensagens_confirmadas_na_fila)
requisicao POST /api/doacoes "$TOKEN_DOADOR" "{\"idCampanha\":\"$CAMPANHA_ID\",\"valorDoacao\":150.00}"
conferir "doação → 202 Accepted" 202 "$STATUS"
conferir "doação nasce Pendente" Pendente "$(jq -r .status <<<"$CORPO")"
DOACAO_ID=$(jq -r '.id // empty' <<<"$CORPO")
requisicao POST /api/doacoes "$TOKEN_GESTOR" "{\"idCampanha\":\"$CAMPANHA_ID\",\"valorDoacao\":10}"
conferir "gestor não doa → 403" 403 "$STATUS"

STATUS_DOACAO=""
for _ in $(seq 1 30); do
  requisicao GET "/api/doacoes/$DOACAO_ID" "$TOKEN_DOADOR"
  STATUS_DOACAO=$(jq -r '.status // empty' <<<"$CORPO")
  [[ "$STATUS_DOACAO" == "Pendente" ]] || break
  sleep 1
done
conferir "worker processou a doação" Processada "$STATUS_DOACAO"

ACKS_DEPOIS=$ACKS_ANTES
for _ in $(seq 1 15); do
  ACKS_DEPOIS=$(mensagens_confirmadas_na_fila)
  [[ "$ACKS_DEPOIS" -gt "$ACKS_ANTES" ]] && break
  sleep 1
done
if [[ "$ACKS_DEPOIS" -gt "$ACKS_ANTES" ]]; then
  passou "mensagem consumida da fila $FILA (ack $ACKS_ANTES → $ACKS_DEPOIS)"
else
  falhou "mensagem consumida da fila $FILA (ack não mudou: $ACKS_ANTES)"
fi

echo "Painel de transparência (público)"
requisicao GET /api/transparencia/campanhas
conferir "painel → 200 sem token" 200 "$STATUS"
conferir "valor arrecadado = 150 (somado pelo worker)" true "$(jq -r --arg id "$CAMPANHA_ID" '[.[] | select(.id == $id) | .valorArrecadado == 150] | any' <<<"$CORPO")"

echo "Regra: campanha cancelada não recebe doação"
requisicao PUT "/api/campanhas/$CAMPANHA_ID" "$TOKEN_GESTOR" "${CAMPANHA%\}},\"status\":\"Cancelada\"}"
conferir "gestor cancela a campanha → 200" 200 "$STATUS"
requisicao POST /api/doacoes "$TOKEN_DOADOR" "{\"idCampanha\":\"$CAMPANHA_ID\",\"valorDoacao\":10}"
conferir "doação para campanha cancelada → 422" 422 "$STATUS"
requisicao GET /api/transparencia/campanhas
conferir "campanha cancelada some do painel" "" "$(jq -r --arg id "$CAMPANHA_ID" '.[] | select(.id == $id) | .id' <<<"$CORPO")"

echo
echo "Resultado: $OK ok, $FALHAS falha(s)"
[[ "$FALHAS" -eq 0 ]]
