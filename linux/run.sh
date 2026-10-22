#!/usr/bin/env bash
# Starts the CAS Admin (which starts the Broker, which starts workers on demand).
#   ./run.sh                         encoder from Admin (Encoder Settings; "unset" = JPG frames), WebSocket
#   ENC=openh264 ./run.sh                force an encoder (openh264); without it, the one chosen in Admin applies
#   ./run.sh --use-webrtc            WebRTC transport
# Admin UI: http://127.0.0.1:57402/admin (this machine only)   Viewer: http://localhost:57400
# Data: $CEFGLUE_CACHE_ROOT (default ~/.local/share/cefglue/cache), broker.db/logs/recordings sit beside it.
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# A user-level install (dotnet-install.sh) is used only when it really has .NET 10; otherwise the system one.
if ls "$HOME/.dotnet/shared/Microsoft.NETCore.App" 2>/dev/null | grep -q "^10."; then export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"; fi
export CEFGLUE_WORKER_EXE_PATH="$HERE/Worker/Xilium.CefGlue.Headless.Service"
export CEFGLUE_BROKER_EXE_PATH="$HERE/Broker/Xilium.CefGlue.Broker"
export CEFGLUE_CACHE_ROOT="${CEFGLUE_CACHE_ROOT:-$HOME/.local/share/cefglue/cache}"
# Only an explicit ENC=... overrides the encoder chosen in Admin (Encoder Settings): a pre-set
# CEFGLUE_VIDEO_ENCODER always wins over the saved setting, so it must not be set by default.
if [ -n "${ENC:-}" ]; then export CEFGLUE_VIDEO_ENCODER="$ENC"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
if [ ! -f "$HERE/Worker/libcef.so" ]; then echo "CEF is missing - run ./setup-cef.sh first"; exit 1; fi
exec dotnet "$HERE/Admin/Xilium.CefGlue.Broker.Admin.dll" --auto-start-broker "$@"
