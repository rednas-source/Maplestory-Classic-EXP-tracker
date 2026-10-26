@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0app\ViddyExpOnly.exe" (
    echo Viddy EXP could not find its app folder.
    echo Right-click the downloaded ZIP, choose Extract All, and try again.
    echo Keep this launcher beside the app folder.
    pause
    exit /b 1
)
start "" /D "%~dp0app" "%~dp0app\ViddyExpOnly.exe"
exit /b 0
