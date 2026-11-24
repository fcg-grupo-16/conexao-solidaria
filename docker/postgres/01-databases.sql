-- Database-per-service: cada serviço tem o seu database no mesmo servidor PostgreSQL.
-- usuariosdb: usuarios-api. campanhasdb: campanhas-api (dona das migrations) e doacoes-worker.
-- Executado pelo entrypoint da imagem oficial só na primeira subida do volume.
CREATE DATABASE usuariosdb;
CREATE DATABASE campanhasdb;
