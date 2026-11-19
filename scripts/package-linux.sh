#!/usr/bin/env bash
# 生成 Linux 发行包：AppImage、deb、rpm。
# 用法：scripts/package-linux.sh <linux-x64|linux-arm64> [configuration] [version] [output-dir]
set -euo pipefail

RID="${1:?usage: package-linux.sh <linux-x64|linux-arm64> [configuration] [version] [output-dir]}"
CONFIG="${2:-Beta}"
VERSION="${3:?usage: package-linux.sh <linux-x64|linux-arm64> [configuration] [version] [output-dir]}"
OUT="${4:-dist}"

case "$RID" in
  linux-x64)
    PLATFORM=x64
    ASSET_ARCH=x64
    DEB_ARCH=amd64
    RPM_ARCH=x86_64
    APPIMAGE_ARCH=x86_64
    PKG_ARCH=amd64
    ;;
  linux-arm64)
    PLATFORM=ARM64
    ASSET_ARCH=ARM64
    DEB_ARCH=arm64
    RPM_ARCH=aarch64
    APPIMAGE_ARCH=aarch64
    PKG_ARCH=arm64
    ;;
  *)
    echo "unsupported RID: $RID (expected linux-x64 or linux-arm64)" >&2
    exit 2
    ;;
esac

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET="${DOTNET:-dotnet}"
OUT="$(mkdir -p "$OUT" && cd "$OUT" && pwd)"
PUBLISH="$OUT/$RID"
ASSET_PREFIX="PCL2_CE_${CONFIG}_linux_${ASSET_ARCH}"
NFPM="${NFPM:-nfpm}"
APPIMAGETOOL="${APPIMAGETOOL:-}"
APPIMAGE_RUNTIME="${APPIMAGE_RUNTIME:-}"

fail() { echo "[linux-package] $*" >&2; exit 1; }

normalize_app_tree() {
  local root="$1"
  find "$root" -type f -exec chmod 0644 {} +
  chmod 0755 "$root/PCL.Launcher"
}

if [ ! -x "$PUBLISH/PCL.Launcher" ]; then
  DOTNET="$DOTNET" "$ROOT/scripts/publish.sh" "$RID" "$CONFIG" "$OUT"
fi
[ -x "$PUBLISH/PCL.Launcher" ] || fail "missing publish output: $PUBLISH/PCL.Launcher"
"$ROOT/scripts/verify-artifact.sh" "$RID" "$PUBLISH"

mkdir -p "$OUT"

# ── AppImage ────────────────────────────────────────────────────────────────
if [ -z "$APPIMAGETOOL" ]; then
  APPIMAGETOOL="$(command -v appimagetool || true)"
fi
if [ -z "$APPIMAGETOOL" ]; then
  fail "appimagetool not found; install it or set APPIMAGETOOL"
fi

APPDIR="$OUT/.AppDir-$RID"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/lib/pcl-ce"
cp -a "$PUBLISH"/. "$APPDIR/usr/lib/pcl-ce/"
normalize_app_tree "$APPDIR/usr/lib/pcl-ce"
cp "$ROOT/scripts/linux/AppRun" "$APPDIR/AppRun"
cp "$ROOT/scripts/linux/pcl-community-edition-appimage.desktop" \
  "$APPDIR/pcl-community-edition.desktop"
cp "$ROOT/PCL.Launcher/Assets/Images/icon.png" \
  "$APPDIR/pcl-community-edition.png"
ln -sf pcl-community-edition.png "$APPDIR/.DirIcon"
chmod 0755 "$APPDIR/AppRun"
grep -q '^Exec=AppRun %U$' "$APPDIR/pcl-community-edition.desktop" \
  || fail "AppImage desktop entry does not point to AppRun"

APPIMAGE_PATH="$OUT/${ASSET_PREFIX}.AppImage"
rm -f "$APPIMAGE_PATH"
APPIMAGE_ARGS=(--no-appstream)
if [ -n "$APPIMAGE_RUNTIME" ]; then
  APPIMAGE_ARGS+=(--runtime-file "$APPIMAGE_RUNTIME")
fi
ARCH="$APPIMAGE_ARCH" APPIMAGE_EXTRACT_AND_RUN=1 \
  "$APPIMAGETOOL" "${APPIMAGE_ARGS[@]}" "$APPDIR" "$APPIMAGE_PATH"
chmod 0755 "$APPIMAGE_PATH"

