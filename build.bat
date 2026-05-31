@echo off
title ClipboardPro - Build Script
color 0A

echo.
echo  ==========================================
echo   ClipboardPro - Building Release EXE
echo  ==========================================
echo.

set PROJECT_DIR=%~dp0
set DOTNET="C:\Program Files\dotnet\dotnet.exe"

echo [1/3] Restoring NuGet packages...
%DOTNET% restore "%PROJECT_DIR%ClipboardPro.csproj"
if errorlevel 1 ( echo ERROR: Restore failed! & pause & exit /b 1 )

echo.
echo [2/3] Building Release (Self-Contained)...
%DOTNET% publish "%PROJECT_DIR%ClipboardPro.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    -o "%PROJECT_DIR%dist"

if errorlevel 1 ( echo ERROR: Build failed! & pause & exit /b 1 )

echo.
echo [3/3] Done!
echo.
echo  ==========================================
echo   Output: %PROJECT_DIR%dist\ClipboardPro.exe
echo  ==========================================
echo.
explorer "%PROJECT_DIR%dist"
pause
