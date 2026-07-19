# CPUAlert

<p align="center">
  <img src="Design/CPUAlert-AppIcon-Generated.png" width="128" height="128" alt="CPUAlert 应用图标">
</p>

<p align="center">
  注重隐私的 macOS 菜单栏 CPU / GPU 实时压力监控工具。
</p>

<p align="center">
  <a href="LICENSE"><img alt="GPL-3.0-or-later" src="https://img.shields.io/badge/license-GPL--3.0--or--later-blue.svg"></a>
  <img alt="macOS 15+" src="https://img.shields.io/badge/macOS-15%2B-black.svg">
  <img alt="Apple 芯片" src="https://img.shields.io/badge/architecture-arm64-orange.svg">
  <img alt="Swift 6" src="https://img.shields.io/badge/Swift-6-F05138.svg">
</p>

<p align="center">
  <a href="README.md">English</a> · 简体中文
</p>

CPUAlert 是一款面向 Apple 芯片、macOS 15 及更高版本的本地菜单栏应用。它展示整机 CPU 压力、尽力而为的 GPU 使用率、进程或应用组排名，并在 GPU 数据不可用时保持 CPU 监控正常工作。

## 主要功能

- 在菜单栏实时显示整机 CPU 和 GPU 压力。
- 查看 CPU 进程排名、按需展开线程，以及展开 GPU 应用组成员。
- 自定义持续高负载提醒阈值、持续时间和冷却时间。
- 安全终止进程：包含确认步骤、PID 重用校验、系统进程保护和特权操作即时认证。
- 支持登录时启动、首次使用引导、权限管理、诊断，以及中英文界面。
- 没有网络请求、遥测、分析 SDK、账号系统或进程历史数据库。

## 当前状态与限制

CPUAlert 仍处于早期开源阶段，仅支持 Apple 芯片和 macOS 15 及以上版本。

CPU 数据来自受支持的进程接口。GPU 数据依赖未公开的 IOReport 与 coalition 接口，系统更新可能导致这些数据暂时或永久不可用。发生这种情况时，界面会显示 `GPU —`，CPU 监控继续运行，GPU 提醒自动停用。

GPU 分组数值是把当前整机 GPU 使用率按观察到的资源组活动份额进行估算，并不是 macOS 提供的直接逐进程 GPU 百分比。因此它适合判断相对活跃程度，不应被当作精确计费或性能归因数据。

## 构建要求

- Apple 芯片 Mac。
- macOS 15 或更高版本。
- 完整版 Xcode；仅安装 Command Line Tools 不足以完成应用构建和签名。
- 用于本机开发构建的 Apple Development 签名身份。

创建不会进入 Git 的 `Config/Local.xcconfig`：

```xcconfig
CPU_ALERT_DEVELOPMENT_TEAM = YOUR_TEAM_ID
```

然后执行：

```bash
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer

xcodebuild build \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -configuration Debug \
  -derivedDataPath build/DerivedData

xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64' \
  -derivedDataPath build/DerivedData
```

CPUAlert 是 `LSUIElement` 应用，不会显示普通 Dock 图标。请启动构建出的 `.app`，然后使用菜单栏中的 CPU / GPU 状态项。

如果 Xcode 提示缺少 Metal 工具链，可执行：

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  xcodebuild -downloadComponent MetalToolchain
```

## 权限与安全边界

- 通知权限只会在用户主动启用提醒时申请。
- 登录项只会在用户主动切换设置后注册或移除。
- 启动应用不会自动安装特权 Helper。
- 同一用户的 `SIGTERM` 在应用进程内完成；`SIGKILL` 是单独确认的操作。
- Root 终止操作每次都要求本机用户重新认证，并在发送信号前复核 PID 与进程启动时间。
- Helper 只接受固定的安全编码操作，不能接收任意 Shell 命令、路径、环境变量或可执行文件。
- 受保护的系统进程不能通过 CPUAlert 终止。

CPUAlert 当前使用已弃用的 `SMJobBless` 支持本地开发构建。面向更多用户分发还需要 Developer ID 签名、公证，并应评估迁移至现代特权 Helper 机制。

## 隐私

CPUAlert 没有联网、遥测或上传路径，也不保存进程历史。采样结果、进程列表、通知上下文和线程细节只暂存在内存中；设置仅保存阈值和用户偏好。

## 仓库结构

- `CPUAlertApp/`：应用、采集器、提醒、设置和界面。
- `CPUAlertHelper/`、`CPUAlertShared/`：受限特权 Helper 与共享 XPC 类型。
- `CPUAlertTests/`、`CPUAlertUITests/`：单元测试和确定性 UI 验收测试。
- `TestFixtures/`：需要主动运行的 CPU / GPU 压力夹具。
- `Scripts/`：基准测试与进程采样工具。
- `docs/cpualert-implementation/`：需求、设计与实施记录。

更多指标语义、基准测试结果、Helper 移除方式和已知限制请参阅[英文主文档](README.md)。

## 参与贡献与安全问题

欢迎提交缺陷报告、聚焦的功能改进、测试和文档修复。提交 Pull Request 前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。安全问题请按照 [SECURITY.md](SECURITY.md) 使用 GitHub 私密漏洞报告，不要创建公开 Issue。

## 开源协议

CPUAlert 采用 [GNU General Public License v3.0 或更高版本](LICENSE)（`GPL-3.0-or-later`）。如果你分发了受 GPL 约束的修改版或衍生作品，就必须同时以 GPL 提供相应源代码。这正是“使用和修改后分发也必须继续开源”的强 Copyleft 协议；Apache-2.0 不具备这项强制要求。
