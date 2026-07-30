# 环境相关验收：DPI 矩阵 · 平台能力 · 操作手册

> 功能 UI 复刻完成后，剩余为**真机环境**工作。本页说明如何可重复验证，以及哪些项是 Windows 平台边界而非缺陷。

## 1. DPI 布局矩阵（DIP → 物理像素）

主窗口 **1194×768 DIP**，Tray HUD **340×720 DIP**。

| Windows 缩放 | DPI | 主窗口物理 | HUD 物理 |
| --- | ---: | ---: | ---: |
| 100% | 96 | 1194×768 | 340×720 |
| 125% | 120 | 1493×960 | 425×900 |
| 150% | 144 | 1791×1152 | 510×1080 |
| 200% | 192 | 2388×1536 | 680×1440 |

代码源：`Ui/DpiScaleMatrix.cs`、`Ui/WindowSizing.cs`、`Ui/TrayHudLayout.cs`。  
单元测试：`DpiScaleMatrixTests`、`TrayHudLayoutTests`、`WindowSizingTests`。

### 混合 DPI 多显示器

- Tray HUD 在 `ShowNearAsync(x,y)` 时按**锚点所在显示器** DPI 计算客户区（`DisplayDpi.GetDpiForPoint` + `TrayHudLayout.ForAnchorPoint`）。
- 避免使用“上次 HUD 所在窗口”的 DPI 导致 125%/150% 混显错位。

## 2. 平台能力边界（诚实矩阵）

代码：`Services/WindowsPlatformCapabilities.cs`。

| Id | Mole | Windows | UI | Data |
| --- | --- | --- | --- | --- |
| fan-rpm | 风扇转速 / Auto·Cool·Max | unavailable | ✓ 分段展示 | — |
| battery-accessories | 配件电池 | unavailable | — | — |
| battery-main | 主机电池 | available | ✓ 环 + 中文状态 | ✓ |
| silent-app-updates | 静默更新列表 | unavailable | ✓ 安静空态 | — |
| startup-inventory | 启动项 | read-only | ✓ 列表 | ✓ |
| gpu-engine | GPU | best-effort | ✓ | 视计数器 |
| process-energy | PWR | proxy | ✓ CPU 代理 | ✓ |
| multi-monitor-dpi | HUD 锚定 | supported | ✓ | ✓ |

## 3. 本地命令

### 3.1 离线校验（CI / 无 UI）

```powershell
powershell -File scripts/verify-env-matrix.ps1
dotnet test Tests/WinMoe.Tests/WinMoe.Tests.csproj -c Release --filter "FullyQualifiedName~Dpi|FullyQualifiedName~TrayHudLayout|FullyQualifiedName~WindowSizing|FullyQualifiedName~WindowsPlatform"
```

### 3.2 当前缩放截图矩阵（需 GUI）

```powershell
# 首次构建并抓取五路由
powershell -File scripts/capture-dpi-matrix.ps1

# 已构建 + 含 HUD
powershell -File scripts/capture-dpi-matrix.ps1 -NoBuild -IncludeHud
```

输出：`artifacts/dpi-matrix/<timestamp>-dpi<DPI>/`

- `route-clean.png` … `route-status.png`
- `REPORT.md`（期望尺寸 + 勾选清单 + 能力边界）

### 3.3 完整 100/125/150/200 人工流程

Windows **不能**在无提权/无注销的情况下可靠切换系统缩放。完整矩阵步骤：

1. 设置 → 系统 → 显示 → 缩放与布局 → **100%**
2. 若提示注销则注销；重新登录
3. `powershell -File scripts/capture-dpi-matrix.ps1 -NoBuild`
4. 对照 `.research/mole-ui/*.jpg` 勾选 `REPORT.md` 清单
5. 对 **125% / 150% / 200%** 重复 1–4
6. 恢复日常缩放（常见 150%）

## 4. Reduce Motion

- 行星慢旋：`Ui/PlanetMotion.cs` 读取 `UISettings.AnimationsEnabled`
- 系统「显示动画」关闭时不启动旋转
- 真机：设置 → 辅助功能 → 视觉效果 → 关闭动画效果后进 Clean/Optimize，球体应静止

## 5. 验收勾选

- [x] DPI 数学矩阵四档单元测试  
- [x] 离线 `verify-env-matrix.ps1`  
- [x] 当前 DPI 截图脚本 + REPORT 模板  
- [x] HUD 锚点显示器 DPI  
- [x] 平台能力表代码化  
- [ ] 人工在本机完成 100/125/150/200 四档截图（需改系统缩放）  
- [ ] 混合 DPI 双显示器手测（外接屏）  

## 6. 与旧报告关系

`machine-test-report-2026-07-30.md` 记录了当时 100–200% 主窗口逻辑尺寸稳定与 HUD 修复。  
本页工具用于**后续回归**可重复抓取；新截图写入 `artifacts/`（gitignore），不提交专有 Mole 参考图。
