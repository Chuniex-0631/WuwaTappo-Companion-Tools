# 构建指南

## 系统自带编译器：构建与回归

在安装 .NET Framework 4.8 的 64 位 Windows 10/11 上，从仓库根目录运行：

```powershell
./scripts/build.ps1 -Test
```

脚本使用 Windows 的 `Framework64/v4.0.30319/csc.exe` 和本机 Framework 程序集，不需要另外安装 .NET SDK、Visual Studio 或第三方依赖。它分别构建 Toolkit 与 AutoPilot，将发布 EXE 写入 `dist/`，并将每套测试分别与两款程序源码编译、运行。编译警告视为错误；构建或任一测试失败会返回失败。

只构建发布 EXE、不运行测试时使用：

```powershell
./scripts/build.ps1
```

`-Test` 包含两款共享状态机与可访问性解析、桌面生命周期、输入发送、按钮交互回归，并额外运行 AutoPilot 工作线程回归（预算、租期、发送失败、过期世代和停止边界）。`ButtonInteractionTests` 使用实际窗口按钮与功能按钮类，回放鼠标/F24 事件交错，并检查标准 Click 与键盘触发；它不通过全局 SendInput 制造测试输入。测试读取离线数据、操作测试窗口，并将输入发送替换为测试接口，不会登录账号或发送真实键鼠输入。桌面生命周期测试需要可交互的 Windows 桌面及通知区域。

## Visual Studio / MSBuild

需要 Visual Studio 2022、“.NET 桌面开发”工作负载和 .NET Framework 4.8 Developer Pack / Targeting Pack。打开根目录的 `WuwaTappoCompanionTools.sln`，选择 `Release|Any CPU` 生成；输出分别位于两个项目的 `bin/Release/net48/`。

在 Developer PowerShell for Visual Studio 中运行：

```powershell
msbuild WuwaTappoCompanionTools.sln /restore /p:Configuration=Release
```

原有状态机测试工程仍可单独构建：

```powershell
msbuild tests/AutoPilotControllerTests.csproj /restore /p:Configuration=Release
./tests/bin/Release/net48/AutoPilotControllerTests.exe
```

发布验证请使用 `./scripts/build.ps1 -Test`，以覆盖两款应用和全部测试。GitHub Actions 同时执行解决方案构建与发布脚本，再上传 `dist/` 中的两款 EXE。

## 设计说明

- UI 使用 Windows Forms、自绘控件和 Per-Monitor V2 DPI。
- `src/Shared/` 包含槽位读取、领取状态机、托盘控制器及 `F24SafeButton` 按钮保护，两款应用编译相同源码。
- 状态读取使用 Windows UI Automation；发送 F24 与鼠标输入使用 Windows `SendInput`。
- 两款应用的窗口与功能按钮过滤 F24 KeyDown/KeyUp，避免持续输入取消鼠标点击；保留标准 Click、Space/Enter 与辅助功能触发。
- Toolkit 计数与监控互斥，监控不自动补点；AutoPilot 按实际余额协调双槽 2000 点储备。
- 隐藏到托盘保留窗口句柄与 Toolkit 热键，最小化不改变操作状态。退出入口统一停止操作并释放托盘资源。
- 两款程序共用会话互斥，并检查旧版互斥，防止同时发送输入；重复启动新版会恢复已运行窗口。
- 工具不发网络请求；桌宠本体的登录与奖励领取仍使用其自身网络流程。

## 发布文件与校验

发布 EXE 位于 `dist/`，合集 ZIP、发布附件与根目录 `CHECKSUMS.txt` 应使用同一批构建结果。SHA-256 可用以下方式核对：

```powershell
Get-FileHash ./dist/WuwaTappoToolkit.exe -Algorithm SHA256
Get-FileHash ./dist/WuwaTappoAutoPilot.exe -Algorithm SHA256
```

自行构建的哈希可能因编译环境不同而变化。发布验证范围及已知限制见 [v1.2.1 验证记录](VALIDATION-v1.2.1.md)。

## 生成发布包

```powershell
./scripts/package.ps1
```

先构建并运行全部测试，再将两款 EXE、文档、许可证和校验值打包到 `artifacts/v1.2.1/`，同时更新仓库根目录 `CHECKSUMS.txt`。ZIP 内的校验文件覆盖两款 EXE；外部校验文件还覆盖 ZIP。已验证当前构建时可用 `-NoBuild` 仅重新打包。
