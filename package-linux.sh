#!/usr/bin/env bash
# Builds the Linux x64 test release: artifacts/CefAsService-v<Version>-linux-x64.tar.gz
#
#   ./package-linux.sh 0.3.0
#
# Run it on Linux (Ubuntu 22.04 or newer; the native components in lib/ are built for glibc 2.35).
# Needs the .NET 10 SDK. The release is framework-dependent: the target machine needs the ASP.NET Core 10
# runtime (see linux/README.txt). CEF is not inside the package; the user runs ./setup-cef.sh once.
#
# Layout of the tarball (cas-linux/):
#   Admin/ Broker/ Worker/   the three apps (Worker has plugins/encoders/ and the native libraries)
#   run.sh setup-cef.sh README.txt LICENSE LICENSE-BINARY.txt THIRD-PARTY-NOTICES.txt
set -euo pipefail
VERSION="${1:?usage: ./package-linux.sh <version>}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RID=linux-x64
NAME="CefAsService-v$VERSION-$RID"
STAGE="$ROOT/artifacts/$NAME/cas-linux"
OUT="$ROOT/artifacts/$NAME.tar.gz"
rm -rf "$ROOT/artifacts/$NAME" "$OUT"
mkdir -p "$STAGE"

# No debug info (it embeds local paths), no git hash in version strings, reproducible builds. CasVersion goes
# into recordings' "Writing application" tag.
PROPS=(-p:Platform=x64 -p:DebugType=none -p:DebugSymbols=false -p:IncludeSourceRevisionInInformationalVersion=false
       -p:ContinuousIntegrationBuild=true "-p:CasVersion=$VERSION" -r "$RID" --no-self-contained --nologo -v quiet)

# A previous Debug/other build of the CEF subprocess leaves stale files; the normal build below publishes it
# into the worker's CefGlueBrowserProcess/ (publish -r alone would not).
rm -rf "$ROOT/CefGlue.BrowserProcess/bin" "$ROOT/CefGlue.BrowserProcess/obj"

declare -A APPS=( [Admin]=CefGlue.Broker.Admin [Broker]=CefGlue.Broker [Worker]=CefGlue.Headless.Service )
for app in Admin Broker Worker; do
  proj="${APPS[$app]}"
  echo "Building $app ($proj)..."
  dotnet build "$ROOT/$proj/$proj.csproj" -c Release "${PROPS[@]}"
  cp -a "$ROOT/$proj/bin/x64/Release/net10.0/$RID" "$STAGE/$app"
done

# Only the CEF subprocess in CefGlueBrowserProcess/ is used; the SDK also drops a stray apphost next to the
# worker. No symbols or docs, and no runtime state a previous run may have left in the build output.
find "$STAGE" -maxdepth 2 -name 'Xilium.CefGlue.BrowserProcess*' -path "$STAGE/Worker/*" -not -path '*/CefGlueBrowserProcess/*' -delete
find "$STAGE" \( -name '*.pdb' -o -name '*.xml' -o -name 'broker.db*' -o -name 'broker-*.json' -o -name 'admin-settings.json' -o -name '*.pfx' -o -name 'cef.log' \) -type f -delete
find "$STAGE" -name 'libopenh264*' -type f -print -delete | sed 's/^/Leaving out (users download it from Cisco): /'

W="$STAGE/Worker"
for required in Xilium.CefGlue.Headless.Service CefGlueBrowserProcess/Xilium.CefGlue.BrowserProcess \
                plugins/encoders/openh264/plugin.json plugins/encoders/vp9/plugin.json plugins/encoders/vp9/libvpx.so libdatachannel.so libopus.so cefrec.so libmp3lame.so; do
  [ -e "$W/$required" ] || { echo "Worker is missing $required" >&2; exit 1; }
done

cp "$ROOT"/linux/run.sh "$ROOT"/linux/setup-cef.sh "$ROOT"/linux/README.txt "$STAGE/"
cp "$ROOT/LICENSE" "$ROOT/LICENSE-BINARY.txt" "$STAGE/"
chmod +x "$STAGE"/*.sh "$W/Xilium.CefGlue.Headless.Service" "$W/CefGlueBrowserProcess/Xilium.CefGlue.BrowserProcess" \
         "$STAGE/Broker/Xilium.CefGlue.Broker" "$STAGE/Admin/Xilium.CefGlue.Broker.Admin"

# Notices: every license file that ships with a component, plus CEF's (added when setup-cef.sh unpacks it).
{
  echo "CefAsService $VERSION (linux-x64) - third-party notices"
  echo
  echo "Chromium and CEF: CEF's BSD license and chrome://credits (in any worker browser) cover Chromium."
  echo
  find "$STAGE" -type f \( -name 'LICENSE-*.txt' -o -name 'COPYING-*.txt' -o -name '*.LICENSE.txt' \) ! -name LICENSE-BINARY.txt -print0 |
    sort -z | while IFS= read -r -d '' f; do
      echo "=============================================================================="
      echo "$(basename "$f")"; echo; cat "$f"; echo
    done
} > "$STAGE/THIRD-PARTY-NOTICES.txt"

( cd "$ROOT/artifacts/$NAME" && tar -czf "$OUT" cas-linux )
echo "Done: $OUT ($(du -h "$OUT" | cut -f1))"
