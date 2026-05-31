@echo off
setlocal enabledelayedexpansion

cd /d "%~dp0"
set INSTALLER_DIR=%CD%
set ROOT_DIR=%CD%\..

echo ===================================================
echo   ClipboardPro - Professional Build Script
echo ===================================================
echo.

echo [1/4] Cleaning up old artifacts...
if exist "Publish" rd /s /q "Publish"
mkdir Publish
if exist "!ROOT_DIR!\bin" rd /s /q "!ROOT_DIR!\bin"
if exist "!ROOT_DIR!\obj" rd /s /q "!ROOT_DIR!\obj"

echo [2/4] Publishing ClipboardPro (Self-Contained)...
cd /d "!ROOT_DIR!"
dotnet build ClipboardPro.csproj -c Release
dotnet publish ClipboardPro.csproj -c Release -r win-x64 --self-contained true -o "!INSTALLER_DIR!\Publish" -p:PublishSingleFile=false -p:DebugType=none -p:DebugSymbols=false -p:UseSharedCompilation=false /nodeReuse:false

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Build failed.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [2b/4] Running Obfuscar to encrypt and protect published binaries...
cd /d "!INSTALLER_DIR!"
taskkill /f /im dotnet.exe 2>nul
taskkill /f /im MSBuild.exe 2>nul
ping 127.0.0.1 -n 4 >nul
"C:\Users\ENVY X360\.nuget\packages\obfuscar\2.2.38\tools\Obfuscar.Console.exe" obfuscar_publish.xml

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Obfuscation failed.
    pause
    exit /b %ERRORLEVEL%
)

echo Copying protected DLL back to Publish...
copy /y "Publish\Obfuscated\ClipboardPro.dll" "Publish\ClipboardPro.dll" >nul
rd /s /q "Publish\Obfuscated"

echo.
echo [3/4] Locating Inno Setup Compiler...
cd /d "!INSTALLER_DIR!"
set ISCC="C:\Users\ENVY X360\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
if not exist !ISCC! (
    set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)
if not exist !ISCC! (
    set ISCC="iscc.exe"
)

echo [4/4] Compiling Installer...
!ISCC! clipboardpro_installer.iss

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Installer compilation failed.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ===================================================
echo   SUCCESS: Installer created in:
echo   !INSTALLER_DIR!\ClipboardPro-Setup.exe
echo ===================================================
echo.
exit /b 0
