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

## Desenvolvimento

Pré-requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download) (versão fixada em `global.json`) e Docker (os testes de integração sobem PostgreSQL com Testcontainers).

```bash
dotnet build ConexaoSolidaria.slnx
dotnet test --solution ConexaoSolidaria.slnx
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

O único `GestorONG` nasce do seed de configuração (`GestorInicial:*`); o cadastro público só cria doadores. A chave, o emissor e o público do JWT (`Jwt:*`) são os mesmos na `usuarios-api`, que emite, e na `campanhas-api`, que valida.

Todos os serviços expõem `/health/live`, `/health/ready` e `/metrics` (Prometheus).

## Decisões de arquitetura

As decisões estão registradas como ADRs em [`docs/adr/`](docs/adr/README.md): monorepo, bancos de dados (PostgreSQL + Redis), mensageria (RabbitMQ + MassTransit com outbox) e observabilidade.
