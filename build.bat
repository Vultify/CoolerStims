@echo off
setlocal

set SPT_PATH=D:\SPT Test
set MOD_NAME=CoolerStims
set MOD_DIR=%SPT_PATH%\SPT\user\mods\%MOD_NAME%
set SRC_DIR=%~dp0

echo [APEX] Building...
dotnet build "%SRC_DIR%CoolerStims.csproj" -c Release /p:SPT_PATH="%SPT_PATH%" -o "%SRC_DIR%bin\Release"

if %ERRORLEVEL% neq 0 (
    echo [APEX] Build FAILED.
    pause
    exit /b 1
)

echo [APEX] Installing to %MOD_DIR% ...
if not exist "%MOD_DIR%" mkdir "%MOD_DIR%"

copy /Y "%SRC_DIR%bin\Release\CoolerStims.dll" "%MOD_DIR%\CoolerStims.dll"
copy /Y "%SRC_DIR%package.json"             "%MOD_DIR%\package.json"

echo.
echo [APEX] Done. Mod installed to:
echo   %MOD_DIR%
echo.
pause
