# WinMoe 设计

## 架构

```text
WinUI Pages / Tray HUD
        |
MVVM ViewModels + Navigation
        |
Shared samplers / plans / operation history
        |
Windows services + Mole engine adapter
        |
Windows APIs / preview-first command engine
```

## 核心组件

- `ShellPage`：自定义标题栏、五区胶囊导航和更多入口。
- `DashboardPage` / `SystemTelemetrySamplerService`：状态、历史和托盘共用
  的遥测源。
- `CleanupViewModel` / `OptimizeViewModel`：预览、确认、执行、结果状态机。
- `WindowsInstalledApplicationService`：注册表与 MSIX 软件清单。
- `DiskAnalyzerService` / `DiskTreemapLayout`：磁盘扫描和树图布局。
- `MoleEngineService`：只通过结构化参数启动引擎，不由 UI 拼接命令。
- `RecycleBinDeletionService`：默认可恢复删除。
- `WindowsTrayIconService` / `TrayHudWindow`：Windows 通知区域入口。

## 页面

| 路由 | 页面 | 主色 | P0 行为 |
|---|---|---|---|
| `clean` | 清理 | 深蓝 | 扫描、预览；安全合约前禁用执行 |
| `apps` | 软件 | 酒红 | 卸载、更新、启动项信息架构 |
| `optimize` | 优化 | 暖灰金 | 预览后确认 |
| `analyze` | 分析 | 深棕 | 目录侧栏、树图、回收站删除 |
| `status` | 状态 | 暖棕 | 概览、历史、活动 |

## 安全不变量

1. 普通 UI 进程不长期持有管理员权限。
2. 参数通过 `ProcessStartInfo.ArgumentList` 传递。
3. apply 只接受未过期、未变化的本机计划。
4. UI、托盘、HTTP/MCP 不得拥有不同的高风险确认策略。
5. 操作必须产生可追踪结果：成功、失败、取消或部分完成。

## 技术决策

### WinUI 3，而不是 Electron

目标依赖 Windows 原生窗口、托盘、DPI、UAC、注册表、MSIX 和无障碍能力。
复用 WinUI 服务层能降低平台桥接和内存开销。

### 适配 Burrow Windows 骨架

Burrow 已实现遥测、历史、托盘、磁盘和应用枚举，并以 MIT 许可发布。
本项目固定来源提交并重做品牌、导航、中文界面和安全合约。

### 原创视觉资产

调研截图只作为内部对照。仓库资产由本项目生成或绘制，避免把购买的软件
使用权误当成再分发许可。

## 验证

- Windows CI：restore、Release x64 build、测试、publish dry-run；
- XML/XAML 可解析性和缺失资产检查；
- Windows 真机：五路由启动、托盘 HUD、DPI、键盘、Reduce Motion；
- 高风险动作：计划过期、权限拒绝、路径变化、取消和部分失败。

下一阶段将通过独立 `ReleaseReadiness` module 聚合 Requirement、Scenario、
Evidence、ReleaseGate 和 GoNoGo；具体 interface、adapter 与阶段计划见
[`roadmap.md`](roadmap.md)。
