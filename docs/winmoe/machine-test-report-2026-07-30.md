# WinMoe Windows 真机 UI 与功能矩阵报告

- 日期：2026-07-30
- 环境：Windows 11 Pro 10.0.28000，x64，3840 × 2160
- 默认显示缩放：150%
- .NET Desktop Runtime：x64 8.0.26，x86 8.0.29

## 1. 总体结论

| 判定 | 结果 | 说明 |
| --- | --- | --- |
| 安全继续开发 | GO | 真机 UI、文件系统保护、受控删除、空间分析、垃圾预览、应用枚举、MCP 与多架构产物均有可重复证据 |
| 作为“预览与分析工具”交付测试版 | GO | 分析、遥测、应用枚举、Purge/Installer 预览和安全保护可用 |
| 宣称所有清理功能已完整可执行 | NO-GO | Clean/Optimize 的 MCP 与主 UI 仍遵循预览优先协议，尚未连接完整 operation-plan 执行链 |
| 面向所有 Windows 架构公开发布 | NO-GO | ARM64 仅完成交叉构建和 PE 校验，尚缺 ARM64 真机；混合 DPI 多显示器仍需专项验证 |

结论摘要：

- **能够真正删除**：生产删除服务已在随机受控夹具上将旧安装包、项目构建垃圾和 LocalAppData 残留移入 Windows 回收站，外部哨兵文件保持不变。
- **能够真正分析**：Unicode、空格、超 260 字符长路径、锁定文件及重解析点边界均已覆盖；真机 UI 与 MCP 对 102400 字节夹具返回一致结果。
- **能够查找垃圾**：Purge 真机发现 1 个项目、1 个 `node_modules` 构建垃圾，约 6.4 MB；Installer 默认扫描发现 10 个候选，共约 590.2 MB。两者均只预览，未删除真实用户数据。
- **能够枚举软件**：从 HKLM/HKCU 的 32 位和 64 位注册表视图枚举 93 个应用；缺失 `EstimatedSize` 时不再递归扫描大型安装目录。

## 2. 安全边界

本次真实删除仅针对随机生成的测试夹具。以下操作没有执行：

- 未清空回收站；
- 未删除真实用户垃圾；
- 未启动任何第三方卸载器；
- 未在 UI 确认框中点击最终删除；
- 未对真实系统目录执行 Clean/Optimize。

UI 中的 Installer 与 Purge 删除确认框均已打开并验证文案，随后点击取消。

早期重解析点 RED 测试曾把 2 个随机 junction 条目本身移入回收站；其指向的外部哨兵均未受影响。独立 machine-delete 测试又产生 3 个可恢复的随机测试条目。没有用户文件被删除，回收站未清空。

用于 UI 矩阵的随机夹具仍位于
`%TEMP%\WinMoeUiMatrix-720c9fb9800245b58969118c0b73f41f`。收尾时的递归删除被执行环境安全策略拦截，未尝试绕过；该目录只包含本次生成的分析和安装包测试文件。

## 3. 真机 UI 矩阵

### 3.1 主窗口 DPI

| Windows 缩放 | 主窗口逻辑截图尺寸 | 页面覆盖 | 结果 |
| --- | ---: | --- | --- |
| 100% | 1180 × 761 | Clean、Apps、Optimize、Analyze、Status、Settings | 通过 |
| 125% | 1182 × 762 | Clean、Apps、Optimize、Analyze、Status、Settings | 通过 |
| 150% | 1182 × 762 | Clean、Apps、Optimize、Analyze、Status、Settings | 通过 |
| 200% | 1183 × 763 | Clean、Apps、Optimize、Analyze、Status、Settings | 通过 |

窗口目标尺寸以 DIP 表达并按当前 DPI 转换为物理像素，因此四档缩放下逻辑尺寸保持稳定。

系统最终已恢复为：

- 显示缩放 150%；
- Windows 动画效果开启。

### 3.2 页面与交互

| 功能 | 真机结果 |
| --- | --- |
| Status | CPU、内存、磁盘、网络和进程实时刷新 |
| History | 1 小时范围显示 70 个样本 |
| Activity | 显示最近 50 条操作 |
| Apps | 约 1.4 秒载入 93 个应用；错误的 323.3 GB 应用大小已消失 |
| Analyze | 受控目录显示 2 个文件夹、100 KB，总量与夹具一致 |
| Installer | 仅识别 45 天前的 24 KB MSI；当天 8 KB EXE 被正确排除 |
| Purge | 找到 1 个项目和 1 个约 6.4 MB 的 `node_modules` |
| Settings | 回环 REST 为 `127.0.0.1:9277`，破坏性 MCP 操作关闭 |
| Reduced motion | 关闭动画后页面导航和 Optimize 滚动正常；测试后已恢复 |
| 键盘 | `Return` 导航和 `Escape` 关闭弹窗有效；UI Automation 对 Tab 焦点的报告不稳定，不能据此宣称完整无障碍键盘矩阵通过 |

