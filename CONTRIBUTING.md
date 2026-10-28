# Contributing

感谢参与 WuwaTappo Companion Tools。

## 提交问题

- 先确认使用的是最新 Release。
- 描述 Windows 版本、显示缩放比例和 Wuwa Tappo 版本。
- 说明上、下宝箱当时显示为空、倒计时还是宝箱。
- 请勿上传账号、路径中的个人姓名或其他敏感信息。

## Pull Request

1. 从 `main` 创建功能分支。
2. 保持两款程序相互独立，不改变已公开功能语义。
3. 使用 `Release|Any CPU` 构建整个解决方案。
4. 对 100%、125%、150% DPI 至少进行一次界面检查。
5. 在 PR 中说明用户可见变化和验证步骤。

代码应保持兼容 .NET Framework 4.8，避免增加不必要的第三方依赖或网络组件。
