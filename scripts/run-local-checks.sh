#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN_UNITY=0

while [ "$#" -gt 0 ]; do
  case "$1" in
    --unity)
      RUN_UNITY=1
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
  shift
done

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$HOME/.dotnet/tools:$PATH"

cd "$ROOT_DIR"

bash scripts/run-static-checks.sh

dotnet tool restore
dotnet tool run udonsharp-lint Packages

if [ "$RUN_UNITY" -eq 1 ]; then
  : "${UNITY_EXE:?Set UNITY_EXE to your Unity executable path before using --unity.}"
  : "${UNITY_PROJECT_PATH:?Set UNITY_PROJECT_PATH to a VRChat Creator Companion host project that includes this repository packages before using --unity.}"
  "$UNITY_EXE" \
    -batchmode \
    -quit \
    -nographics \
    -logFile - \
    -projectPath "$UNITY_PROJECT_PATH"
fi
