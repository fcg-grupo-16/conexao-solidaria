# Roteiro do vídeo de demonstração (máximo de 15 minutos)

O PDF pede um vídeo que **comprove a arquitetura e o funcionamento**, sem leitura de código linha a linha,
mostrando obrigatoriamente, nesta ordem:

| # | Item do PDF | Bloco do roteiro | Tempo |
|---|---|---|---|
| a | Explicação do diagrama de arquitetura | Bloco 2 | 2:30 |
| b | Pipeline de CI executando e gerando a imagem Docker | Blocos 3 e 7 | 1:30 + 0:45 |
| c | `kubectl get pods` e o dashboard do Grafana em tempo real | Bloco 4 | 1:45 |
| d.i | Autenticação via Postman/Swagger e obtenção do token JWT | Bloco 5 | 1:30 |
| d.ii | Criação de uma campanha | Bloco 5 | 1:00 |
| d.iii | Doação: payload enviado → mensagem na fila do RabbitMQ → API pública com o valor atualizado pelo worker | Bloco 6 | 3:30 |

Total planejado: **~13 minutos**, com folga para os 15.

---

## Antes de gravar (30 minutos antes)

### 1. Subir o ambiente do zero

```bash
minikube start --cpus=4 --memory=8g
./scripts/deploy-minikube.sh
./scripts/port-forward.sh            # em um terminal separado; deixe rodando
./scripts/verify-entrega.sh          # tem que terminar com 0 falha(s)
```

O `verify-entrega.sh` roda o smoke test, que também "aquece" os serviços (o primeiro login depois da
subida é mais lento) e coloca dados no Grafana.

### 2. Janelas e abas abertas, nesta ordem

1. **Diagrama**: `docs/arquitetura/arquitetura.png` em tela cheia (ou o README no GitHub, que abre com ele).
2. **GitHub Actions**: https://github.com/fcg-grupo-16/conexao-solidaria/actions/workflows/ci.yml
   — deixe aberta também a aba da **última execução verde da `main`** (para mostrar as imagens publicadas).
3. **Terminal** com fonte grande (≥ 16 pt), já em `conexao-solidaria/`.
4. **Grafana**: http://localhost:3000/d/conexao-solidaria?orgId=1&refresh=5s&from=now-15m&to=now
5. **Postman** com a coleção `Conexão Solidária` e o ambiente **Conexão Solidária — gateway** selecionado
   (importados de `docs/postman/`). Execute antes "1. Usuários → Cadastrar doador" uma vez para já existir
   um doador no ambiente.
6. **RabbitMQ Management**: http://localhost:15672/#/queues/%2F/doacoes-worker-doacao-recebida
   (login `conexao` / `conexao-demo`).
7. Opcional: **Swagger** da `campanhas-api` em http://localhost:5082/swagger.

### 3. Conferências rápidas

- `kubectl -n conexao-solidaria get pods` → 13 pods `Running`, `READY 1/1`, sem `RESTARTS`.
- No Grafana, os painéis do topo mostram números (não "No data").
- No Postman, o token não expira durante a gravação (validade de 60 min): faça os logins já gravando.

---

## Bloco 1 — Abertura (0:00 – 0:45)

**Mostrar:** o README no GitHub (topo, com o diagrama).

**Falar:**
> "Somos o grupo [nome do grupo] e este é o MVP da **Conexão Solidária**, a plataforma da ONG Esperança
> Solidária para gestão de campanhas e doações. Hoje a ONG faz isso manualmente. Nesta demonstração vamos
> mostrar a arquitetura, o pipeline de CI, a aplicação rodando no Kubernetes com observabilidade, e o fluxo
> completo: login com JWT, criação de campanha e uma doação processada de forma assíncrona."

**Ação (sem comentar):** na aba do GitHub Actions, clique em **Run workflow → main → Run workflow**. O
pipeline leva alguns minutos; ele será mostrado rodando no Bloco 3 e concluído no Bloco 7.

## Bloco 2 — a. Diagrama de arquitetura (0:45 – 3:15)

**Mostrar:** o diagrama em tela cheia; aponte com o cursor cada parte enquanto fala.

