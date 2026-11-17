## 修复补点期间最小化、停止等按钮点击失效

本版包含 **Toolkit 1.5.1** 和 **AutoPilot 1.1.1**，发布日期为 **2026-11-17**。

AutoPilot 补点期间点击“−”没有反应是 v1.2.0 的回归，并非限制后台运行的设计。本版修复这一交互，并将同一保护用于两款工具的最小化、关闭与功能按钮。

### 问题与修复

v1.2.0 将按钮切换为标准 Click，以支持正常鼠标、键盘和辅助功能操作。但补点或计数时持续产生的 F24 释放事件可能穿插在鼠标按下和释放之间，清除 WinForms 按钮的按下标记，使这次点击丢失。

本版新增共用 `F24SafeButton`，过滤按钮收到的 F24 KeyDown/KeyUp；窗口按钮和功能按钮统一采用该实现。保留标准 Click、Space/Enter 键盘操作及辅助功能触发。

- 补点或计数期间可点击“−”最小化到托盘，已经开启的操作继续运行。
- 从托盘可恢复窗口、停止全部操作或退出。
- 停止按钮和关闭按钮也受到相同的 F24 事件保护。
- Toolkit 的 Pause 热键、计数与监控互斥，以及 AutoPilot 的双槽 2000 点储备规则保持原有行为。

### 回归与验证

v1.2.0 的生命周期测试直接调用托盘控制器，没有覆盖实际按钮上“鼠标按下 → F24 释放 → 鼠标释放”的事件交错，因此漏检了这个问题。

本版新增 `ButtonInteractionTests`，使用实际按钮类回放这一交错，并检查标准点击及键盘触发；原有状态机、界面解析、生命周期、输入发送和工作线程测试继续运行。测试不使用真实桌宠账号，不进行真实补点或领取。最终构建结果、断言数量和覆盖边界见 [v1.2.1 验证记录](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/blob/main/docs/VALIDATION-v1.2.1.md)。

### 下载与升级

- [工具箱 WuwaTappoToolkit.exe](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.2.1/WuwaTappoToolkit.exe)
- [自动挂机 WuwaTappoAutoPilot.exe](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.2.1/WuwaTappoAutoPilot.exe)
- [两款程序合集 ZIP](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.2.1/WuwaTappo-Companion-Tools-v1.2.1.zip)
- [SHA-256 校验值 CHECKSUMS.txt](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.2.1/CHECKSUMS.txt)

先退出旧版助手，再替换 EXE，选择一款启动。若旧版补点时无法点击关闭，可按 Alt+F4 退出；仅最小化旧版不会结束其进程。新版也不会在启动后自动恢复上次任务，需手动开启。

工具自身不发网络请求，不修改 WuwaTappo.exe。实际奖励结果以桌宠背包与账号记录为准。

### English

Toolkit 1.5.1 and AutoPilot 1.1.1 fix mouse clicks being cancelled by repeated F24 key-up events during manual counting or automatic filling. Both apps now use a shared F24-safe button for window and operation controls, preserving standard Click, keyboard and accessibility activation. Minimizing keeps active operations running. New regression tests replay mouse/F24 event interleaving on the actual button classes; v1.2.0 lifecycle tests did not cover this path. Tests do not access a real pet account or claim rewards.
