# HealthyPet - 健康守护桌宠

一个 Windows 桌面小工具，养只宠物在桌面上，到点提醒你起来活动。

![效果截图](./Data/healthpet.png)

## 它能干什么

- **定时提醒你休息** — 设个间隔，时间到了弹窗提醒，带倒计时
- **番茄钟** — 专注 25 分钟 / 休息 5 分钟的循环模式
- **桌面宠物** — 宠物在屏幕上到处走，可以拖来拖去
- **励志名言** — 定时冒出一句名言气泡
- **完全离线** — 不联网、不写注册表、不装任何依赖

## 效果一览

### 休息弹窗（可自定义）

半透明背景 + 圆形倒计时环 + 进度条，底部有跳过按钮：

![休息弹窗](./Data/restpic.png)

### 桌面宠物（可自定义图片【静态】和拖拽时候的图片【静态】）


宠物在桌面上自由移动，支持多只同屏，如果导入自定义图片记得用工具扣掉背景，最好是png图片。

![桌面宠物](Data/healthpet.png)

## 怎么用

### 直接运行

1. 解压 `HealthyPet_分发包.zip` 到任意文件夹
2. 双击 `HealthyPet.exe`
3. 程序自动缩到右下角托盘

> 右键托盘图标可以打开设置、暂停/恢复提醒、退出等。

### 从源码编译

需要 .NET Framework 4.7.2（Win10 自带）：

```bash
build.bat           # 编译主程序
build_tests.bat     # 跑测试（43 个）
```

然后运行 `bin\Release\HealthyPet.exe`。


## 设置说明

| 设置项 | 说明 |
|--------|------|
| 提醒方式 | 弹窗（半透明背景+倒计时）或 宠物动作（跳跃动画） |
| 番茄钟 | 开启后按 专注/休息 循环，时长可调 |
| 固定间隔 | 单一间隔定时提醒（分钟） |
| 休息时长 | 弹窗停留多久（秒，默认 30） |
| 宠物数量 | 1~10 只同时显示 |
| 宠物尺寸 | 像素范围，可调大小 |
| 开机自启 | 可选 |

## 文件结构

```
HealthyPet/
├── Data/                    # 图片资源
├── Tests/                   # 单元测试
│   ├── TestFramework.cs
│   ├── AppConfigTests.cs
│   ├── ReminderSchedulerTests.cs
│   └── ImageHelperTests.cs
├── AlertForm.cs             # 休息弹窗
├── AppConfig.cs             # 配置读写
├── ImageHelper.cs           # 图片裁剪工具
├── MainApp.cs               # 主控制器
├── PetForm.cs               # 桌宠窗体
├── Program.cs               # 入口
├── QuoteManager.cs          # 名言库
├── ReminderScheduler.cs     # 提醒调度器
├── ResourceGenerator.cs     # 内置图片生成
├── SettingsForm.cs          # 设置窗口
├── build.bat                # 编译脚本
├── build_tests.bat          # 测试脚本
└── package.bat              # 打包脚本
```


## 开发相关

```bash
# 编译
build.bat

# 测试
build_tests.bat

# 打包分发包
package.bat

# 创建桌面快捷方式
CreateShortcut.bat
```

## 感谢赞助

wxh

## License

MIT

