#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEBGL_DIR="$PROJECT_ROOT/Build/WebGL"
REMOTE_NAME="${REMOTE_NAME:-origin}"
TARGET_BRANCH="${TARGET_BRANCH:-gh-pages}"
COMMIT_MESSAGE="${COMMIT_MESSAGE:-Deploy WebGL build}"

if [[ ! -f "$WEBGL_DIR/index.html" ]]; then
  echo "WebGL build not found: $WEBGL_DIR/index.html" >&2
  echo "Run ./scripts/build_webgl.sh first." >&2
  exit 1
fi

ORIGIN_URL="$(git -C "$PROJECT_ROOT" remote get-url "$REMOTE_NAME" 2>/dev/null || true)"
if [[ -z "$ORIGIN_URL" ]]; then
  echo "Git remote '$REMOTE_NAME' not found." >&2
  exit 1
fi

TMP_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

rsync -a --delete "$WEBGL_DIR"/ "$TMP_DIR"/
touch "$TMP_DIR/.nojekyll"

pushd "$TMP_DIR" >/dev/null
git init -b "$TARGET_BRANCH" >/dev/null
git config user.name "${GIT_AUTHOR_NAME:-Codex Deploy}"
git config user.email "${GIT_AUTHOR_EMAIL:-codex-deploy@local}"
git add .
git commit -m "$COMMIT_MESSAGE" >/dev/null
git remote add "$REMOTE_NAME" "$ORIGIN_URL"
git push --force "$REMOTE_NAME" "$TARGET_BRANCH"
popd >/dev/null

echo "Pushed WebGL build to $REMOTE_NAME/$TARGET_BRANCH"
echo "If first deploy: GitHub repo Settings -> Pages -> Branch '$TARGET_BRANCH' / root"
