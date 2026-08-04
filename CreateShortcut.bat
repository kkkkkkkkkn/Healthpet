@echo off
chcp 65001 >nul
title 健康守护桌宠 - 创建桌面快捷方式

echo ============================================
echo    健康守护桌宠 - 创建桌面快捷方式
echo ============================================
echo.

set "EXE_PATH=%~dp0HealthyPet.exe"
set "DESKTOP=%USERPROFILE%\Desktop"
set "SHORTCUT=%DESKTOP%\健康守护桌宠.lnk"

if not exist "%EXE_PATH%" (
    echo [错误] 找不到 HealthyPet.exe，请将此批处理放在程序目录下运行。
    echo 当前目录: %~dp0
    pause
    exit /b 1
)

echo 正在创建桌面快捷方式...
echo 目标: %EXE_PATH%
echo 快捷方式: %SHORTCUT%
echo.

REM 使用 PowerShell 创建快捷方式
powershell -Command ^
    "$ws = New-Object -ComObject WScript.Shell; ^
     $s = $ws.CreateShortcut('%SHORTCUT%'); ^
     $s.TargetPath = '%EXE_PATH%'; ^
     $s.WorkingDirectory = '%~dp0'; ^
     $s.Description = '健康守护桌宠 - 定时提醒、桌面宠物、番茄钟'; ^
     $s.IconLocation = '%EXE_PATH%'; ^
     $s.Save()"

if %ERRORLEVEL% NEQ 0 (
    echo [错误] 创建快捷方式失败，请尝试以管理员身份运行。
    pause
    exit /b 1
)

echo [成功] 桌面快捷方式已创建！
echo 双击桌面上的"健康守护桌宠"即可启动程序。
echo.
pause
