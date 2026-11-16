#!/usr/bin/env bash
# 生成 macOS .app。可选 Developer ID 签名、公证与 staple。
# 用法：scripts/package-macos.sh <osx-arm64|osx-x64> <configuration> <version> <output-dir>
#
# 可选环境变量：
#   CODESIGN_IDENTITY             Developer ID Application 证书名称
#   APPLE_ID / APPLE_TEAM_ID / APPLE_APP_SPECIFIC_PASSWORD
#   PCL_BUILD_VERSION             CFBundleVersion（默认读取 metadata.json 的 version.code）
#   PCL_SKIP_CODESIGN=1           跳过签名
#   PCL_SKIP_NOTARIZATION=1       跳过公证
set -euo pipefail

RID="${1:?usage: package-macos.sh <osx-arm64|osx-x64> <configuration> <version> <output-dir>}"
CONFIG="${2:?usage: package-macos.sh <osx-arm64|osx-x64> <configuration> <version> <output-dir>}"
VERSION="${3:?usage: package-macos.sh <osx-arm64|osx-x64> <configuration> <version> <output-dir>}"
OUT="${4:?usage: package-macos.sh <osx-arm64|osx-x64> <configuration> <version> <output-dir>}"

case "$RID" in
  osx-arm64) PLATFORM=ARM64 ;;
  osx-x64) PLATFORM=x64 ;;
  *) echo "unsupported RID: $RID (expected osx-arm64 or osx-x64)" >&2; exit 2 ;;
esac

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
APP_NAME="PCL CE.app"
APP="$OUT/$APP_NAME"
TEMP="$OUT/.publish-$RID"

fail() { echo "[macos-package] $*" >&2; exit 1; }

rm -rf "$APP" "$TEMP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

"$DOTNET" publish "$ROOT/PCL.Launcher/PCL.Launcher.csproj" \
  -c "$CONFIG" -r "$RID" --no-self-contained \
  -p:Platform="$PLATFORM" \
  -p:DebugType=None -p:DebugSymbols=false \
  -o "$TEMP"

cp -a "$TEMP"/. "$APP/Contents/MacOS/"
find "$APP/Contents/MacOS" -name '*.pdb' -delete
chmod +x "$APP/Contents/MacOS/PCL.Launcher"
"$ROOT/scripts/verify-artifact.sh" "$RID" "$APP/Contents/MacOS"

cp "$ROOT/PCL.Launcher/Assets/Images/icon.icns" "$APP/Contents/Resources/icon.icns"
cp "$ROOT/LICENSE" "$APP/Contents/Resources/LICENSE"
cp "$ROOT/PCL.Launcher.Core/LICENCE" "$APP/Contents/Resources/LICENCE.PCL.Launcher.Core"
printf 'macos-app\n' > "$APP/Contents/Resources/package-kind"

cat > "$APP/Contents/MacOS/launcher" <<'LAUNCHER'
#!/bin/sh
set -eu

HERE="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

export PCL_PACKAGE_KIND=macos-app
export PCL_DATA_DIR="${PCL_DATA_DIR:-$HOME/PCLCE}"
export PCL_MINECRAFT_DIR="${PCL_MINECRAFT_DIR:-$HOME/PCLCE/.minecraft}"

exec "$HERE/PCL.Launcher" "$@"
LAUNCHER
chmod 0755 "$APP/Contents/MacOS/launcher"

BUILD_VERSION="${PCL_BUILD_VERSION:-}"
if [ -z "$BUILD_VERSION" ]; then
  BUILD_VERSION="$(python3 - "$ROOT/PCL.Launcher/Resources/metadata.json" <<'PY' 2>/dev/null || true
import json
import sys
with open(sys.argv[1], encoding="utf-8") as f:
    print(json.load(f)["version"]["code"])
PY
)"
fi
if [ -z "$BUILD_VERSION" ]; then
  BUILD_VERSION="$(printf '%s' "$VERSION" | tr -cd '0-9')"
fi
[ -n "$BUILD_VERSION" ] || BUILD_VERSION=0

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>PCL CE</string>
    <key>CFBundleDisplayName</key>
    <string>PCL Community Edition</string>
    <key>CFBundleIdentifier</key>
    <string>cc.pclc.pcl-ce</string>
    <key>CFBundleExecutable</key>
    <string>launcher</string>
    <key>CFBundleIconFile</key>
    <string>icon.icns</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleVersion</key>
    <string>${BUILD_VERSION}</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.utilities</string>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © PCL Community</string>
    <key>CFBundleLocalizations</key>
    <array>
        <string>zh-Hans</string>
        <string>zh-Hant</string>
        <string>en</string>
        <string>en-GB</string>
        <string>ja</string>
        <string>fr</string>
        <string>es</string>
    </array>
    <key>CFBundleAllowMixedLocalizations</key>
    <true/>
</dict>
</plist>
PLIST

printf 'APPL????' > "$APP/Contents/PkgInfo"

if command -v plutil >/dev/null 2>&1; then
  plutil -lint "$APP/Contents/Info.plist"
fi

SIGN_IDENTITY="${CODESIGN_IDENTITY:-}"
if [ "${PCL_SKIP_CODESIGN:-0}" != "1" ] && [ -n "$SIGN_IDENTITY" ] && command -v codesign >/dev/null 2>&1; then
  echo "[macos-package] signing with: $SIGN_IDENTITY"
  while IFS= read -r -d '' file; do
    if file "$file" | grep -q "Mach-O"; then
      codesign --force --options runtime --timestamp --sign "$SIGN_IDENTITY" "$file"
    fi
  done < <(find "$APP/Contents/MacOS" -type f -print0)
  codesign --force --options runtime --timestamp \
    --entitlements "$ROOT/scripts/macos/Entitlements.plist" \
    --sign "$SIGN_IDENTITY" "$APP"
  codesign --verify --deep --strict --verbose=2 "$APP"

  if [ "${PCL_SKIP_NOTARIZATION:-0}" != "1" ] \
    && [ -n "${APPLE_ID:-}" ] \
    && [ -n "${APPLE_TEAM_ID:-}" ] \
    && [ -n "${APPLE_APP_SPECIFIC_PASSWORD:-}" ]; then
    NOTARY_ZIP="$OUT/.notary-$RID.zip"
    ditto -c -k --sequesterRsrc --keepParent "$APP" "$NOTARY_ZIP"
    xcrun notarytool submit "$NOTARY_ZIP" \
      --apple-id "$APPLE_ID" \
      --team-id "$APPLE_TEAM_ID" \
      --password "$APPLE_APP_SPECIFIC_PASSWORD" \
      --wait
    xcrun stapler staple "$APP"
    xcrun stapler validate "$APP"
    rm -f "$NOTARY_ZIP"
  fi
elif [ "${PCL_SKIP_CODESIGN:-0}" != "1" ] && command -v codesign >/dev/null 2>&1; then
  codesign --force --deep --sign - "$APP" >/dev/null
fi

rm -rf "$TEMP"
echo "created: $APP"
