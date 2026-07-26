@echo off
set ISCC="C:\Users\ENVY X360\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
echo Compiling standalone installer...
%ISCC% "clipboardpro_standalone_installer .iss"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Compilation failed.
    pause
    exit /b 1
)
echo.
echo SUCCESS: ClipboardPro.exe created in Installer folder.
