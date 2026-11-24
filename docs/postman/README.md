# Coleção Postman

Fluxo completo do MVP, na ordem do vídeo: cadastro de doador → login (JWT) → criação de campanha → doação → painel de transparência atualizado pelo `doacoes-worker`.

## Como usar

1. Importe `conexao-solidaria.postman_collection.json` e `local.postman_environment.json` no Postman.
2. Selecione o ambiente **Conexão Solidária — local** e ajuste `usuariosUrl` e `campanhasUrl` se necessário (com o gateway, as duas apontam para o endereço dele).
3. Rode a coleção pelo **Runner**, em ordem. Cada requisição guarda o que a próxima precisa (`tokenGestor`, `tokenDoador`, `campanhaId`, `doacaoId`); o cadastro gera e-mail e CPF válidos novos a cada execução.

As credenciais do gestor no ambiente são as **de demonstração** do seed (`GestorInicial:*`).

Pela linha de comando:

```bash
npx newman run docs/postman/conexao-solidaria.postman_collection.json -e docs/postman/local.postman_environment.json
```

## Swagger

As duas APIs publicam o Swagger em `/swagger`. Faça o login em `POST /api/auth/login` na `usuarios-api`, copie o `token` e use o botão **Authorize** (sem o prefixo `Bearer`).
