#!/usr/bin/env bash
# Publishes the desktop build and wraps it in a macOS .app bundle.
#
#   tools/make-macos-app.sh                 # ad-hoc signed, runnable locally
#   tools/make-macos-app.sh --sign          # sign with the installed dev identity
#   RID=osx-x64 tools/make-macos-app.sh
#
#   PROFILE=~/Downloads/rel-police-mac.provisionprofile \
#     IDENTITY="Apple Distribution: ..." tools/make-macos-app.sh --sign  # App Store build
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RID="${RID:-osx-arm64}"
VERSION="${VERSION:-1.0.1}"
SELF_CONTAINED="${SELF_CONTAINED:-0}"
PROFILE="${PROFILE:-}"
BUNDLE_ID="uk.co.allthejohnsons.policechaser"
APP_NAME="Police Chaser"
OUT="$ROOT/artifacts/macos/$RID"
APP="$OUT/$APP_NAME.app"

echo "== publishing $RID =="
rm -rf "$OUT"
if [[ "$SELF_CONTAINED" == "1" ]]; then
  dotnet publish "$ROOT/src/PoliceChaser.Desktop/PoliceChaser.Desktop.csproj" \
    -c Release -r "$RID" --self-contained true -o "$OUT/publish" --nologo \
    -p:PublishTrimmed=false -p:UseMonoRuntime=false
else
  dotnet publish "$ROOT/src/PoliceChaser.Desktop/PoliceChaser.Desktop.csproj" \
    -c Release -r "$RID" --self-contained false -o "$OUT/publish" --nologo \
    -p:UseMonoRuntime=false
fi

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
  <key>CFBundleVersion</key><string>2</string>
  <key>LSMinimumSystemVersion</key><string>26.0</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.games</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSHumanReadableCopyright</key><string>all-the-johnsons</string>
  <key>NSSupportsAutomaticGraphicsSwitching</key><true/>
</dict>
</plist>
PLIST

chmod +x "$APP/Contents/MacOS/PoliceChaser"

ENTITLEMENTS="$ROOT/src/PoliceChaser.macOS/entitlements.plist"
if [[ -n "$PROFILE" ]]; then
  [[ -f "$PROFILE" ]] || { echo "provisioning profile not found: $PROFILE" >&2; exit 1; }
  PROFILE="$(cd "$(dirname "$PROFILE")" && pwd)/$(basename "$PROFILE")"
  echo "== embedding $(basename "$PROFILE") =="
  ditto --norsrc --noextattr "$PROFILE" "$APP/Contents/embedded.provisionprofile"

  # The profile carries the App ID and team identifier; the app plist carries
  # the sandbox keys. Sign with both so the signature matches the profile, and
  # keep the team ID out of the repository.
  security cms -D -i "$PROFILE" > "$OUT/profile.plist"
  python3 - "$OUT/profile.plist" "$ENTITLEMENTS" "$OUT/entitlements.plist" <<'PY'
import plistlib
import sys

with open(sys.argv[1], "rb") as handle:
    profile = plistlib.load(handle)
with open(sys.argv[2], "rb") as handle:
    entitlements = plistlib.load(handle)
entitlements.update(profile.get("Entitlements", {}))
with open(sys.argv[3], "wb") as handle:
    plistlib.dump(entitlements, handle)
PY
  ENTITLEMENTS="$OUT/entitlements.plist"
fi

# The provisioning profile is downloaded from the developer portal and carries
# com.apple.quarantine, which TestFlight and the App Store reject in a payload
# (ITMS-91109). Strip every extended attribute before signing seals the bundle.
xattr -cr "$APP"

if [[ "${1:-}" == "--sign" ]]; then
  IDENTITY="${IDENTITY:-Apple Development}"
  echo "== signing with $IDENTITY =="

  # Two passes. The first seals every nested file (dylibs and the managed
  # assemblies, which codesign also treats as nested code) with no entitlements:
  # `--deep` copies the entitlements it is given, and App Store Connect then wants
  # a provisioning profile for each nested executable carrying an App ID (90885).
  codesign --force --deep --options runtime --timestamp=none \
    --sign "$IDENTITY" \
    "$APP"

  # Bundled helper executables (the .NET createdump tool) must be sandboxed as well,
  # or App Store validation rejects the package with 90296. They get the sandbox key
  # without the App ID, so they still need no provisioning profile.
  NESTED_ENTITLEMENTS="$OUT/nested-entitlements.plist"
  cat > "$NESTED_ENTITLEMENTS" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>com.apple.security.app-sandbox</key>
  <true/>
</dict>
</plist>
PLIST

  while IFS= read -r -d '' nested; do
    case "$(file -b "$nested")" in
      *Mach-O*executable*)
        codesign --force --options runtime --timestamp=none \
          --sign "$IDENTITY" \
          --entitlements "$NESTED_ENTITLEMENTS" \
          "$nested" ;;
    esac
  done < <(find "$APP/Contents/MacOS" -type f -print0 | sort -z)

  # The second pass re-signs the bundle itself, so the main executable -- and only
  # it -- carries the App ID, team identifier and sandbox entitlements the profile
  # describes. Deliberately no --deep here.
  codesign --force --options runtime --timestamp=none \
    --sign "$IDENTITY" \
    --entitlements "$ENTITLEMENTS" \
    "$APP"
else
  echo "== ad-hoc signing =="
  codesign --force --deep --sign - "$APP"
fi

# Refresh the Launch Services database so Finder shows the new icon.
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$APP" >/dev/null 2>&1 || true

echo "built: $APP"
