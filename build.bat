@echo off
REM ===== HealthyPet 构建脚本 =====
REM 自动定位 .NET 4.x csc 编译器，优先 64 位，回退 32 位
set CSC=
if exist "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
) else if exist "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)
if "%CSC%"=="" (
    echo [ERROR] 找不到 .NET Framework 4.x 编译器，请确认已安装 .NET Framework 4.7.2 或更高版本。
    pause
    exit /b 1
)

set SRC=d:\biancheng\healthstander\HealthyPet
set OUT=%SRC%\bin\Release

if not exist "%OUT%" mkdir "%OUT%"

echo 正在编译 HealthyPet...
"%CSC%" /target:winexe /win32icon:"%SRC%\Data\tray.ico" /out:"%OUT%\HealthyPet.exe" /nologo /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll /r:System.Windows.Forms.dll "%SRC%\Program.cs" "%SRC%\MainApp.cs" "%SRC%\PetForm.cs" "%SRC%\AlertForm.cs" "%SRC%\SettingsForm.cs" "%SRC%\AppConfig.cs" "%SRC%\QuoteManager.cs" "%SRC%\ReminderScheduler.cs" "%SRC%\ResourceGenerator.cs" "%SRC%\ImageHelper.cs" > "%SRC%\build_result.txt" 2>&1
echo Exit code: %ERRORLEVEL% >> "%SRC%\build_result.txt"
type "%SRC%\build_result.txt"

if %ERRORLEVEL% equ 0 (
    echo.
    echo [OK] 编译成功：%OUT%\HealthyPet.exe
    REM 复制随附的《剑来》名言文件到输出目录，供桌宠随机展示
    if exist "%SRC%\名言名句.md" copy /Y "%SRC%\名言名句.md" "%OUT%\名言名句.md" >nul
) else (
    echo.
    echo [FAIL] 编译失败，请查看 build_result.txt
    pause
    exit /b 1
)