APPIMAGE_CHECK="$OUT/.AppImage-check-$RID"
rm -rf "$APPIMAGE_CHECK"
mkdir -p "$APPIMAGE_CHECK"
if ! (cd "$APPIMAGE_CHECK" && "$APPIMAGE_PATH" --appimage-extract >/dev/null 2>&1); then
  rm -rf "$APPIMAGE_CHECK"
  fail "failed to extract AppImage for verification"
fi
grep -q '^Exec=AppRun %U$' "$APPIMAGE_CHECK/squashfs-root/pcl-community-edition.desktop" \
  || { rm -rf "$APPIMAGE_CHECK"; fail "packaged desktop entry does not point to AppRun"; }
[ -x "$APPIMAGE_CHECK/squashfs-root/AppRun" ] \
  || { rm -rf "$APPIMAGE_CHECK"; fail "packaged AppRun is missing or not executable"; }
rm -rf "$APPIMAGE_CHECK"
rm -rf "$APPDIR"

# ── deb / rpm ───────────────────────────────────────────────────────────────
command -v "$NFPM" >/dev/null 2>&1 || fail "nfpm not found; install nfpm or set NFPM"

make_nfpm_config() {
  local kind="$1"
  local stage="$2"
  local config="$3"

  local depends
  if [ "$kind" = "deb" ]; then
    depends='  - libc6 (>= 2.27)
  - libx11-6
  - libice6
  - libsm6
  - libfontconfig1
  - libfreetype6
  - libgl1
  - libasound2
  - libpulse0'
  else
    depends='  - glibc >= 2.27
  - libX11
  - libICE
  - libSM
  - fontconfig
  - freetype
  - mesa-libGL
  - alsa-lib
  - pulseaudio-libs'
  fi

  cat > "$config" <<EOF
name: pcl-ce
arch: $PKG_ARCH
platform: linux
version: "$VERSION"
version_schema: semver
release: 1
section: games
priority: optional
maintainer: PCL Community <pcl-community@users.noreply.github.com>
description: PCL Community Edition Minecraft launcher
vendor: PCL Community
homepage: https://github.com/PCL-Community/PCL-CE
license: MIT
depends:
$depends
recommends:
  - dotnet-runtime-10.0
contents:
  - src: "$stage/"
    dst: /opt/pcl-ce
    type: tree
  - src: "$ROOT/scripts/linux/pcl-ce-wrapper"
    dst: /usr/bin/pcl-ce
    file_info:
      mode: 0755
  - src: "$ROOT/scripts/linux/pcl-community-edition.desktop"
    dst: /usr/share/applications/pcl-community-edition.desktop
    file_info:
      mode: 0644
  - src: "$ROOT/PCL.Launcher/Assets/Images/icon.png"
    dst: /usr/share/icons/hicolor/256x256/apps/pcl-community-edition.png
    file_info:
      mode: 0644
  - src: "$ROOT/LICENSE"
    dst: /usr/share/doc/pcl-ce/LICENSE
    file_info:
      mode: 0644
  - src: "$ROOT/PCL.Launcher.Core/LICENCE"
    dst: /usr/share/doc/pcl-ce/LICENCE.PCL.Launcher.Core
    file_info:
      mode: 0644
scripts:
  postinstall: "$ROOT/scripts/linux/postinstall.sh"
EOF
}

PKG_DIR="$OUT/.packages-$RID"
rm -rf "$PKG_DIR"
mkdir -p "$PKG_DIR"

for kind in deb rpm; do
  stage="$OUT/.pkg-stage-$RID-$kind"
  config="$PKG_DIR/nfpm-$kind.yaml"
  rm -rf "$stage"
  cp -a "$PUBLISH" "$stage"
  normalize_app_tree "$stage"
  printf '%s\n' "$kind" > "$stage/.package-kind"
  make_nfpm_config "$kind" "$stage" "$config"
  "$NFPM" pkg --config "$config" --packager "$kind" --target "$PKG_DIR"
  rm -rf "$stage"
done

shopt -s nullglob
for f in "$PKG_DIR"/*.deb; do
  mv -f "$f" "$OUT/${ASSET_PREFIX}_${DEB_ARCH}.deb"
done
for f in "$PKG_DIR"/*.rpm; do
  mv -f "$f" "$OUT/${ASSET_PREFIX}_${RPM_ARCH}.rpm"
done
rm -rf "$PKG_DIR"

echo "[linux-package] created:"
ls -lh "$OUT/${ASSET_PREFIX}"*
