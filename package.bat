@echo off
chcp 65001 >nul
echo ===================================
echo   健康守护桌宠 - 打包分发
echo ===================================
echo.

set SRC=d:\biancheng\healthstander\HealthyPet\bin\Release
set OUT=d:\biancheng\healthstander\HealthyPet\HealthyPet_分发包

if exist "%OUT%" rd /s /q "%OUT%"
mkdir "%OUT%"

:: 复制可执行文件
copy "%SRC%\HealthyPet.exe" "%OUT%\" >nul

:: 复制 Data 文件夹（含 Pop-up image.png / tray.ico）
if exist "%SRC%\Data" (
    xcopy "%SRC%\Data" "%OUT%\Data\" /E /I /Q >nul
    del /q "%OUT%\Data\config.json" 2>nul
)

:: 使用说明
(
echo 健康守护桌宠 - 使用说明
echo =======================
echo.
echo 【运行要求】
echo - Windows 7/8/10/11（自带 .NET Framework 4.0+，无需安装）
echo.
echo 【使用方法】
echo 1. 将整个文件夹解压到任意位置
echo 2. 双击 HealthyPet.exe 启动
echo 3. 右下角托盘图标右键 → 打开设置
echo.
echo 【主要功能】
echo - 弹窗提醒：休息倒计时 + 圆环进度 + 跳过按钮
echo - 番茄钟：专注 20 分钟 / 休息 2 分钟（可调）
echo - 桌面宠物：多只猫猫随机走动、显示名言
echo - 右键托盘：隐藏宠物 / 冻结移动 / 暂停提醒
echo.
echo 【托盘右键菜单】
echo - 打开设置
echo - 隐藏/显示宠物
echo - 暂停/恢复提醒
echo - 冻结/解冻宠物
echo - 退出程序
echo.
echo 【提示】
echo - 如果已运行，再次双击会提示"已在运行中"，查看托盘即可
echo - 完全离线可用，无需网络
echo - Data 文件夹包含默认宠物图片和弹窗背景，可自行替换
echo.
) > "%OUT%\使用说明.txt"

echo.
echo 打包完成！
echo 位置: %OUT%
echo.
echo 文件列表:
dir "%OUT%" /b /s
echo.
echo 把该文件夹压缩成 ZIP 发给别人即可！
echo.
pause
