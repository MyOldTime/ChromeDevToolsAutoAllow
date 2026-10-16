# 构建与测试

## 环境

- Windows x64；.NET Framework 4.8 运行环境。
- Windows PowerShell 5.1 或 PowerShell 7。
- 命令行构建使用 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`。
- 解决方案面向 .NET Framework 4.8；使用 IDE 时安装相应开发工具和引用程序集。
- 不需要 NuGet 或现代 .NET SDK。代码保持 C# 5 兼容。

## 应用与测试隔离

`src/ChromeDevToolsAutoAllow` 只包含生产代码。模拟窗体、状态探针和测试入口位于独立的 `ChromeDevToolsAutoAllow.Tests.exe` 中；发布 ZIP 不包含测试 EXE。

应用程序集通过 `InternalsVisibleTo` 向测试程序集暴露内部实现。测试只操作自己创建的子进程；`ProcessPolicy` 用精确 fixture PID 限定测试目标。所有测试配置位于单独测试输出目录。

## 命令

```powershell
# 编译应用与测试
.\scripts\build.ps1

# 72 项非交互检查，供 CI 使用
.\scripts\test.ps1 -UnitOnly

# 128 项完整检查，需要本地交互桌面；包含上面的 72 项
.\scripts\test.ps1

# 重新生成原创图标和预览后重新编译
.\scripts\build-icons.ps1
.\scripts\build.ps1
```

默认输出：`outputs/build/`。测试日志：`work/tests-<id>/`；也可用 `-OutputDirectory` 指定一个新目录。脚本拒绝把不同测试运行追加到同一份日志。

## 测试范围

- 匹配器：中英文标题、风险提示、按钮唯一性、隐藏/禁用控件、进程与网页排除。
- 模式：过期、永久保存、独立进程恢复、设置损坏/写入失败与路径绑定。
- 图标：多尺寸 ICO、透明度、原生 256px 资源、实际应用 EXE 图标提取比对。
- 结果检查：延迟消失、读取异常、取消、不可确认时停用。
- 完整集成：真实 WinEvent + UIA，但目标是自建模拟窗体；覆盖不消失、立即关闭、延迟关闭和按钮变灰。

**测试不触发或点击真实 Chrome 授权。** 人工 Chrome/MCP 联调应独立记录，不得以模拟通过推断真实授权成功。

## 并发与维护

WinEvent 回调只入队，UIA 读取在 MTA 工作线程中执行，托盘更新通过 WinForms 消息线程。等待均为事件驱动或有限的事件后观察；不要加入固定周期的全窗口扫描。

沿用 `Local\ChromeConsentGuard.Observer.v1` 单实例锁是刻意的迁移兼容，不是漏改品牌。应用名称、EXE 和资源名均已使用新名称。

核心授权识别和调用策略沿用前身 v1.2.1；此次首发整理未增加调用方式、降低匹配门槛或清除去重保护。
