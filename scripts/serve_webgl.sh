#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEBGL_DIR="$PROJECT_ROOT/Build/WebGL"
PORT="${PORT:-8000}"

if [[ ! -f "$WEBGL_DIR/index.html" ]]; then
  echo "WebGL build not found: $WEBGL_DIR/index.html" >&2
  echo "Run ./scripts/build_webgl.sh first." >&2
  exit 1
fi

echo "Serving $WEBGL_DIR at http://localhost:$PORT"
python3 -m http.server "$PORT" --directory "$WEBGL_DIR"
