## 系统托盘、统一退出与两款工具逻辑修复

本版包含 **Toolkit 1.5.0** 和 **AutoPilot 1.1.0**，发布日期为 **2026-11-17**。

两款工具加入统一的托盘控制，并修复此次审查发现的读取、输入与领取等待问题。Toolkit 保持手动计数与监控互斥，监控期间不自动补点；AutoPilot 保持宝箱优先领取及双槽 2000 点储备。

### 托盘如何使用

- 点击“−”隐藏到系统托盘，当前已开启的操作继续运行。
- 双击图标，或右键“显示主窗口”恢复窗口。
- 右键查看运行状态，选择“停止全部操作”停止工作但保留程序。
- X、Alt+F4 和托盘“退出”均停止操作并真正退出。
- 同一 Windows 会话只运行一款助手；重复启动任一款新版 EXE 会恢复正在运行的工具。要切换工具，请先退出当前工具。

Toolkit 隐藏后保留 Pause 快捷键。托盘图标可能被 Windows 收纳，可展开通知区域，或再次打开新版 EXE 恢复窗口。退出后不会保存或自动恢复工作任务。

### 本次修复

- UIA 读取绑定选定的桌宠进程，点击前重新复核进程、输入窗口、余额、目标及两槽忙碌状态。
- 禁用按钮与倒计时同时出现在可访问性树时保持忙碌，避免错误继续输入。
- 领取后“隐藏/倒计时”反复变化时仍受响应超时限制，避免无限等待。
- 过滤非数值、无穷大及超界的控件坐标；鼠标距离计算避免整数溢出。
- 鼠标按下与抬起合并发送，部分发送失败时补发抬起；停止与点击发送同步，鼠标恢复使用 `finally` 清理。
- Toolkit 的 F24 发送失败后停止并提示；主按钮支持键盘及辅助功能触发。
- 退出入口统一取消后续操作、停止输入并清理计时器和托盘资源；同时检查两款旧版互斥。

### 验证

使用系统 .NET Framework 编译器构建两款程序，编译警告视为错误，并分别运行共享状态机/界面解析、输入发送和桌面生命周期回归。测试使用离线数据和替代输入接口，未登录真实账号、未发送真实键鼠输入、未实际领取奖励。详细结果及界面检查范围见 [v1.2.0 验证记录](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/blob/main/docs/VALIDATION-v1.2.0.md)。

这些验证覆盖本次发现的问题和列明场景，不构成所有 Windows 环境及未来桌宠版本都无故障的保证。桌宠自身的奖励结果仍以背包和账号记录为准。

### 下载与升级

- [工具箱 WuwaTappoToolkit.exe](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.2.0/WuwaTappoToolkit.exe)
- [自动挂机 WuwaTappoAutoPilot.exe](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.2.0/WuwaTappoAutoPilot.exe)
- [两款程序合集 ZIP](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.2.0/WuwaTappo-Companion-Tools-v1.2.0.zip)
- [SHA-256 校验值 CHECKSUMS.txt](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.2.0/CHECKSUMS.txt)

先退出两款旧版助手，再替换 EXE，按需选择一款启动。若提示旧版仍在运行，请从旧窗口退出；仅最小化旧版不会结束其进程。建议在桌宠中开启“始终显示宝箱”，但该选项不会覆盖原桌宠的余额显示门槛。

工具本身不发网络请求，不使用 OCR、抓包或进程注入，也不修改 WuwaTappo.exe；管理员权限用于匹配原桌宠的输入权限。

### English

Toolkit 1.5.0 and AutoPilot 1.1.0 add a shared system-tray lifecycle: minimize to keep working, restore from the tray, stop all operations, or exit. Launching either tool again restores the running assistant. A shared session lock, including legacy checks, prevents competing instances. This release also fixes busy-state interpretation, unstable claim-response deadlines, process-bound reads, fresh pre-click checks, input cleanup and Toolkit input-failure reporting. Regression tests use offline data and substituted input delivery; no real account or reward claim is used.
