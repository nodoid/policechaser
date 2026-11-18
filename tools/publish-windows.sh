#!/usr/bin/env bash
# Publishes the Windows 11 build (win-x64) and stages the MSIX package manifest.
#
#   tools/publish-windows.sh            # framework-dependent build (needs .NET 10 runtime)
#   SELF_CONTAINED=1 tools/publish-windows.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RID="${RID:-win-x64}"
OUT="$ROOT/artifacts/$RID"
SELF_CONTAINED="${SELF_CONTAINED:-0}"

echo "== publishing $RID =="
rm -rf "$OUT"
if [[ "$SELF_CONTAINED" == "1" ]]; then
  dotnet publish "$ROOT/src/PoliceChaser.Desktop/PoliceChaser.Desktop.csproj" \
    -c Release -r "$RID" --self-contained true -o "$OUT" --nologo \
    -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true \
    -p:UseMonoRuntime=false -p:OutputType=WinExe
else
  dotnet publish "$ROOT/src/PoliceChaser.Desktop/PoliceChaser.Desktop.csproj" \
    -c Release -r "$RID" --self-contained false -o "$OUT" --nologo \
    -p:UseMonoRuntime=false -p:OutputType=WinExe
fi

mkdir -p "$OUT/Assets"
cp "$ROOT/windows/Package.appxmanifest" "$OUT/Package.appxmanifest"
for name in "$ROOT"/assets/StoreLogo*.png "$ROOT"/assets/Square[0-9]*.png \
            "$ROOT"/assets/SplashScreen*.png; do
  [[ -f "$name" ]] && cp "$name" "$OUT/Assets/"
done

echo "staged: $OUT"
