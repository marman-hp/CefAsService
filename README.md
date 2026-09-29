# Cef As Service

**Chromium as a service.** Run real Chromium (CEF) browsers on a Windows server and stream them to any
web browser: desktop, phone or tablet. A **Broker** gives each client its own headless browser worker
and relays video, audio and input over **WebSocket** or **WebRTC**. A small **Admin** web app manages it all.

```
 ┌──────────────┐   WebSocket / WebRTC    ┌──────────┐ WebSocket ┌─────────────────────┐
 │ Any browser  │ ◄─────────────────────► │  Broker  │ ◄───────► │ Worker (CEF, OSR)   │ × N
 │ (viewer page)│   video · audio · input │          │           │ one per session     │
 └──────────────┘                         └────▲─────┘           └─────────────────────┘
                                               │ loopback control channel
                                          ┌────┴─────┐
                                          │  Admin   │  settings · workers · encoders
                                          └──────────┘
```

## Features

- **One real Chromium per client.** Each session runs in its own worker process and uses Chromium's own user agent, with no spoofing (only while you emulate a mobile device does it take that device's user agent, as DevTools device mode does).
- **Two transports:** WebSocket (JPG/PNG/WebP frames or encoded video) and WebRTC (video + Opus audio, TURN-ready).
- **Multi-account isolation:** by default, each tenant gets one persistent browser context. With `--isolation-mode-session`, every tab gets its own context, so you can run several logins to the same site side by side.
- **Sessions that survive:** configurable session timeout and manifest TTL, last-URL restore, and logins kept across Broker restarts.
- **Full interaction:** mouse, keyboard, touch and on-screen keyboard (Android), popups, HTML5 drag & drop, and mobile device emulation. Phones get a mobile viewer page automatically.
- **Pluggable video encoders:** loaded from `plugins/encoders/`. The OpenH264 plugin is included (the codec itself is a one-click download from Cisco, see below); the built-in formats are JPG, PNG and WebP.
- **Admin web UI:** password-protected and loopback-only. Start/stop/restart the Broker, view live workers (OS, client IP, created), and manage encoder settings, session timeout, default URL and executables.

## Quick start (release build)

