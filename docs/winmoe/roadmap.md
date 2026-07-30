# WinMoe 下一阶段开发路线

> 目标版本：`v0.1.0-preview.1`
> 当前判断：开发 **Go**，公开发布 **No-Go**
> 最后更新：2026-07-30 15:12 CST

## 1. 当前基线

本轮已完成本地工程的 `MoleWindows` → `WinMoe` 品牌迁移，包括解决方案、
程序集、命名空间、测试、XAML 资源、MCP 工具、数据目录、安装器和发布脚本。
旧 `%LOCALAPPDATA%\MoleWindows` 数据会按文件迁移到 `%LOCALAPPDATA%\WinMoe`，
迁移失败时继续使用旧文件，避免静默丢失设置和历史。

当前验证证据（本轮工作树尚未提交，以下是开发证据，不是不可变的发布证据）：

| 检查 | 命令或来源 | 结果与产物 |
|---|---|---|
| 上游一致性 | `git fetch origin`、`git ls-remote origin refs/heads/main` | 本地 HEAD、`origin/main`、远端 main 均为 `9679ecf96f5107a636743c71231d20956a076d46` |
| 工作树状态 | `git status --short` | 本地 WinMoe 迁移改动未提交；发布候选必须在提交后重新采证 |
| 静态门禁 | `powershell -File scripts/validate-source.ps1` | 16 个 XML/XAML、15 个资产、旧工程移除和 P0 安全门禁通过 |
| Release build | `dotnet build WinMoe.csproj -c Release -p:Platform=x64` | .NET SDK 8.0.423，Windows 10.0.28000 x64，0 warning / 0 error |
| 测试 | `dotnet test Tests/WinMoe.Tests/WinMoe.Tests.csproj -c Release --logger trx` | `106/106` 通过；`artifacts/test-results/winmoe-tests.trx` |
| publish | `dotnet publish WinMoe.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained false` | portable win-x64 payload 生成成功 |
| 依赖漏洞 | `dotnet list WinMoe.sln package --vulnerable --include-transitive` | 2026-07-30 查询 NuGet 源；四个项目均未发现已知易受攻击包 |
| 真机 smoke | `run-local.ps1 -NoBuild -SmokeTest -NoTray -Route status -RequireHealth ...` | 窗口、`status` 路由和 HTTP health 通过；启动日志与 smoke PNG 已保留 |
| 视觉证据 | 人工查看 `artifacts/smoke/status-final.png` | 状态页内容可见，但捕获区域包含窗外内容且 viewport 有裁切；只能作为 smoke 辅助，不计作任务 7 矩阵证据 |

仍然存在的发布阻塞：

1. 五路由 × 四档 DPI、键盘、托盘和 Reduce Motion 尚未完成真机矩阵验收；
2. 当前安装包未签名、未验证可信时间戳；
3. GitHub 远端仓库仍名为 `CPUAlert`，发布 URL 尚未切换；
4. `WinMoe` 的商标、公开名称和分发权限尚未形成可审计结论。

## 2. 推荐设计：ReleaseReadiness deep module

下一阶段不继续把规则堆进 PowerShell，而是新增独立的
`ReleaseReadiness` module。它位于产品进程之外，调用方只提出“候选版本是否
就绪”，module 在 seam 后统一 Requirement、Scenario、Evidence、ReleaseGate
和 GoNoGo。

建议的唯一入口：

```csharp
public interface IReleaseReadinessService
{
    Task<ReleaseReadinessReport> EvaluateAsync(
        ReleaseCandidate candidate,
        ReadinessProfile profile,
        CancellationToken cancellationToken = default);
}
```

核心不变量：

1. `Go` 只能由规则聚合产生，adapter 不得直接宣布 Go；
2. 每个必需的 Passed gate 必须引用持久化 Evidence；
3. `Failed` 表示产品或产物不合格，`Blocked` 表示无法取得必需证据，
   两者都产生 No-Go；
4. 截图必须记录实际 DPI，单纯“生成了 PNG”不能证明 DPI 场景通过；
5. 签名、时间戳、GitHub 和 WinGet 等 true external 检查失败时不得降级为通过；
6. 商标和分发权属于人工 Evidence，不能由代码猜测。

