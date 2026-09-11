# CheatMode 与三个参考 mod 的差异对比报告（基准：各仓库已提交源文件）

> 对比对象（仅两方）：
> - **当前改动（分析主体）**：`CheatMode` v1.6.3（本仓库 `CheatMode/` 下的源码：`CheatMode.cs`、`CheatAmmoPatches.cs`、`CheatInvinciblePatches.cs`、`CheatESPPatches.cs`、`Render.cs`）。
> - **仓库源文件（基准，远端/已提交版本）**：以下三个 GitHub 仓库默认分支源文件（2026-09-08 快照），各锚定到其默认分支提交：
>   1. [`Bluehawk8908/InfiniteAmmo`](https://github.com/Bluehawk8908/InfiniteAmmo) @ `5ab5a00`（2026-05-31），`InfiniteAmmo.cs`，**v1.0.1**
>   2. [`QwertyRyo/Invincible-Tank`](https://github.com/QwertyRyo/Invincible-Tank) @ `979841b`（2026-06-20），`Invincible_Tank.cs`，**v1.0.0**
>   3. [`k4yt3x/GHPCESP`](https://github.com/k4yt3x/GHPCESP) @ `35a4c56`（2024-12-12），`src/GHPCESPMod.cs`、`src/Patches.cs`、`src/Render.cs`，**v1.2.0**

---

## 0. 对比基准与方法（重要，请先读）

### 0.1 对比双方
- 差异比对**只针对两方**：左侧为 CheatMode 的当前源码（本仓库内，随本发布提交），右侧为四个仓库**已提交（远端）** 的源文件。

### 0.2 为什么以“远端/已提交源版本”为基准
- 四个链接指向的仓库默认分支是社区可引用、可复现、可归因的公开事实，只有它们能作为 diff 的右侧基准。
- “当前改动”相对仓库源文件之间的差异，才是本报告要回答的问题：CheatMode 相对各上游**改了什么、加了什么、重写了什么**。

### 0.3 文件级 diff 方式
- 因 CheatMode 与各上游在文件划分、命名空间上不同（例如弹药逻辑分布在 `CheatMode.cs` 与 `CheatAmmoPatches.cs` 两个文件），本报告按**“上游文件/方法 → CheatMode 对应文件/方法”逐项映射**进行比对，并给出**净变化**（`一致` / `扩展` / `重写` / `新增` / `无对应`）。
- 每个 mod 一节的“差异比对结果”表即为该映射的产出；第 4 节为其汇总。

---

## 1. 与 InfiniteAmmo 的对比（无限弹药）— 基准 `InfiniteAmmo.cs` v1.0.1

**文件映射**：上游单文件 `InfiniteAmmo.cs` ↔ CheatMode `CheatMode.cs`（扫描/标记/补弹辅助）+ `CheatAmmoPatches.cs`（全部补丁）。净变化：**从“硬编码白名单 + 仅玩家补一发”全面重写为“通用阵营化全单位无限弹药”**。

### 1.1 上游 v1.0.1 的真实形态（已提交版）
- **硬编码车种白名单**：GameReady 后遍历所有 `Vehicle`，仅当 `UniqueName ∈ {M2BRADLEY, M2BRADLEY(ALT), BMP2_SA, BMP2, MARDERA1, MARDERA1PLUS, MARDER1A2, MARDERA1_NO_ATGM}` 时补弹夹并打标记；
- **仅玩家单位、仅换弹后补一发**：`AmmoFeed.FinishClipReload` Postfix 解析玩家单位，仅当换弹车 == 玩家当前单位时补回 1 个弹夹（对名为 `"Malyutka ammo"` 的架额外补 1 发）；
- **无配置、无友军、无步兵、无“无需装填”**。

### 1.2 差异比对结果（功能级）
| 上游 v1.0.1 功能点 | 对应文件/方法 | CheatMode 净变化 |
|---|---|---|
| `Chainguns` 白名单遍历（8 个车名） | `CheatMode.cs` `ApplyInfiniteAmmo/SeedUnit` | **重写**：阵营级联 + 排成员 + 架设归属，覆盖全阵营，不再依赖车名 |
| `InfAmmo` 标记防重复 | `CheatMarker`（+`Managed()` 判断） | **一致**（改名） |
| `FinishClipReload` 后补 1 发（仅玩家） | `CheatAmmoPatches.cs` `CheatModeReplenishPatch` | **扩展**：按“换弹前后数量差”补回实际消耗，绝不多补 |
| `"Malyutka ammo"` 名字特判 | — | **移除**（改为通用逻辑） |
| — | `AmmoFeedEnsureClipPatch`（`FeedNewClip` 前空架自愈） | **新增**（修复 AI 空架拒换弹哑火） |
| — | `NoReloadFeedNewClipPatch` / `NoReloadWeaponFiredPatch`（仅玩家载具，F9 开关） | **新增**（上游无“无需装填”） |
| — | `AmmoFeedDualFeedPatch`（`FeedNonSelectedClip`） | **新增**（双供弹补链） |
| — | `InfantryWeaponAmmoPatch` / `InfantryThrowableAmmoPatch` / `AmmoSupplies*Patch` | **新增**（步兵/投掷/阵地弹药箱） |
| 无配置 | `[CheatMode]`：`SelfInfiniteAmmo / FriendlyInfiniteAmmo / NoReload` | **新增**（玩家/友军分档可开关） |

### 1.3 相对上游 v1.0.1 的修复/改进
1. **车名白名单 → 通用阵营机制**：上游 8 个硬编码 `UniqueName` 遇新载具/新场景即失效；CheatMode 全覆盖且自动生效。
2. **仅玩家 → 玩家/友军可分离**：`SelfInfiniteAmmo`/`FriendlyInfiniteAmmo` 独立控制；“无需装填”仍只作用于玩家载具。
3. **空架卡死修复**：上游只对白名单车在换弹完成后补弹；CheatMode 的 `AmmoFeedEnsureClipPatch` 全量兜底。
4. **补弹记账化**：只补实际消耗、删除按架名特判，备弹不膨胀。
5. **行为真实性**：NoReload 仅玩家，友军 AI 保留原版装填节奏。

---

## 1. 与 Invincible-Tank 的对比（无敌）— 基准 `Invincible_Tank.cs` v1.0.0

**文件映射**：上游单文件 `Invincible_Tank.cs` ↔ CheatMode `CheatInvinciblePatches.cs`（+`CheatMode.cs` 中两个开关）。净变化：**从“单点 penCheck 吞弹”扩展为“18 点纵深防御 + Self/Friendly 独立开关”**。

### 1.1 上游 v1.0.0 的真实形态（已提交版）
- `Patch_MapController_InitControlState`（Postfix）：把玩家 `Allegiance` 字符串存入 `PlayerFactionStore.Name`；
- `Patch_LiveRound_PenCheck`（Prefix）：弹丸命中阵营==玩家阵营的单位时“否决”该发（写 3 个位置字段 + `AddEvent` + `reportShotTraceFrame`，返回 `false`）；
- **无 Self/Friendly 之分、无配置、无组件/舱室/乘员/步兵补丁**。

### 1.2 差异比对结果（功能级）
| 上游 v1.0.0 功能点 | 对应文件/方法 | CheatMode 净变化 |
|---|---|---|
| `PlayerFactionStore`（一次性字符串） | `CheatMode.cs` 场景扫描里的权威阵营级联 + `CheatDamageFilter` | **重写**：权威阵营级联 + 每事件实时解析 |
| `MapController.InitControlState` Postfix | — | **无对应**（不依赖 UI 初始化时机） |
| `penCheck` Prefix 吞弹 | `CheatInvinciblePatches.cs` `Patch_LiveRound_PenCheck` | **扩展**：同一否决点，但补写 `_frameData/_armed/_impactTime/_impactObject/_materialHit/_impactSkipDecal/_heHitNormalRha` 等，HE/HEAT 引信与特效/AAR 更完整 |
| 保护“阵营==玩家”全体 | `CheatDamageFilter.IsProtectedUnit` | **重写**：先判玩家自身（`SelfInvincible`），再判友军（`FriendlyInvincible`），两档独立 |
| — | `Patch_DestructibleComponent_ApplyProjectileDamage`(+float/Overpressure/BlastShock) | **新增**（4 个补丁点） |
| — | `Patch_Compartment_NotifyPenetrated/InsertOverpressure` | **新增** |
| — | `Patch_Human_Kill/SetPartHealth`、`Patch_AntiPersonnelGrenade*` | **新增** |
| — | `Patch_InfantryUnit_OnCollisionWithVehicle/GoreOnLimbDismembered` | **新增** |
| — | `Patch_CrewManager_KillAllRemainingCrew` | **新增** |
| — | `Patch_Unit_NotifyDestroyed/NotifyIncapacitated` | **新增**（最终兜底） |
| 无配置 | `SelfInvincible` / `FriendlyInvincible` | **新增** |

### 1.3 相对上游 v1.0.0 的修复/改进
1. **纵深防御**：上游只有 `penCheck` 一处；绕过它的伤害路径（超压直插舱室、`Human.Kill`、碾压、断肢、`KillAllRemainingCrew`、`NotifyDestroyed` 等）上游下仍会杀死友军，CheatMode 全部封住并以“绝不被标记摧毁/失能”兜底。
2. **Self/Friendly 分离**：支持“自己脆皮、AI 无敌”等组合。
3. **否决弹状态完整还原**：HE/HEAT 仍在友军车体外正常起爆、命中记录/AAR 完整。
4. **可维护性**：强类型 `FieldRef`/`TargetMethod` 集中判定，替代上游按名字反射。

---

## 1. 与 GHPCESP 的对比（ESP）— 基准 `src/GHPCESPMod.cs` 等 v1.2.0

**文件映射**：上游 3 文件（`GHPCESPMod.cs` + `Patches.cs` + `Render.cs`）↔ CheatMode `CheatMode.cs`（ESP 绘制部分）+ `CheatESPPatches.cs` + `Render.cs`。净变化：**结构最接近，属加固 + 按键收敛 + 信息增强**。

### 1.1 共同点
包围盒 + 阵营着色 + 摧毁置灰 + 底线 + 分辨率缩放 + `Unit.Start/InfantryUnit.Start` 收集。

### 1.2 差异比对结果（功能级）
| 上游 v1.2.0 功能点 | 对应文件/方法 | CheatMode 净变化 |
|---|---|---|
| 载具/ATGM ESP（F8 开关） | `CheatMode.cs` `DrawUnitsESP(Units)` | **扩展**：并入总开关 F8 |
| 步兵 ESP（F9 开关） | `DrawUnitsESP(InfantryUnits)` | **扩展**：并入总开关 F8 |
| 双开关 F8/F9 | `OnUpdate` 键位处理 | **重写**：F8=ESP 总开关；F9 让位给“无需装填” |
| 屏幕标题两个 | `OnGUI` | **收敛**：一个 “CheatMode ESP Enabled” |
| 标签无距离 | `DrawUnitsESP` 标签构造 | **新增**：追加 `[距离m]` |
| `MainCam` 无判空 | 绘制入口 | **修复**：判空防御 |
| 单位清理双循环 | `OnUpdate` | **收敛**：统一循环 |

### 1.3 相对上游 GHPCESP 的修复/改进
空引用防御、按键收敛（两种 ESP 合并到 F8）、标签带距离、自清理循环统一。

---

## 1. CheatMode 相对三个上游的整体净变化汇总

| 子系统 | 上游基准 | 净变化 |
|---|---|---|
| 弹药 | InfiniteAmmo v1.0.1 | 全面重写：白名单+玩家补一发 → 阵营化全单位无限弹药 + 仅玩家无需装填 + 空架自愈 + 记账补弹 |
| 无敌 | Invincible-Tank v1.0.0 | 大幅扩展：单点 penCheck → 18 点纵深防御 + Self/Friendly 开关 + 完整命中状态还原 |
| ESP | GHPCESP v1.2.0 | 加固 + 收敛：判空、单总开关、距离标签 |

贯穿性新增（各上游均无）：单 DLL 单实例整合、统一 `[CheatMode]` 配置、权威阵营级联、AAR 回放保护、统一前缀日志。

---

## 5. 局限与诚实说明（相对已提交源版本）

- **无** GHPCESP 的 F8/F9 双 ESP 独立开关（CheatMode 刻意合并）。
- 属作弊/单机向：未做多人公平性处理。
- 若上游日后发布新版本，需按新提交重新核对此报告（基准 commit 见文件头）。
