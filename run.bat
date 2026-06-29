@echo off
chcp 65001 > nul
title Telegram Chat Converter by shteo

:menu
cls
echo ========================================
echo    Telegram HTML to TXT Converter
echo    by shteo
echo ========================================
echo.
echo Выберите действие:
echo.
echo [1] Запустить
echo [2] Собрать проект (Release)
echo [3] Очистить проект
echo [4] Выход
echo.
set /p choice="Ваш выбор (1-4): "

if "%choice%"=="1" goto run_normal
if "%choice%"=="2" goto build
if "%choice%"=="3" goto clean
if "%choice%"=="4" goto exit

echo Неверный выбор!
pause
goto menu

:run_normal
cls
echo Запуск конвертера...
echo.
:: Явно указываем путь к проекту и рабочую папку
cd /d "%~dp0"
dotnet run --project "%~dp0TelegramChatConverter.csproj"
echo.
pause
goto menu

:run_with_folder
cls
echo Введите путь к папке с экспортом (в адресе не должны быть пробелы символы кроме английского языка):
echo (или нажмите Enter, чтобы выбрать через программу)
set /p folder_path="Путь: "
echo.
cd /d "%~dp0"
if "%folder_path%"=="" (
    dotnet run --project "%~dp0TelegramChatConverter.csproj"
) else (
    dotnet run --project "%~dp0TelegramChatConverter.csproj" "%folder_path%"
)
echo.
pause
goto menu

:build
cls
echo Сборка проекта...
cd /d "%~dp0"
dotnet build --configuration Release --project "%~dp0TelegramChatConverter.csproj"
echo.
pause
goto menu

:clean
cls
echo Очистка проекта...
cd /d "%~dp0"
dotnet clean --project "%~dp0TelegramChatConverter.csproj"
echo.
pause
goto menu

:exit
echo До свидания! shteo благодарит Вас
exit