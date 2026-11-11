<div align="center">

# WuwaTappo Companion Tools

### 鸣潮桌宠 Wuwa Tappo 的纯本地 Windows 辅助工具集

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows)](https://www.microsoft.com/windows/)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/Chuniex-0631/WuwaTappo-Companion-Tools?include_prereleases)](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases)

**自动连点 · 双槽宝箱识别 · 自动领取 · 领取验证 · DPI 适配 · 全程本地运行**

[下载最新版](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/latest) · [构建指南](docs/BUILD.md) · [问题反馈](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/issues)

</div>

> [!IMPORTANT]
> 本项目是非官方社区工具，与库洛游戏、鸣潮（Wuthering Waves）及 Wuwa Tappo 官方无隶属、授权或合作关系。项目不会分发、修改或替换 WuwaTappo.exe。

## 项目简介

WuwaTappo Companion Tools 是面向 **鸣潮桌宠 / Wuwa Tappo** 的开源 Windows 本地辅助工具。仓库包含两款独立程序：一个适合手动控制和调试，另一个提供真正的一键自动挂机。

所有识别均通过 Windows UI Automation 读取桌宠本地界面；工具不抓包、不劫持网络、不访问账号数据，也不会修改桌宠文件。

## 两款工具

| 程序 | 适合场景 | 主要功能 |
| --- | --- | --- |
| **WuwaTappoToolkit** | 希望分别控制计数与宝箱监控 | 50 次/秒 F24 计数加速、Pause 快捷键、上下宝箱实时状态、自动领取与结果验证 |
| **WuwaTappoAutoPilot 1.0** | 希望完全无人值守 | 一键挂机、自动补点、双槽 2000 点储备、低余额隐藏恢复、逐槽退避 |

## 界面预览

### 桌宠工具箱

<p align="center"><img src="assets/toolkit.png" alt="WuwaTappo Toolkit 鸣潮桌宠计数加速与宝箱监控界面" width="480"></p>

### 桌宠自动挂机助手

<p align="center"><img src="assets/autopilot.png" alt="WuwaTappo AutoPilot 鸣潮桌宠自动挂机界面" width="480"></p>

## 功能亮点

- **纯本地识别**：直接读取上下宝箱的“空 / 倒计时 / 宝箱”状态。
- **逐槽领取处理**：工具箱按原槽冷却验证；自动挂机可处理领取后槽位隐藏并重新补点，不再统计领取数量。
- **安全输入**：领取前停止 F24；检测到用户移动或按下鼠标时自动延迟点击。
- **双槽点数规划**：自动挂机版为两个宝箱保留 2000 点余额，领取后按实际余额重新规划。
- **稳定后台运行**：最小化后继续工作；关闭时释放 F24、计时器和后台线程。
- **Windows DPI 适配**：支持 100%、125%、150% 及 Per-Monitor V2 动态缩放。
- **无需安装**：下载单文件 EXE 即可运行。

## 快速开始

1. 从 [Releases](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/latest) 下载需要的 EXE。
2. 启动 Wuwa Tappo，并建议在设置中开启“始终显示宝箱”。
3. 启动工具并通过一次 Windows UAC 提示。
4. 工具箱用户可分别开启计数或监控；自动挂机用户只需点击“开启自动挂机”。

> Wuwa Tappo 自身以管理员权限运行。Windows 不允许普通权限程序向管理员窗口发送输入，因此本项目也需要管理员权限。每次启动只申请一次，后台运行期间不会重复弹出 UAC。

## 自动挂机如何处理双槽

自动挂机版把桌宠显示数值视为可用点数余额：

- 一个未来宝箱需要保留 **1000 点**。
- 上、下两个槽位都需要后续宝箱时，目标余额为 **2000 点**。
- 余额不足时自动发送 F24；达到目标立即停止。
- 任意宝箱出现时，领取优先级高于补点。
- 每次领取消耗点数后，会重新读取实际余额和两个槽位；储备不足时再次补点。

低于 1000 点时，原桌宠会隐藏两个槽位；1000～1999 点时只显示其中一个。
因此自动挂机显示的“空/隐藏”代表未观察到控件，不代表底层没有宝箱或冷却。
新版能处理正常的“宝箱 → 扣点 → 空/隐藏 → 补点 → 槽位重新可见”循环，
不再强制等待原槽位显示倒计时。完整规则见 [自动挂机状态机说明](docs/AUTOPILOT.md)。

工具本身不发网络请求；桌宠本体的账号登录与奖励领取仍使用其自身的网络流程。

## 隐私与安全

- 不联网，不包含遥测或自动更新。
- 不读取账号、密码、Cookie 或游戏网络流量。
- 不注入、不修改进程内存，不替换桌宠文件。
- 自动点击前再次确认目标窗口属于 Wuwa Tappo。
- 源码和发布版 SHA-256 均可公开核验，详见 [CHECKSUMS.txt](CHECKSUMS.txt)。

详细说明见 [SECURITY.md](SECURITY.md)。

## 从源码构建

需要 Windows、Visual Studio 2022（.NET 桌面开发）和 .NET Framework 4.8 Targeting Pack：

```powershell
msbuild WuwaTappoCompanionTools.sln /p:Configuration=Release
```

完整步骤见 [docs/BUILD.md](docs/BUILD.md)。GitHub Actions 会在每次提交时构建两款程序。

## 目录结构

```text
├─ src/
│  ├─ WuwaTappoToolkit/      # 手动工具箱源码
│  └─ WuwaTappoAutoPilot/    # 全自动挂机源码
├─ dist/                     # 已验收的 Windows 可执行文件
├─ assets/                   # 项目截图与视觉资源
├─ docs/                     # 构建及技术说明
└─ .github/                  # CI 与 Issue 模板
```

## 参与贡献

欢迎提交 Issue 和 Pull Request。开始前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。报告领取错误时，请提供 Windows 缩放比例、上下槽位状态和复现步骤，不要上传账号或个人信息。

## English summary

**WuwaTappo Companion Tools** is an open-source Windows utility suite for the Wuthering Waves desktop companion **Wuwa Tappo**. It provides local F24 auto-clicking, dual chest-slot monitoring, verified auto-claiming, and a fully automatic idle mode. No packet interception, memory injection, telemetry, or modification of the original app is involved.

Keywords: `Wuwa Tappo`, `Wuthering Waves desktop pet`, `鸣潮桌宠`, `auto clicker`, `chest auto claim`, `Windows UI Automation`.

## 许可证

本项目采用 [MIT License](LICENSE) 开源。

---

<div align="center">如果这个项目对你有帮助，欢迎点一个 Star ⭐</div>
