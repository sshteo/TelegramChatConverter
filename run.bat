@echo off
chcp 65001 > nul 2>nul
title Telegram Chat Converter by shteo

:menu
cls
echo ========================================
echo    Telegram HTML to TXT Converter
echo    by shteo
echo ========================================
echo.
echo Select action:
echo.
echo [1] Run (normal mode)
echo [2] Run with folder path
echo [3] Build Release
echo [4] Clean project
echo [5] Exit
echo.
set /p choice="Your choice (1-5): "

if "%choice%"=="1" goto run_normal
if "%choice%"=="2" goto run_with_folder
if "%choice%"=="3" goto build
if "%choice%"=="4" goto clean
if "%choice%"=="5" goto exit

echo Invalid choice!
pause
goto menu

:run_normal
cls
echo ========================================
echo    Running converter (normal mode)
echo ========================================
echo.
cd /d "%~dp0"
dotnet run --project "%~dp0TelegramChatConverter.csproj"
echo.
echo ========================================
echo Program finished.
pause
goto menu

:run_with_folder
cls
echo ========================================
echo    Running converter with folder path
echo ========================================
echo.
echo Enter path to export folder:
echo (example: D:\Downloads\Telegram Desktop\ChatExport)
echo.
set /p folder_path="Path: "
echo.
cd /d "%~dp0"
if "%folder_path%"=="" (
    echo No folder specified. Running in normal mode...
    dotnet run --project "%~dp0TelegramChatConverter.csproj"
) else (
    echo Using folder: %folder_path%
    dotnet run --project "%~dp0TelegramChatConverter.csproj" "%folder_path%"
)
echo.
echo ========================================
echo Program finished.
pause
goto menu

:build
cls
echo ========================================
echo    Building project (Release)
echo ========================================
echo.
cd /d "%~dp0"
dotnet build --configuration Release --project "%~dp0TelegramChatConverter.csproj"
echo.
echo ========================================
echo Build complete!
echo EXE file location: bin\Release\net8.0\
pause
goto menu

:clean
cls
echo ========================================
echo    Cleaning project
echo ========================================
echo.
cd /d "%~dp0"
dotnet clean --project "%~dp0TelegramChatConverter.csproj"
echo.
echo ========================================
echo Clean complete!
pause
goto menu

:exit
cls
echo ========================================
echo    Goodbye!
echo    Thank you for using!
echo ========================================
timeout /t 2 >nul
exit