### 依赖规则

- `Domain/` 与 `Core/` 只能引用 BCL 和自身类型，不得引用 WinUI、脚本、网络、
  系统时钟、进程或文件系统；
- adapters 只采集和持久化原始事实，例如命令输出、UIA 树、截图、DPI、签名链，
  不得把事实预先解释为 Go；
- 只有 `ReleaseGatePolicy` 能把 Evidence 判定为 Passed、Failed、Blocked 或
  Skipped，并由聚合器产生最终 Go/No-Go；
- PowerShell、WinUI 和 CI 只能依赖公开入口或报告 schema，不得绕过 policy
  直接写最终结论。

### 依赖与 adapter

| 类别 | module 后的实现 |
|---|---|
| in-process | Requirement/Scenario catalog、gate policy、GoNoGo 聚合、报告生成 |
| local-substitutable | `dotnet`、文件、`run-local.ps1`、UI Automation、截图、DPI/动画设置读取 |
| true external | Authenticode/时间戳、GitHub Actions/Release、WinGet、人工品牌批准 |

建议落地结构：

```text
Tools/ReleaseReadiness/
  WinMoe.ReleaseReadiness.csproj
  Domain/
  Core/
  Adapters/
Tests/WinMoe.ReleaseReadiness.Tests/
scripts/check-release-readiness.ps1
artifacts/release-readiness/
  report.json
  report.md
  evidence/
```

PowerShell 只保留为薄入口和已有脚本 adapter；release policy 不放进 WinUI
产品进程，避免产品运行时反向依赖签名、GitHub 或 WinGet。

### 初始 gate catalog

| Requirement | Scenario / profile | 必需 Evidence | 通过标准 | 稳定 failure code |
|---|---|---|---|---|
| RQ-1 | `shell_routes` / LocalUi | 五路由 UIA + WGC 截图 | 标题、选中导航、主要操作与截图全部匹配 | `UI_ROUTE_MISMATCH` |
| RQ-1 | `dpi_matrix` / LocalUi | 20 组 UIA、截图、`GetDpiForWindow` | 实际为 100/125/150/200%，控件不溢出 | `DPI_LAYOUT_INVALID` |
| RQ-1 | `reduce_motion` / LocalUi | `UISettings.AnimationsEnabled` + UIA/WGC | 关闭动画时所有状态仍可理解和操作 | `MOTION_STATE_INVALID` |
| RQ-2 | `tray_shared_snapshot` / LocalUi | 主状态、托盘、HUD 的 snapshot id/value | 三处读取同一采样结果 | `TRAY_SNAPSHOT_DIVERGED` |
| RQ-3 | `safety_plan` / 两 profile | 单元测试 TRX + 运行时负向日志 | 未确认、过期、变化、拒绝、取消均安全失败 | `SAFETY_CONTRACT_FAILED` |
| RQ-4 | `apps_disk` / LocalUi | UIA 树、扫描日志、受控样本 | 应用清单和磁盘结果可见且可追踪 | `FEATURE_EVIDENCE_MISSING` |
| RQ-5 | `build_publish` / ReleaseCandidate | validator/build/test/publish 日志与 hash | 全部成功且 commit、产物相互匹配 | `BUILD_EVIDENCE_INVALID` |
| RQ-5 | `provenance` / ReleaseCandidate | 第三方声明、许可证、vendored-tree 报告 | 来源和本地修改被准确声明 | `PROVENANCE_INVALID` |
| RQ-5 | `signed_distribution` / ReleaseCandidate | 签名链、RFC3161 时间戳、installer/hash/WinGet | 所有目标签名有效且 hash 在签名后生成 | `SIGNATURE_INVALID` |
| RQ-5 | `brand_approval` / ReleaseCandidate | 项目所有者签署的 `approved.json` | 名称、商标、分发权、仓库 URL 均明确批准 | `BRAND_APPROVAL_MISSING` |

## 3. 实施阶段

### M0：收口品牌迁移

预计：0.5 天。

- 保持 `molewindows.exe`、`molewindows_cli` 为外部 Mole conductor 协议；
- 保持 `MOLEWINDOWS_*` 仅作为兼容回退，新增流程统一使用 `WINMOE_*`；
- 经项目所有者确认后，再把 GitHub 仓库改名为 `winmoe`，随后更新
  `origin`、安装器链接和 release/WinGet URL；
