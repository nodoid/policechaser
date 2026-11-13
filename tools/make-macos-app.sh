#!/usr/bin/env bash
# Publishes the desktop build and wraps it in a macOS .app bundle.
#
#   tools/make-macos-app.sh                 # ad-hoc signed, runnable locally
#   tools/make-macos-app.sh --sign          # sign with the installed dev identity/profile
#   RID=osx-x64 tools/make-macos-app.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RID="${RID:-osx-arm64}"
VERSION="1.0.0"
BUNDLE_ID="uk.co.allthejohnsons.policechaser"
APP_NAME="Police Chaser"
OUT="$ROOT/artifacts/macos/$RID"
APP="$OUT/$APP_NAME.app"

echo "== publishing $RID =="
rm -rf "$OUT"
dotnet publish "$ROOT/src/PoliceChaser.Desktop/PoliceChaser.Desktop.csproj" \
  -c Release -r "$RID" --self-contained false -o "$OUT/publish" --nologo

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$OUT/publish/." "$APP/Contents/MacOS/"
cp "$ROOT/assets/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>$APP_NAME</string>
  <key>CFBundleDisplayName</key><string>$APP_NAME</string>
  <key>CFBundleExecutable</key><string>PoliceChaser</string>
  <key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>LSMinimumSystemVersion</key><string>26.0</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.games</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSHumanReadableCopyright</key><string>all-the-johnsons</string>
  <key>NSSupportsAutomaticGraphicsSwitching</key><true/>
</dict>
</plist>
PLIST

chmod +x "$APP/Contents/MacOS/PoliceChaser"

if [[ "${1:-}" == "--sign" ]]; then
  IDENTITY="${IDENTITY:-Apple Development}"
  echo "== signing with $IDENTITY =="
  codesign --force --deep --options runtime --timestamp=none \
    --sign "$IDENTITY" \
    --entitlements "$ROOT/src/PoliceChaser.macOS/entitlements.plist" \
    "$APP"
else
  echo "== ad-hoc signing =="
  codesign --force --deep --sign - "$APP"
fi

# Refresh the Launch Services database so Finder shows the new icon.
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$APP" >/dev/null 2>&1 || true

echo "built: $APP"