1. Download the latest zip from **Releases** (CEF included) and extract it to a folder you can write to, e.g. `C:\CefAsService` (not `C:\Program Files`, see [Running on Windows](#running-on-windows)).
2. Run `Admin\Xilium.CefGlue.Broker.Admin.exe`.
3. Open **http://localhost:57402**, create the admin password and press **Start**.
4. Clients open **`https://<host>:57443/broker`**. On a LAN you'll see a one-time warning for the self-signed certificate.
5. For H.264 video (required for WebRTC): **Admin → Encoder Settings → openh264 → *Download from Cisco***, then Restart.
6. To stop everything, press **Ctrl+C** in the Admin console window. The Broker and all workers run in that window and stop with it.

The release is self-contained: no .NET installation is needed. Its `README.txt` lists every port and
first-run prompt.

## Build from source

Requirements: **Windows x64**, **.NET 10 SDK**.

CEF version: **147.0.14+g76d2442+chromium-147.0.7727.138** (Chromium 147), Windows 64-bit *minimal*
distribution. `get-cef.ps1` downloads it for you. To download it yourself, get
[cef_binary_147.0.14+g76d2442+chromium-147.0.7727.138_windows64_minimal.tar.bz2](https://cef-builds.spotifycdn.com/cef_binary_147.0.14%2Bg76d2442%2Bchromium-147.0.7727.138_windows64_minimal.tar.bz2)
(about 150 MB, all builds: [cef-builds.spotifycdn.com](https://cef-builds.spotifycdn.com/index.html)) and pass it to
`get-cef.ps1 -FromArchive <path to the .tar.bz2>`. It must be exactly this version, because CefGlue's
bindings are generated for it.

```powershell
powershell -ExecutionPolicy Bypass -File .\get-cef.ps1      # download the CEF runtime (once)
dotnet build CefAsService.sln -c Release -p:Platform=x64
```

Then run `CefGlue.Broker.Admin\bin\x64\Release\net10.0\Xilium.CefGlue.Broker.Admin.exe` and continue
from step 3 above.

## Command-line flags

Pass them to the Admin exe; it forwards everything except `--auto-start-broker` to the Broker it starts.

| Flag | Effect |
|---|---|
| `--auto-start-broker` | Start the Broker right away, without pressing Start |
| `--use-webrtc` | Use the WebRTC transport instead of WebSocket (fixed while the Broker runs). The video encoder defaults to OpenH264, which must be downloaded first |
| `--isolation-mode-session` | Give every tab its own persistent browser context (default: one per tenant) |
| `--enc-openh264` | Use OpenH264 video by default. Without an encoder, sessions start with plain JPG frames |
| `--port-mode` | Clients connect straight to the worker port instead of through the Broker relay |
| `--no-selfsigned-https` | Turn off the built-in self-signed HTTPS. Use this behind a reverse proxy (Caddy, Nginx) with a real certificate |

## Ports

| Port | What | Reachable from |
|---|---|---|
| 57402 | Admin web UI | this machine only |
| 57401 | Admin ↔ Broker control channel | this machine only |
| 57400 | Broker HTTP (workers, API); browsers are sent on to 57443 | LAN |
| 57443 | Broker HTTPS: the page clients open | LAN (the one to open in the firewall) |

In the default relay mode, 57443 is the only port other devices need. `--port-mode` also needs the
worker ports (set a fixed range with `CEFGLUE_WORKER_PORT_RANGE_START` / `_END`), and WebRTC sends
media over UDP on dynamic ports (use a TURN server across NATs). Every default port can be changed
with an environment variable: `CEFGLUE_BROKER_ADMIN_UI_PORT`, `CEFGLUE_BROKER_ADMIN_PORT`,
`CEFGLUE_BROKER_PORT`, `CEFGLUE_BROKER_HTTPS_PORT`.

## Running on Windows

- **No "Run as administrator" needed.** Admin, Broker and workers run as a normal user: every port is above 1024, and the self-signed certificate goes into your own (CurrentUser) certificate store.
- **Firewall:** the first time, Windows asks to allow the Broker and the worker on the network. Clicking *Allow access* may show a UAC prompt, because creating a firewall rule needs admin rights; the app itself still runs as a normal user. If you decline, only this machine (localhost) can connect.
- **Certificate prompt:** on the first Start, Windows asks to install the Broker's self-signed certificate. *Yes* makes `https://localhost:57443` warning-free on this machine. *No* is fine too, the browser just shows a warning.
- **Extract to a writable folder**, not `C:\Program Files`: the settings files (`Admin\admin-settings.json`, `Broker\broker-settings.json`) and the OpenH264 download are written next to the executables. Browser profiles and caches live in `C:\ProgramData\CefGlue` (a hidden folder).
- **Don't mix elevated and normal runs.** Files created while running as administrator may not be writable by a normal run afterwards. Pick one and stick to it.

## Projects

| Folder | What it is |
|---|---|
| `CefGlue`, `CefGlue.Common`, `CefGlue.Common.Shared`, `CefGlue.BrowserProcess` | CefGlue: .NET bindings for CEF (Xilium/OutSystems, MIT), with changes |
| `CefGlue.Headless`, `CefGlue.Headless.Server` | Off-screen browser host and frame transport |
| `CefGlue.Headless.Service` | Worker process, one per client session |
| `CefGlue.Broker` | Assigns workers to clients, relays traffic, serves the viewer page |
| `CefGlue.Broker.Admin` | Admin web UI |
| `CefGlue.Common.Encoder` | Encoder plugin contract + built-in JPG/PNG/WebP |
| `lib/` | Prebuilt components (binary only, see `LICENSE-BINARY.txt`) |

## Video encoders & OpenH264

The OpenH264 plugin is included, **but Cisco's `openh264.dll` is not**. Cisco's free H.264 patent
coverage applies only when the end user downloads Cisco's binary, so you fetch it on your own machine:

- **Admin → Encoder Settings → openh264 → *Download from Cisco***, or
- `powershell -ExecutionPolicy Bypass -File .\get-openh264.ps1` (after building; `-Remove` undoes it).
  The release zip also has `get-openh264.cmd`, which doesn't need PowerShell.

The download is checked against a fixed SHA-256 and must be **version 2.6.0**. You can also get it by hand:
download `https://ciscobinary.openh264.org/openh264-2.6.0-win64.dll.bz2`, extract it, rename it to
`openh264.dll` and put it in the worker's `plugins/encoders/openh264/`. Admin → Encoder Settings then
checks its SHA-256 and warns if it is a different build.

*OpenH264 Video Codec provided by Cisco Systems, Inc.* Cisco's patent coverage is for personal and
non-commercial use. A paid service that streams H.264 may need its own H.264 patent license.

> WebRTC mode needs a video encoder. WebSocket mode can also stream plain JPG frames.

## Security notes

- The Admin UI and the control channel are **loopback-only** by design. Don't expose them.
- Worker pages run in a real browser on your server. Treat whoever reaches `/broker` as someone using that machine's network.
- For public deployments, put a reverse proxy with a real certificate in front and use `--no-selfsigned-https`.
- Workers run with media streaming enabled (needed for audio), so a page opened in a worker can use the **server's** microphone or camera without a prompt. On a server with a microphone, keep that in mind.

## License

- **Source code:** MIT. See `LICENSE` (CefGlue, Xilium/OutSystems) and the `LICENSE` file in each project folder.
- **Prebuilt components in `lib/`:** free for personal and commercial use, binary only. See `LICENSE-BINARY.txt`.
- **Third-party native libraries** (libdatachannel, OpenSSL, Opus, libyuv, OpenH264) keep their own licenses in the `LICENSE-*` / `COPYING-*` files next to them. CEF/Chromium: BSD.
