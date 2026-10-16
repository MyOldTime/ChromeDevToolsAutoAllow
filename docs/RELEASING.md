# 发布流程

## 版本规则

`VERSION` 保存无前缀三段版本，例如 `1.0.0`。Git 标签为 `v1.0.0`。

本仓库从 v1.0.0 起发布；它基于旧名 ChromeConsentGuard v1.2.1。后续按语义化版本维护，不再沿用旧项目版本号。

升级版本时同步修改：

1. `VERSION`
2. `src/ChromeDevToolsAutoAllow/Properties/AssemblyInfo.cs`
3. `src/ChromeDevToolsAutoAllow/app.manifest`
4. `Program.cs` 的用户界面版本文字
5. `CHANGELOG.md` 与 `docs/releases/v<版本>.md`

## 本地发布包

```powershell
.\scripts\build.ps1
.\scripts\test.ps1 -UnitOnly
.\scripts\test.ps1
# 提交干净源码后
.\scripts\release.ps1 -SkipBuild -IncludeSource
```

输出为：

- `outputs/ChromeDevToolsAutoAllow-v1.0.0-windows-x64.zip`
- `outputs/ChromeDevToolsAutoAllow-v1.0.0-source.zip`
- `outputs/ChromeDevToolsAutoAllow-v1.0.0-SHA256SUMS.txt`

二进制包包含应用 EXE、许可证、版本、使用说明及发布说明；**不含个人设置、日志或测试 EXE**。源码包来自干净的 Git 提交，不从整台机器或整个工作目录打包。

脚本不会覆盖已有发布目录；重复打包应指定新的 `-OutputDirectory`。

## GitHub 发布

先确认仓库所属账号/组织与可见性，并在有权限的 GitHub 账号下建立 `ChromeDevToolsAutoAllow` 仓库。添加对应的 `origin` 后推送 `main` 和经过检查的 `v1.0.0` 标签。

标签发布工作流会：

1. 检查标签是否与 `VERSION` 一致。
2. 在 Windows runner 编译并运行非交互测试。
3. 打包 Windows 产物、干净源码和 SHA-256 清单。
4. 使用仓库范围的 `GITHUB_TOKEN` 创建同名 Release 并上传附件。

工作流仅在发布任务授予 `contents: write`；使用固定 SHA 的官方 Actions。不会修改 Chrome、操作真实授权或接收个人访问令牌文件。

**本地打包与创建标签不等于已经上传 GitHub。** 必须检查远程仓库、工作流结果和 Release 页面后才能声称完成远程发布。

## 发布前清单

- [ ] Git 状态干净，源文件不包含个人路径、凭证、浏览器内容和日志。
- [ ] 本机单元与完整模拟测试通过。
- [ ] 版本元数据一致，EXE / 托盘图标正确。
- [ ] ZIP 内没有 `settings.ini`、`logs/`、测试 EXE 或旧项目包。
- [ ] SHA-256 校验值已生成。
- [ ] 发布说明明确真实 Chrome 行为的验证边界。
- [ ] 远程发布结果已实际确认，不能仅凭命令已发出判断成功。
