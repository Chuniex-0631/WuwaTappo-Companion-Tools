# Security Policy

## Supported versions

当前仓库只维护最新发布版本。

## 权限说明

程序清单请求管理员权限，是因为 Wuwa Tappo 自身运行在管理员完整性级别。Windows 的 UIPI 安全机制会阻止低权限进程向高权限窗口发送输入。

程序不会因此获得或执行以下行为：

- 不访问游戏账号或凭据；
- 不拦截网络流量；
- 不注入或修改其他进程；
- 不写入 `C:\Program Files\WuwaTappo\WuwaTappo.exe`；
- 不安装服务、驱动或计划任务。

## 报告安全问题

请不要在公开 Issue 中披露可被滥用的漏洞细节。可通过 GitHub 个人资料中公开的联系方式联系维护者，并附上复现步骤、影响范围和建议修复方式。
