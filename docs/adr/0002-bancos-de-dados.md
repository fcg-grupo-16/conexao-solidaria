# ADR 0002 — PostgreSQL como banco transacional e Redis como cache do painel

- **Status:** aceita
- **Data:** 2026-11-23

## Contexto

Os dados da plataforma têm duas naturezas bem diferentes:

1. **Dados transacionais** — usuários (e-mail e CPF únicos, hash de senha, role), campanhas
   (valores monetários, datas, status) e doações (intenção, status de processamento). Exigem
   integridade: unicidade garantida pelo banco, valores monetários exatos e atualizações
   concorrentes corretas do "Valor Total Arrecadado".
2. **Leitura pública de alto volume** — o painel de transparência é anônimo, é o endpoint mais
   acessado em campanhas de arrecadação e muda pouco (só quando uma doação é processada).

Há ainda um requisito de mensageria (ADR 0003): a mensagem de doação é entregue *pelo menos uma vez*,
então o worker precisa somar cada doação **exatamente uma vez** mesmo com reentregas.

## Decisão

### PostgreSQL 17 para os dados transacionais

- `usuarios-api` → database `usuariosdb`; `campanhas-api` → database `campanhasdb`
  (**database-per-service**: nenhum serviço lê o banco do outro).
- Motivos:
  - **ACID e transações** — a idempotência do worker depende de, na mesma transação, mudar a doação
    de `Pendente` para `Processada` e incrementar o arrecadado. Se a transição de status não
    acontecer (a doação já foi processada), o incremento também não acontece.
  - **Incremento atômico** — `UPDATE campanhas SET valor_arrecadado = valor_arrecadado + @valor`
    resolve concorrência entre réplicas do worker sem lock na aplicação nem leitura-modifica-escrita.
  - **`numeric(18,2)`** — valores monetários exatos, sem erro de ponto flutuante.
  - **Restrições declarativas** — índice único de e-mail e de CPF garantidos pelo banco, não só pela
    aplicação (duas requisições simultâneas não criam duplicata).
  - **Outbox transacional** — o MassTransit grava o evento na mesma transação da doação usando o
    EF Core sobre o PostgreSQL (ADR 0003).
  - **Operacional** — imagem oficial leve, roda confortavelmente no minikube, provider EF Core
    (Npgsql) maduro.

### Redis 7 para o cache do painel de transparência

- Padrão *cache-aside*: o painel lê do Redis; na falta, consulta o PostgreSQL e grava com TTL curto.
- O `doacoes-worker` **invalida** a chave do painel logo após processar uma doação, então o valor
  novo aparece na leitura seguinte, sem esperar o TTL.
- Motivo decisivo para um cache **distribuído**: quem invalida é o worker, um processo diferente da
  API. Um cache em memória dentro do pod da API não poderia ser invalidado pelo worker, e cada
  réplica da API teria uma cópia diferente.
- Falha do Redis não derruba o painel: a API cai para a consulta no banco.

### O `doacoes-worker` escreve em `campanhasdb`

O worker é o **processador assíncrono do contexto de Campanhas**, não um contexto próprio: o dado que
ele altera (o arrecadado de uma campanha) pertence a esse contexto. Por isso ele usa o mesmo
database da `campanhas-api`. As migrations são de responsabilidade exclusiva da `campanhas-api`; o
worker nunca altera o schema.

## Consequências

- **Positivas:** integridade dos valores garantida pelo banco; idempotência simples e verificável
  por teste; painel rápido e barato mesmo com muitos acessos anônimos.
- **Negativas:** dois componentes de dados para operar; o painel pode exibir um valor com até o TTL
  de atraso se a invalidação falhar (mitigado pelo TTL curto).

## Alternativas descartadas

- **MongoDB** (usado nas fases anteriores): atende, mas transações multi-documento exigem replica
  set — complexidade operacional que já nos custou incidentes — e o domínio é naturalmente
  relacional (campanha ↔ doações, unicidade, valores monetários).
- **SQL Server:** imagem pesada (≈ 2 GB de RAM mínima) para rodar ao lado de toda a plataforma no
  minikube, sem vantagem funcional sobre o PostgreSQL aqui.
- **Calcular o arrecadado com `SUM` das doações a cada leitura:** contraria o requisito (o worker deve
  atualizar o valor) e fica caro no endpoint mais acessado.
- **Cache em memória (`IMemoryCache`):** não pode ser invalidado pelo worker (ver acima).
- **Banco próprio para o worker:** obrigaria a API a consultar o worker (ou a sincronizar de volta)
  para montar o painel, criando acoplamento síncrono para um dado que pertence a Campanhas.
