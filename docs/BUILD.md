# 构建指南

## 环境要求

- Windows 10 或 Windows 11
- Visual Studio 2022
- 工作负载：`.NET 桌面开发`
- .NET Framework 4.8 Developer Pack / Targeting Pack

## Visual Studio

1. 打开根目录的 `WuwaTappoCompanionTools.sln`。
2. 选择 `Release` 和 `Any CPU`。
3. 使用“生成解决方案”。
4. 输出分别位于两个项目的 `bin/Release/net48/`。

## 命令行

在 Developer PowerShell for Visual Studio 中运行：

```powershell
msbuild WuwaTappoCompanionTools.sln /restore /p:Configuration=Release
```

## 设计说明

- UI：Windows Forms，自绘圆角控件，Per-Monitor V2 DPI。
- 状态读取：Windows UI Automation。
- 计数输入：Windows `SendInput`，发送 F24 按下与抬起。
- 宝箱领取：两款程序编译 `src/Shared/` 同一份识别和控制器代码，点击后观察原槽变化并重新规划。
- 低余额导致槽位隐藏时，工具箱继续监控，自动挂机按余额重新补点；两款都不统计领取数量。
- 网络：工具不发网络请求，桌宠本体的登录和奖励领取仍走自身网络流程。

## 离线状态机测试

```powershell
msbuild tests/AutoPilotControllerTests.csproj /restore /p:Configuration=Release
./tests/bin/Release/net48/AutoPilotControllerTests.exe
```

测试包含自动补点与纯监控两种模式，不发送真实键鼠输入。

`dist/` 中的 EXE 是发布二进制，经过编译与离线回归验证；自行构建的哈希会因编译环境而不同。
