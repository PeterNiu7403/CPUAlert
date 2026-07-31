# Mole for Mac → WinMoe 视觉一比一对照

> 调研来源：mole.fit 官方产品图、Tw93 公开发布截图、设计博客。  
> 闭源 Mac App 仅作**内部对照**；仓库资产保持原创/可再分发（design.md 约束）。  
> 参考图缓存：`.research/mole-ui/`（不进入发布包）。

## 1. 总体设计语言

| 维度 | Mole for Mac | WinMoe 复刻 |
|---|---|---|
| 主题 | 仅 Dark | Dark-only ✓ |
| 窗口 | 连续单色表面 | Shell 随路由换底色 ✓ |
| 顶栏导航 | 胶囊 Logo + 5 Tab | 胶囊 + 白 pill 选中 + 未选 hover ✓ |
| 行星隐喻 | 圆形球体 | Clean/Optimize/Analyze 圆形 + 慢旋（Reduce Motion 停）✓ |
| 文案 | 安静、结果优先 | 中文克制 ✓ |
| Review-first | 预览确认后删除 | OperationPlan + 回收站 ✓ |

### 路由底色

| 路由 | 背景 token |
|---|---|
| Clean | `#0E1B36` |
| Apps | `#241416` |
| Optimize | `#221E14` |
| Analyze | `#1A130F` / 侧栏 `#16100C` |
| Status | `#1C1810` |

## 2. 表面完成度

| 表面 | 状态 | 要点 |
|---|---|---|
| Shell | **完成** | 56 顶栏 / 38 胶囊；设置 35% 透明；路由底色；暗色悬停 |
| Clean | **完成** | Idle/Scan/Review/Complete；**原生扫描真实数据**（临时/浏览器/应用/开发缓存/转储）；分类折叠树；**橙色圆角复选框**；回收站 apply |
| Optimize | **完成** | Mercury + 清单 ✓/●/✦；预览写入 history |
| Apps 卸载 | **完成** | **深暖棕子页签**；图标/排序（含已安装）/上次使用/多选/底栏/残留 review；**"·"分隔副标题**；暗色未选勾选框；橙色激活排序链接 |
| Apps 更新 | **完成** | 安静空态（无静默更新源） |
| Apps 启动项 | **完成** | 只读 Run 键 + 启动文件夹清单 |
| Analyze | **完成** | 自动扫描；下钻；右键打开/回收站；Current·Disk；**细缝近直角 treemap** |
| Status | **完成** | 4×2 Bento；**太阳健康卡**；真实温度/风扇/双GPU/磁盘聚合；**压力徽章/负载文案全中文**；进程 50 行真实图标；电池环；风扇分段展示 |
| Tray HUD | **完成** | 健康/2×3/**电池卡**/进程 12 行/快捷/生涯聚合；**温度徽章；无中部空白；刷新不闪烁** |

## 2.1 传感器与平台探测（2026-07-31 轮）

传感器层是 **通用提供方链**（`WindowsHardwareSensorService` 编排，
`HardwareSensorMerger` 合并），不再绑定品牌；每个源都是标准用户权限、
无内核驱动，机器支持什么就显示什么，不支持的诚实显示 "—"：

| 源 | 覆盖 | 权限 |
|---|---|---|
| NVIDIA NVAPI（nvapi64/nvapi.dll，按进程位数加载） | 任意品牌 N 卡的逐卡温度 + 风扇 RPM | 标准用户 |
| AMD ADL（atiadlxx/atiadlxy.dll，OverdriveN/6/5） | 任意品牌 A 卡的逐卡温度 + 风扇 RPM | 标准用户 |
| Intel Level Zero Sysman（ze_loader.dll，zesDeviceEnumTemperatureSensors/Fans） | Intel Arc 独显的温度/风扇/额定转速（核显大多无传感器，实测 iGPU count=0） | 标准用户 |
| Dell DCIM（root\DCIM\SYSMAN，DCIM_Fan/DCIM_TemperatureProbe） | 装了 Dell Command \| Monitor 的戴尔商用机风扇/CPU 温度 | 标准用户 |
| ACPI 热区（PDH `\Thermal Zone Information(*)\Temperature`，英文计数器 API 免本地化） | 固件暴露热区的机器的 CPU/系统温度回退 | 标准用户 |
| 存储温度（`IOCTL_STORAGE_QUERY_PROPERTY` StorageDeviceTemperatureProperty + WMI `MSFT_StorageReliabilityCounter` 兜底，60s 缓存） | 存储栈支持温度属性的磁盘（按 MSFT_Partition 映射盘符） | 标准用户（驱动而定） |
| Lenovo GameZone WMI | Legion 的 CPU/GPU 温度、双风扇、峰值 RPM | 标准用户 |

