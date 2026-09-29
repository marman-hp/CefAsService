<#
.SYNOPSIS
  Builds the ready-to-run release zip: artifacts\CefAsService-v<Version>-win-x64.zip.

.DESCRIPTION
  Publishes Admin, Broker and the worker self-contained for win-x64 (no .NET install needed on the
  target machine), adds the CEF runtime, encoder plugins and native components, and writes
  THIRD-PARTY-NOTICES.txt. Needs the CEF runtime first (.\get-cef.ps1).

  Zip layout:
    Admin\    Xilium.CefGlue.Broker.Admin.exe  (start here)
    Broker\   Xilium.CefGlue.Broker.exe
    Worker\   Xilium.CefGlue.Headless.Service.exe + CEF + plugins\encoders\
    LICENSE, LICENSE-BINARY.txt, THIRD-PARTY-NOTICES.txt, README.txt

.EXAMPLE
  .\package.ps1 -Version 0.1.0
#>
param(
    [Parameter(Mandatory = $true)][string]$Version
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$rid = "win-x64"
$name = "CefAsService-v$Version-$rid"
$stage = Join-Path $root "artifacts\$name"
$zip = Join-Path $root "artifacts\$name.zip"

if (-not (Test-Path (Join-Path $root "cef\runtimes\win-x64\native\libcef.dll"))) {
    throw "CEF runtime missing - run .\get-cef.ps1 first."
}

if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
if (Test-Path $zip) { Remove-Item -Force $zip }
New-Item -ItemType Directory -Force $stage | Out-Null

# The normal solution build first: it is what publishes the CEF subprocess (CefGlue.BrowserProcess)
# and copies the encoder plugins next to the worker; `publish -r` alone does neither.
# No debug info (it embeds local paths), no git hash in version strings, reproducible builds.
$releaseProps = @("-p:DebugType=none", "-p:DebugSymbols=false", "-p:IncludeSourceRevisionInInformationalVersion=false", "-p:ContinuousIntegrationBuild=true")

Write-Host "Building the solution (Release x64)..."
dotnet build (Join-Path $root "CefAsService.sln") -c Release -p:Platform=x64 @releaseProps --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Solution build failed." }

# Extras: folders the build copies next to the exe with its own targets, which `publish` never sees.
$apps = @(
    @{ Folder = "Admin";  Project = "CefGlue.Broker.Admin\CefGlue.Broker.Admin.csproj"; Extras = @() },
    @{ Folder = "Broker"; Project = "CefGlue.Broker\CefGlue.Broker.csproj"; Extras = @("wwwroot") },
    @{ Folder = "Worker"; Project = "CefGlue.Headless.Service\CefGlue.Headless.Service.csproj"; Extras = @("CefGlueBrowserProcess", "plugins", "runtimes\win-x64\native") }
)

foreach ($app in $apps) {
    $out = Join-Path $stage $app.Folder
    Write-Host "Publishing $($app.Folder)..."
    # BuildingSolutionFile=true: Admin must not rebuild Broker/Worker itself in the middle of this.
    dotnet publish (Join-Path $root $app.Project) -c Release -r $rid --self-contained true `
        -p:Platform=x64 -p:BuildingSolutionFile=true -p:PublishReadyToRun=false @releaseProps -o $out --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Publishing $($app.Folder) failed." }

    # The extras come from the solution build output, never overwriting what publish produced.
    $projectDir = Split-Path (Join-Path $root $app.Project)
    $buildOut = Join-Path $projectDir "bin\x64\Release\net10.0"
    foreach ($extra in $app.Extras) {
        $from = Join-Path $buildOut $extra
        if (-not (Test-Path $from)) { throw "$($app.Folder): build output has no $extra." }
        Get-ChildItem $from -Recurse -File | ForEach-Object {
            $relative = $_.FullName.Substring($buildOut.Length + 1)
            $target = Join-Path $out $relative
            if (-not (Test-Path $target)) {
                New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
                Copy-Item $_.FullName $target
            }
        }
    }

    # Nothing but win-x64 natives, no debug symbols or XML docs in the release.
    foreach ($foreign in "osx", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64", "win-arm64", "win-x86") {
        $dir = Join-Path $out "runtimes\$foreign"
        if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    }
    Get-ChildItem $out -Recurse -File -Include "*.pdb", "*.xml" | Remove-Item -Force

    # Runtime state a previous run may have left in the build output - never ship it.
    Get-ChildItem $out -Recurse -Include "broker-settings.json", "broker-workers.json", "admin-settings.json", "*.pfx", "cef.log" -File |
        Remove-Item -Force
}

$worker = Join-Path $stage "Worker"
foreach ($required in "runtimes\win-x64\native\libcef.dll", "CefGlueBrowserProcess\Xilium.CefGlue.BrowserProcess.exe", "plugins\encoders\openh264\plugin.json", "datachannel.dll", "opus.dll") {
    if (-not (Test-Path (Join-Path $worker $required))) { throw "Worker is missing $required." }
}

# openh264.dll must reach end users only as their own download from Cisco (Admin > Encoder
# Settings), never inside the release - Cisco's H.264 patent coverage depends on it. A copy left in a
# build folder by a local Download test would otherwise slip in here, so drop it from the staging copy.
Get-ChildItem $stage -Recurse -Filter "openh264.dll" | ForEach-Object {
    Write-Host "Leaving out $($_.FullName.Substring($stage.Length + 1)) - users download it from Cisco."
    Remove-Item $_.FullName -Force
}

# Licenses and notices.
Copy-Item (Join-Path $root "LICENSE") $stage
Copy-Item (Join-Path $root "LICENSE-BINARY.txt") $stage
Copy-Item (Join-Path $root "get-openh264.cmd") $stage
Copy-Item (Join-Path $root "get-openh264.ps1") $stage

$notices = New-Object System.Text.StringBuilder
[void]$notices.AppendLine("CefAsService $Version - third-party notices")
[void]$notices.AppendLine("")
[void]$notices.AppendLine("Chromium and its own third-party components: see chrome://credits in any worker browser.")
[void]$notices.AppendLine("")
$cefLicense = Join-Path $root "cef\LICENSE-cef.txt"
if (Test-Path $cefLicense) {
    [void]$notices.AppendLine("=" * 78)
    [void]$notices.AppendLine("Chromium Embedded Framework (CEF)")
    [void]$notices.AppendLine("")
    [void]$notices.AppendLine((Get-Content $cefLicense -Raw))
    [void]$notices.AppendLine("The CEF logo (Broker\wwwroot\img\cef-logo.svg, shown as ""Powered by CEF"") is")
    [void]$notices.AppendLine("Copyright (c) CEF Project Team, under the 3-clause BSD license above.")
    [void]$notices.AppendLine("")
} else {
    Write-Warning "cef\LICENSE-cef.txt not found (run get-cef.ps1 -FromDirectory ... -LicenseFile <CEF LICENSE.txt>) - CEF's BSD license is missing from THIRD-PARTY-NOTICES.txt."
}
Get-ChildItem $stage -Recurse -File -Include "LICENSE-*.txt", "COPYING-*.txt" -Exclude "LICENSE-BINARY.txt" | Sort-Object Name -Unique | ForEach-Object {
    [void]$notices.AppendLine("=" * 78)
    [void]$notices.AppendLine($_.BaseName)
    [void]$notices.AppendLine("")
    [void]$notices.AppendLine((Get-Content $_.FullName -Raw))
}

# NuGet packages that ship inside the zip (self-contained .NET runtime, Silk.NET, SkiaSharp): their
# license texts come from the exact package versions the published *.deps.json files name, read
# from the active NuGet global-packages folder.
$shipped = @{}
Get-ChildItem $stage -Recurse -Filter "*.deps.json" | ForEach-Object {
    foreach ($m in [regex]::Matches((Get-Content $_.FullName -Raw), '"(?:runtimepack\.)?([A-Za-z0-9_.\-]+)/([0-9][^"]*)"\s*:\s*\{')) {
        $shipped[$m.Groups[1].Value.ToLowerInvariant()] = $m.Groups[2].Value
    }
}
Push-Location $root
$globalPackages = ((& dotnet nuget locals global-packages --list) -replace '^global-packages:\s*', '').Trim()
Pop-Location

function Add-PackageNotice([string]$Title, [string]$PackageId, [string[]]$Files, [string]$FallbackText) {
    $version = $shipped[$PackageId.ToLowerInvariant()]
    if (-not $version) { return }
    [void]$notices.AppendLine("=" * 78)
    [void]$notices.AppendLine("$Title ($PackageId $version)")
    [void]$notices.AppendLine("")
    $found = $false
    foreach ($file in $Files) {
        $path = Join-Path $globalPackages "$($PackageId.ToLowerInvariant())\$version\$file"
        if (Test-Path $path) {
            [void]$notices.AppendLine((Get-Content $path -Raw))
            $found = $true
        }
    }
    if (-not $found) {
        if ($FallbackText) {
            [void]$notices.AppendLine($FallbackText)
        } else {
            Write-Warning "No license file for $PackageId $version under $globalPackages - its notice is missing."
        }
    }
}

Add-PackageNotice ".NET runtime" "Microsoft.NETCore.App.Runtime.win-x64" @("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT")
Add-PackageNotice "ASP.NET Core runtime" "Microsoft.AspNetCore.App.Runtime.win-x64" @("LICENSE.txt", "THIRD-PARTY-NOTICES.TXT")
Add-PackageNotice "SkiaSharp" "SkiaSharp" @("LICENSE.txt")
Add-PackageNotice "SkiaSharp native assets (includes Skia)" "SkiaSharp.NativeAssets.Win32" @("LICENSE.txt", "THIRD-PARTY-NOTICES.txt")
# Silk.NET's packages carry only an "MIT" license expression (no file); copyright holder per their
# nuspec <authors>.
$silkMit = @"
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"@
$silk = @("Silk.NET.Core", "Silk.NET.Maths", "Silk.NET.DXGI", "Silk.NET.Direct3D11") | Where-Object { $shipped[$_.ToLowerInvariant()] }
if ($silk) {
    [void]$notices.AppendLine("=" * 78)
    [void]$notices.AppendLine("Silk.NET (" + (($silk | ForEach-Object { "$_ $($shipped[$_.ToLowerInvariant()])" }) -join ", ") + ")")
    [void]$notices.AppendLine("")
    [void]$notices.AppendLine($silkMit)
}

Set-Content (Join-Path $stage "THIRD-PARTY-NOTICES.txt") $notices.ToString() -Encoding UTF8

@"
CefAsService $Version
==================================================

No .NET installation is needed - everything is self-contained.


QUICK START
-----------
1. Run Admin\Xilium.CefGlue.Broker.Admin.exe
2. Open http://localhost:57402 and create the admin password.
3. Press Start. Clients open https://<this machine>:57443/broker
4. To shut the app down, press Ctrl+C in the Admin console window - the
   Broker and all workers run in that same window and stop with it.

Out of the box the stream is JPG frames. For H.264 video - required for
WebRTC - see OPENH264 below: one click in Admin downloads it from Cisco.
Using --use-webrtc? Download OpenH264 first - Admin shows a banner with
the button until it is installed.


OPENH264 (H.264 VIDEO)
----------------------
The OpenH264 codec is NOT included. It is downloaded to your machine
directly from Cisco, which keeps it covered by Cisco's own H.264 patent
license:

1. Admin > Encoder Settings > choose "openh264".
2. Click "Download from Cisco". The file is checked against a fixed
   SHA-256 before it is kept (Worker\plugins\encoders\openh264\).
3. Press Restart.

"Remove" (with the Broker stopped) disables it again; download again to
re-enable it.

Without a browser (a server started with --auto-start-broker), run
get-openh264.cmd (double-click, or from a command prompt - no PowerShell
needed):
    get-openh264.cmd              download from Cisco
    get-openh264.cmd remove       remove it again
    (add "nopause" when calling it from another script)
or, with PowerShell:
    powershell -ExecutionPolicy Bypass -File .\get-openh264.ps1 [-Remove]

Or download it yourself, without this app:
1. Download https://ciscobinary.openh264.org/openh264-2.6.0-win64.dll.bz2
   (linked from Cisco's release page, github.com/cisco/openh264).
2. Extract the .bz2 (e.g. with 7-Zip) and rename the result to
   openh264.dll.
3. Put it in Worker\plugins\encoders\openh264\ and restart the Broker.
It must be version 2.6.0 - the plugin is built against that API. Admin >
Encoder Settings checks the file's SHA-256 and says "verified", or warns
if it is a different build.

    OpenH264 Video Codec provided by Cisco Systems, Inc.

Cisco's binary license terms are reproduced in THIRD-PARTY-NOTICES.txt.
Note their patent coverage is for personal / non-commercial use; running
a paid service that streams H.264 may need your own H.264 patent license
(Via LA) - that is the operator's responsibility.


HARDWARE
--------
* Server CPU: OpenH264 encodes in software, one encoder per streaming
  session. From our testing: 6 cores minimum, 8 or more recommended -
  more for several sessions at once or high resolutions.
* Server GPU: not needed (Disable GPU is on by default).
* Viewers: a browser with hardware H.264 decoding (any normal PC or phone)
  is smoothest. In a VM or without a GPU it decodes in software - it
  works, just slower.


FIRST RUN - WHAT WINDOWS WILL ASK
---------------------------------
* Windows Security Alert (firewall) for Xilium.CefGlue.Broker and
  Xilium.CefGlue.Headless.Service: click "Allow access" (Private networks)
  so other devices on your LAN can connect.

* On the first Start the Broker creates a self-signed HTTPS certificate for
  this machine (localhost + its LAN IP addresses) and asks Windows to trust
  it. Windows shows "Security Warning - You are about to install a
  certificate from a certification authority ..." - click Yes.
  This only makes https://localhost:57443 warning-free on THIS machine.
  Clicking No is fine too: everything still works, the browser just shows
  a certificate warning.

* Other devices (phones, other PCs) show a certificate warning the first
  time they open https://<this machine>:57443/broker - choose
  "Advanced" -> "Proceed". For a real deployment put a reverse proxy with a
  real certificate in front and start with --no-selfsigned-https.


PORTS
-----
  57402  TCP  Admin web page                      localhost only
  57401  TCP  Admin <-> Broker control channel    localhost only
  57400  TCP  Broker HTTP (workers + API)         LAN - browsers opening
                                                  it go on to 57443
  57443  TCP  Broker HTTPS - the page clients     LAN - open this one
              open

  Relay mode (default): all client traffic goes through 57443 - that is the
  only port other devices need.

  Port mode (--port-mode, or Admin > Transport): clients connect straight
  to each worker's own port. Workers pick a free port at random unless you
  set a fixed range - then open that range in the firewall:
      CEFGLUE_WORKER_PORT_RANGE_START=58500
      CEFGLUE_WORKER_PORT_RANGE_END=58599

  WebRTC (--use-webrtc): video/audio travel over UDP on dynamic ports, so
  the worker exe must be allowed through the firewall. Clients behind
  another NAT need a TURN server (Admin > Transport > ICE servers).

  Every default port can be changed with an environment variable:
      CEFGLUE_BROKER_ADMIN_UI_PORT   (57402)
      CEFGLUE_BROKER_ADMIN_PORT      (57401)
      CEFGLUE_BROKER_PORT            (57400)
      CEFGLUE_BROKER_HTTPS_PORT      (57443)


COMMAND-LINE PARAMETERS
-----------------------
Pass them to Admin\Xilium.CefGlue.Broker.Admin.exe - Admin forwards
everything except --auto-start-broker to the Broker it starts.

  --auto-start-broker       Start the Broker right away (no Start click).
  --use-webrtc              WebRTC transport instead of WebSocket. The video
                            encoder defaults to OpenH264 (JPG frames cannot
                            go over WebRTC) - download it first, see
                            OPENH264.
  --port-mode               Clients connect directly to worker ports
                            instead of through the Broker (see PORTS).
  --isolation-mode-session  Every browser tab gets its own isolated
                            profile (cookies, logins), even for the same
                            site. Default: tabs of one user share a profile.
  --no-selfsigned-https     Don't create/listen with the self-signed
                            certificate (no 57443) - for use behind a
                            reverse proxy with a real certificate.
  --enc-openh264            Use OpenH264 video instead of JPG frames (needs
                            the Cisco download above). Same
                            as picking it in Admin > Encoder Settings.

Example:
  Admin\Xilium.CefGlue.Broker.Admin.exe --auto-start-broker --use-webrtc

Transport (WebSocket/WebRTC) and isolation mode are fixed at launch; all
other settings are changed on the Admin page and apply on Restart.


KNOWN ISSUES
------------
* Firefox on Windows: in WebRTC mode the video does not show (the page
  stays blank). The WebRTC viewer needs MediaStreamTrackProcessor, which
  Firefox on Windows does not have. Use Chrome or Edge there, or run the
  Broker in WebSocket mode (without --use-webrtc), which works in Firefox.


FILES
-----
  Admin\admin-settings.json     admin password (hashed), exe paths
  Broker\broker-settings.json   everything set on the Admin page
  C:\ProgramData\CefGlue        browser profiles (logins) and caches
                                (hidden folder)

Delete these for a completely fresh start.

See LICENSE, LICENSE-BINARY.txt and THIRD-PARTY-NOTICES.txt.
"@ | Set-Content (Join-Path $stage "README.txt") -Encoding UTF8

Write-Host "Compressing..."
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal
$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB)
Write-Host "Done: $zip ($sizeMb MB)"
