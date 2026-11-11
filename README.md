<div align="center">

# WuwaTappo Companion Tools

### 鸣潮桌宠 Wuwa Tappo 的纯本地 Windows 辅助工具集

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows)](https://www.microsoft.com/windows/)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Build](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/actions/workflows/build.yml/badge.svg)](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/Chuniex-0631/WuwaTappo-Companion-Tools?include_prereleases)](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases)

**F24 计数加速 · 双槽宝箱监控 · 低余额隐藏恢复 · 一键自动挂机**

[下载最新版](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/latest) · [构建指南](docs/BUILD.md) · [问题反馈](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/issues)

</div>

> [!IMPORTANT]
> 本项目是非官方社区工具，与库洛游戏、鸣潮（Wuthering Waves）及 Wuwa Tappo 官方无隶属、授权或合作关系。项目不会分发、修改或替换 WuwaTappo.exe。

## 项目简介

WuwaTappo Companion Tools 是面向 **鸣潮桌宠 / Wuwa Tappo** 的开源 Windows 本地辅助工具。仓库包含两款独立程序：工具箱让你分别控制计数与监控，自动挂机助手则统一安排补点和领取。

两款工具共用槽位识别与领取状态机，通过 Windows UI Automation 读取本地界面，无需 OCR 或截图模板。工具本身不发网络请求；桌宠本体的账号登录与奖励领取仍使用其自身网络流程。

## 最新版本与下载

**合集 v1.1.0：Toolkit 1.4.0 + AutoPilot 1.0.1**

本次更新修复低点数领取后“宝箱 → 空/隐藏”的正常变化被误判为失败的问题，并将相同处理逻辑同步到两款程序。两款工具均移除领取次数统计，按实际余额和可见槽位继续运行。

离线回归覆盖 **16 组场景、1343 项断言**，包括自动补点和工具箱纯监控模式；没有使用真实账号领取作为测试步骤。

[工具箱 EXE](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.1.0/WuwaTappoToolkit.exe) · [自动挂机 EXE](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.1.0/WuwaTappoAutoPilot.exe) · [合集 ZIP](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.1.0/WuwaTappo-Companion-Tools-v1.1.0.zip) · [SHA-256](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.1.0/CHECKSUMS.txt)

## 两款工具

| 程序 | 适合场景 | 主要功能 |
| --- | --- | --- |
| **WuwaTappoToolkit 1.4.0** | 希望分别控制计数与宝箱监控 | 约 50 次/秒 F24、Pause 快捷键、上下槽位监控、自动领取；监控期间不自动补点 |
| **WuwaTappoAutoPilot 1.0.1** | 希望一键自动运行 | 自动补点、双槽 2000 点储备、宝箱优先领取、低余额隐藏后自动恢复补点 |

两款工具具有相同的槽位观察、点击前复核、领取等待和逐槽退避机制。差异在于谁决定补点：工具箱由你手动控制，自动挂机助手由内部计划控制。

## 界面预览

### 桌宠工具箱

<p align="center"><img src="assets/toolkit.png" alt="WuwaTappo Toolkit 鸣潮桌宠计数加速与宝箱监控界面" width="480"></p>

### 桌宠自动挂机助手

<p align="center"><img src="assets/autopilot.png" alt="WuwaTappo AutoPilot 鸣潮桌宠自动挂机界面" width="480"></p>

截图展示界面布局，具体状态文案以当前版本为准。

## 功能亮点

- **纯本地识别**：读取“空/隐藏、倒计时、宝箱、处理中”等可见槽位状态。
- **低余额状态恢复**：领取后槽位被隐藏时，两款工具都会结束当前等待并重新观察，不再强制要求原槽显示倒计时。
- **逐槽退避**：持续无响应的槽位会延迟重试，另一槽可领取时仍有处理机会；禁用中的按钮只等待。
- **安全输入**：领取前停止 F24；检测到用户移动或按下鼠标时自动延迟点击。
- **双槽点数规划**：自动挂机版为两个宝箱保留 2000 点余额，领取后按实际余额重新规划。
- **稳定后台运行**：最小化后继续工作；关闭时释放 F24、计时器和后台线程。
- **Windows DPI 适配**：使用 Per-Monitor V2 处理窗口缩放。
- **无需安装**：下载单文件 EXE 即可运行。

## 快速开始

1. 从 [Releases](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/latest) 下载需要的 EXE。
2. 启动 Wuwa Tappo，并建议在设置中开启“始终显示宝箱”。
3. 启动工具并通过一次 Windows UAC 提示。
4. 工具箱用户按需开启计数或监控；自动挂机用户点击“开启自动挂机”。

工具箱开启监控后会停止计数加速，监控期间不会自行恢复连点。余额不足时，可通过正常键鼠操作积累点数，或停止监控后手动使用计数加速。需要自动协调两者时，选择 AutoPilot。

> Wuwa Tappo 自身以管理员权限运行。Windows 不允许普通权限程序向管理员窗口发送输入，因此本项目也需要管理员权限。每次启动只申请一次，后台运行期间不会重复弹出 UAC。

## 为什么会出现“空/隐藏”

依据 Wuwa Tappo 1.0.2 的界面显示逻辑，正常登录并完成同步后：

| 可用点数 | 可见槽位 |
| --- | --- |
| 不足 1000 | 两槽隐藏 |
| 1000～1999 | 只显示其中一槽 |
| 至少 2000 | 两槽均具备展示条件；倒计时还受“始终显示宝箱”或悬停条件影响 |

因此“空/隐藏”表示没有观察到槽位控件，不能推断底层没有冷却或宝箱。例如领取前有 1500 点，领取扣除 1000 后，两个槽位都可能正常隐藏。

新版会等待本次操作的界面响应，再根据实际余额和可见槽位继续规划。它不会把按钮消失或本地预测倒计时直接称为奖励到账，也不再累计开箱数量。最终结果以桌宠背包及账号侧记录为准。

## 自动挂机如何处理双槽

自动挂机版把桌宠显示数值视为可用点数余额：

- 一个未来宝箱需要保留 **1000 点**。
- 上、下两个槽位都需要后续宝箱时，目标余额为 **2000 点**。
- 余额不足时自动发送 F24；达到目标立即停止。
- 任意宝箱出现时，领取优先级高于补点。
- 每次领取消耗点数后，会重新读取实际余额和两个槽位；储备不足时再次补点。

正常循环为“宝箱 → 操作后重读 → 空/隐藏或倒计时 → 检查余额 → 补点或等待”。
完整规则见 [自动挂机状态机说明](docs/AUTOPILOT.md)；工具箱的手动边界见 [工具箱使用与监控说明](docs/TOOLKIT.md)。

## 隐私与安全

- 工具自身不发网络请求，不包含遥测或自动更新。
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

完整步骤见 [docs/BUILD.md](docs/BUILD.md)。GitHub Actions 构建两款程序并运行离线状态机测试。

## 目录结构

```text
├─ src/
│  ├─ Shared/                # 两款程序共用的槽位读取和领取状态机
│  ├─ WuwaTappoToolkit/      # 手动工具箱源码
│  └─ WuwaTappoAutoPilot/    # 全自动挂机源码
├─ dist/                     # 发布版 Windows 可执行文件
├─ tests/                    # 离线状态机测试
├─ assets/                   # 项目截图与视觉资源
├─ docs/                     # 构建及技术说明
└─ .github/                  # CI 与 Issue 模板
```

## 参与贡献

欢迎提交 Issue 和 Pull Request。开始前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。报告领取错误时，请提供 Windows 缩放比例、上下槽位状态和复现步骤，不要上传账号或个人信息。

## English summary

**WuwaTappo Companion Tools** is an open-source Windows utility suite for the Wuthering Waves desktop companion **Wuwa Tappo**. Toolkit provides manual F24 clicking and chest monitoring; AutoPilot coordinates both automatically. They share local UI Automation reading, hidden-slot handling, guarded mouse input, and per-slot retry backoff. The tools make no network requests; the original app still handles account login and reward claims through its own network flow. No OCR, packet interception, memory injection, or modification of the original app is involved.

Keywords: `Wuwa Tappo`, `Wuthering Waves desktop pet`, `鸣潮桌宠`, `auto clicker`, `chest auto claim`, `Windows UI Automation`.

## 许可证

本项目采用 [MIT License](LICENSE) 开源。

---

<div align="center">如果这个项目对你有帮助，欢迎点一个 Star ⭐</div>
