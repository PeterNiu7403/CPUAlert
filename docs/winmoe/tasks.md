# WinMoe 任务与进度

> 本文件是项目进度的唯一真实来源；每个任务完成后立即更新。

## 进度总览

| 指标 | 数值 |
|---|---:|
| 总任务数 | 9 |
| 已完成 | 7 (78%) |
| 待执行 | 2 (22%) |
| 最后更新 | 2026-07-31 14:20 CST |

## 任务

- [x] **9. 深度修复：崩溃、真实传感器、原生清理、动效与打磨**
  - 完成时间：2026-07-31 01:05 CST
  - 背景：第 1-6 轮自称"完成"的页面实际大量不可用：软件页加载即崩
    （xaml_unhandled COMException）、清理页只显示 49 B 假种子数据、温度/风扇
    全无数据、星球绕左上角"漂浮"而非自转、设置页全英文。
  - 产出：
    1. **线程崩溃类（RPC_E_WRONG_THREAD）**：`ViewModelBase` 重写
       `OnPropertyChanged` 自动封送回 UI 线程；`UninstallViewModel` 三处后台
       线程集合/命令通知包裹 `RunOnUiThread`；`LocalStartupDiagnosticsService`
       记录完整异常堆栈。
    2. **星球动效**：`PlanetMotion` 无条件设置 `RenderTransformOrigin=(0.5,0.5)`，
       XAML 已声明 RotateTransform 的元素不再绕左上角公转。
    3. **真实传感器**（新增 `IHardwareSensorService`/`WindowsHardwareSensorService`）：
       Lenovo GameZone WMI（`LENOVO_OTHER_METHOD.GetFeatureValue` 命名参数调用）
       读取 CPU/GPU 温度与双风扇 RPM + 峰值；不支持机器诚实显示不可用。
    4. **GPU 独显/集显拆分**（新增 `IGpuAdapterService`/`WindowsGpuAdapterService`）：
       DXGI `[ComImport]`（PreserveSig + 完整 IDXGIObject 基座）枚举适配器，
       按 LUID 归组 GPU Engine 计数器得到每卡 3D 占用。
    5. **磁盘聚合**：全部固定卷总容量/总可用 + `\\.\PhysicalDriveN` 零权限探测
       物理盘数；徽章显示"N 盘 · 总量"。
    6. **原生清理扫描**（新增 `ICleanupScanService`/`CleanupScanService`）：替代
       残缺的引擎 `clean --dry-run`（上游 `bin/` 命令脚本曾整体缺失，已从
       tw93/Mole windows 分支补齐 8 个脚本）。扫描用户临时文件（>24h、前 40 大）、
       浏览器/应用/开发缓存、崩溃转储，真机实测 54 项 3.44 GB；执行仍走
       OperationPlan + 回收站合约。
    7. **状态页**：CPU/GPU 温度徽章、风扇真实 RPM 与负载、双 GPU 页脚、
       磁盘聚合徽章、网络徽章按适配器类型（Wi-Fi/以太网）、健康分改为检查制
       （空闲 100 与"检查项均通过"一致）、设备芯片显示 CPU 型号（Ultra 9 275HX）、
       进程表 50 行 + 真实应用图标（`ProcessIconLoader` 复用缩略图缓存）。
    8. **杂项**：活动页不再被引擎 `--version` 探活刷屏；截图工具链修复
       （GetWindowRect 的 DPI 虚拟化导致"错位"假象，改用 DWM 扩展帧边界）；
       设置/项目清理/安装包页面中文化；齿轮与品牌按钮暗色悬停。
    9. **托盘 HUD 数据对齐**（2026-07-31 10:39 补）：健康分/磁盘聚合改走共享的
       `SystemHealthEvaluator`（仪表盘、HUD、托盘 tooltip 同一公式）；设备芯片
       改 CPU 型号（`CpuModelNameResolver`，HUD 不再显示机器名 PETER）；风扇显示
       真实 RPM 与负载、GPU 显示独显/集显与温度（移除"Windows 无 SMC"等占位文案）；
       修复底部导航胶囊文字被裁——`WinMoeGhostPillButtonStyle` 继承的
       `MinWidth=132` 在约 61 DIP 宽的列里溢出，按钮补 `MinWidth=0`。
    10. **多分区磁盘支持**（2026-07-31 11:05 补）：快照新增 `Volumes`
       （`DiskVolumeTelemetry`，每卷标签/总量/可用）；状态页新增"磁盘分区"面板
       （聚合卡片保留，C:/D:/E: 每卷一行进度条 + 用量/可用）；清理扫描从仅系统盘
       扩展为全部分区（各固定卷根目录 Temp/tmp，>24h 规则，与用户 TEMP 路径去重）；
       分析页左侧栏新增"整盘扫描"按钮组，一键分析任意分区。
    11. **Mole 官方截图逐页像素对照打磨**（2026-07-31 12:45 补）：以
       `.research/mole-ui/`（clean/status/optimize/analyze/uninstall + HUD + 菜单）
       为基准重抓全部页面截图逐张对照，修复一轮视觉债——
       **状态页**：健康卡换程序化生成的原创太阳资产 `Assets/Hero/sun.png`
       （`artifacts/gen_sun.py`，纯 PIL 径向渐变+粒化+边缘昏暗），"健康度"标签、
       "已运行 X · 自 M月d日"、"低负载 · 负载 x / N 核"、内存徽章"压力 NN%"、
       磁盘"已用 X · NN%"、网络副标题"↑ 上行 · 介质"（IP 入 tooltip）、电池去掉
       重复副标题（放电时显示预计剩余）、温度徽章 ≤60 绿 / 61-80 金 / >80 红、
       进程候选池 Take(30)→Take(50) 使表头 50 行名符其实；
       **软件页**：子页签选中改深暖棕 pill（非白 pill）、未选中勾选框改暗色细描边、
       "review"→"残留"、副标题补 "·" 分隔、未选中行右侧大小改暗色（选中才橙）、
       新增"已安装"排序、激活排序链接修复为橙色文字（`SetTextLink` 不再被
       `ApplyNavigationState` 刷成白 pill，清除残留局部画刷值）；
       **托盘 HUD**：头部换太阳、进程卡吃满剩余高度消除中部空白、隐藏引擎日志
       残留行、CPU/GPU 卡加温度徽章与负载/短 GPU 副标题、内存/磁盘卡加细用量条、
       风扇副标题去省略号（峰值入 tooltip）、新增电池卡（无电池机器自动隐藏）、
       进程 0% 显示 dim "—"、进程行 5→12 填满卡片、禁用 ListView 入场动画
       （修复每次刷新整表闪烁/截图发灰）；
       **活动页**：状态点 8px 居中对齐、行高收紧至 30、"Succeeded (0)"→"成功/失败"
       （`AppActivityFormatter.FormatOperationResult`，设置页同步）、引擎输出
       乱码清洗 `SanitizeEngineText`（U+FFFD 与独立 ?? 折叠为 —，记录侧+显示侧
       双保险，新增 6 个单测）；
       **清理 Review**：CheckBox 轻量样式键换肤（橙底深勾圆角，三态不变）；
       **分析页**：treemap 瓦片缝隙 2.5→1.5、圆角 3→2。
       验证：222/222 测试、Debug+Release 构建、validate-source 全绿；
       parity2-parity5 真机截图逐页复核。
    12. **剩余能力边界攻关**（2026-07-31 14:20 补）：
       - **状态页火花线根因修复**：内存/GPU/网络卡折线从未渲染——双重 bug：
         图表点坐标系（100×100 / 520×80）与宿主 Canvas（100×48 / 200×48）不匹配，
         且宿主 `MinHeight=48` 在 152 DIP 卡内使 `*` 行被压到近零；改坐标系对齐
         Canvas、去 MinHeight、`HistoryChartSeries` 新增 `AreaPoints` 封闭多边形，
         实现 Mole 式渐变面积图。
       - **电池真实数据**：新增 `BatteryDetailProbe`（`IOCTL_BATTERY_QUERY_TAG`
         + `IOCTL_BATTERY_QUERY_INFORMATION` 取设计/满充容量与循环数，句柄需
         GENERIC_READ|WRITE；WMI `BatteryCycleCount` 兜底、`BatteryStatus` 取
         充放功率 mW）与 `BatteryDetailFormatter`（健康度=满充/设计、徽章/页脚
         文案，17 个单测）。电池卡徽章"健康 100%"（实测 85990/80000 mWh）、
         页脚"7 次循环"与 powercfg /batteryreport 完全一致。
       - **保持常亮**：`DisplaySleepPreventionService`（SetThreadExecutionState
         ES_DISPLAY|ES_SYSTEM，定时自动释放）+ 托盘子菜单 1/2/4/8 小时/不限时/
         停止（当前项打勾、激活时父项显示剩余时间）。
       - **擦屏幕**：`CleanScreenWindow` 全屏黑窗（Esc/点击退出，提示 3s 淡出），
         托盘菜单入口；新增 `WINMOE_SHOW_CLEAN_SCREEN` 诊断启动项与
         `run-local.ps1 -ShowCleanScreen` 冒烟开关。
       - **通用传感器链（全机型适配）**：传感器层从 Lenovo 单源重构为提供方链
         （`ISensorProbe` + `HardwareSensorMerger`），全部标准用户权限、无内核驱动：
         NVIDIA NVAPI（nvapi64/nvapi.dll 按进程位数，逐卡温度+风扇）、AMD ADL
         （atiadlxx/atiadlxy.dll，OverdriveN/6/5）、Intel Level Zero Sysman
         （ze_loader.dll，Arc 独显温度/风扇/额定转速；实测 iGPU 无传感器诚实为空）、
         Dell DCIM WMI（Command Monitor 的 DCIM_Fan/DCIM_TemperatureProbe）、
         ACPI 热区 PDH 英文计数器（CPU 温度回退）、存储温度
         （IOCTL StorageDeviceTemperatureProperty + WMI 可靠性计数器，60s 缓存，
         0.1K/0.1°C 双单位启发式解码）、Lenovo GameZone 降为增强源。
         逐卡读数按 PCI VendorId 匹配 DXGI 适配器（`GpuTemperatureAttachment`），
         GPU 页脚/磁盘徽章/分区行显示温度；实测 `Lenovo GameZone + NVAPI`
         双源互证 49-53°C。
       - **监控软件底层调研定论**（写入 parity 文档）：CPU-Z/HWiNFO/AIDA64/LHM
         读 CPU 核心温度（DTS MSR）与主板风扇（SuperIO/EC）全部依赖自带内核
         驱动（RDMSR/端口 I/O 是 ring-0 特权）；WinRing0 已被微软封禁并被
         Defender 查杀，FanControl V238+ 转投 PawnIO（仍需管理员装驱动）；
         NVAPI 多风扇 ClientFanCoolers* 属 NDA SDK（GPU-Z 用）。免安装分发
         模型下不引入驱动，PawnIO 留作未来"高级传感器"模式的参考设计。
       - 验证：274/274 测试、Debug+Release 0 警告、validate-source、
         11 路由 + HUD + 擦屏真机冒烟全绿。
  - 验收：215/215 测试通过；`MachineCleanFlowIntegrationTests`（opt-in）
    端到端证明 扫描→计划校验→回收站 链路；全路由真机冒烟（clean/apps/
    optimize/analyze/status/history/activity/settings/purge/installer/HUD）
    无 xaml_unhandled。
  - _需求参考：RQ-1, RQ-2, RQ-3, RQ-4_

