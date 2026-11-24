# ADR 0001 — Monorepo com serviços independentes

- **Status:** aceita
- **Data:** 2026-11-23

## Contexto

A plataforma tem três microsserviços (`usuarios-api`, `campanhas-api`, `doacoes-worker`), um
contrato de evento compartilhado (`DoacaoRecebidaEvent`), manifestos Kubernetes, configuração de
observabilidade e um gateway. O PDF do hackathon exige um repositório público com um README que
leve os professores, passo a passo, a subir a infraestrutura e a aplicação localmente.

Nas fases anteriores usamos um repositório por serviço mais um repositório de orquestração. Isso
funcionou, mas cobrou um preço: mudanças de contrato precisavam de PRs coordenados em vários
repositórios, e quem corrige precisava clonar e construir cada repositório separadamente.

## Decisão

Um único repositório (`conexao-solidaria`) contendo todos os serviços, organizado de forma que cada
serviço continue sendo uma **unidade de deploy independente**:

- cada serviço tem seus próprios projetos (`Domain`, `Infrastructure`, `Api`/`Worker`), Dockerfile,
  imagem, Deployment e configuração;
- nenhum serviço referencia o código de outro serviço. O único código compartilhado fica em
  `src/BuildingBlocks/` (contrato de eventos e configuração comum de observabilidade);
- o CI constrói e publica uma imagem por serviço.

Microsserviço é uma fronteira de deploy, de dados e de responsabilidade, não de repositório.

## Consequências

- **Positivas:** um clone, um README e um script sobem tudo; mudança de contrato é atômica (o
  publicador e o consumidor mudam no mesmo PR, validados pelo mesmo CI); versões de pacote
  centralizadas em `Directory.Packages.props`.
- **Negativas:** o CI constrói tudo a cada mudança; mitigado com cache e matriz por serviço. Exige
  disciplina para não criar referências diretas entre serviços — verificado na revisão de código.

## Alternativas descartadas

- **Um repositório por serviço + orquestração:** coordenação de contrato mais cara e setup de
  correção mais longo, sem ganho real para um MVP com um time de quatro pessoas.