**Falar, da esquerda para a direita:**
> "Os clientes — o doador, o gestor da ONG e o público — entram por um **API Gateway**, o KrakenD, que é o
> ponto único de entrada e roteia para os microsserviços."
>
> "São **três microsserviços .NET 10**:
> a **usuarios-api** faz o cadastro público de doadores — com e-mail único, CPF validado e senha com BCrypt —
> e o login, emitindo um **JWT** com o perfil `GestorONG` ou `Doador`;
> a **campanhas-api** valida esse token com a mesma chave, faz a gestão de campanhas, restrita ao gestor,
> expõe o painel público de transparência e recebe as intenções de doação;
> e o **doacoes-worker**, que processa as doações."
>
> "O ponto central é a mensageria: quando chega uma doação, a campanhas-api **não atualiza o valor
> arrecadado**. Ela grava a doação como Pendente e publica o evento `DoacaoRecebidaEvent` no **RabbitMQ**,
> na mesma transação, usando o padrão outbox — então não existe doação sem evento. O worker consome a fila
> e soma o valor à campanha **exatamente uma vez**, mesmo que a mensagem seja reentregue."
>
> "Usamos dois bancos: **PostgreSQL**, um database por serviço, para os dados transacionais — valores
> monetários exatos, unicidade e o incremento atômico — e **Redis** como cache do painel público, que o
> worker invalida a cada doação processada. A justificativa completa está no PDF da entrega."
>
> "Tudo roda no **Kubernetes** (minikube). Cada serviço expõe `/health` e `/metrics`; o **Prometheus**
> coleta e o **Grafana** mostra. E o **GitHub Actions** compila, testa e publica as imagens Docker."

## Bloco 3 — b. Pipeline de CI executando (3:15 – 4:45)

**Mostrar:** a execução disparada no Bloco 1, em andamento.

**Falar:**
> "O pipeline roda em todo pull request e em todo push na branch principal — aqui eu disparei manualmente
> na main. São quatro etapas: **Build e testes**, com mais de 150 testes de unidade e de integração contra
> PostgreSQL, RabbitMQ e Redis reais; **validação dos manifestos** Kubernetes e dos scripts; um teste
> **ponta a ponta**, que constrói as três imagens, sobe a plataforma inteira e executa o fluxo de doação;
> e, na main, a **geração e publicação das imagens Docker** no GitHub Container Registry, com a tag igual ao
> commit."

**Mostrar:** clique no job **Build e testes** e role até o resumo dos testes (`succeeded`). Volte para a
visão geral. *Voltamos a ele no fim, quando terminar.*

## Bloco 4 — c. Pods no Kubernetes e Grafana (4:45 – 6:30)

**Mostrar (terminal):**

```bash
kubectl -n conexao-solidaria get pods
```

**Falar:**
> "Esta é a aplicação no cluster: duas réplicas de cada microsserviço e do gateway, o PostgreSQL e o
> RabbitMQ como StatefulSet com volume, o Redis, o Prometheus e o Grafana. Tudo declarado nos YAMLs da
> pasta deploy/k8s — Deployments, Services, ConfigMaps e Secrets."

**Mostrar (Grafana):** o dashboard "Conexão Solidária — Visão geral".

**Falar:**
> "O Grafana já sobe com este dashboard provisionado. No topo, as doações recebidas pela API e as
> processadas pelo worker, o valor processado e a fila do RabbitMQ com os consumidores. Abaixo, as
> requisições HTTP por serviço, a latência e os status — e aqui o consumo de **CPU e memória de cada pod**,
> coletados do Kubernetes. Atualiza a cada cinco segundos; vamos voltar aqui depois da doação."

## Bloco 5 — d.i e d.ii. Login (JWT) e criação de campanha (6:30 – 9:00)

**Mostrar (Postman, ambiente gateway):** "2. Autenticação → Login do gestor (GestorONG)" → **Send**.

**Falar:**
> "Fazendo login como gestor da ONG, pelo gateway. A resposta traz o **token JWT**, do tipo Bearer, com o
> perfil `GestorONG` — o script da coleção guarda o token para as próximas requisições."

**Mostrar:** o corpo da resposta (token, `perfil`, `expiraEm`). Opcional: no Swagger, mostrar o botão
**Authorize** com o mesmo token.

**Mostrar:** "Login do doador" → **Send** (perfil `Doador`).

**Mostrar:** "3. Campanhas → Criar campanha" → aba **Body** (Título, Descrição, Datas, Meta, Status) → **Send**.

**Falar:**
> "Criando uma campanha com título, descrição, data de início, data de término, meta financeira e status.
> Resposta **201 Created**, campanha Ativa, valor arrecadado zero."

**Mostrar (rápido, regras de negócio):** "Criar campanha inválida (regras do PDF)" → **400** com erros em
`DataFim` e `MetaFinanceira`; "Doador não pode criar campanha" → **403**.

