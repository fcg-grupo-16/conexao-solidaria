---
title: "Conexão Solidária — Justificativa dos bancos de dados"
subtitle: "PostgreSQL (dados transacionais) e Redis (cache do painel de transparência)"
lang: pt-BR
---

# 1. Contexto

A plataforma **Conexão Solidária** é o MVP da ONG Esperança Solidária para gestão de campanhas de
arrecadação e doações. Ela é formada por três microsserviços:

| Serviço | Responsabilidade | Dados que escreve |
|---|---|---|
| `usuarios-api` | Cadastro público de doadores, login e emissão de JWT | usuários |
| `campanhas-api` | Gestão de campanhas, painel de transparência, intenção de doação | campanhas, doações (`Pendente`), outbox |
| `doacoes-worker` | Consome `DoacaoRecebidaEvent` e soma o valor arrecadado | doações (`Processada`/`Rejeitada`), valor arrecadado |

Os dados da plataforma têm duas naturezas bem diferentes, e por isso usamos **dois bancos**, cada um
na função em que é mais forte:

- **X — PostgreSQL 17:** dados transacionais (usuários, campanhas, doações e o outbox de mensagens).
- **Y — Redis 7:** cache do painel de transparência, a leitura pública de maior volume.

# 2. O que os dados exigem

| Requisito do PDF ou do domínio | Exigência sobre o banco |
|---|---|
| E-mail do doador **único** no banco | Restrição de unicidade garantida pelo próprio banco, inclusive sob concorrência |
| Senha com hash, CPF validado | Integridade e tipos bem definidos |
| Meta financeira (`decimal`) e valor arrecadado | Valores monetários **exatos**, sem erro de ponto flutuante |
| A API **não** atualiza o arrecadado; um worker consome o evento e atualiza | Escrita concorrente correta no mesmo registro (várias réplicas do worker) |
| Entrega de mensagens *pelo menos uma vez* (RabbitMQ) | Somar cada doação **exatamente uma vez**, mesmo com reentrega |
| Publicar o evento sem perder doações | Gravar a doação e a mensagem **atomicamente** (padrão outbox) |
| Painel de transparência público | Leitura anônima, frequente e barata; valor atualizado logo após cada doação |

# 3. Banco X — PostgreSQL 17

## 3.1 Por que PostgreSQL

1. **Transações ACID.** A idempotência do worker depende de duas escritas na mesma transação: a
   transição da doação `Pendente → Processada` e o incremento do arrecadado. Se a transição não
   acontece (porque a doação já foi processada), o incremento também não acontece.
2. **Incremento atômico.** `UPDATE campanhas SET valor_arrecadado = valor_arrecadado + @valor` resolve a
   concorrência entre réplicas do worker sem *lock* na aplicação e sem ler-modificar-gravar. Testado com
   20 doações simultâneas na mesma campanha: o total fecha.
3. **`numeric(18,2)`.** Valores monetários exatos.
4. **Restrições declarativas.** Índices únicos de e-mail e CPF, chave estrangeira doação → campanha e
   *check constraints* (meta > 0, arrecadado ≥ 0, término > início, valor da doação > 0) protegem os
   dados mesmo contra escrita fora da aplicação. Duas requisições simultâneas com o mesmo e-mail não
   criam duplicata: a segunda recebe `409`.
5. **Outbox transacional.** O MassTransit grava o `DoacaoRecebidaEvent` em tabelas do próprio
   PostgreSQL, no mesmo `SaveChanges` da doação; um serviço em background entrega ao RabbitMQ depois do
   commit. Não existe doação sem evento nem evento sem doação, e a API segue aceitando doações com o
   broker fora do ar.
6. **Operação.** Imagem oficial leve, roda confortavelmente no minikube; provider EF Core (Npgsql)
   maduro, com migrations versionadas.

## 3.2 Como está organizado

- **Database-per-service:** `usuariosdb` é exclusivo da `usuarios-api`; `campanhasdb`, do contexto de
  Campanhas. Nenhum serviço lê o banco do outro — eles se comunicam por JWT (identidade) e por eventos.
