#!/usr/bin/env bash
# 校验发布产物的原生库隔离与 PDB 清理。
# 用法：scripts/verify-artifact.sh <rid> <dir>
set -euo pipefail

RID="${1:?usage: verify-artifact.sh <rid> <dir>}"
DIR="${2:?usage: verify-artifact.sh <rid> <dir>}"
fail() { echo "[artifact] $*" >&2; exit 1; }
[ -d "$DIR" ] || fail "missing directory: $DIR"

if [ -n "$(find "$DIR" -maxdepth 1 -name '*.pdb' -print -quit)" ]; then
  fail "PDB files found in Beta/Release artifact"
fi

win_native=(av_libglesv2.dll dtls_native.dll e_sqlite3.dll libSkiaSharp.dll libHarfBuzzSharp.dll miniaudioex.dll)
linux_native=(libSkiaSharp.so libHarfBuzzSharp.so libe_sqlite3.so libminiaudioex.so libdtls_native.so)
mac_native=(libSkiaSharp.dylib libHarfBuzzSharp.dylib libe_sqlite3.dylib libminiaudioex.dylib libdtls_native.dylib libAvaloniaNative.dylib)

case "$RID" in
  linux-*)
    if [ -n "$(find "$DIR" -maxdepth 1 \( -name '*.exe' -o -name '*.dylib' \) -print -quit)" ]; then
      fail "Windows/macOS native files found in Linux artifact"
    fi
    for name in "${win_native[@]}"; do
      [ ! -e "$DIR/$name" ] || fail "Windows native library in Linux artifact: $name"
    done
    for name in "${linux_native[@]}"; do
      [ -e "$DIR/$name" ] || fail "missing Linux native library: $name"
    done
    ;;
  win-*)
    if [ -n "$(find "$DIR" -maxdepth 1 \( -name '*.so' -o -name '*.dylib' \) -print -quit)" ]; then
      fail "Linux/macOS native files found in Windows artifact"
    fi
    for name in "${win_native[@]}"; do
      [ -e "$DIR/$name" ] || fail "missing Windows native library: $name"
    done
    ;;
  osx-*)
    if [ -n "$(find "$DIR" -maxdepth 1 -name '*.so' -print -quit)" ]; then
      fail "Linux native files found in macOS artifact"
    fi
    for name in "${win_native[@]}"; do
      [ ! -e "$DIR/$name" ] || fail "Windows native library in macOS artifact: $name"
    done
    for name in "${mac_native[@]}"; do
      [ -e "$DIR/$name" ] || fail "missing macOS native library: $name"
    done
    ;;
  *) fail "unsupported RID: $RID" ;;
esac

echo "[artifact] $RID OK ($DIR)"