- 将品牌残留扫描加入静态门禁，只允许历史说明、兼容键和外部协议白名单。

验收：源码中不存在未分类的旧品牌；GitHub 改名属于独立外部操作，未确认前
不得把发布 URL 指向尚不存在的仓库。

远端改名 runbook：

1. 取得项目所有者明确批准，检查 `winmoe` 名称可用、无进行中的 release，
   并记录当前仓库名、默认分支、保护规则和 remote；
2. 在 GitHub 执行仓库改名，随后运行
   `git remote set-url origin https://github.com/PeterNiu7403/winmoe.git`；
3. 更新 README、安装器、release/WinGet、徽章和 CI 中的 canonical URL；
4. 用 `git ls-remote origin refs/heads/main`、GitHub Actions、旧 URL 重定向和
   新 release URL 做回读验证；
5. 如果权限、Actions 或发布链接失效，先恢复原仓库名与本地 remote，再根据
   记录逐项回滚 URL。公开发布脚本在最终 URL 验证前保持 Blocked。

### M1：实现 readiness 核心

预计：1–2 天。

- 建立 Requirement、Scenario、Evidence、ReleaseGate、GoNoGo 类型；
- 实现 `ReleaseGatePolicy`，先用 fake Evidence 测试聚合；
- 支持 `LocalUi`、`ReleaseCandidate` 两个 profile；
- 同时输出 JSON 与 Markdown 报告；
- 固定 `Failed / Blocked / Skipped` 语义和稳定 failure code。

验收：无 Windows UI、无网络时也能用测试 adapter 完整证明 Go/No-Go 规则；
任何必需 gate 缺证据时必须 No-Go。

### M2：修复并接入本地证据采集

预计：2–3 天。

- 把 `validate-source.ps1`、build/test/publish 和 `run-local.ps1` 包装成 adapters；
- 为五个主路由生成独立启动日志、health 结果和截图；
- 替换或增强当前 `CopyFromScreen`：优先评估 Windows Graphics Capture，
  并对截图做尺寸、方差、结构或 UIA 对照校验；
- 接入 UI Automation，验证页面标题、选中导航、主要操作和可访问名称；
- 报告中记录 commit、版本、机器、OS、分辨率、实际 DPI 和产物 hash。

验收：截图不能只因文件存在就通过；每张图必须能与对应 route 和 UIA
快照关联，失败时保留日志和 failure code。

证据实现约束：

- UI 结构 provider 使用原生 UI Automation 3 COM `IUIAutomation`；只有在
  spike 证明不可用时才允许替换，并记录原因；
- 截图使用 Windows Graphics Capture；实际缩放由目标 HWND 的
  `GetDpiForWindow(hwnd) * 100 / 96` 计算；
- Reduce Motion 状态读取
  `Windows.UI.ViewManagement.UISettings.AnimationsEnabled`；
- 截图尺寸与窗口矩形误差不超过 2 px，根 UIA 元素必须匹配目标进程和 HWND；
- UIA 必须匹配预期页面标题、选中导航，并证明至少 3 个必需控件边界落在
  viewport 内；
- PNG 必须可解码，灰度标准差至少 8、边缘像素比例至少 0.5%；至少 3 个 UIA
  控件裁剪区域的边缘像素比例至少 0.3%。阈值校准完成前，任务 7 仍需人工
  `approved.json`，自动指标不能单独宣布通过；
- 产物按
  `{commit}/{os-build}/dpi-{scale}/motion-{on|off}/{route}/{timestamp}.{png,uia.json,log}`
  命名；采证时间必须晚于构建，且进程、commit 和产物 hash 必须一致。

### M3：完成任务 7 真机矩阵

预计：3–5 个工程日，另预留 1–3 个日历日用于 VM/profile 设置和人工复核。

