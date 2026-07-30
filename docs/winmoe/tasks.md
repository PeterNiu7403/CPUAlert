# WinMoe 任务与进度

> 本文件是项目进度的唯一真实来源；每个任务完成后立即更新。

## 进度总览

| 指标 | 数值 |
|---|---:|
| 总任务数 | 8 |
| 已完成 | 6 (75%) |
| 待执行 | 2 (25%) |
| 最后更新 | 2026-07-30 14:55 CST |

## 任务

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