## 历史任务

- [x] **1. 固定需求、来源与全新工程边界**
  - 完成时间：2026-07-30 12:08 CST
  - 产出：`requirements.md`、`design.md`、WinUI 解决方案、服务与测试骨架、
    `THIRD_PARTY_NOTICES.md`
  - 验收：旧 CPUAlert 工作树已被 WinUI 工程完整替换；`.git` 与历史保留；
    上游提交和许可证已固定
  - _需求参考：RQ-1, RQ-5_

- [x] **2. 完成原创品牌资产、主题和五区导航**
  - 完成时间：2026-07-30 12:12 CST
  - 产出：`Assets/Brand/winmoe-mark.svg`、完整 Windows 图标集、
    `Assets/Hero/*.png`、`Styles/WinMoeTheme.xaml`、`ShellPage`
  - 验收：全部 XAML/manifest 通过 XML 解析；清理、软件、优化、分析、状态
    五个中文路由均进入选中态；项目引用的所有视觉资产存在且尺寸已校验
  - _需求参考：RQ-1_

- [x] **3. 完成清理、软件、优化和分析的 P0 页面**
  - 完成时间：2026-07-30 12:14 CST
  - 产出：四个中文 P0 页面、原创行星 Hero、Windows 软件清单与磁盘树图接入
  - 验收：相关 XAML 全部可解析；清理与优化真实执行显式禁用；软件残留
    默认不选择且通过 `RecycleOption.SendToRecycleBin` 执行
  - _需求参考：RQ-1, RQ-3, RQ-4_

