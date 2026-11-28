#!/usr/bin/env bash
set -euo pipefail

APPHOST="${1:?usage: update-apphost-sdk.sh <apphost> <libAvaloniaNative.dylib>}"
AVALONIA_NATIVE="${2:?usage: update-apphost-sdk.sh <apphost> <libAvaloniaNative.dylib>}"
BUILD_VERSION="$(xcrun vtool -show-build "$APPHOST")"
ARCH="$(lipo -archs "$APPHOST")"
if [[ "$ARCH" == *" "* ]]; then
    echo "[macos-apphost] expected a single-architecture apphost" >&2
    exit 1
fi

NATIVE_VERSION="$(xcrun vtool -arch "$ARCH" -show-build "$AVALONIA_NATIVE")"
PLATFORM="$(printf '%s\n' "$BUILD_VERSION" | awk '$1 == "platform" { print $2 }')"
MIN_OS="$(printf '%s\n' "$BUILD_VERSION" | awk '$1 == "minos" { print $2 }')"
SDK="$(printf '%s\n' "$BUILD_VERSION" | awk '$1 == "sdk" { print $2 }')"
NATIVE_PLATFORM="$(printf '%s\n' "$NATIVE_VERSION" | awk '$1 == "platform" { print $2 }')"
NATIVE_SDK="$(printf '%s\n' "$NATIVE_VERSION" | awk '$1 == "sdk" { print $2 }')"
if [[ "$PLATFORM" != MACOS || "$NATIVE_PLATFORM" != MACOS || -z "$MIN_OS" || -z "$SDK" || -z "$NATIVE_SDK" ]]; then
    echo "[macos-apphost] missing macOS build metadata" >&2
    exit 1
fi
[[ "$SDK" == "$NATIVE_SDK" ]] && exit 0

set -- -set-build-version macos "$MIN_OS" "$NATIVE_SDK"
while read -r tool version; do
    set -- "$@" -tool "$tool" "$version"
done < <(printf '%s\n' "$BUILD_VERSION" | awk '
    $1 == "tool" { tool = tolower($2) }
    $1 == "version" && tool != "" { print tool, $2; tool = "" }
')
xcrun vtool "$@" -output "$APPHOST" "$APPHOST"
codesign --force --sign - "$APPHOST"
codesign --verify --strict "$APPHOST"
echo "[macos-apphost] SDK $SDK -> Avalonia SDK $NATIVE_SDK; minimum macOS remains $MIN_OS"
