#!/bin/zsh
set -euo pipefail
project_dir="${0:A:h:h}"
cd "$project_dir"
if [[ "${1:-}" == "--universal" ]]; then
  xcodebuild -quiet -project PCLMac.xcodeproj -scheme PCLMac -configuration Release \
    -destination 'generic/platform=macOS' -derivedDataPath .build/Xcode.noindex \
    ARCHS='arm64 x86_64' ONLY_ACTIVE_ARCH=NO CODE_SIGNING_ALLOWED=NO build
  binary_dir="$project_dir/.build/Xcode.noindex/Build/Products/Release/PCL Mac版.app/Contents/MacOS"
  binary_name="PCL-SwiftUI"
elif [[ -z "${1:-}" ]]; then
  build_dir="$project_dir/.build/app"
  swift build -c release --scratch-path "$build_dir"
  binary_dir="$(swift build -c release --scratch-path "$build_dir" --show-bin-path)"
  binary_name="PCLMac"
else
  print -u2 "Usage: $0 [--universal]"
  exit 2
fi
app_dir="$project_dir/.build/dist.noindex/PCL Mac版.app"
mkdir -p "$app_dir/Contents/MacOS" "$app_dir/Contents/Resources"
binary_staging="$app_dir/Contents/MacOS/.PCL-SwiftUI.$$.staging"
trap 'rm -f "$binary_staging"' EXIT
cp "$binary_dir/$binary_name" "$binary_staging"
mv -f "$binary_staging" "$app_dir/Contents/MacOS/PCL-SwiftUI"
cp Resources/Info.plist "$app_dir/Contents/Info.plist"
cp LICENCE "$app_dir/Contents/Resources/LICENCE"
for resource in "$project_dir"/Resources/*(N); do
  [[ "${resource:t}" == "Info.plist" ]] && continue
  ditto "$resource" "$app_dir/Contents/Resources/${resource:t}"
done
codesign --force --deep --sign - "$app_dir"
print "App built: $app_dir"
