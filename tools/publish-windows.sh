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
    -p:UseMonoRuntime=false
else
  dotnet publish "$ROOT/src/PoliceChaser.Desktop/PoliceChaser.Desktop.csproj" \
    -c Release -r "$RID" --self-contained false -o "$OUT" --nologo \
    -p:UseMonoRuntime=false
fi

mkdir -p "$OUT/Assets"
cp "$ROOT/windows/Package.appxmanifest" "$OUT/Package.appxmanifest"
for name in StoreLogo Square150x150Logo Square44x44Logo Square44x44Logo.targetsize-48; do
  if [[ -f "$ROOT/assets/$name.png" ]]; then
    cp "$ROOT/assets/$name.png" "$OUT/Assets/$name.png"
  fi
done

echo "staged: $OUT"
