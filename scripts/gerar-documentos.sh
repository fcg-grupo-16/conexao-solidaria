#!/usr/bin/env bash
# Regera os entregáveis de documentação a partir das fontes versionadas:
#   - docs/arquitetura/arquitetura.svg e .png  (fonte: docs/arquitetura/gerar-diagrama.py)
#   - docs/justificativa-bancos.pdf            (fonte: docs/justificativa-bancos.md)
#
#   ./scripts/gerar-documentos.sh
#
# Requer python3, pandoc e Google Chrome/Chromium (renderização headless).
set -euo pipefail

cd "$(dirname "$0")/.."

encontrar_chrome() {
  local candidato
  for candidato in \
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" \
    "/Applications/Chromium.app/Contents/MacOS/Chromium" \
    google-chrome google-chrome-stable chromium chromium-browser; do
    if [[ -x "$candidato" ]] || command -v "$candidato" >/dev/null 2>&1; then
      echo "$candidato"
      return 0
    fi
  done
  return 1
}

CHROME=$(encontrar_chrome) || { echo "ERRO: Google Chrome ou Chromium não encontrado." >&2; exit 1; }
command -v pandoc >/dev/null || { echo "ERRO: pandoc não encontrado." >&2; exit 1; }

echo "==> Diagrama de arquitetura"
python3 docs/arquitetura/gerar-diagrama.py
"$CHROME" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=2 --window-size=1360,900 \
  --screenshot="$PWD/docs/arquitetura/arquitetura.png" "file://$PWD/docs/arquitetura/arquitetura.svg" 2>/dev/null

echo "==> PDF da justificativa dos bancos"
BASE=$(mktemp -t justificativa.XXXXXX)
HTML="$BASE.html"
pandoc docs/justificativa-bancos.md --standalone --embed-resources --css docs/estilo-pdf.css \
  --metadata pagetitle="Justificativa dos bancos de dados" -o "$HTML"
"$CHROME" --headless=new --disable-gpu --no-pdf-header-footer \
  --print-to-pdf="$PWD/docs/justificativa-bancos.pdf" "file://$HTML" 2>/dev/null
rm -f "$BASE" "$HTML"

ls -la docs/arquitetura/arquitetura.svg docs/arquitetura/arquitetura.png docs/justificativa-bancos.pdf
