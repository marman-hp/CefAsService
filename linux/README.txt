CAS for Linux x64 (preview). Video: JPG frames, or H.264 through the OpenH264 plugin (software; downloaded from Cisco).

1. Runtime (once):   ASP.NET Core 10 runtime
     Ubuntu/Debian:  sudo apt install -y aspnetcore-runtime-10.0     (or: dotnet-install.sh --channel 10.0 --runtime aspnetcore)
2. System libraries (once):
     sudo apt install -y libnss3 libatk1.0-0 libatk-bridge2.0-0 libcups2 libgbm1 libxkbcommon0 \
       libxcomposite1 libxdamage1 libxfixes3 libxrandr2 libpango-1.0-0 libcairo2 libasound2 libx11-6 \
       libxext6 libdrm2 libsqlite3-0 fontconfig fonts-noto-cjk fonts-noto-color-emoji
3. CEF (once, downloads ~326 MB):   ./setup-cef.sh
4. Run:   ./run.sh            then open http://localhost:57400 in a browser (accept the certificate warning)
     Choose the video encoder in Admin (Encoder Settings, then restart the Broker). ENC=openh264 ./run.sh forces one instead.
5. H.264 (OpenH264, downloaded from Cisco):
     dotnet Admin/Xilium.CefGlue.Broker.Admin.dll --install-encoder openh264     then   ENC=openh264 ./run.sh
     (or Admin -> Encoder Settings -> openh264 -> Download from Cisco)

Admin: http://127.0.0.1:57402/admin (loopback only; over SSH: ssh -L 57402:127.0.0.1:57402 user@host).
No X11/Wayland needed (the worker falls back to Chromium's headless ozone backend).
Natives are built for glibc 2.35 or newer (Ubuntu 22.04+, Debian 12+); the .NET parts are not glibc-sensitive.
WebSocket and WebRTC transports both work; software encoding only (no GPU encoder).
