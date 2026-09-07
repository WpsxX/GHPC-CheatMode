# CheatMode for Gunner, HEAT, PC!

一个整合版 [Gunner, HEAT, PC!](https://store.steampowered.com/app/1705180/Gunner_HEAT_PC/) MelonLoader 作弊模组：把 **无敌、无限弹药 / 无需装填、无限与秒达火力支援、ESP** 合并进 **一个 DLL**，并加入 **「玩家 / 友军」粒度控制** 与 **火炮阵营识别伤害**，同时刻意让**敌方单位与脚本剧情保持原版行为**。

本项目整合并加固了四个社区 mod 的思路：[GHPC_Artillery_Rework](https://github.com/QwertyRyo/GHPC_Artillery_Rework) · [InfiniteAmmo](https://github.com/Bluehawk8908/InfiniteAmmo) · [Invincible-Tank](https://github.com/QwertyRyo/Invincible-Tank) · [GHPCESP](https://github.com/k4yt3x/GHPCESP)。详细对比见 [COMPARISON.md](docs/COMPARISON.md)。

> ⚠️ **作弊模组**：仅供单人 / 私人测试使用，请勿用于竞技类多人。

> 🌐 **双语阅读器：** 想在同一页面内切换中英文而不跳转？打开交互式 **[README.html](docs/README.html)**（切换语言时自动保持阅读位置）。或切换至 [English README](README.md)。

---

## 目录

1. [功能一览](#1-功能一览)
2. [安装](#2-安装)
3. [快捷键](#3-快捷键)
4. [功能参考与配置](#4-功能参考与配置)
5. [使用注意事项](#5-使用注意事项)
6. [问题排查](#6-问题排查)
7. [从源码构建](#7-从源码构建)
8. [许可证与致谢](#8-许可证与致谢)

---

## 1. 功能一览

- **无敌** —— 可分别保护**玩家自身单位**与**友军 AI 单位**。
- **无限弹药** —— 玩家与/或友军的载具、步兵、投掷武器、架设武器阵地。
- **无需装填**（仅玩家载具）—— 弹夹永不打空，游戏内按 **F9** 开关。
- **火力支援** —— 玩家阵营火炮 / CAS 呼叫次数**无限**、**无冷却**，并可调**每轮发数 / 抵达时间 / 精度**。
- **火炮阵营识别伤害** —— 己方火炮打实弹（有伤害），敌方火炮打空炮（无伤害）。
- **ESP** —— **F8** 同时开关载具 / 反坦克导弹 / 步兵透视，标签显示距离。

以上每一项都可在 `MelonPreferences.cfg` 中独立开关。

---

## 2. 安装

> 适用于 `CheatMode.dll` v1.6.0。

### 前置条件

- 正版游戏 **Gunner, HEAT, PC!**（Steam）。
- 已安装 **MelonLoader**（建议 v0.6.1 及以上）。未安装请先运行官方安装器并选择 GHPC：

  ```
  https://github.com/LavaGang/MelonLoader.Installer/releases/latest/download/MelonLoader.Installer.exe
  ```

### 安装步骤

1. 确认游戏目录结构大致如下：
   ```
   <游戏目录>/Bin/GHPC_Data/...
   <游戏目录>/Bin/MelonLoader/...
   <游戏目录>/Bin/Mods/...
   ```
   若 `Bin\Mods\` 不存在，可自行创建，或首次运行 MelonLoader 后自动生成。
2. 把编译好的 **`CheatMode.dll`** 复制到 **`<游戏目录>\Bin\Mods\`**。
3. **（重要）** 若 `Mods` 目录里还有旧的独立 **`InfiniteAmmo.dll`、`Invincible_Tank.dll`、`GHPCESP.dll`**（或任何旧火炮 dll），请**删除或移到别处禁用**。CheatMode 已整合这些功能，重复加载会造成重复的 Harmony 补丁与场景扫描，可能冲突。
4. 启动游戏。首次运行后会在 `UserData\MelonPreferences.cfg` 生成 `[CheatMode]` 段。
5. 如需修改默认值，退出游戏后用文本编辑器编辑该文件，或按游戏内 MelonLoader 偏好面板修改。

> **验证加载：** MelonLoader 控制台 / `Latest.log` 应出现：
> `[CheatMode] Loaded. F8 = ESP on/off. F9 = no reload on/off. ...`

### 卸载

- 从 `Bin\Mods\` 删除 `CheatMode.dll` 即可，不留残留文件。

---

## 3. 快捷键

| 按键 | 作用 |
|---|---|
| `F8` | 开关 ESP（载具、反坦克导弹、步兵一并显示/隐藏） |
| `F9` | 开关载具武器「无需装填」（仅玩家当前操控载具） |

---

## 4. 功能参考与配置

CheatMode 分五大块，每一项都能单独开关。

> 所有设置位于 `MelonPreferences.cfg` 的 `[CheatMode]` 段。

### 4.1 无敌（Invincibility）

| 设置 | 默认 | 作用 |
|---|---|---|
| `SelfInvincible` | `true` | 玩家**当前操控单位**不受伤、不会被击毁 |
| `FriendlyInvincible` | `true` | 与玩家同阵营的**友军 AI 单位**不受伤、不会被击毁 |

- 两档相互独立：可「自己无敌、AI 脆皮」，或「自己脆皮、AI 无敌」。
- 通过深度补丁（`penCheck`、组件伤害、超压、人类击杀、碾压、爆舱、整体摧毁标记等 **18 个点位**）实现多层次防护，并尽量让命中特效 / AAR 记录保持正常（被格挡弹显示「非穿透命中」而非凭空消失）。

### 4.2 无限弹药（Infinite Ammo）

| 设置 | 默认 | 作用 |
|---|---|---|
| `SelfInfiniteAmmo` | `true` | 玩家单位弹药无限 |
| `FriendlyInfiniteAmmo` | `true` | 友军 AI（载具/步兵/投掷/架设阵地）弹药无限 |

- 覆盖主炮、自动炮、同轴/车顶/遥控机枪、导弹；步兵步枪/机枪/狙击/火箭筒；手雷类；三脚架机枪等阵地及阵地弹药箱。
- 任何弹药架被打空会自动补上逻辑弹夹，避免「AI 卡死哑火」。
- **敌方单位不受影响**，弹药正常消耗。

### 4.3 无需装填（No Reload）— 仅玩家载具

| 设置 | 默认 | 作用 |
|---|---|---|
| `NoReload` | `true` | 载具武器无需装填；仅玩家当前操控载具生效，友军 AI 保留原版装填；游戏内按 F9 开关 |

- 开启后，**只有玩家当前操控的载具**弹夹打空后**不进入装填流程**，直接补满，实现持续射击。
- **友军 AI 载具不受影响**：即使它们有无限弹药，仍保留原版装填节奏（行为更真实、火力不过于失衡）。
- 开启时屏幕左上角显示 `CheatMode No Reload Enabled`。
- 需有至少一个「无限弹药」开关生效、且该武器属于玩家载具，才会被管理。

> ⚠️ **重要限制——切换弹种：** 当「无需装填」开启时，**玩家将无法切换弹种**——该功能会在开火前把「当前弹种」的弹夹直接补满并跳过换弹流程（弹种切换通常发生在换弹过程中），因此开启状态下切换不会生效。正确操作：① 先按 **F9** **关闭**「无需装填」；② 在装填流程中切换到你想要的弹种；③ 切换完成后若仍想持续射击，再按 **F9** 重新开启。

### 4.4 火力支援 / 火炮（Fire Support / Artillery）

| 设置 | 默认 | 作用 |
|---|---|---|
| `InfiniteFireSupport` | `true` | 玩家阵营火炮 / CAS **呼叫次数永不耗尽**（敌方与脚本规划炮击保持原版；纯配置、无快捷键、无提示） |
| `FireSupportNoCooldown` | `true` | 玩家阵营火炮 / CAS **呼叫后无冷却**，可立刻再呼叫（纯配置） |
| `ArtilleryVolleyRounds` | `-1` | 一轮发数：`-1` = 用阵地自带原版发数；否则固定 1~128 发。仅玩家阵营 |
| `ArtilleryTimeToTarget` | `1.0` | 抵达时间比例：`1.0` = 原版；`0.5` = 一半；`0.1` = 10%；`-1.0` 或任意 `<=0` = **秒抵达**（见下文说明；仅玩家阵营、仅即时呼叫） |
| `ArtilleryAccuracy` | `1.0` | 散布比例：`1.0` = 原版；越小越准；`0.1` = 10%；`-1.0` 或 `<=0` = 零散布（全落同一点）。仅玩家阵营 |

- 上述火力相关参数**只对「玩家阵营」的阵地/支援机生效**；敌方阵营阵地与**剧情脚本规划炮击一律保持原版**，不会干扰战役脚本。

**内置固定行为（不可配置）：**

- **火炮阵营识别伤害** —— 己方火炮打实弹（有伤害），敌方火炮打空炮（无伤害）。要关闭只能改 `CheatMode.cs` 里的 `ArtilleryFactionAwareEnabled` 常量后重新编译。
- **齐射压缩** —— 当 `ArtilleryTimeToTarget <= 0`（「秒抵达」）时，呼叫成功的同一帧把整轮一次打完（真正一轮齐落），面板倒计时归零。

**`ArtilleryTimeToTarget` 说明**

一次炮击总抵达时间 = **首发延迟** + **铺开时间**（`(发数-1) × 每发间隔`）+ **炮弹飞行时间**。

- 只要 `< 1`，首发延迟一律取消（第一发立刻开始落下）；该参数只控制「每发之间的间隔」。
- 例（2S3：原版首发 30s、12 发、间隔 3s）：
  - `0.5` → 无首发延迟、间隔 1.5s，整轮约 16.5s 落完；
  - `0.1` → 间隔 0.3s；
  - `-1.0` → 12 发同一帧全部落下。

### 4.5 ESP

| 设置 | 默认 | 作用 |
|---|---|---|
| `ESP` | `true` | ESP 总开关 |

- 游戏内按 **`F8`** 开/关，同时显示/隐藏**载具、反坦克导弹、步兵**的方框 ESP。
- 阵营着色：蓝 = 蓝方、红 = 红方、绿 = 绿方、黄 = 中立、白 = 其它；被击毁（neutralized）显示为灰色。
- 每单位标签显示名称与距离，如 `T-80BVM [342m]`。

---

## 5. 使用注意事项

1. **属于作弊，请用于单人 / 私人测试。** 个别多人/反作弊环境使用可能违反规则，风险自负。
2. **不要与四个独立 mod 同装。** 务必移除 `InfiniteAmmo.dll`、`Invincible_Tank.dll`、`GHPCESP.dll` 及任何旧火炮 dll，否则可能重复补丁/重复扫描。
3. **修改配置需重启生效**（或通过 MelonLoader 偏好面板，多数即时生效；火炮参数在下一次呼叫时生效）。
4. **改动源码内置行为需自行编译**：`ArtilleryFactionAwareEnabled`、齐射压缩等是硬编码的，无设置项。
5. **`NoReload` 只作用于玩家载具**：不要期望友军 AI 免装填。
6. **切换弹种前请先关闭「无需装填」**：开启状态下无法切换弹种。需先按 **F9** 关闭 → 切换弹种 → 需要时再按 **F9** 开启（详见上文第 4.3 节）。
7. **友军判定采用「权威阵营级联」。** 自定义场景（如 Fulda 1989）若出现「0 单位被授予」，请查看日志 `[CheatMode] Scene scan finished: ... side=... (via ...)` 一行，其中 `SceneController` / `MissionData` / `PlayerUnit` 表明阵营来源，便于定位。
8. **AAR / 回放**：即时齐射（秒抵达）在 AAR 回放中被显式放回原版逐发流程，避免回放异常。
9. **兼容性**：面向特定 GHPC 版本编写，使用 publicized 程序集，依赖私有时/后字段（经反射 `AccessTools`）。游戏大版本更新后可能失效，需等待适配。
10. **备份**：更新游戏前建议备份 `UserData\MelonPreferences.cfg`，以便回退设置。

---

## 6. 问题排查

- **没加载 / 日志没出现 `[CheatMode] Loaded`** → 确认 dll 在 `Bin\Mods\`、MelonLoader 版本、游戏路径引用正确。
- **启动报 "Another instance of CheatMode is already loaded"** → Mods 里放了两份，删掉多余。
- **ESP 看不见** → 按 F8 确认已开启；确认目标在视野内且不在你当前单位身上。
- **敌方火炮仍在开火** → 那是正常的（阵营伤害只让敌方「打空炮」；若你看到敌方仍有弹，说明该炮为临时脚本炮击，属设计如此）。

---

## 7. 从源码构建

需要 MSBuild 与 GHPC 安装目录（`.csproj` 引用游戏 `Bin\` 下 MelonLoader 与 publicized `Assembly-CSharp` 的 DLL，请把 `HintPath` 改成你机器上的路径）。

```
MSBuild.exe CheatMode.sln /p:Configuration=Release
```

或用 Visual Studio 打开 `CheatMode.csproj` 构建 `Release`。

### 项目结构

```
CheatMode/
├── CheatMode.cs                主入口：配置、阵营级联、弹药扫描、ESP 绘制
├── CheatAmmoPatches.cs         无限弹药 / 无需装填补丁（载具、步兵、投掷武器、阵地）
├── CheatFireSupportPatches.cs  火炮 / CAS 火力支援作弊
├── CheatInvinciblePatches.cs   无敌伤害过滤补丁
├── CheatESPPatches.cs          ESP 单位追踪补丁
├── Render.cs                   IMGUI 绘制辅助
├── Properties/AssemblyInfo.cs
└── CheatMode.csproj
```

---

## 8. 许可证与致谢

采用 **GNU Affero General Public License v3.0**（见 [LICENSE](LICENSE)）。

CheatMode 整合了下列遵循相应许可证发布的社区 mod 代码，请尊重其原作者：

- [Invincible-Tank](https://github.com/QwertyRyo/Invincible-Tank) — AGPL-3.0（QwertyRyo）
- [InfiniteAmmo](https://github.com/Bluehawk8908/InfiniteAmmo) — GPL-3.0（Bluehawk8908）
- [GHPCESP](https://github.com/k4yt3x/GHPCESP) — MIT（K4YT3X）
- [GHPC_Artillery_Rework](https://github.com/QwertyRyo/GHPC_Artillery_Rework)（QwertyRyo）— 火力支援 / 火炮子系统的灵感来源

**相关文档**

- CheatMode 与这四个 mod 的完整差异对比（功能、实现差异、修复与改进）：[docs/COMPARISON.md](docs/COMPARISON.md)
