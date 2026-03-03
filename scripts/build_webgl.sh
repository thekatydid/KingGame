#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/MacOS/Unity}"
UNITY_APP_CONTENTS="${UNITY_BIN%/MacOS/Unity}"
UNITY_EDITOR_ROOT="$(cd "$UNITY_APP_CONTENTS/../.." && pwd)"
WEBGL_SUPPORT_DIR_APP="$UNITY_APP_CONTENTS/PlaybackEngines/WebGLSupport"
WEBGL_SUPPORT_DIR_ROOT="$UNITY_EDITOR_ROOT/PlaybackEngines/WebGLSupport"
LOG_FILE="$PROJECT_ROOT/Logs/build-webgl.log"

if [[ ! -x "$UNITY_BIN" ]]; then
  echo "Unity binary not found or not executable: $UNITY_BIN" >&2
  echo "Set UNITY_BIN to your Unity executable path and try again." >&2
  exit 1
fi

if [[ ! -d "$WEBGL_SUPPORT_DIR_APP" && ! -d "$WEBGL_SUPPORT_DIR_ROOT" ]]; then
  echo "WebGL build support module is missing." >&2
  echo "Expected one of:" >&2
  echo "  - $WEBGL_SUPPORT_DIR_APP" >&2
  echo "  - $WEBGL_SUPPORT_DIR_ROOT" >&2
  echo "Install 'WebGL Build Support' from Unity Hub for version 6000.3.2f1." >&2
  exit 1
fi

mkdir -p "$PROJECT_ROOT/Logs"

echo "Building WebGL..."
"$UNITY_BIN" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "$PROJECT_ROOT" \
  -executeMethod BuildWebGL.BuildFromCommandLine \
  -logFile "$LOG_FILE"

echo "WebGL build finished."
echo "Output: $PROJECT_ROOT/Build/WebGL"
echo "Log:    $LOG_FILE"
