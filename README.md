# Mole Windows

Mole Windows 是一个原生 WinUI 3 系统工具，提供清理、软件管理、优化、
磁盘分析和实时状态五个工作区。项目采用深色行星视觉语言，同时遵循
Windows 的窗口、托盘、DPI、键盘和权限语义。

> 当前处于早期开发阶段。所有删除或系统维护操作都必须先生成预览，
> 再由用户明确确认；未完成安全合约的入口保持禁用。

## 当前能力

- 原生 WinUI 3 / .NET 8 桌面壳与五区导航；
- Windows CPU、内存、磁盘、网络、进程和电池遥测；
- 系统托盘图标、状态 HUD、历史记录和操作日志；
- Windows 已安装软件枚举、启动项基础能力和磁盘树图；
- 内置 Mole Windows 引擎探测与结构化进程调用；
- 回收站优先的安全删除服务；
- x86、x64、ARM64 工程配置与 Windows CI。

## 开发

要求：

- Windows 10 1809 或更高版本；
- Visual Studio 2022，安装 Windows App SDK / .NET 桌面开发工作负载；
- .NET 8 SDK。

```powershell
dotnet restore .\MoleWindows.sln
dotnet build .\MoleWindows.csproj -c Debug -p:Platform=x64
dotnet test .\Tests\MoleWindows.Tests\MoleWindows.Tests.csproj -c Debug
.\run-local.ps1
```

规格与实时进度位于
[`docs/windows-mole/`](docs/windows-mole/)。第三方来源与许可证见
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)。

## 品牌说明

本项目使用原创图标和行星素材，不包含付费 Mole App 的源代码、截图或
专有视觉资产。公开发布前仍需单独确认产品名、商标和分发权限。
