#!/usr/bin/env bash
# 生成未签名（可选本地 ad-hoc 签名）的 macOS .app。
# 用法：scripts/package-macos.sh <osx-x64|osx-arm64> <version> <output-dir>
set -euo pipefail

RID="${1:?usage: package-macos.sh <osx-x64|osx-arm64> <version> <output-dir>}"
VERSION="${2:?usage: package-macos.sh <osx-x64|osx-arm64> <version> <output-dir>}"
OUT="${3:?usage: package-macos.sh <osx-x64|osx-arm64> <version> <output-dir>}"

case "$RID" in
  osx-x64|osx-arm64) ;;
  *) echo "unsupported RID: $RID (expected osx-x64 or osx-arm64)" >&2; exit 2 ;;
esac

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
APP_NAME="PCL CE.app"
APP="$OUT/$APP_NAME"
TEMP="$OUT/.publish-$RID"

rm -rf "$APP" "$TEMP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

if [ "$RID" = "osx-arm64" ]; then PLATFORM=ARM64; else PLATFORM=x64; fi

"$DOTNET" publish "$ROOT/PCL.Launcher/PCL.Launcher.csproj" \
  -c Beta -r "$RID" --no-self-contained \
  -p:Platform="$PLATFORM" \
  -p:DebugType=None -p:DebugSymbols=false \
  -o "$TEMP"

cp -a "$TEMP"/. "$APP/Contents/MacOS/"
find "$APP/Contents/MacOS" -name '*.pdb' -delete
chmod +x "$APP/Contents/MacOS/PCL.Launcher"

cp "$ROOT/PCL.Launcher/Assets/Images/icon.icns" "$APP/Contents/Resources/icon.icns"
cp "$ROOT/LICENSE" "$APP/Contents/Resources/LICENSE"
cp "$ROOT/PCL.Launcher.Core/LICENCE" "$APP/Contents/Resources/LICENCE.PCL.Launcher.Core"

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
    <string>PCL.Launcher</string>
    <key>CFBundleIconFile</key>
    <string>icon.icns</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
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
</dict>
</plist>
PLIST

printf 'APPL????' > "$APP/Contents/PkgInfo"

if command -v plutil >/dev/null 2>&1; then
  plutil -lint "$APP/Contents/Info.plist"
fi

# 本地测试用 ad-hoc 签名；不进行公证。可用 PCL_SKIP_CODESIGN=1 跳过。
if [ "${PCL_SKIP_CODESIGN:-0}" != "1" ] && command -v codesign >/dev/null 2>&1; then
  codesign --force --deep --sign - "$APP" >/dev/null
fi

rm -rf "$TEMP"
echo "created: $APP"
