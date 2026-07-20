# 内存监控、释放与三指标菜单栏设计

## 设计原则

1. **压力优先**：内存不是“越空越好”。界面突出压力相关占用，不把可回收缓存渲染成问题。
2. **真实操作**：所谓释放内存，本质是让用户选择并退出不再需要的应用。
3. **微型但可读**：菜单栏只编码最重要的三组信息，固定结构、固定宽度、单一阅读顺序。
4. **共享采样成本**：物理占用跟随现有进程计数器一次读取，避免重复遍历系统进程。

## 信息架构

### 菜单栏三联仪表

固定宽度 78pt、高度 20pt，三个 24pt 单元，中间保留 3pt 间隔：

```text
┌────────┬────────┬────────┐
│ C   42 │ G   18 │ M   67 │
│━━━━━━  │━━      │━━━━━   │
└────────┴────────┴────────┘
```

- 字母使用 7pt 中等字重；数字使用 9pt monospaced rounded semibold。
- 压力色条宽度表达使用比例、颜色表达级别；文字数值保证信息不只依赖颜色。
- CPU 使用系统蓝、GPU 使用紫、内存使用青绿作为资源识别色；告警时色条改用统一压力色。
- 整体无外框、弱背景，避免在 macOS 菜单栏中形成沉重胶囊。

### 主面板

- 摘要区改为三个等宽紧凑卡片，分别显示百分比、状态和内存容量补充信息。
- 资源分段控件为 CPU / GPU / 内存。
- 内存榜标题区域右侧提供“释放内存…”按钮。
- 释放弹窗采用选择清单，不预选；底部固定显示选中数量与预计物理占用。

## 数据模型

```text
CPUAProcessCounter
  + physical_footprint_bytes
        │
        v
ProcessRankingSnapshot
  ├─ cpu: [ProcessMetric]
  └─ memory: [ProcessMetric]

SystemMemoryCollector ──> MemoryMetric

SamplingEngine ──> MetricsSnapshot
  ├─ memory
  ├─ memoryProcesses
  └─ memoryLevel
```

### MemoryMetric

- `totalBytes: UInt64`
- `usedBytes: UInt64`
- `compressedBytes: UInt64`
- `usage: Double`，严格限制在 0...1

### ProcessMetric 扩展

- 新增 `physicalFootprintBytes: UInt64`。
- CPU 榜和内存榜共享身份与应用元数据，分别排序。

### ProcessRankingSnapshot

- 由 `ProcessCPUCollector.sampleProcessRankings()` 一次返回 CPU 与内存排行。
- 首轮没有 CPU delta 时，CPU 榜可为空，但内存榜立即可用。

## 采集设计

### 系统内存

- `CPUAGetSystemMemory` 调用 `host_page_size` 与 `host_statistics64(HOST_VM_INFO64)`。
- 压力相关已用页数 = active + wired + compressor；使用饱和加法与总量上限防溢出。
- 物理内存总量由 `ProcessInfo.processInfo.physicalMemory` 提供给计算层。
- 纯函数 `SystemMemoryCollector.metric(totalBytes:statistics:)` 负责转换，便于确定性测试。

### 进程内存

- `CPUACopyProcessCounter` 从 `proc_pid_rusage` 的公开 `rusage_info_current` 读取 `ri_phys_footprint`。
- 与 CPU 时间、身份、UID 和名称一起返回；Swift 侧只做一次应用元数据装饰。
- CPU 与内存榜都先保留前 20 项，再由面板按 5/10 设置裁切。

## 释放流程

```text
点击“释放内存…”
  -> 生成安全候选（当前用户 + regular app + 非保护）
  -> 默认空选择
  -> 用户勾选
  -> 二次确认（未保存内容提示）
  -> 逐个 requestGraceful
  -> 汇总结果并刷新采样
```

- `MemoryCleanupPolicy` 是无副作用的候选过滤层。
- `MemoryCleanupCoordinator` 顺序调用现有 `TerminationCoordinator.requestGraceful`，避免同时弹起多个应用退出流程。
- `.forceAvailable` 在批量流程中解释为“仍在运行”，不自动调用 `requestForce`。
- 释放弹窗只显示 GUI 应用；单个进程原有终止菜单仍保留。

## 状态与并发

- `SamplingEngine` 并发采集 system CPU、system GPU、system memory。
- 排行周期只调用一次 `sampleProcessRankings()`，GPU 分组和展开线程保持并发。
- `MonitorModel` 在主 actor 上发布单一快照；候选选择属于弹窗局部状态。
- 内存清理执行期间禁用确认按钮并显示进度，结束后请求一次刷新或等待下一轮快照。

## 可访问性

- 菜单栏：完整朗读三项百分比与压力等级。
- 内存摘要：朗读百分比和“已用/总量”。
- 候选行：朗读应用名、PID、物理占用和选择状态。
- 色条、趋势图和压力颜色均有文字等价物。

## 兼容与迁移

- `ResourceKind.memory` 使用 Codable 字符串，不改变既有 CPU/GPU 值。
- 新字段通过统一初始化器加入所有测试夹具。
- 版本升级到 0.2.1 / Build 6；保留上一份可运行包以便回退。

## 风险与缓解

- **内存口径与 Activity Monitor 不完全一致**：界面文案称“压力相关占用”，文档公开计算口径。
- **退出应用造成未保存内容丢失**：默认零选择、二次确认、只优雅退出、不自动强杀。
- **第三项导致菜单栏拥挤**：固定三列、数字去百分号、完整信息放到可访问性标签与面板。
- **采样增加卡顿**：复用进程扫描，不在主线程查询 NSWorkspace 全量列表。
