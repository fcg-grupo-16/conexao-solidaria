#!/usr/bin/env python3
"""Gera docs/arquitetura/arquitetura.svg (fonte editável do diagrama de arquitetura).

    python3 docs/arquitetura/gerar-diagrama.py

O PNG é exportado do SVG com o Chrome headless (ver docs/arquitetura/README.md).
"""
from pathlib import Path

LARGURA, ALTURA = 1360, 900
partes: list[str] = []


def add(trecho: str) -> None:
    partes.append(trecho)


def caixa(x, y, w, h, titulo, linhas, cor, fundo):
    add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" fill="{fundo}" stroke="{cor}" stroke-width="2"/>')
    add(f'<rect x="{x}" y="{y}" width="{w}" height="30" rx="10" fill="{cor}"/>'
        f'<rect x="{x}" y="{y + 20}" width="{w}" height="10" fill="{cor}"/>')
    add(f'<text x="{x + w / 2}" y="{y + 20}" font-size="15" font-weight="700" fill="#fff" text-anchor="middle">{titulo}</text>')
    for i, linha in enumerate(linhas):
        add(f'<text x="{x + w / 2}" y="{y + 50 + i * 18}" font-size="12.5" fill="#263238" text-anchor="middle">{linha}</text>')


def cilindro(x, y, w, h, titulo, linhas, cor, fundo):
    ry = 12
    add(f'<path d="M{x},{y + ry} A{w / 2},{ry} 0 0,1 {x + w},{y + ry} L{x + w},{y + h - ry} '
        f'A{w / 2},{ry} 0 0,1 {x},{y + h - ry} Z" fill="{fundo}" stroke="{cor}" stroke-width="2"/>')
    add(f'<ellipse cx="{x + w / 2}" cy="{y + ry}" rx="{w / 2}" ry="{ry}" fill="{cor}"/>')
    add(f'<text x="{x + w / 2}" y="{y + ry + 28}" font-size="14" font-weight="700" fill="{cor}" text-anchor="middle">{titulo}</text>')
    for i, linha in enumerate(linhas):
        add(f'<text x="{x + w / 2}" y="{y + ry + 48 + i * 17}" font-size="12" fill="#263238" text-anchor="middle">{linha}</text>')


def seta(pontos, rotulo=None, rx=0, ry=0, cor="#37474f", marca="seta", tracejada=False, ancora="middle", tamanho=11.5):
    caminho = "M" + " L".join(f"{a},{b}" for a, b in pontos)
    traco = ' stroke-dasharray="6 5"' if tracejada else ""
    add(f'<path d="{caminho}" fill="none" stroke="{cor}" stroke-width="2"{traco} marker-end="url(#{marca})"/>')
    if rotulo:
        for i, linha in enumerate(rotulo.split("|")):
            add(f'<text x="{rx}" y="{ry + i * 14}" font-size="{tamanho}" fill="{cor}" text-anchor="{ancora}" '
                f'font-weight="600" paint-order="stroke" stroke="#fff" stroke-width="4">{linha}</text>')


add(f'<svg xmlns="http://www.w3.org/2000/svg" width="{LARGURA}" height="{ALTURA}" viewBox="0 0 {LARGURA} {ALTURA}" '
    'font-family="Segoe UI, Helvetica Neue, Arial, sans-serif">')
add('<defs>')
for nome, cor in [("seta", "#37474f"), ("seta-roxa", "#6a1b9a"), ("seta-marrom", "#8d6e63"), ("seta-laranja", "#e65100")]:
    add(f'<marker id="{nome}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" '
        f'orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="{cor}"/></marker>')
add('</defs>')
add(f'<rect width="{LARGURA}" height="{ALTURA}" fill="#ffffff"/>')
add('<text x="30" y="42" font-size="26" font-weight="700" fill="#1a237e">Conexão Solidária — arquitetura</text>')
add('<text x="30" y="66" font-size="14" fill="#546e7a">Microsserviços .NET 10 · mensageria assíncrona · '
    'Kubernetes (minikube) · observabilidade · CI/CD</text>')

# Fronteira do cluster
add('<rect x="235" y="90" width="1105" height="710" rx="14" fill="#f5f8ff" stroke="#3f51b5" stroke-width="2" stroke-dasharray="8 6"/>')
add('<text x="250" y="114" font-size="13" font-weight="700" fill="#3f51b5">Kubernetes · minikube · namespace conexao-solidaria</text>')

# Componentes
caixa(25, 330, 180, 130, "Clientes", ["Doador · GestorONG", "Swagger / Postman", "Público (painel)"], "#455a64", "#eceff1")
caixa(265, 330, 175, 130, "API Gateway", ["KrakenD 2.13", "ponto único de entrada", ":8088 · 2 réplicas"], "#00796b", "#e0f2f1")
caixa(510, 140, 230, 120, "usuarios-api", ["cadastro de doador (CPF, BCrypt)", "login → JWT (roles)", "seed do GestorONG"],
      "#1565c0", "#e3f2fd")
