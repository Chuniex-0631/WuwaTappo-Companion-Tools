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
- 宝箱领取：验证目标窗口后发送鼠标输入，随后按原槽位倒计时确认结果。
- 网络：无网络依赖。

`dist/` 中的 EXE 是人工验收版本；自行构建的二进制哈希会因编译环境而不同。
