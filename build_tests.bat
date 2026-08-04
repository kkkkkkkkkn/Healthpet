@echo off
REM ===== HealthyPet 单元测试构建与运行脚本 =====
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo [ERROR] 找不到 .NET Framework 4.x 编译器
    pause
    exit /b 1
)

set SRC=d:\biancheng\healthstander\HealthyPet
set OUT=%SRC%\bin\Tests
if not exist "%OUT%" mkdir "%OUT%"

echo 正在编译单元测试...
"%CSC%" /target:exe /out:"%OUT%\HealthyPet.Tests.exe" /nologo /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll /r:System.Windows.Forms.dll "%SRC%\AppConfig.cs" "%SRC%\PetForm.cs" "%SRC%\ReminderScheduler.cs" "%SRC%\ImageHelper.cs" "%SRC%\QuoteManager.cs" "%SRC%\Tests\TestFramework.cs" "%SRC%\Tests\Program.cs" "%SRC%\Tests\AppConfigTests.cs" "%SRC%\Tests\ReminderSchedulerTests.cs" "%SRC%\Tests\ImageHelperTests.cs" > "%SRC%\build_tests_result.txt" 2>&1
echo Exit code: %ERRORLEVEL% >> "%SRC%\build_tests_result.txt"
type "%SRC%\build_tests_result.txt"

if %ERRORLEVEL% equ 0 (
    echo.
    echo 运行测试...
    echo.
    "%OUT%\HealthyPet.Tests.exe"
) else (
    echo.
    echo [FAIL] 测试编译失败
    pause
    exit /b 1
)
