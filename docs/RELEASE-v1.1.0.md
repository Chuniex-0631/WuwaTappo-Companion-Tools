## 低点数领取后的隐藏状态，现在两款工具都能正确处理

Wuwa Tappo 会根据点数余额控制槽位显示：低于 1000 时隐藏两槽，1000～1999 时只显示一槽。
因此领取后“宝箱 → 空/隐藏”可能是正常扣点后的界面变化。

本次将新版槽位处理逻辑同步到 Toolkit，并发布两款程序的更新。工具不再强制等待原槽位倒计时，也不再累计开箱数量；操作后根据实际余额和可见槽位继续运行。

### 包含版本

| 程序 | 版本 | 使用方式 |
| --- | --- | --- |
| WuwaTappoToolkit | **1.4.0** | 手动约 50 次/秒 F24、Pause 快捷键；监控时自动领取但不自动补点 |
| WuwaTappoAutoPilot | **1.0.1** | 一键自动协调补点、双槽 2000 点储备和领取 |

### 本次变化

- 两款工具共用槽位识别与领取状态机，正确处理低余额隐藏。
- 原槽位持续无响应时逐槽退避，避免不断重复点击。
- 禁用按钮、读取异常或 UIA 超时时暂停操作，恢复后继续观察。
- 移除领取次数统计和缺少充分证据的“领取成功”提示。
- AutoPilot 保留低余额时自动补点；Toolkit 保留计数与监控互斥，监控中不自动连点。
- 增加离线状态机回放测试，不使用真实账号领取完成测试。

### 下载

本地编译零错误、零警告。两款程序分别编译并运行共享控制器离线测试，均通过 16 组场景、1343 项断言；不以真实账号领取作为测试步骤。

- [桌宠工具箱 WuwaTappoToolkit.exe](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.1.0/WuwaTappoToolkit.exe)
- [自动挂机助手 WuwaTappoAutoPilot.exe](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.1.0/WuwaTappoAutoPilot.exe)
- [两款程序合集 ZIP](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.1.0/WuwaTappo-Companion-Tools-v1.1.0.zip)
- [SHA-256 校验值 CHECKSUMS.txt](https://github.com/Chuniex-0631/WuwaTappo-Companion-Tools/releases/download/v1.1.0/CHECKSUMS.txt)

### 升级

两款 EXE 的 SHA-256：

```text
8E10F3C7B9DBC718062588FF9AA87FDB2B65A0922BC3DBB10E111F691925D2BC  WuwaTappoToolkit.exe
676E3A1CEBD156A25DC220B362A667CF21CA45174FE2BE748E022D6495841207  WuwaTappoAutoPilot.exe
```

退出旧版助手，下载新 EXE 后直接替换。选择一款运行即可；Toolkit 适合手动控制，AutoPilot 适合一键自动运行。
建议在桌宠设置中开启“始终显示宝箱”，但该选项不会覆盖原桌宠的点数显示门槛。

工具本身只读取本机界面并发送输入，不使用 OCR、抓包或进程注入，也不修改 WuwaTappo.exe。
桌宠本体的账号登录与奖励领取仍使用其自身网络流程；实际奖励以背包和账号记录为准。

### English

Toolkit 1.4.0 and AutoPilot 1.0.1 now share chest-state handling. A chest can disappear normally after claiming when the remaining balance falls below the app's display threshold. Both tools reconcile this hidden state instead of waiting indefinitely for the original countdown. Toolkit keeps manual F24 control and never auto-refills during monitoring; AutoPilot retains automatic 2,000-point reserve planning. Chest totals and unsupported reward-success claims have been removed.
