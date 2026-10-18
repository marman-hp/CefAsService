<#
.SYNOPSIS
  Downloads the OpenH264 video codec from Cisco onto this machine (or removes it again).

.DESCRIPTION
  The same thing as Admin > Encoder Settings > openh264 > "Download from Cisco", for machines run
  without a browser. OpenH264 is never shipped with CefAsService: Cisco's free H.264 patent
  coverage applies only when the end user downloads Cisco's binary themselves. The file is checked
  against a fixed SHA-256 before it is kept.

    OpenH264 Video Codec provided by Cisco Systems, Inc.

  Works from the release folder (next to Admin\ and Worker\) and from a source checkout after
  `dotnet build CefAsService.sln -c <Configuration> -p:Platform=x64`. Restart the broker afterwards.

.PARAMETER Remove
  Remove the downloaded codec again (stop the broker first).

.PARAMETER Configuration
  Source checkout only: which build to install into (default Release).

.EXAMPLE
  .\get-openh264.ps1
.EXAMPLE
  .\get-openh264.ps1 -Remove
#>
param(
    [switch]$Remove,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$command = if ($Remove) { "--remove-encoder" } else { "--install-encoder" }

$admin = Join-Path $root "Admin\Xilium.CefGlue.Broker.Admin.exe"
if (-not (Test-Path $admin)) {
    # Source checkout: point Admin at the built worker explicitly.
    $admin = Join-Path $root "CefGlue.Broker.Admin\bin\x64\$Configuration\net10.0\Xilium.CefGlue.Broker.Admin.exe"
    $worker = Join-Path $root "CefGlue.Headless.Service\bin\x64\$Configuration\net10.0\Xilium.CefGlue.Headless.Service.exe"
    if (-not (Test-Path $admin) -or -not (Test-Path $worker)) {
        throw "No Admin/worker build found - build first: dotnet build CefAsService.sln -c $Configuration -p:Platform=x64"
    }
    $env:CEFGLUE_WORKER_EXE_PATH = $worker
}

& $admin $command openh264
exit $LASTEXITCODE
