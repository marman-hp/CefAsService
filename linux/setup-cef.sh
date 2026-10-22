#!/usr/bin/env bash
# Downloads CEF 154 (linux64 "minimal", ~326 MB) from cef-builds.spotifycdn.com, verifies its SHA-1
# against the CDN's own .sha1 file, and lays it out next to the worker (hard links, so no extra disk).
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VER='154.0.33+ga03e714+chromium-154.0.8037.94'
NAME="cef_binary_${VER}_linux64_minimal"
URL="https://cef-builds.spotifycdn.com/${NAME//+/%2B}.tar.bz2"
W="$HERE/Worker"

if [ -f "$W/libcef.so" ] && [ -f "$W/CefGlueBrowserProcess/libcef.so" ]; then
  echo "CEF is already installed in $W"; exit 0
fi

TMP="$HERE/.cef-tmp"; mkdir -p "$TMP"; cd "$TMP"
[ -f "$NAME.tar.bz2" ] || curl -fL --progress-bar -o "$NAME.tar.bz2" "$URL"
echo "$(curl -fsSL "$URL.sha1")  $NAME.tar.bz2" | sha1sum -c -
tar -xjf "$NAME.tar.bz2"

mkdir -p "$W/CefGlueBrowserProcess"
cp -al "$NAME/Release/."   "$W/"
cp -al "$NAME/Resources/." "$W/"
cp -al "$NAME/Release/."   "$W/CefGlueBrowserProcess/"
cp -al "$NAME/Resources/." "$W/CefGlueBrowserProcess/"
cd "$HERE" && rm -rf "$TMP"
echo "CEF installed. Next: ./run.sh"