caixa(510, 330, 230, 150, "campanhas-api", ["gestão de campanhas (GestorONG)", "painel de transparência (público)",
                                             "intenção de doação (Doador)", "→ DoacaoRecebidaEvent (outbox)"], "#1565c0", "#e3f2fd")
caixa(510, 585, 230, 130, "RabbitMQ 3.13", ["exchange doacao-recebida (fanout)", "fila doacoes-worker-",
                                             "doacao-recebida (+ _error)"], "#6a1b9a", "#f3e5f5")
caixa(830, 585, 230, 130, "doacoes-worker", ["consome DoacaoRecebidaEvent", "Pendente → Processada",
                                              "arrecadado += valor (1 vez)"], "#1565c0", "#e3f2fd")
cilindro(845, 130, 200, 110, "PostgreSQL 17", ["usuariosdb"], "#2e7d32", "#e8f5e9")
cilindro(845, 340, 200, 130, "PostgreSQL 17", ["campanhasdb", "campanhas · doações", "outbox do MassTransit"], "#2e7d32", "#e8f5e9")
cilindro(1115, 345, 190, 110, "Redis 7", ["cache do painel", "(cache-aside, TTL 30 s)"], "#c62828", "#ffebee")
caixa(1115, 545, 190, 95, "Prometheus", ["scrape /metrics (3 serviços)", "RabbitMQ :15692 · cAdvisor"], "#e65100", "#fff3e0")
caixa(1115, 670, 190, 90, "Grafana 12", ["dashboard provisionado", "doações · fila · HTTP · CPU"], "#e65100", "#fff3e0")
caixa(25, 560, 180, 150, "CI/CD", ["GitHub Actions", "build · testes · e2e", "imagens → GHCR", "(tag = SHA do commit)"],
      "#37474f", "#eceff1")

# Fluxos
seta([(205, 395), (263, 395)], "HTTP + JWT", 234, 385)
seta([(440, 370), (475, 370), (475, 200), (508, 200)], "/api/usuarios|/api/auth", 470, 280, ancora="end")
seta([(440, 410), (508, 410)])
for i, rota in enumerate(["/api/campanhas", "/api/doacoes", "/api/transparencia"]):
    add(f'<text x="502" y="{492 + i * 14}" font-size="10.5" fill="#37474f" text-anchor="end" font-weight="600">{rota}</text>')
seta([(740, 190), (843, 190)], "EF Core", 792, 180)
seta([(740, 380), (843, 380)], "Pendente + evento|(mesma transação)", 792, 400, tamanho=10)
seta([(700, 330), (700, 300), (1210, 300), (1210, 343)], "painel: lê / grava cache", 1060, 293)
seta([(625, 480), (625, 583)], "publica (outbox)", 632, 535, cor="#6a1b9a", marca="seta-roxa", ancora="start")
seta([(740, 650), (828, 650)], "consome", 784, 640, cor="#6a1b9a", marca="seta-roxa")
seta([(945, 585), (945, 472)], "UPDATE idempotente", 952, 532, ancora="start")
seta([(1060, 610), (1085, 610), (1085, 430), (1113, 430)], "invalida|painel", 1092, 500, ancora="start", tamanho=10.5)
seta([(1210, 640), (1210, 668)], cor="#e65100", marca="seta-laranja")
seta([(560, 262), (560, 328)], "JWT: mesma chave|(emite → valida)", 568, 288, cor="#8d6e63", marca="seta-marrom",
     tracejada=True, ancora="start", tamanho=10.5)
seta([(115, 712), (115, 770), (260, 770)], "deploy-minikube.sh|(imagem por commit)", 122, 790, ancora="start", tamanho=10.5)
add('<text x="1210" y="782" font-size="11" fill="#e65100" text-anchor="middle" font-style="italic">'
    'todos os serviços expõem /health e /metrics</text>')

# Legenda
legenda_x, legenda_y = 30, 835
add(f'<text x="{legenda_x}" y="{legenda_y}" font-size="12" font-weight="700" fill="#263238">Legenda:</text>')
x = legenda_x + 70
for cor, texto in [("#1565c0", "microsserviço .NET 10"), ("#2e7d32", "banco de dados"), ("#c62828", "cache"),
                   ("#6a1b9a", "mensageria"), ("#e65100", "observabilidade"), ("#00796b", "gateway")]:
    add(f'<rect x="{x}" y="{legenda_y - 11}" width="14" height="14" rx="3" fill="{cor}"/>'
        f'<text x="{x + 20}" y="{legenda_y}" font-size="12" fill="#263238">{texto}</text>')
    x += 40 + len(texto) * 7
add(f'<text x="{legenda_x}" y="{legenda_y + 24}" font-size="12" fill="#546e7a">Fluxo da doação: Doador → gateway → '
    'campanhas-api (202; doação Pendente + evento no mesmo commit) → RabbitMQ → doacoes-worker (soma ao arrecadado, '
    'exatamente uma vez) → painel público atualizado.</text>')
add('</svg>')

destino = Path(__file__).with_name("arquitetura.svg")
destino.write_text("\n".join(partes) + "\n", encoding="utf-8")
print(f"gerado: {destino}")