- 合并策略：CPU 温度 Lenovo → ACPI 热区；GPU 聚合温度取逐卡读数最高值，
  平台标量兜底；逐卡读数按 PCI VendorId 匹配 DXGI 适配器
  （`GpuTemperatureAttachment`，同厂商多卡按枚举序）；厂商 API 的 GPU 风扇
  在平台接口未报 GPU 风扇时补入；`SensorSource` 列出全部贡献源
  （本机实测 `Lenovo GameZone + NVAPI`，NVAPI 49-53°C 与 Lenovo 读数互相印证）。
- GPU：DXGI 枚举适配器分独显/集显，按 LUID 归组引擎计数器显示每卡 3D 占用，
  页脚显示逐卡温度（"独显 RTX 5080 Laptop 53°C · 集显 0.0%"）。
- 磁盘：全部固定卷聚合（实测 2 物理盘 / 3 卷 / 2.7 TB），徽章"N 盘 · 总量
  · 最高温度"；分区行在有磁盘温度时显示（本机存储栈不支持温度属性，诚实省略）。
- 网络徽章按适配器类型显示 Wi-Fi / 以太网 / 蓝牙。
- 健康分改为检查制：磁盘 ≥95/90、内存 ≥90、CPU ≥90、电池 ≤20 放电逐级扣分，
  空闲机器 100 分与"检查项均通过"一致。
- 活动页只记录真实操作；引擎 `--version` 探活不再写入历史。

## 3. Windows 能力边界（诚实保留）

以下为 **平台差异**，非未完成 UI 占位：

- 风扇转速 / SMC Auto·Cool·Max 可控：Windows 无通用 API → 只读展示  
  （调研：Lenovo 机型可经 GameZone WMI 写热模式，属固件写路径，
  LegionToolkit（GPL）同款机制；写入风险与授权模型不匹配，暂不实现）
- 主板/机箱风扇（SuperIO/EC）、无热区无厂商接口机器的 CPU 核心温度：
  **所有能读到这些值的软件都带内核驱动**——CPU-Z 装 `cpuz_x64.sys`
  读 IA32_THERM_STATUS MSR，HWiNFO 装 `HWiNFO64.sys`，
  LibreHardwareMonitor/OpenHardwareMonitor/AIDA64 用 WinRing0 做 RDMSR 和
  SuperIO 端口 I/O；RDMSR 与 0x2E/0x4E 端口访问都是 ring-0 特权操作，
  用户态物理上不可达。WinRing0 已于 2025 年被微软易受攻击驱动封禁列表
  收入并被 Defender 查杀（FanControl 因此 V238+ 转投 PawnIO 签名驱动，
  但 PawnIO 同样需要管理员安装服务）。结论：免安装、标准用户的分发
  模型下这条路不存在；如未来愿意加"高级传感器"模式，PawnIO 是唯一
  现代可参考设计（FanControl 同款），当前不做
- N 卡多风扇精确转速：NVAPI `ClientFanCoolers*` 系列属 NDA 版 SDK
  （GPU-Z 用的就是这套），公开 ABI 只有 GetTachReading（本机实测
  NOT_SUPPORTED）；已按公开 ABI 实现，读不到时回退厂商 WMI
- Intel 核显温度：Level Zero Sysman 是唯一用户态通道（GPU-Z 同款），
  实测 Arrow Lake 核显传感器数为 0——Arc 独显才有值，核显诚实为空
- 电池温度：WMI `BatteryTemperature` 类存在但 Legion 实例为空 → 省略该段
- 电池配件（AirPods 等）：无通用 API → 不伪造  
- 软件静默更新：无可信元数据源 → 安静空态，不假装可更新列表  
- 启动项：只读清单，不写注册表（安全合约）

### 3.1 已攻破的原"边界"（2026-07-31 轮）

