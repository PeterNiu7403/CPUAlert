# WinMoe 需求

## 目标

把原 macOS CPUAlert 仓库完整替换为一个原生 Windows 系统工具。视觉
以既有调研中采集的 Mole 页面层次为基线，工程以 WinUI 3 和经过验证的
Windows 服务层为基础，公开仓库只使用原创或可再分发资产。

## 用户故事与验收

### RQ-1：一致的主界面

作为 Windows 用户，我希望从一个优美、清晰的主窗口进入清理、软件、
优化、分析和状态功能，以便不用学习命令行。

1. WHEN 应用启动 THEN 系统 SHALL 显示五个中文一级入口。
2. WHEN 用户切换入口 THEN 系统 SHALL 在同一窗口展示对应色彩与行星场景。
3. WHEN Windows 缩放为 100%、125%、150% 或 200% THEN 系统 SHALL
   保持可滚动且不裁掉主要操作。
4. IF 系统启用 Reduce Motion THEN 系统 SHALL 不依赖持续动画传达状态。

### RQ-2：状态与托盘

作为需要持续观察电脑状态的用户，我希望主窗口、历史和托盘 HUD 使用
同一份遥测数据。

1. WHEN 采样器产生快照 THEN 状态页、历史和托盘 SHALL 读取同一快照源。
2. WHEN 指标不可读 THEN UI SHALL 显示“不可用”，不得伪造为 0。
3. WHEN 用户点击托盘图标 THEN 系统 SHALL 打开可关闭的状态 HUD。

### RQ-3：安全清理和优化

作为谨慎的用户，我希望任何破坏性操作都先预览再执行。

1. WHEN 用户请求清理或优化 THEN 系统 SHALL 先创建计划。
2. WHEN 计划可执行 THEN 系统 SHALL 展示条目、容量、风险和权限需求。
3. IF 用户未确认、计划过期或路径变化 THEN 系统 SHALL 拒绝执行。
4. WHEN 删除文件 THEN 系统 SHALL 默认使用回收站。
5. IF 非交互 JSON 合约不可用 THEN 系统 SHALL 禁用真实执行，不得解析
   彩色终端文本后直接删除。

### RQ-4：软件和磁盘

1. WHEN 用户打开软件页 THEN 系统 SHALL 枚举注册表和 MSIX 应用，并区分
   卸载、更新和启动项。
2. WHEN 用户分析磁盘 THEN 系统 SHALL 支持目录钻取和树图。
3. WHEN 用户请求删除分析结果 THEN 系统 SHALL 复用 RQ-3 的确认边界。

### RQ-5：可维护与可发布

1. WHEN 代码推送到 `main` THEN Windows CI SHALL 恢复、构建并运行测试。
2. WHEN 构建发布物 THEN 系统 SHALL 支持 win-x64，并保留 x86/ARM64 配置。
3. WHEN 使用上游代码 THEN 仓库 SHALL 保留精确来源、提交和许可证。

## 非目标

- P0 不建立长期常驻的管理员服务；
- P0 不伪造风扇、温度或外设电量；
- 不把付费 App 的截图、Logo 或专有行星素材提交到公开仓库；
- 不把“编译通过”视为 Windows 真机视觉验收。