> "As regras do PDF: término no passado e meta zero são recusados, e só o gestor cria campanhas — o doador
> recebe 403."

## Bloco 6 — d.iii. Doação passando pela fila e painel atualizado (9:00 – 12:30)

Para a mensagem **ficar visível na fila**, o worker é parado por alguns segundos — isso também prova que
a API não atualiza o valor sozinha.

**Mostrar (terminal):**

```bash
kubectl -n conexao-solidaria scale deployment/doacoes-worker --replicas=0
```

> "Vou desligar o worker por um instante, para conseguirmos ver a mensagem parada na fila."

**Mostrar (Postman):** "4. Doação (Doador) → Doar" → aba **Body**:
`{ "idCampanha": "{{campanhaId}}", "valorDoacao": 150.00 }` → **Send**.

**Falar:**
> "Este é o payload da doação: o id da campanha e o valor, enviados pelo doador autenticado. A API responde
> **202 Accepted**: a doação está **Pendente**. A API gravou a doação e publicou o evento — mas não tocou no
> valor arrecadado."

**Mostrar (RabbitMQ Management, fila `doacoes-worker-doacao-recebida`):** aguarde ~5 s a atualização:
**Ready = 1**, **Consumers = 0**. Em **Get messages**, escolha *Ack mode: Nack message requeue true* e clique
em **Get Message(s)**.

> "Aqui está a mensagem na fila `doacoes-worker-doacao-recebida`, com o `DoacaoRecebidaEvent`: id da
> doação, campanha, doador e valor. Ela entrou pelo exchange `doacao-recebida`."

**Mostrar (Postman):** "5. Transparência → Painel de transparência" → **Send**.

> "O painel público ainda mostra zero para essa campanha: quem soma é o worker, e ele está parado."

**Mostrar (terminal):**

```bash
kubectl -n conexao-solidaria scale deployment/doacoes-worker --replicas=2
kubectl -n conexao-solidaria get pods -l app.kubernetes.io/name=doacoes-worker -w
```

(Ctrl+C quando os dois pods estiverem `1/1 Running`, em ~10 s.)

**Mostrar (RabbitMQ):** a fila volta a **0** mensagens e **2 consumidores**; no gráfico *Message rates*,
aparecem o *deliver* e o *ack*.

> "O worker voltou, consumiu a mensagem e confirmou o processamento."

**Mostrar (Postman):** "Status da doação" → `Processada`; depois "Painel de transparência" de novo → a
campanha com **valorArrecadado = 150**.

> "A doação está Processada, e a API pública mostra o valor atualizado **pelo worker**. Essa soma é
> idempotente: se a mesma mensagem chegar de novo, ela não é somada duas vezes."

**Mostrar (Grafana):** os painéis do topo com a doação recebida e processada, e o pico na fila.

## Bloco 7 — b (conclusão). Pipeline verde e imagens geradas (12:30 – 13:15)

**Mostrar:** a execução do Bloco 1, agora com todos os jobs verdes; abra um job **Imagem …** e o resumo com
`ghcr.io/fcg-grupo-16/conexao-solidaria/<serviço>:<sha>`.

> "O pipeline terminou: build, testes, validação, ponta a ponta e as **três imagens Docker geradas e
> publicadas**, cada uma marcada com o commit que a originou."

## Bloco 8 — Encerramento (13:15 – 13:45)

> "Recapitulando: três microsserviços, comunicação assíncrona com RabbitMQ em que a API não atualiza o
> valor e o worker processa, Kubernetes com os YAMLs, observabilidade com Prometheus e Grafana, pipeline de
> CI gerando as imagens, e os bônus: testes de unidade no CI e API Gateway. O passo a passo para subir tudo
> está no README do repositório. Obrigado!"

---

## Se algo der errado durante a gravação

| Sintoma | O que fazer |
|---|---|
| Postman retorna 401 | Token expirado: refaça o login correspondente. |
| Grafana sem dados no topo | Rode `./scripts/smoke-test.sh` e espere 10 s. |
| Fila não mostra a mensagem | O Management atualiza a cada ~5 s: aguarde ou clique em atualizar. Confira se o worker está mesmo com 0 réplicas. |
| Worker não volta | `kubectl -n conexao-solidaria rollout status deployment/doacoes-worker`. |
| Port-forward caiu (conexão recusada) | Encerre e rode `./scripts/port-forward.sh` de novo. |
| Pipeline demorando | Mostre a execução verde mais recente da `main` (aba aberta na preparação). |