- O **`doacoes-worker` escreve em `campanhasdb`** porque é o processador assíncrono do próprio contexto de
  Campanhas: o dado que ele altera (o arrecadado de uma campanha) pertence a esse contexto. As
  migrations são da `campanhas-api`; o worker nunca altera o schema e espera o schema ficar pronto antes
  de consumir.
- Migrations aplicadas na subida da API dona, serializadas por *advisory lock* do PostgreSQL (duas
  réplicas subindo juntas não aplicam a mesma migration duas vezes).

| Tabela (`campanhasdb`) | Conteúdo |
|---|---|
| `campanhas` | título, descrição, período, meta (`numeric`), status, valor arrecadado (`numeric`) |
| `doacoes` | campanha, doador, valor, status (`Pendente`/`Processada`/`Rejeitada`), motivo |
| `outbox_message`, `outbox_state`, `inbox_state` | outbox transacional do MassTransit |

# 4. Banco Y — Redis 7

## 4.1 Por que Redis

O painel de transparência é **público e anônimo**, é o endpoint mais acessado numa campanha de
arrecadação e **muda pouco** (só quando uma doação é processada ou uma campanha é criada/editada).
É o perfil clássico de cache.

1. **Cache-aside.** O painel lê do Redis; na falta, consulta o PostgreSQL e grava com TTL curto (30 s).
2. **Invalidação ativa.** O `doacoes-worker` remove a chave do painel **logo depois do commit** de cada
   doação processada, e a `campanhas-api` depois de criar ou editar uma campanha. O valor novo aparece
   na leitura seguinte, sem esperar o TTL — que fica só como rede de segurança.
3. **Precisa ser distribuído.** Quem invalida é o worker, um processo diferente da API. Um cache em
   memória dentro do pod da API não poderia ser invalidado pelo worker, e cada réplica da API teria
   uma cópia diferente. O Redis é o ponto comum entre todas as réplicas de API e worker.
4. **Falha não derruba o painel.** Leitura e gravação no cache falham em silêncio, com timeouts curtos,
   e a consulta cai para o PostgreSQL. Na prontidão, o Redis aparece como `Degraded`, sem tirar a API
   do balanceamento.
5. **Leve e sem persistência.** É cache: roda sem disco, com limite de memória e política LRU.

# 5. Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **MongoDB** (usado nas fases anteriores do curso) | Atende, mas transações multi-documento exigem replica set — complexidade operacional que já causou incidentes —, e o domínio é naturalmente relacional (campanha ↔ doações, unicidade, valores monetários). |
| **SQL Server** | Imagem pesada (≈ 2 GB de RAM mínima) para rodar ao lado de toda a plataforma no minikube, sem vantagem funcional sobre o PostgreSQL neste problema. |
| **Calcular o arrecadado com `SUM` das doações a cada leitura** | Contraria o requisito (o worker deve atualizar o valor) e fica caro justamente no endpoint mais acessado. |
| **Cache em memória (`IMemoryCache`)** | Não pode ser invalidado pelo worker e diverge entre réplicas da API. |
| **Banco próprio para o worker** | A API teria de consultar o worker (ou sincronizar de volta) para montar o painel: acoplamento síncrono para um dado que pertence a Campanhas. |
| **Sem cache** | Funciona no MVP, mas toda visita ao painel público iria ao banco transacional que também recebe as doações. |

# 6. Evidências

- Testes de integração com **PostgreSQL e Redis reais** (Testcontainers) no CI: unicidade de e-mail e CPF
  (inclusive com máscara diferente), precisão decimal, *check constraints*, chave estrangeira,
  **mesma mensagem entregue duas vezes soma uma vez**, **20 doações concorrentes somam o total
  correto**, migrations concorrentes, `HIT`/`MISS` do cache, invalidação pelo worker e painel
  respondendo com o cache fora do ar.
- `scripts/smoke-test.sh` e o dashboard do Grafana mostram, no cluster, a doação passando pela fila e o
  painel refletindo o valor somado pelo worker.

Decisão registrada em `docs/adr/0002-bancos-de-dados.md` (repositório `fcg-grupo-16/conexao-solidaria`).
