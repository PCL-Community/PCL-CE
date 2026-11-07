#!/usr/bin/env bash
# 串行发布单个 RID，避免多个 publish 同时覆盖 obj/project.assets.json。
# 用法：scripts/publish.sh <rid> [configuration] [output-root]
set -euo pipefail

RID="${1:?usage: publish.sh <rid> [configuration] [output-root]}"
CONFIG="${2:-Beta}"
OUT_ROOT="${3:-dist}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"

case "$RID" in
  win-x64|win-arm64) PLATFORM=$([ "$RID" = win-arm64 ] && echo ARM64 || echo x64) ;;
  linux-x64|linux-arm64) PLATFORM=$([ "$RID" = linux-arm64 ] && echo ARM64 || echo x64) ;;
  osx-x64|osx-arm64) PLATFORM=$([ "$RID" = osx-arm64 ] && echo ARM64 || echo x64) ;;
  *) echo "unsupported RID: $RID" >&2; exit 2 ;;
esac

OUT="$ROOT/$OUT_ROOT/$RID"
rm -rf "$OUT"
mkdir -p "$OUT"

"$DOTNET" publish "$ROOT/PCL.Launcher/PCL.Launcher.csproj" \
  -c "$CONFIG" -r "$RID" -p:Platform="$PLATFORM" \
  --no-self-contained -o "$OUT"

find "$OUT" -name '*.pdb' -delete
"$ROOT/scripts/verify-artifact.sh" "$RID" "$OUT"
echo "published: $OUT"