- [x] **4. 完成状态页、历史、活动和托盘 HUD**
  - 完成时间：2026-07-30 12:17 CST
  - 产出：中文状态/历史/活动页面、原创状态 Hero、托盘 HUD 与原生菜单
  - 验收：主状态页调用共享采样器，托盘与 HUD 读取同一实例的
    `LatestSnapshot`；不可用的电池、风扇、网络和 GPU 不伪造数值
  - _需求参考：RQ-2_

- [x] **5. 固化预览后执行合约并补充安全测试**
  - 完成时间：2026-07-30 12:23 CST
  - 产出：`OperationPlan`、SHA-256 指纹、统一 `BackendEvent`、
    `OperationPlanValidator` 与 7 个合约测试
  - 验收：隔离 .NET 8 环境实测 7/7 通过；未确认、过期、内容变化、空选择、
    磁盘根目录和 Windows 目录均被拒绝
  - _需求参考：RQ-3_

- [x] **6. 完成构建、静态校验和 Windows CI**
  - 完成时间：2026-07-30 12:32 CST
  - 产出：`scripts/validate-source.ps1`、`.github/workflows/windows-ci.yml`
  - 验收：Windows runner 上的源码安全门禁、restore、WinUI/XAML build、
    完整测试、publish dry-run 和测试产物上传全部通过
  - 证据：GitHub Actions run `30514003553`
  - _需求参考：RQ-5_

- [ ] **7. 在 Windows 真机完成视觉和交互验收**
  - 覆盖五路由、托盘、100%-200% DPI、键盘和 Reduce Motion
  - _需求参考：RQ-1, RQ-2, RQ-4_

- [ ] **8. 完成发布打包与正式品牌/商标确认**
  - 生成签名安装包并确认公开名称、商标和分发权限
  - 当前状态：本地工程与产品标识已迁移为 `WinMoe`；GitHub 远端改名、
    签名、可信时间戳和品牌/分发批准仍未完成
  - _需求参考：RQ-5_

## 上下文摘要

**架构：** WinUI 3 + MVVM + Windows 服务层 + Mole 安全引擎适配器。
**当前批次：** P0 工程、视觉与可验证构建。
**下一任务：** 任务 7，Windows 真机视觉与交互验收。
**详细路线：** [`roadmap.md`](roadmap.md)。
