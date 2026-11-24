# Conexão Solidária

[![CI](https://github.com/fcg-grupo-16/conexao-solidaria/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/fcg-grupo-16/conexao-solidaria/actions/workflows/ci.yml)

MVP da plataforma digital da ONG Esperança Solidária para gestão de campanhas de arrecadação e doações — Hackathon da pós-graduação FIAP.

> Documentação completa (passo a passo para subir localmente, diagrama e justificativa dos bancos) em construção. Acompanhe o andamento no épico [#1](https://github.com/fcg-grupo-16/conexao-solidaria/issues/1).

## Visão geral

| Serviço | Responsabilidade |
|---|---|
| `usuarios-api` | Cadastro público de doadores, login e emissão de JWT (roles `GestorONG` e `Doador`) |
| `campanhas-api` | Gestão de campanhas, painel de transparência público e intenção de doação |
| `doacoes-worker` | Consome `DoacaoRecebidaEvent` e atualiza o valor arrecadado da campanha |

Infraestrutura: PostgreSQL, Redis, RabbitMQ, Prometheus, Grafana e Kubernetes (minikube).

## Estrutura do repositório

```
src/            código dos serviços e building blocks compartilhados
tests/          testes de unidade e de integração
deploy/         manifestos Kubernetes e configuração do gateway
observability/  Prometheus e dashboards do Grafana
scripts/        deploy no minikube, smoke test e verificação da entrega
docs/           ADRs, diagrama, justificativa dos bancos e roteiro do vídeo
```

## Rodando no Kubernetes (minikube)

Pré-requisitos: Docker, [minikube](https://minikube.sigs.k8s.io/docs/start/) e `kubectl`.

```bash
minikube start --cpus=4 --memory=8g
./scripts/deploy-minikube.sh       # constrói as imagens (tag do commit), aplica deploy/k8s e espera tudo ficar pronto
kubectl -n conexao-solidaria get pods
./scripts/port-forward.sh          # expõe os serviços em localhost (Ctrl+C encerra)
```

| Serviço | Endereço (com o port-forward) |
|---|---|
| `usuarios-api` | http://localhost:5081/swagger |
| `campanhas-api` | http://localhost:5082/swagger |
| `doacoes-worker` | http://localhost:5083/health/ready |
| RabbitMQ Management | http://localhost:15672 (`conexao` / `conexao-demo`) |
| Grafana | http://localhost:3000 (dashboard **Conexão Solidária — Visão geral**; leitura sem login, admin `admin` / `conexao-demo`) |
| Prometheus | http://localhost:9090 |

Os manifestos ficam em [`deploy/k8s/`](deploy/k8s) (Kustomize): namespace, ConfigMaps, Secrets, PostgreSQL e RabbitMQ (StatefulSet com volume), Redis, e um Deployment + Service por serviço — APIs e worker com 2 réplicas, probes de *startup*, *readiness* e *liveness*, limites de recurso, usuário não root e sistema de arquivos somente leitura. A chave do JWT e as credenciais ficam num Secret só, referenciado por quem precisa (um valor, um lugar).

Para remover: `./scripts/undeploy-minikube.sh`.

## Rodando com Docker Compose (desenvolvimento)

Pré-requisito: Docker. Sobe PostgreSQL, RabbitMQ, Redis e os três serviços:

```bash
docker compose up --build -d
docker compose ps          # infraestrutura "healthy" e serviços "running"
```

| Serviço | Endereço |
|---|---|
| `usuarios-api` | http://localhost:5081/swagger |
| `campanhas-api` | http://localhost:5082/swagger |
| `doacoes-worker` | http://localhost:5083/health/ready |
| RabbitMQ Management | http://localhost:15672 (`conexao` / `conexao-demo`) |
| PostgreSQL | `localhost:15432` (`conexao` / `conexao-demo`) |
| Redis | `localhost:16379` |

Gestor de demonstração: `gestor@esperancasolidaria.org` / `Gestor@2026`. Para exercitar o fluxo inteiro:

```bash
npx newman run docs/postman/conexao-solidaria.postman_collection.json -e docs/postman/local.postman_environment.json
```

Para derrubar e apagar os dados: `docker compose down -v`. Todas as credenciais versionadas são **de demonstração**.

A entrega roda no Kubernetes (minikube); o compose existe para desenvolvimento e usa as mesmas variáveis de ambiente dos manifestos.

## Desenvolvimento

Pré-requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download) (versão fixada em `global.json`) e Docker (os testes de integração sobem PostgreSQL com Testcontainers).

```bash
dotnet build ConexaoSolidaria.slnx
dotnet test --solution ConexaoSolidaria.slnx --max-parallel-test-modules 1
```

Migrations do EF Core (ferramenta local fixada em `.config/dotnet-tools.json`):

```bash
dotnet tool restore
dotnet ef migrations add <Nome> -p src/Usuarios/Usuarios.Infrastructure -s src/Usuarios/Usuarios.Api -o Persistencia/Migrations
dotnet ef migrations add <Nome> -p src/Campanhas/Campanhas.Infrastructure -s src/Campanhas/Campanhas.Infrastructure -o Persistencia/Migrations
```

As APIs aplicam as migrations pendentes ao iniciar (`Banco:AplicarMigrations`, padrão `true`).

Versões de pacotes ficam centralizadas em `Directory.Packages.props`; os `.csproj` não declaram versão.

## Endpoints

| Método | Rota | Acesso | Serviço | Descrição |
|---|---|---|---|---|
| `POST` | `/api/usuarios` | público | `usuarios-api` | Cadastro de doador (e-mail e CPF únicos, senha com BCrypt) |
| `POST` | `/api/auth/login` | público | `usuarios-api` | Login; devolve JWT com a claim `role` (`GestorONG` ou `Doador`) |
| `GET` | `/api/usuarios/me` | autenticado | `usuarios-api` | Dados do dono do token |
| `GET` | `/api/usuarios/{id}` | `GestorONG` | `usuarios-api` | Dados de um usuário |
| `POST` | `/api/campanhas` | `GestorONG` | `campanhas-api` | Cria campanha (término não no passado, meta > 0) |
| `PUT` | `/api/campanhas/{id}` | `GestorONG` | `campanhas-api` | Edita dados e status (`Ativa`, `Concluida`, `Cancelada`) |
| `GET` | `/api/campanhas` | `GestorONG` | `campanhas-api` | Lista campanhas (`?status=` opcional) |
| `GET` | `/api/campanhas/{id}` | `GestorONG` | `campanhas-api` | Detalhes de uma campanha |
| `POST` | `/api/doacoes` | `Doador` | `campanhas-api` | Intenção de doação → `202 Accepted`; publica `DoacaoRecebidaEvent` |
| `GET` | `/api/transparencia/campanhas` | público | `campanhas-api` | Painel de transparência: campanhas `Ativa` com título, meta e valor arrecadado |
| `GET` | `/api/doacoes/{id}` | `Doador` (própria) ou `GestorONG` | `campanhas-api` | Status do processamento (`Pendente`, `Processada`, `Rejeitada`) |

O único `GestorONG` nasce do seed de configuração (`GestorInicial:*`); o cadastro público só cria doadores. A chave, o emissor e o público do JWT (`Jwt:*`) são os mesmos na `usuarios-api`, que emite, e na `campanhas-api`, que valida.

O `doacoes-worker` consome a fila `doacoes-worker-doacao-recebida`, soma o valor ao arrecadado da campanha **exatamente uma vez** (a transição `Pendente → Processada` é a guarda, na mesma transação do incremento atômico) e marca a doação como `Processada`, ou `Rejeitada` se a campanha foi cancelada ou concluída antes do processamento. Falhas transitórias são retentadas; as persistentes vão para a fila `_error`.

O painel é servido do Redis (*cache-aside*, TTL de 30 s) e invalidado logo depois de cada doação processada e de cada criação ou edição de campanha; o header `X-Cache` indica `HIT` ou `MISS`. Sem Redis, o painel lê direto do PostgreSQL.

A doação é assíncrona: a `campanhas-api` grava a doação como `Pendente` e o `DoacaoRecebidaEvent` no mesmo commit (outbox do MassTransit), e o evento é entregue no exchange `doacao-recebida` do RabbitMQ. A API **não** altera o valor arrecadado.

Todos os serviços expõem `/health/live`, `/health/ready` e `/metrics` (Prometheus).

## Swagger e Postman

- Swagger em `/swagger` nas duas APIs, com o botão **Authorize** para o JWT.
- Coleção Postman do fluxo completo em [`docs/postman/`](docs/postman/README.md).

## Observabilidade

Cada serviço expõe:

| Endpoint | Para quê |
|---|---|
| `/health/live` | O processo responde (probe de *liveness*; não consulta dependências) |
| `/health/ready` | Dependências: PostgreSQL, RabbitMQ e Redis conforme o serviço (probe de *readiness*; `503` se uma dependência essencial cair) |
| `/metrics` | Métricas no formato Prometheus (OpenTelemetry) |

Métricas de negócio, além das HTTP (`http_server_request_duration_seconds`), do runtime .NET e do MassTransit:

| Serviço | Métrica |
|---|---|
| `usuarios-api` | `usuarios_cadastrados_total`, `usuarios_logins_total{resultado,perfil}` |
| `campanhas-api` | `doacoes_recebidas_total`, `doacoes_valor_recebido_BRL_total`, `doacoes_recusadas_total{motivo}` |
| `doacoes-worker` | `doacoes_processadas_total`, `doacoes_rejeitadas_total`, `doacoes_reentregas_ignoradas_total`, `doacoes_valor_processado_BRL_total`, `doacoes_atraso_processamento_seconds` |

No cluster, o **Prometheus** coleta os serviços (pods com a anotação `prometheus.io/scrape`), o RabbitMQ (plugin `rabbitmq_prometheus`, por fila) e o cAdvisor do kubelet (CPU e memória por pod). O **Grafana** sobe com o datasource e o dashboard **provisionados** a partir de [`deploy/k8s/observabilidade/`](deploy/k8s/observabilidade): doações recebidas × processadas, valor processado, fila do RabbitMQ e seus consumidores, atraso até o processamento, requisições/latência/status HTTP por serviço e rota, cadastros e logins, CPU e memória por pod e mensagens do MassTransit.

Na prontidão, a queda do RabbitMQ ou do Redis deixa a `campanhas-api` como `Degraded` (o outbox guarda as doações e o painel lê do banco) e o `doacoes-worker` como `Unhealthy` (sem broker ele não trabalha).

Os logs são JSON (Serilog) com o `TraceId` da requisição; o MassTransit propaga o contexto, então a doação na API e o processamento no worker compartilham o mesmo trace.

## CI/CD

O workflow [`CI`](.github/workflows/ci.yml) roda em todo PR e em todo push na `main`:

| Job | O que faz |
|---|---|
| **Build e testes** | `dotnet build` e todos os testes (unidade e integração com PostgreSQL, RabbitMQ e Redis via Testcontainers); resultados `.trx` como artefato |
| **Manifestos e scripts** | `yamllint`, `docker compose config`, `kubeconform` nos manifestos renderizados pelo Kustomize, JSON do dashboard e `shellcheck` |
| **Ponta a ponta (docker compose)** | Constrói as três imagens, sobe a plataforma inteira e executa a coleção Postman do fluxo de doação |
| **Imagem \<serviço\>** | Só na `main`: publica `ghcr.io/fcg-grupo-16/conexao-solidaria/<serviço>:<sha do commit>` |

A `main` é protegida: só recebe PR com os três primeiros jobs verdes.

## Decisões de arquitetura

As decisões estão registradas como ADRs em [`docs/adr/`](docs/adr/README.md): monorepo, bancos de dados (PostgreSQL + Redis), mensageria (RabbitMQ + MassTransit com outbox) e observabilidade.
