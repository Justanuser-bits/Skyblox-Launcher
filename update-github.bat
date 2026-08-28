@echo off
color 0B
title Skyblox GitHub Updater

echo =======================================
echo     Skyblox-Launcher Updater
echo =======================================
echo.

cd /d "%~dp0"
if %errorlevel% neq 0 (
    echo [ERROR] Could not find the Skyblox-Launcher folder!
    pause
    exit /b
)

echo [1/4] Adding all new and modified files...
git add -A
if %errorlevel% neq 0 (
    echo [ERROR] Failed to add files. Is Git installed?
    pause
    exit /b
)

set /p commitMsg="Enter a short description of your changes (e.g., 'Fixed download errors'): "

:: If the user pressed enter without typing anything, give it a default message
if "%commitMsg%"=="" set commitMsg=Update Launcher

echo.
echo [2/4] Committing changes...
git commit -m "%commitMsg%"

echo.
echo [3/4] Pulling latest changes from GitHub to prevent conflicts...
git pull origin main

echo.
echo [4/4] Pushing to GitHub...
git push origin main

echo.
echo =======================================
if %errorlevel% equ 0 (
    echo [SUCCESS] Your repository is now fully up to date on GitHub!
) else (
    echo [ERROR] Failed to push to GitHub. Make sure you have internet and your credentials are correct.
)
echo =======================================
echo.
pause