| 场景 | 必需证据 |
|---|---|
| `clean/apps/optimize/analyze/status` | 页面标题、选中导航、主要操作可见、截图 |
| 100/125/150/200% DPI | 实际 scale 读数、五路由无遮挡证据 |
| 键盘 | Tab 顺序、焦点可见、Enter/Space 可触发、Esc 可关闭临时层 |
| Reduce Motion | 系统设置读数、无持续动画依赖、状态仍可理解 |
| 托盘 HUD | 图标、菜单、HUD 打开/关闭、与主状态同源 |
| 安全动作 | 未确认、过期、路径变化、权限拒绝和取消均被拒绝或可追踪 |

DPI 和 Reduce Motion 不建议在开发者主会话中强制切换。优先使用独立 Windows
测试账户或 VM profile，由 adapter 读取真实系统状态并生成 Evidence。

验收：`LocalUi` profile 返回 Go；20 个“路由 × DPI”组合均有可判读证据，
键盘、Reduce Motion 和托盘 gate 全部 Passed。

### M4：完成任务 8 发布链

预计：2–4 个工程日；证书采购、硬件令牌/云签名开通和商标审查按外部实际
lead time 另计。

- 在 `build-release.ps1` 中加入显式签名阶段；
- 签名自有 `WinMoe.exe`、`WinMoe.dll`、
  `Assets/Mcp/winmoe-mcp-stdio.exe|dll`，以及实际随包存在的
  `Assets/Mole/mo.exe|dll`；再生成 ZIP 和 Inno installer，最后签名 installer；
- 签名和可信时间戳验证通过后再生成 SHA256 与 WinGet manifest；
- 验证 installer、ZIP、hash、release notes 和 WinGet URL 指向同一版本；
- 保存 Authenticode 证书链、时间戳、GitHub Actions 和 WinGet 验证 Evidence；
- 由项目所有者提供 `WinMoe` 名称/商标/分发权的人工批准记录。

验收：`ReleaseCandidate` profile 返回 Go；未签名、时间戳缺失、commit 不一致、
URL 不存在或人工批准缺失均必须 No-Go。

签名输入和顺序：

1. 从 Windows Certificate Store 的 thumbprint 或外部签名 provider 发现证书；
   PFX 和密码不得进入仓库。RFC3161 HTTPS 时间戳 URL 必须显式传入；
2. 发现并记录 Windows SDK `signtool.exe` 与 Inno Setup `ISCC.exe` 版本，同时
   要求最终 HTTPS 仓库 URL 和项目所有者批准文件；
3. 先签名所有自有 PE 文件并逐个运行 `signtool verify /pa /all /v`，再生成 ZIP；
4. 生成 Inno installer、签名 installer，再用相同命令验证；
5. 同时要求 `Get-AuthenticodeSignature` 返回 `Valid` 且
   `TimeStamperCertificate` 非空；最后才生成 SHA256 和 WinGet manifest。

缺少工具、证书、时间戳服务、最终 URL 或人工批准属于 `Blocked`；签名无效、
证书链不可信、时间戳缺失或 hash 不一致属于 `Failed`。两者都必须 No-Go。

### M5：自动化与长期维护

预计：1–2 天。

- PR CI 运行 in-process 和无副作用 local gates；
- 真机 UI matrix 由专用 runner 或发布操作员执行；
- tag release workflow 只消费已通过且 commit/hash 匹配的 readiness report；
- 将报告、TRX、截图、UIA snapshot、签名信息作为 release Evidence 归档。

## 4. 优先级

1. P0：readiness 核心与不可伪造的 Go/No-Go 规则；
2. P0：修复截图证据并完成五路由基本 UIA smoke；
3. P0：DPI、键盘、Reduce Motion、托盘真机矩阵；
4. P0：签名、时间戳、品牌人工批准和发布 URL；
5. P1：GitHub/WinGet 外部 adapter 与专用 Windows runner；
6. P2：x86/ARM64 真机运行验证；当前只要求保留工程配置。

## 5. 当前 Go/No-Go

- 下一步开发：**Go**。代码基线、Release build、106 项测试、publish 和基本启动
  smoke 均已通过。
- 任务 7：**No-Go**。单张 smoke 截图尚未满足窗口边界和 viewport 完整性，
  DPI/键盘/Reduce Motion/托盘矩阵也未完成。
- 任务 8 / 公开发布：**No-Go**。安装包未签名，远端仍为 `CPUAlert`，品牌与
  分发批准尚未完成。
