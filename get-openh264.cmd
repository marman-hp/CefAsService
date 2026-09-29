@echo off
rem Downloads the OpenH264 video codec from Cisco onto this machine, or removes it again with
rem "get-openh264.cmd remove". Add "nopause" when calling it from another script. Same as Admin > Encoder Settings > openh264 > "Download from Cisco".
rem OpenH264 is never shipped with CefAsService: Cisco's free H.264 patent coverage applies only when
rem the end user downloads Cisco's binary themselves. Needs no PowerShell (works on stripped-down
rem Windows installs too). Release folder only - in a source checkout use get-openh264.ps1.
rem
rem   OpenH264 Video Codec provided by Cisco Systems, Inc.

setlocal
set "ADMIN=%~dp0Admin\Xilium.CefGlue.Broker.Admin.exe"
if not exist "%ADMIN%" (
    echo Admin\Xilium.CefGlue.Broker.Admin.exe not found next to this file.
    echo In a source checkout, use get-openh264.ps1 instead.
    set "RESULT=2"
    goto done
)

set "COMMAND=--install-encoder"
set "NOPAUSE="
for %%A in (%*) do (
    if /i "%%~A"=="remove" set "COMMAND=--remove-encoder"
    if /i "%%~A"=="-remove" set "COMMAND=--remove-encoder"
    if /i "%%~A"=="nopause" set "NOPAUSE=1"
)

"%ADMIN%" %COMMAND% openh264
set "RESULT=%ERRORLEVEL%"
if "%RESULT%"=="0" echo Restart the broker (or press Start in Admin) to use the change.

:done
rem Keep the window open when started by double-click, so the result can be read.
if not defined NOPAUSE echo %CMDCMDLINE% | find /i "%~nx0" >nul && pause
exit /b %RESULT%
