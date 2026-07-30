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
| Shell | **完成** | 56 顶栏 / 38 胶囊；设置 35% 透明；路由底色 |
| Clean | **完成** | Idle/Scan/Review/Complete；分类折叠树；回收站 apply |
| Optimize | **完成** | Mercury + 清单 ✓/●/✦；预览写入 history |
| Apps 卸载 | **完成** | 图标/排序/上次使用/多选/底栏/残留 review |
| Apps 更新 | **完成** | 安静空态（无静默更新源） |
| Apps 启动项 | **完成** | 只读 Run 键 + 启动文件夹清单 |
| Analyze | **完成** | 自动扫描；下钻；右键打开/回收站；Current·Disk |
| Status | **完成** | 4×2 Bento；进程 pin 持久化；电池环；风扇分段展示 |
| Tray HUD | **完成** | 健康/2×3/进程/快捷/生涯聚合 |

## 3. Windows 能力边界（诚实保留）

以下为 **平台差异**，非未完成 UI 占位：

- 风扇转速 / SMC Auto·Cool·Max 可控：Windows 无等价 API → 只读展示  
- 电池配件（AirPods 等）：无通用 API → 不伪造  
- 软件静默更新：无可信元数据源 → 安静空态，不假装可更新列表  
- 启动项：只读清单，不写注册表（安全合约）

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
8. Shell hover · Analyze 右键 · **启动项/更新空态/行星动效/默认分析扫描**（本轮）
