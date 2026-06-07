@echo off
title ClipboardPro - Build Android APK
color 0B

echo.
echo  =====================================================
echo    ClipboardPro - Building Android APK
echo  =====================================================
echo.

set PROJECT_DIR=%~dp0

REM Check if ANDROID_HOME is set
if not defined ANDROID_HOME (
    echo [ERROR] ANDROID_HOME environment variable is not set!
    echo.
    echo  Please set ANDROID_HOME to your Android SDK path.
    echo  Example: set ANDROID_HOME=C:\Users\YourName\AppData\Local\Android\Sdk
    echo.
    echo  If Android Studio is not installed, download it from:
    echo  https://developer.android.com/studio
    echo.
    pause
    exit /b 1
)

echo [1/3] Cleaning previous build...
call "%PROJECT_DIR%gradlew.bat" clean
if errorlevel 1 ( echo ERROR: Clean failed! & pause & exit /b 1 )

echo.
echo [2/3] Building Release APK...
call "%PROJECT_DIR%gradlew.bat" assembleRelease
if errorlevel 1 ( echo ERROR: Build failed! & pause & exit /b 1 )

echo.
echo [3/3] Done!
echo.
echo  =====================================================
echo   APK Location:
echo   %PROJECT_DIR%app\build\outputs\apk\release\
echo  =====================================================
echo.
explorer "%PROJECT_DIR%app\build\outputs\apk\release"
pause
