@echo off
setlocal

set PATH=%PATH%;C:\Program Files (x86)\WiX Toolset v3.7\bin\;

set INSTALLERDIR=%~dp0\Installer
set TARGETDIR=%~dp0\output\v472\AnyCPU\Release\bin

set SolutionDir=%~dp0
set Configuration=Release

set OutputFile="%TARGETDIR%\coapp.tools.powershell.msi"
erase %OutputFile%

echo Creating MSI

cd %TARGETDIR%

candle %INSTALLERDIR%\Product.wxs  || goto fin
light "%TARGETDIR%\product.wixobj"  -sice:ICE80 -out %OutputFile%

echo signing installer
REM "C:\Program Files (x86)\Windows Kits\8.0\bin\x86\signtool.exe" sign /a /t http://timestamp.verisign.com/scripts/timstamp.dll %OutputFile% || goto fin

powershell set-executionpolicy unrestricted

REM powershell  " ipmo 'C:\Program Files (x86)\Outercurve Foundation\Modules\CoApp\CoApp.psd1' ; copy-itemex -force %TargetDir%\CoApp.Tools.Powershell.msi coapp:files\Development.CoApp.Tools.Powershell.msi"


pskill powershell
msiexec /i %OutputFile% || goto fin


: uh, ignore this stuff...
:mkdir c:\root\builds
:copy %OutputFile% c:\root\builds\Development.CoApp.Tools.Powershell.msi || goto fin
:powershell ipmo coapp ; copy-itemex -force %OutputFile% coapp:files\Development.CoApp.Tools.Powershell.msi"

:FIN