### 3.3 托盘 HUD 缺陷与修复

首次测试时，HUD 在 150% 缩放下只有约 275 × 567 逻辑像素，底部导航虽然存在于 UI Automation 树中，但没有可点击边界。

根因是 XAML 使用 DIP，而 `AppWindow` 和 `SetWindowPos` 使用物理像素；430 × 860 被错误地直接当成物理像素。

修复后：

- 144 DPI 下目标客户区为 645 × 1290 物理像素；
- 真机截图为 433 × 891 逻辑像素（包含标题栏）；
- “高占用进程”和六个底部导航按钮全部可见；
- “历史”按钮已真实点击，主窗口成功切换至历史页；
- 最终置顶调用使用 `SWP_NOSIZE`，不再覆盖已换算的尺寸。

## 4. Windows 文件系统矩阵

### 4.1 删除保护

生产删除服务拒绝：

- 相对路径；
- UNC 和设备路径；
- 盘符根目录；
- 未解析环境变量；
- 文件、目录及祖先链上的重解析点；
- 扫描后大小或修改时间发生变化的安装包。

真实回收站集成测试证明：

- 旧 Unicode 安装包可删除；
- `node_modules` 可删除；
- 专用 LocalAppData 子目录残留可删除；
- 夹具目录之外的哨兵文件保持存在。

### 4.2 空间分析

覆盖结果：

- 中文、希腊字母和空格路径；
- 超 260 字符路径；
- 被占用文件；
- 重解析根目录拒绝；
- 子目录、文件和嵌套 artifact 重解析点跳过；
- 总大小与可见子项排序正确。

### 4.3 应用和残留

- 注册表矩阵：HKLM/HKCU × Registry64/Registry32；
- 共枚举 93 个应用；
- 缺失 `EstimatedSize` 时返回 Unknown/0，不再递归测量整个安装目录；
- 残留预览跳过重解析点；
- 第三方卸载器必须经显式确认且设置允许，本次未启动。

## 5. MCP/REST 功能矩阵

服务仅监听回环地址 `127.0.0.1:9277`。

| 功能 | 结果 |
| --- | --- |
| `/health` | `ok=true`，引擎可用 |
| `/snapshot` | Windows 原生遥测返回正常 |
| `/metrics?limit=3` | 返回 3 个历史样本 |
| `/tools` | 13 个 WinMoe 工具 |
| JSON-RPC `tools/list` | 13 个工具，无错误 |
| clean preview | HTTP 200，`dry_run=true`，成功 |
| clean `confirm=true` | 明确返回 `supported=false` |
| optimize preview | HTTP 200，`dry_run=true`，成功 |
| optimize `confirm=true` | 明确返回 `supported=false` |
| history/top/process usage | 均返回 5 条结果 |
| analyze | Unicode 路径正确，102400 字节，2 个子目录 |
| list_apps | 93 个应用 |
| purge `confirm=true` | 仍为 preview，MCP removal 不支持 |
| installer `confirm=true` | 仍为 preview，MCP removal 不支持 |
| uninstall list | 93 个应用 |
| preview_leftovers | 只读调用成功 |
| launch_uninstaller `confirm=false` | 被安全门拒绝，未启动进程 |

PowerShell 客户端必须将含 Unicode 路径的 JSON 请求体明确编码为 UTF-8。使用旧式默认编码会把中文变为 `??`；UTF-8 请求已通过，这属于客户端编码边界而非分析器路径缺陷。

## 6. 架构矩阵

| RID | WinMoe.exe | Mole `mo.exe` | MCP stdio | 真机状态 |
| --- | --- | --- | --- | --- |
| win-x64 | PE `0x8664` | PE `0x8664` | PE `0x8664` | 完整 UI、MCP 和功能矩阵通过 |
| win-x86 | PE `0x014C` | PE `0x014C` | PE `0x014C` | 在 x64 Windows/WoW64 上启动成功；遥测、Unicode 分析和 clean dry-run 通过 |
| win-arm64 | PE `0xAA64` | PE `0xAA64` | PE `0xAA64` | 交叉构建和 PE 校验通过；缺 ARM64 真机运行 |