| 项 | 方案 | 来源调研 |
|---|---|---|
| 电池健康度（设计/满充容量） | `IOCTL_BATTERY_QUERY_TAG`(0x294040) + `IOCTL_BATTERY_QUERY_INFORMATION`(0x294044)，需 GENERIC_READ\|WRITE 句柄 | powercfg `/batteryreport` 同款路径，免管理员；实测 design 80000 / full 85990 mWh |
| 电池循环次数 | 同一 IOCTL（WMI `BatteryCycleCount` 兜底） | 实测 7 次，与 powercfg 一致 |
| 充放功率 ⚡W | WMI `root\wmi BatteryStatus` ChargeRate/DischargeRate(mW)，>10W 才显示 | GaryTown/MS Learn WMI 电池文档 |
| N 卡逐卡温度/风扇 | NVAPI 动态加载（按进程位数选 nvapi64/nvapi.dll），GetThermalSettings + GetTachReading | NVAPI SDK 公开 ABI；FanControl/LHM 同款通道但免驱动 |
| A 卡逐卡温度/风扇 | ADL 动态加载（atiadlxx/atiadlxy.dll），OverdriveN/6 温度 + OverdriveN/5 风扇 | ADL SDK 公开 ABI |
| Intel 显卡温度/风扇 | Level Zero Sysman（ze_loader.dll），zesDeviceEnumTemperatureSensors/Fans + zesFanGetProperties 额定转速 | oneAPI 公开规范（ABI 取自官方 zes_api.h）；GPU-Z 同款通道；实测 iGPU 无传感器、Arc 独显有值 |
| 戴尔商用机风扇/CPU 温度 | Dell Command \| Monitor 的 DCIM WMI（root\DCIM\SYSMAN，DCIM_Fan/DCIM_TemperatureProbe） | Dell 官方参考指南 |
| 无品牌机 CPU 温度回退 | PDH `\Thermal Zone Information(*)\Temperature`（PdhAddEnglishCounter 免本地化） | 固件 ACPI 热区，标准用户可读 |
| 磁盘温度 | `IOCTL_STORAGE_QUERY_PROPERTY` StorageDeviceTemperatureProperty（access=0 开 PhysicalDriveN，0.1K/0.1°C 双单位启发式）+ WMI 可靠性计数器兜底 | Win11 设置"磁盘和卷"同款属性；本机存储栈不支持→诚实省略 |
| 状态页火花线 | 内存/GPU/网络卡面积渐变+折线（历史采样缓冲） | — |
| 保持常亮 | `SetThreadExecutionState`（托盘子菜单 1/2/4/8h/不限时/停止） | 通用 Win32，无等价物缺失 |
| 擦屏幕 | 全屏黑窗（Esc/点击退出，3s 后隐藏提示） | Mole Clean Screen 等价 |

## 4. 验收清单

在 100% / 150% DPI 下对照 `.research/mole-ui/{clean,uninstall,optimize,analyze,status}.jpg` 与 HUD 参考图：

- [x] 顶栏胶囊形态与选中/hover 态  
- [x] Clean/Optimize 中心球体圆形、无调试日志、慢旋可停  
- [x] Clean Review 分类树 + 回收站确认  
- [x] Status 4×2 + 进程表 pin/…，无二级 Tab  
- [x] Analyze 左窄右宽 + 土色 treemap + 右键  
- [x] Apps 底栏 Remove N + 启动项列表 + 更新空态  
- [x] Tray HUD 生涯统计  
- [x] 中文文案，无工程占位作主 CTA  

环境相关（DPI 矩阵、平台能力、截图脚本）：见 **`docs/winmoe/env-matrix.md`**。

## 5. 实施轮次摘要

1. Shell / token / 行星页骨架  
2. Status / Apps / Analyze 布局  
3. Clean 四态 + HUD 生涯  
4. Pin 持久化 + Clean apply + 图标缓存  
5. Clean 分类树 · Optimize 清单 · Analyze 指标  
6. Apps 底栏 · HUD 密度  
7. Status 进程密度 · 使用中 · 电池环 · last-used  
8. Shell hover · Analyze 右键 · 启动项/更新空态/行星动效/默认分析扫描  
9. 传感器/多分区/原生扫描（见 tasks.md 任务 9）  
10. **官方截图逐页像素对照**（2026-07-31）：太阳资产（PIL 程序化生成）、
    状态页文案全中文化（已运行/负载/压力/已用）、Apps 深暖棕子页签 + 橙色
    排序链接 + 暗色勾选框 + "已安装"排序、HUD 电池卡 + 温度徽章 + 进程 12 行
    填充 + 禁入场动画防闪烁、活动页密度与中文结果 + 引擎乱码清洗、
    Clean 橙色复选框、Analyze 细缝 treemap
