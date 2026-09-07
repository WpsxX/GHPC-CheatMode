# CheatMode vs. the Four Reference Mods — Detailed Comparison (Baseline: Committed Repository Sources)

> Comparison participants (exactly two sides):
> - **Current changes (subject under analysis)**: `CheatMode` v1.6.0 source in this repository (`CheatMode/`: `CheatMode.cs`, `CheatAmmoPatches.cs`, `CheatFireSupportPatches.cs`, `CheatInvinciblePatches.cs`, `CheatESPPatches.cs`, `Render.cs`).
> - **Repository sources (baseline — remote / committed versions)**: default-branch source files of the four GitHub repositories below (snapshotted on 2026-09-08), each pinned to its default-branch commit:
>   1. [`QwertyRyo/GHPC_Artillery_Rework`](https://github.com/QwertyRyo/GHPC_Artillery_Rework) @ `b66a766` (2026-04-22), `GHPC_Artillery_Rework.cs`, **v1.1.1**
>   2. [`Bluehawk8908/InfiniteAmmo`](https://github.com/Bluehawk8908/InfiniteAmmo) @ `5ab5a00` (2026-05-31), `InfiniteAmmo.cs`, **v1.0.1**
>   3. [`QwertyRyo/Invincible-Tank`](https://github.com/QwertyRyo/Invincible-Tank) @ `979841b` (2026-06-20), `Invincible_Tank.cs`, **v1.0.0**
>   4. [`k4yt3x/GHPCESP`](https://github.com/k4yt3x/GHPCESP) @ `35a4c56` (2024-12-12), `src/GHPCESPMod.cs`, `src/Patches.cs`, `src/Render.cs`, **v1.2.0**

---

## 0. Baseline & Methodology (important — please read first)

### 0.1 The two sides of the diff
- The comparison has exactly two sides: the left side is CheatMode's current source (in this repository, shipped with this release); the right side is the **committed (remote)** source files of the four repositories.

### 0.2 Why the "remote / committed source versions" are the baseline
- The default branches of the four linked repositories are public, reproducible and attributable facts; only they can serve as the right-hand baseline of a diff.
- The question this report answers is: what did CheatMode (the current changes) **change, add or rewrite relative to each upstream repository**.

### 0.3 File-level diff method
- Because CheatMode and each upstream mod differ in file layout and namespaces (e.g. CheatMode's ammo logic spans `CheatMode.cs` and `CheatAmmoPatches.cs`), the diff is performed as an item-by-item mapping of **"upstream file/method → corresponding CheatMode file/method"**, and each row reports the net change: `identical` / `extended` / `rewritten` / `added` / `no counterpart`.
- The "diff result" table in each per-mod section is the output of that mapping; section 5 summarizes it.

---

## 1. vs. GHPC_Artillery_Rework (Artillery / Fire Support) — baseline `GHPC_Artillery_Rework.cs` v1.1.1

**File mapping**: upstream single file `GHPC_Artillery_Rework.cs` ↔ CheatMode `CheatFireSupportPatches.cs` (+ the artillery settings/constant in `CheatMode.cs`). Net change: **rewrite + extension (cheat-oriented trimming plus several new capabilities)**.

### 1.1 Targeted methods
- **Artillery Rework (committed)**: patches `ArtilleryBattery.SendFireMission` and rewrites the **method parameters via Prefix** (`delaySeconds / roundCount / radiusMeters / secondsBetweenRounds` as `ref`s); it also patches the UI layer (`MapIconControlType.UpdateCooldownDelayResponse`, `MapFireSupportPanel.OnClickSupportButton`, `FireMissionManager.SendFireMissionPlanned`, `MapController.InitControlState`, `DynamicMissionLauncher.LaunchDynamicMission`) and `AlertHud`.
- **CheatMode**: patches **`SendFireMission` in Postfix**, rewriting the per-call "current value" fields (`_currentShotQuota / _currentInterShotTime / _currentRadius / TimeUntilImpactSeconds / RemainingDelay`); patches **`DoSingleShot`** for faction-aware damage and `DoUpdate` for no-cooldown. It does not touch the UI layer.

### 1.2 Diff result (feature level)
| Upstream v1.1.1 feature | Corresponding file/method | CheatMode net change |
|---|---|---|
| Round volume multiplier | No same-name item (uses `VolleyRounds` fixed count) | **Rewritten** (-1 = vanilla / fixed 1-128) |
| Time-to-target multiplier | `ArtilleryTimeToTarget` ratio + first-delay removal + instant | **Rewritten** (adds "whole volley in one frame" that upstream lacks) |
| Accuracy multiplier | `ArtilleryAccuracy` ratio + zero dispersion | **Rewritten** (semantics changed from divide-by-multiplier to ratio scaling) |
| HE/Smoke/Illu × OnCall/Planned 18 multipliers | — | **No counterpart** (deliberately trimmed: uniform params) |
| `CallerDetector` stack-trace OnCall/Planned split | — | **No counterpart** (uses `ignoreCooldown` + faction-array check instead) |
| Mid-mission cancellation (UI click) | — | **No counterpart** (not needed in a cheat scenario) |
| `AlertHud` inbound alert + voicelines | — | **No counterpart** (config only, no hints) |
| — | `InfiniteFireSupport` (restores `_missionsAvailable`) | **Added** (upstream has none) |
| — | `FireSupportNoCooldown` (`DoUpdate` zeroing / CAS setter interception) | **Added** (upstream has none) |
| — | `ArtilleryFactionAwareEnabled` faction damage (`DoSingleShot`) | **Added** (upstream has none) |
| — | Instant same-frame full volley + `_shotCounter` topped to quota | **Added** (panel never sticks on "Incoming") |

### 1.3 Fixes / improvements over upstream
1. **Never pollutes battery templates**: upstream scales method arguments directly (its changelog even documents a "squared volume multiplier" compound bug it fixed); CheatMode only writes the per-call current-value fields vanilla reassigns each call, never the template fields (`_shots / _interShotDelaySeconds / _randomDispersionRadiusMeters / _onCallImpactDelay`), structurally preventing "each use gets faster/more accurate".
2. **Never disturbs scripted story events**: batteries with `ignoreCooldown == true` stay fully vanilla; upstream relies on stack-trace detection, which isolates story scripts less cleanly.
3. **Panel-state correctness**: for instant arrival it fills the whole volley in the same frame and pushes `_shotCounter` to quota, so the panel never sticks on "Incoming".
4. **Correct no-cooldown approach**: explicitly does *not* block `RemainingCooldown` writes (which would break CooldownManager panel tracking); it zeroes residual cooldown in a `DoUpdate` postfix instead.

---

## 2. vs. InfiniteAmmo (Infinite Ammo) — baseline `InfiniteAmmo.cs` v1.0.1

**File mapping**: upstream single file `InfiniteAmmo.cs` ↔ CheatMode `CheatMode.cs` (scan / marker / replenish helpers) + `CheatAmmoPatches.cs` (all patches). Net change: **full rewrite from "hard-coded whitelist + one clip back for the player" into a generic faction-based all-unit infinite-ammo system**.

### 2.1 What upstream v1.0.1 actually is (committed version)
- **Hard-coded vehicle whitelist**: after GameReady it iterates every `Vehicle` and only when `UniqueName ∈ {M2BRADLEY, M2BRADLEY(ALT), BMP2_SA, BMP2, MARDERA1, MARDERA1PLUS, MARDER1A2, MARDERA1_NO_ATGM}` seeds clips and stamps a marker;
- **Player only, one clip back per reload**: the `AmmoFeed.FinishClipReload` Postfix resolves the player unit and only when the reloading vehicle == the player's current unit adds 1 clip back (plus an extra invisible clip for a rack literally named `"Malyutka ammo"`);
- **No settings, no friendlies, no infantry, no "no reload"**.

### 2.2 Diff result (feature level)
| Upstream v1.0.1 feature | Corresponding file/method | CheatMode net change |
|---|---|---|
| `Chainguns` whitelist pass (8 vehicle names) | `CheatMode.cs` `ApplyInfiniteAmmo / SeedUnit` | **Rewritten**: faction cascade + platoon members + emplacement ownership; all factions, no name dependence |
| `InfAmmo` marker dedup | `CheatMarker` (+`Managed()`) | **Identical** (renamed) |
| 1 clip back after `FinishClipReload` (player only) | `CheatAmmoPatches.cs` `CheatModeReplenishPatch` | **Extended**: replenishes exactly the count consumed (pre/post reload delta), never more |
| `"Malyutka ammo"` name special case | — | **Removed** (replaced by generic logic) |
| — | `AmmoFeedEnsureClipPatch` (pre-`FeedNewClip` empty-rack self-heal) | **Added** (fixes AI stalling on an empty reserve) |
| — | `NoReloadFeedNewClipPatch` / `NoReloadWeaponFiredPatch` (player vehicle only, F9) | **Added** (upstream has no "no reload") |
| — | `AmmoFeedDualFeedPatch` (`FeedNonSelectedClip`) | **Added** (dual-feed belt upkeep) |
| — | `InfantryWeaponAmmoPatch` / `InfantryThrowableAmmoPatch` / `AmmoSupplies*Patch` | **Added** (infantry / throwables / emplacement ammo crates) |
| No settings | `[CheatMode]`: `SelfInfiniteAmmo / FriendlyInfiniteAmmo / NoReload` | **Added** (player/friendly independent toggles) |

### 2.3 Fixes / improvements over upstream v1.0.1
1. **Name whitelist → generic faction mechanism**: upstream's 8 hard-coded `UniqueName`s break with new vehicles/scenarios; CheatMode covers everything automatically.
2. **Player-only → separable self/friendly**: `SelfInfiniteAmmo`/`FriendlyInfiniteAmmo` are independent; "No Reload" stays player-vehicle-only.
3. **Empty-rack deadlock fix**: upstream only refills whitelisted vehicles after a reload completes; CheatMode's `AmmoFeedEnsureClipPatch` universally guarantees a logical clip before reload.
4. **Bookkeeping-style replenish**: restores exactly what was consumed and drops the name-based special case, so reserve ammo never inflates.
5. **Behavioral realism**: No Reload is player-only; friendly AI keep vanilla reload cadence.

---

## 3. vs. Invincible-Tank (Invincibility) — baseline `Invincible_Tank.cs` v1.0.0

**File mapping**: upstream single file `Invincible_Tank.cs` ↔ CheatMode `CheatInvinciblePatches.cs` (+ the two toggles in `CheatMode.cs`). Net change: **from a single `penCheck` negation to an 18-point layered defense + independent Self/Friendly toggles**.

### 3.1 What upstream v1.0.0 actually is (committed version)
- `Patch_MapController_InitControlState` (Postfix): stores the player unit's `Allegiance` string in `PlayerFactionStore.Name`;
- `Patch_LiveRound_PenCheck` (Prefix): negates a hit when the struck unit's faction == the player's faction (writes 3 position fields + `AddEvent` + `reportShotTraceFrame`, returns `false`);
- **No Self/Friendly split, no settings, no component/compartment/crew/infantry patches**.

### 3.2 Diff result (feature level)
| Upstream v1.0.0 feature | Corresponding file/method | CheatMode net change |
|---|---|---|
| `PlayerFactionStore` (one-shot string) | `CheatModeMod.ResolvePlayerFaction()` + `CheatDamageFilter` | **Rewritten**: authoritative faction cascade + per-event resolution |
| `MapController.InitControlState` Postfix | — | **No counterpart** (does not depend on UI init timing) |
| `penCheck` Prefix negation | `CheatInvinciblePatches.cs` `Patch_LiveRound_PenCheck` | **Extended**: same negation point, but also writes `_frameData/_armed/_impactTime/_impactObject/_materialHit/_impactSkipDecal/_heHitNormalRha` etc.; HE/HEAT fuze & VFX/AAR more complete |
| Protect "faction == player" as one group | `CheatDamageFilter.IsProtectedUnit` | **Rewritten**: player first (`SelfInvincible`), then friendlies (`FriendlyInvincible`); two independent tiers |
| — | `Patch_DestructibleComponent_ApplyProjectileDamage` (+float/Overpressure/BlastShock) | **Added** (4 patch points) |
| — | `Patch_Compartment_NotifyPenetrated / InsertOverpressure` | **Added** |
| — | `Patch_Human_Kill / SetPartHealth`, `Patch_AntiPersonnelGrenade*` | **Added** |
| — | `Patch_InfantryUnit_OnCollisionWithVehicle / GoreOnLimbDismembered` | **Added** |
| — | `Patch_CrewManager_KillAllRemainingCrew` | **Added** |
| — | `Patch_Unit_NotifyDestroyed / NotifyIncapacitated` | **Added** (final safety net) |
| No settings | `SelfInvincible` / `FriendlyInvincible` | **Added** |

### 3.3 Fixes / improvements over upstream v1.0.0
1. **Defense in depth**: upstream only guards `penCheck`; damage paths that bypass it (overpressure straight into compartments, `Human.Kill`, run-overs, dismemberment, `KillAllRemainingCrew`, `NotifyDestroyed`, ...) can still kill friendlies under upstream. CheatMode closes every such entry and uses "never mark the unit destroyed/incapacitated" as the final net.
2. **Self/Friendly decoupling**: enables combos such as "self fragile + AI invincible".
3. **Complete state restoration for negated shots**: HE/HEAT rounds still detonate with correct VFX on the outside of allied vehicles; hit records/AAR stay complete.
4. **Maintainability**: typed `FieldRef`/`TargetMethod` with centralized `CheatDamageFilter` decisions, replacing name-based reflection.

---

## 4. vs. GHPCESP (ESP) — baseline `src/GHPCESPMod.cs` etc. v1.2.0

**File mapping**: upstream 3 files (`GHPCESPMod.cs` + `Patches.cs` + `Render.cs`) ↔ CheatMode `CheatMode.cs` (ESP drawing part) + `CheatESPPatches.cs` + `Render.cs`. Net change: **closest match of the four — hardening + key consolidation + distance labels**.

### 4.1 Common ground
Bounding-box ESP + faction coloring + gray when neutralized + snap line + resolution scaling + `Unit.Start / InfantryUnit.Start` tracking.

### 4.2 Diff result (feature level)
| Upstream v1.2.0 feature | Corresponding file/method | CheatMode net change |
|---|---|---|
| Vehicle/ATGM ESP (F8 toggle) | `CheatMode.cs` `DrawUnitsESP(Units)` | **Extended**: folded into master toggle F8 |
| Infantry ESP (F9 toggle) | `DrawUnitsESP(InfantryUnits)` | **Extended**: folded into master toggle F8 |
| Two independent keys F8/F9 | `OnUpdate` key handling | **Rewritten**: F8 = ESP master; F9 repurposed for "No Reload" |
| Two screen headers | `OnGUI` | **Consolidated**: one "CheatMode ESP Enabled" |
| Labels without distance | label building in `DrawUnitsESP` | **Added**: appends `[distance m]` |
| No `MainCam` null check | drawing entry | **Fixed**: null guards added |
| Two cleanup loops | `OnUpdate` | **Consolidated**: one unified loop |

### 4.3 Fixes / improvements over upstream GHPCESP
Null-reference robustness, key consolidation (both ESP modes merged under F8), distance in labels, unified cleanup loop.

---

## 5. CheatMode's overall net change vs. the four upstream repositories

| Subsystem | Upstream baseline | Net change |
|---|---|---|
| Artillery / fire support | Artillery Rework v1.1.1 | Rewrite + extension: adds unlimited calls / no cooldown / faction damage / instant volleys; trims shell-type & Planned subdivision, cancellation, HUD voicelines |
| Ammo | InfiniteAmmo v1.0.1 | Full rewrite: whitelist + player one-clip-back → faction-based all-unit infinite ammo + player-only no-reload + empty-rack self-heal + bookkeeping replenish |
| Invincibility | Invincible-Tank v1.0.0 | Major extension: single penCheck → 18-point layered defense + Self/Friendly toggles + complete hit-state restoration |
| ESP | GHPCESP v1.2.0 | Hardening + consolidation: null guards, single master toggle, distance labels |

Cross-cutting additions (none present in any upstream): single-DLL single-instance integration, unified `[CheatMode]` configuration, authoritative faction cascade, AAR-replay protection, unified prefixed logging.

---

## 6. Limitations & honest differences (vs. the committed sources)

- **No** Artillery Rework HUD inbound alerts / voicelines, HE/Smoke/Illu × OnCall/Planned 18-way config, or mid-mission cancellation.
- **No** GHPCESP F8/F9 dual ESP toggles (CheatMode deliberately merges them).
- Cheat-oriented / single-player: no multiplayer fairness handling; only instant artillery volleys are explicitly avoided during AAR replay.
- If upstream publishes newer versions, re-verify this report against the new commits (baseline commits are pinned at the top).
