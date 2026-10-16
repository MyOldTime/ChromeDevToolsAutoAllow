<p align="center"><img src="assets/app-1024.png" width="112" alt="ChromeDevToolsAutoAllow icon"></p>

# ChromeDevToolsAutoAllow

**Windows 上的 Chrome 远程调试授权辅助工具。事件驱动、托盘常驻、默认仅检测。**

[English](docs/README.en.md) · [使用说明](docs/USER_GUIDE.md) · [构建与测试](docs/DEVELOPMENT.md) · [安全边界](SECURITY.md) · [更新记录](CHANGELOG.md)

> **安全提示**：启用自动允许会批准匹配的 Chrome 远程调试请求，可能授予浏览器会话和页面的访问能力。弹窗不提供可验证的请求者身份，本工具**不能保证请求来自 Chrome DevTools MCP**。请仅在你控制、信任的开发环境中使用。
>
> 默认不自动点击；没有开机自启、服务、提权或 Chrome 配置修改。此项目不隶属于 Google 或 Chrome DevTools MCP。

## 功能

- `SetWinEventHook` 事件监听，专用 MTA 线程读取 Windows UI Automation；没有常驻定时窗口扫描。
- 校验目标 Chrome 路径、原生窗口类、中英文标题、提示文本及按钮；不遍历网页 Document。
- 仅检测 / 临时 10 分钟 / 用户确认后保存的永久模式。
- 同一按钮按进程与 UIA RuntimeId 去重，不做重复输入或鼠标、键盘兜底。
- 一次调用后的有限只读检查；结果未确认时停止自动允许，提示手动处理。
- 托盘状态、日志、设置持久化、原创多尺寸 EXE / 托盘图标。

![图标预览](assets/icon-preview.png)

## 下载与运行

从本仓库的 **Releases** 页面下载 `ChromeDevToolsAutoAllow-v1.0.0-windows-x64.zip`，解压到可写目录，运行 `ChromeDevToolsAutoAllow.exe`。

- 目标环境：Windows x64、.NET Framework 4.8、桌面版 Google Chrome。
- 发布包不含个人 `settings.ini`，首次启动保持仅检测。
- 日志与设置写入 EXE 所在目录；不能写入时程序会报错或保持关闭状态。
- EXE 图标已经内嵌，运行不依赖外部图标或 NuGet 包。
- 如果旧版 ChromeConsentGuard 正在运行，请先从旧版托盘退出。两者共用单实例锁，避免重复操作。

## 从源码构建

在 Windows PowerShell 中，从仓库根目录运行：

```powershell
.\scripts\build.ps1
.\scripts\test.ps1 -UnitOnly
.\scripts\test.ps1
```

产物位于 `outputs/build/`。脚本使用系统 .NET Framework 的 C# 编译器，不需要 .NET SDK；IDE 构建可打开 `ChromeDevToolsAutoAllow.sln`，需要 .NET Framework 4.8 开发工具。

完整测试会短暂创建本项目的模拟窗体，**不会点击真实 Chrome 授权**。CI 只运行不需要交互桌面的单元检查。

## 验证状态与限制

- 首发基于旧项目 ChromeConsentGuard v1.2.1，重新命名为独立项目 **v1.0.0**，不表示功能回退。
- 项目拆分后，本机单元检查 **72 项**、完整模拟集成检查 **128 项**通过；后者已包含前者。
- 前身版本的一次真实联调记录到调用返回、弹窗消失及 MCP 连接成功，但这**不是长期稳定性、其他 Chrome 版本或其他系统的保证**。
- `INVOKE_RETURNED` 不代表授权成功；`DIALOG_DISMISSED` 不代表 MCP 已连接。
- UIA 提供方可能阻塞，有限等待不是外部调用的硬超时。原生 UI、语言或 Chrome 版本变化可能导致识别失败。
- 永久模式仍受单次运行 256 次尝试的保护上限约束；退出不撤销 Chrome 已有的调试连接。

## 仓库结构

```text
src/ChromeDevToolsAutoAllow/       应用源码与项目文件
tests/ChromeDevToolsAutoAllow.Tests/ 独立测试程序与模拟窗体
assets/                          图标、SVG、PNG 预览
tools/                           原创图标生成器
scripts/                         构建、测试、图标与打包脚本
docs/                            使用说明、开发说明、发布说明
.github/                         CI、标签发布与问题模板
outputs/                         本地产物（忽略，不提交）
work/                            本地临时文件与历史归档（忽略，不提交）
```

## 发布

`scripts/release.ps1 -IncludeSource` 生成 Windows ZIP、干净 Git 提交的源码 ZIP 和 SHA-256 清单。推送与 `VERSION` 一致的 `v*` 标签会触发 GitHub 发布工作流；仓库建立与首次推送需要有权限的 GitHub 账号。

详见 [发布流程](docs/RELEASING.md)。许可证为 [MIT](LICENSE)；图标为本项目原创资源。