x86 复核：

- x86 Desktop Runtime 8.0.29 已安装；
- 原框架依赖 x86 产物可正常打开 WinMoe；
- x86 进程的 `/health`、`/snapshot`、Unicode analyze 和 clean dry-run 全部通过；
- 另已生成 x86 self-contained 测试产物，但框架依赖版已经足以证明当前机器的 runtime 与 WoW64 链可用。

解决方案新增 Debug/Release x64 配置。主项目映射 x64，测试与托管工具保持 Any CPU；解决方案级 x64 构建已通过。

三架构产物目录：

`artifacts/final-arch-matrix/run-20260730-002`

## 7. 自动化证据

| Gate | 结果 |
| --- | --- |
| Release 单元/集成测试 | 143/143，通过，0 失败，0 跳过 |
| machine-delete opt-in | 单独运行 1/1，通过 |
| `scripts/validate-source.ps1` | 16 个 XML/XAML、15 个资源、旧命名移除和 P0 安全门通过 |
| `WinMoe.sln -c Release -p:Platform=x64` | 0 警告，0 错误 |
| `WinMoe.sln -c Debug -p:Platform=x64` | 0 警告，0 错误 |
| `git diff --check` | 通过；仅有仓库现存 LF→CRLF 提示 |

最终 TRX：

`artifacts/test-results/winmoe-tests-final.trx`

## 8. 本轮修复

1. operation plan 拒绝相对、UNC、设备和危险根路径。
2. 回收站删除拒绝环境变量残留、根目录和重解析点。
3. Installer 删除前重新校验大小和修改时间。
4. Disk Analyzer 和 Purge 跳过所有重解析边界。
5. 应用枚举覆盖 32/64 位注册表，避免未知大小的深度递归。
6. MCP clean/optimize 强制预览；Purge/Installer MCP 不支持真实删除。
7. x64/x86/ARM64 MCP stdio 子产物按 RID 隔离输出。
8. 主窗口 DPI 尺寸修复、Optimize 滚动和关键自动化名称补齐。
9. 托盘 HUD 的 DIP/物理像素尺寸及定位修复。
10. 解决方案级 x64 配置补齐。

## 9. 下一阶段开发规划

### P0：完整清理执行协议

目标：让 Clean/Optimize 从“安全预览”升级为“可审计执行”。

1. 定义结构化 `OperationPlan`：来源、目标、预计释放空间、风险级别、可恢复性。
2. 预览与执行之间重新验证路径、重解析点、大小、时间戳和文件身份。
3. 默认使用回收站；不可恢复操作必须单独分组并二次确认。
4. 每项执行写入 operation history，包含成功、跳过、失败及原因。
5. 为执行中断、文件锁、权限不足和 TOCTOU 变化增加集成测试。

验收门：

- 受控夹具 clean/optimize 真实执行通过；
- 外部哨兵和重解析目标零变化；
- UI 显示精确计划、结果和可恢复性；
- MCP 保持 preview-only，直到另行定义可信确认协议。

### P0：ARM64 真机

1. 在 Windows on ARM 设备启动主程序、Mole 和 MCP stdio。
2. 验证注册表双视图、Program Files 路径、遥测 Performance Counter 和回收站 COM。
3. 运行相同 Unicode/长路径/reparse/locked-file 夹具。
4. 对安装器升级、卸载和签名进行 ARM64 专项验证。

### P1：混合 DPI 多显示器

1. 覆盖 100% 主屏 + 150%/200% 副屏。
2. HUD 定位 DPI 应以目标显示器而非当前窗口显示器为准。
3. 验证负坐标显示器、任务栏位于上/左侧，以及小工作区。
4. 工作区不足 860 DIP 时增加可滚动降级布局。

### P1：卸载、启动项和更新

1. 卸载器只负责启动厂商命令，残留清理保持独立预览和确认。
2. Startup 管理需要保留原始注册表值和恢复记录。
3. Updates 需要可信清单、签名验证、原子替换和回滚。

## 10. 发布判定

当前适合作为 **Windows 预览/分析测试版** 继续开发和内测。

在以下条件满足前，不应宣传为“完整一键清理工具”：

- Clean/Optimize 的 operation-plan 执行闭环完成；
- ARM64 真机矩阵完成；
- 混合 DPI 多显示器完成；
- 卸载、启动项和更新功能达到同等级安全门；
- 对真实发布安装器完成签名、升级和回滚验证。
