# CheatMode 中文使用教程

> 适用于 `CheatMode.dll` v1.6.3。本教程涵盖**安装方式、功能说明、使用注意事项**三部分。

---

## 一、安装方式

### 前置条件
- 正版游戏 **Gunner, HEAT, PC!**（Steam）。
- 已安装 **MelonLoader**（建议 v0.6.1 及以上）。未安装请先运行官方安装器：
  `https://github.com/LavaGang/MelonLoader.Installer/releases/latest/download/MelonLoader.Installer.exe`，选择 GHPC 后安装。

### 安装步骤
1. 确认游戏目录结构大致为：
   ```
   <游戏目录>/Bin/GHPC_Data/...
   <游戏目录>/Bin/MelonLoader/...
   <游戏目录>/Bin/Mods/...
   ```
   若 `Bin\Mods\` 不存在，可自行创建，或首次运行 MelonLoader 后自动生成。
2. 把编译好的 **`CheatMode.dll`** 复制到 **`<游戏目录>\Bin\Mods\`**。
3. （重要）若 `Mods` 目录里还有旧的 **`InfiniteAmmo.dll`、`Invincible_Tank.dll`、`GHPCESP.dll`**，**请删除或移到别处禁用**。CheatMode 已整合这些功能，重复加载会做重复的 Harmony 补丁与场景扫描，可能冲突。
4. 启动游戏。首次运行后会在 `UserData\MelonPreferences.cfg` 生成 `[CheatMode]` 段。
5. 如需修改默认值，退出游戏后用文本编辑器编辑该文件，或按游戏内 MelonLoader 偏好面板修改。

> 验证是否加载成功：MelonLoader 控制台 / `Latest.log` 应出现
> `[CheatMode] Loaded. F8 = ESP on/off. F9 = no reload on/off. ...`

### 卸载
- 从 `Bin\Mods\` 删除 `CheatMode.dll` 即可，不留残留文件。

---

## 二、功能说明

CheatMode 分五大块，每一项都能单独开关。

### 1. 无敌（Invincibility）
| 设置 | 默认 | 作用 |
|---|---|---|
| `SelfInvincible` | true | 玩家**当前操控单位**不受伤、不会被击毁 |
| `FriendlyInvincible` | true | 与玩家同阵营的**友军 AI 单位**不受伤、不会被击毁 |

- 两档相互独立：可“自己无敌、AI 脆皮”，或“自己脆皮、AI 无敌”。
- 通过深度补丁（`penCheck`、组件伤害、超压、人类击杀、碾压、爆舱、整体摧毁标记等 18 个点位）实现**多层次防护**，并尽量让命中特效 / AAR 记录保持正常（被格挡弹显示“非穿透命中”而非凭空消失）。

### 2. 无限弹药（Infinite Ammo）
| 设置 | 默认 | 作用 |
|---|---|---|
| `SelfInfiniteAmmo` | true | 玩家单位弹药无限 |
| `FriendlyInfiniteAmmo` | true | 友军 AI（载具/步兵/投掷/架设阵地）弹药无限 |

- 覆盖主炮、自动炮、同轴/车顶/遥控机枪、导弹；步兵步枪/机枪/狙击/火箭筒；手雷类；三脚架机枪等阵地及阵地弹药箱。
- 任何弹药架被打空会自动补上逻辑弹夹，避免“AI 卡死哑火”。
- **敌方单位不受影响**，弹药正常消耗。

### 3. 无需装填（No Reload）— 仅玩家载具
| 设置 | 默认 | 说明 |
|---|---|---|
| `NoReload` | true | 见下 |

- 开启后，**只有玩家当前操控的载具**弹夹打空后**不进入装填流程**，直接补满，实现持续射击。
- **友军 AI 载具不受影响**：即使它们有无限弹药，仍保留原版装填节奏（行为更真实、火力不过于失衡）。
- 游戏内按 **`F9`** 随时开/关；开启时屏幕左上角显示 `CheatMode No Reload Enabled`。
- 需有至少一个“无限弹药”开关生效、且该武器属于玩家载具，才会被管理。

> ✅ **「无需装填」开启时现在可以直接切换弹种（1.6.2 修复）**：选弹种会**立即**换弹夹（不进换弹流程、不播装填动画），且“弹夹 + 炮膛”仍恰好等于一个满弹夹 —— 不会多出一发；炮膛里那一发保持旧弹种直到打出去。
> （1.6.2 之前需要：按 **`F9`** 关闭 → 切弹种 → 再按 **`F9`** 开启。）

### 4. ESP（透视）
| 设置 | 默认 | 说明 |
|---|---|---|
| `ESP` | true | ESP 总开关 |

- 游戏内按 **`F8`** 开/关，同时显示/隐藏**载具、反坦克导弹、步兵**的方框 ESP。
- 阵营着色：蓝=蓝方、红=红方、绿=绿方、黄=中立、白=其它；被击毁（neutralized）显示为灰色。
- 每单位标签显示名称与距离，如 `T-80BVM [342m]`。

---

## 三、使用注意事项

1. **属于作弊，请用于单人 / 私人测试**。个别多人/反作弊环境使用可能违反规则，风险自负。
2. **不要与三个独立 mod 同装**。务必移除 `InfiniteAmmo.dll`、`Invincible_Tank.dll`、`GHPCESP.dll`，否则可能重复补丁/重复扫描。
3. **修改配置需重启生效**（或通过 MelonLoader 偏好面板，多数即时生效）。
4. **改动源码内置行为需自行编译**：内置行为是硬编码的，无设置项。
5. **`NoReload` 只作用于玩家载具**：不要期望友军 AI 免装填。
6. **「无需装填」开启时可直接切换弹种**（1.6.2 起）：选弹种即立刻换弹夹，且不会多出一发；炮膛里那一发保持旧弹种直到打出去。
7. **友军判定采用“权威阵营级联”**：自定义场景（如 Fulda 1989）若出现“0 单位被授予”，请查看日志 `[CheatMode] Scene scan finished: ... side=... (via ...)` 一行，通常说明阵营来源（`SceneController` / `MissionData` / `PlayerUnit`），便于定位。
8. **兼容性**：面向特定 GHPC 版本编写，使用 publicized 程序集，依赖私有时/后字段（经反射 `AccessTools`）。游戏大版本更新后可能失效，需等待适配。
9. **备份**：更新游戏前建议备份 `UserData\MelonPreferences.cfg`，以便回退设置。
---

## 附：问题排查速查
- **没加载 / 日志没出现 `[CheatMode] Loaded`** → 确认 dll 在 `Bin\Mods\`、MelonLoader 版本、游戏路径引用正确。
- **启动报 “Another instance of CheatMode is already loaded”** → Mods 里放了两份，删掉多余。
- **ESP 看不见** → 按 F8 确认已开启；确认目标在视野内且不在你当前单位身上。
