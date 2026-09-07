# CheatMode for Gunner, HEAT, PC!

An all-in-one MelonLoader cheat mod for [Gunner, HEAT, PC!](https://store.steampowered.com/app/1705180/Gunner_HEAT_PC/) that merges **invincibility, infinite ammo / no-reload, unlimited & instant fire support, and ESP** into a **single DLL**, adds **player-vs-friendly granularity** and **faction-aware artillery damage**, and is carefully scoped so enemy units and scripted campaign events keep their vanilla behavior.

Built by integrating and hardening the ideas behind four community mods: [GHPC_Artillery_Rework](https://github.com/QwertyRyo/GHPC_Artillery_Rework) · [InfiniteAmmo](https://github.com/Bluehawk8908/InfiniteAmmo) · [Invincible-Tank](https://github.com/QwertyRyo/Invincible-Tank) · [GHPCESP](https://github.com/k4yt3x/GHPCESP). See the [detailed comparison](docs/COMPARISON.en.md).

> ⚠️ **Cheat mod.** Intended for single-player / private testing only. Not for competitive multiplayer.

> 🌐 **Bilingual viewer:** prefer switching between English / 中文 *without leaving the page*? Open the interactive **[README.html](docs/README.html)** (keeps your reading position when toggling languages). Or switch to the [中文版 README](README.zh-CN.md).

---

## Table of Contents

1. [Features at a glance](#1-features-at-a-glance)
2. [Installation](#2-installation)
3. [Hotkeys](#3-hotkeys)
4. [Feature Reference & Configuration](#4-feature-reference--configuration)
5. [Usage Notes](#5-usage-notes)
6. [Troubleshooting](#6-troubleshooting)
7. [Building from Source](#7-building-from-source)
8. [License & Credits](#8-license--credits)

---

## 1. Features at a glance

- **Invincibility** — separately protect the player's own unit and/or friendly AI units.
- **Infinite ammo** — player and/or friendly vehicles, infantry, throwables and crew-served weapon emplacements.
- **No reload** (player vehicle only) — the clip never empties; toggled in-game with **F9**.
- **Fire support** — unlimited player-side artillery/CAS calls, **no cooldown**, plus tunable **volley size**, **arrival time** and **accuracy**.
- **Faction-aware artillery damage** — your faction's artillery fires live (damaging) rounds; the enemy's fires blanks.
- **ESP** — toggle vehicles, ATGM and infantry boxes together with **F8**; labels show distance.

All of the above can be switched on/off individually in `MelonPreferences.cfg`.

---

## 2. Installation

> Applies to `CheatMode.dll` v1.6.0.

### Prerequisites

- A legitimate copy of **Gunner, HEAT, PC!** on Steam.
- **MelonLoader** installed (v0.6.1+ recommended). If not installed, run the official installer and pick GHPC:

  ```
  https://github.com/LavaGang/MelonLoader.Installer/releases/latest/download/MelonLoader.Installer.exe
  ```

### Steps

1. Confirm your game folder looks roughly like:
   ```
   <game>/Bin/GHPC_Data/...
   <game>/Bin/MelonLoader/...
   <game>/Bin/Mods/...
   ```
   If `Bin\Mods\` does not exist, create it (or let MelonLoader create it on first run).
2. Copy the compiled **`CheatMode.dll`** into **`<game>\Bin\Mods\`**.
3. **(Important)** If `Mods` still contains the standalone **`InfiniteAmmo.dll`**, **`Invincible_Tank.dll`**, **`GHPCESP.dll`** (or any older artillery DLL), **remove or disable them**. CheatMode already covers these features; having both causes duplicate Harmony patches and duplicate scene scans.
4. Launch the game. On the first run the `[CheatMode]` section is created in `UserData\MelonPreferences.cfg`.
5. To change defaults, exit the game and edit that file with a text editor, or use the in-game MelonLoader preferences panel.

> **Verify it loaded:** the MelonLoader console / `Latest.log` should show:
> `[CheatMode] Loaded. F8 = ESP on/off. F9 = no reload on/off. ...`

### Uninstall

- Just delete `CheatMode.dll` from `Bin\Mods\`. No leftover files.

---

## 3. Hotkeys

| Key | Action |
|---|---|
| `F8` | Toggle ESP (vehicles, ATGM & infantry together) on/off |
| `F9` | Toggle vehicle-weapon "No Reload" on/off (player vehicle only) |

---

## 4. Feature Reference & Configuration

CheatMode is split into five blocks; each can be toggled independently.

> All settings live under `[CheatMode]` in `MelonPreferences.cfg`.

### 4.1 Invincibility

| Setting | Default | Effect |
|---|---|---|
| `SelfInvincible` | `true` | The unit you are currently controlling takes no damage and cannot be destroyed. |
| `FriendlyInvincible` | `true` | Same-faction friendly AI units take no damage and cannot be destroyed. |

- The two tiers are independent: you can have "self invincible + AI fragile" or "self fragile + AI invincible".
- Protection is layered across **18 patch points** (penCheck, component damage, overpressure, human kills, run-overs, compartment blowout, unit-destroyed markers, etc.). Blocked hits keep their impact VFX / AAR record intact (shown as a non-penetrating hit rather than vanishing).

### 4.2 Infinite Ammo

| Setting | Default | Effect |
|---|---|---|
| `SelfInfiniteAmmo` | `true` | Your unit never runs out of ammo. |
| `FriendlyInfiniteAmmo` | `true` | Friendly AI (vehicles / infantry / throwables / emplacements) never run out. |

- Covers main guns, autocannons, coaxial/top/RC machine guns, missiles; infantry rifles/MGs/sniper/rocket launchers; grenades; tripod emplacements and their ammo crates.
- Any rack that is emptied is auto-seeded with a logical clip, preventing "AI stuck / goes quiet".
- **Enemy units are unaffected** and consume ammo normally.

### 4.3 No Reload (player vehicle only)

| Setting | Default | Effect |
|---|---|---|
| `NoReload` | `true` | Vehicle weapons never need reloading; only the player's currently controlled vehicle is affected; friendly AI keep vanilla reload; toggled in-game with F9. |

- When on, **only the vehicle you currently control** skips the reload flow and refills the clip directly for sustained fire.
- **Friendly AI vehicles are unaffected**: even though they have infinite reserve ammo, they keep the vanilla reload cadence (more realistic, not overpowered).
- While on, the top-left shows `CheatMode No Reload Enabled`.
- It only manages weapons that belong to your vehicle and that have at least one infinite-ammo toggle active.

> ⚠️ **Important limitation — switching ammunition types:** While "No Reload" is enabled, **you cannot switch ammunition types** — the feature refills the clip of the *current* type before every shot and skips the reload flow (ammo-type switching normally happens during a reload), so a switch simply will not take effect. Correct workflow: ① press **`F9`** to turn it **off**; ② switch to your desired ammunition type during the reload flow; ③ if you still want sustained fire afterward, press **`F9`** again to re-enable it.

### 4.4 Fire Support / Artillery

| Setting | Default | Effect |
|---|---|---|
| `InfiniteFireSupport` | `true` | Artillery / CAS **calls never run out** — player faction only; enemy and script-planned strikes stay vanilla (config only, no hotkey / HUD). |
| `FireSupportNoCooldown` | `true` | Fire support has **no cooldown** — player faction only (config only). |
| `ArtilleryVolleyRounds` | `-1` | Rounds per volley: `-1` = the battery's vanilla count; otherwise a fixed 1–128. Player faction only. |
| `ArtilleryTimeToTarget` | `1.0` | Arrival time as a fraction of vanilla: `1.0` = vanilla; `0.5` = half; `0.1` = 10%; `-1.0` or any `<=0` = **instant** (see notes; player faction, on-call calls only). |
| `ArtilleryAccuracy` | `1.0` | Dispersion as a fraction of vanilla: `1.0` = vanilla spread; smaller = tighter; `0.1` = 10%; `-1.0` or `<=0` = zero dispersion (all rounds on one point). Player faction only. |

- These fire-support parameters apply **only to batteries/airframes on the player's faction**; enemy batteries and **script-planned story strikes always stay vanilla**, so campaign scripts are never disturbed.

**Always-on built-in behaviors (not configurable):**

- **Faction-aware artillery damage** — your faction's artillery fires live (damaging) rounds; the enemy's fires blanks (no damage). To disable it you must edit the `ArtilleryFactionAwareEnabled` constant in `CheatMode.cs` and recompile.
- **Volley compression** — when `ArtilleryTimeToTarget <= 0` ("instant"), the whole volley is fired on the same frame the call succeeds (a true single-volley strike) and the panel countdown is zeroed.

**`ArtilleryTimeToTarget` notes**

For a strike, total arrival time = **first-round delay** + **spread time** (`(rounds-1) × per-round interval`) + **shell flight time**.

- As soon as the value is `< 1`, the first-round delay is removed (the first round starts dropping immediately); the setting only controls the interval between rounds.
- Example (2S3, vanilla: 30 s first-round delay, 12 rounds, 3 s interval):
  - `0.5` → no first-round delay, 1.5 s interval → the volley finishes dropping in ~16.5 s;
  - `0.1` → 0.3 s interval;
  - `-1.0` → all 12 rounds drop on the same frame (truly one volley).

### 4.5 ESP

| Setting | Default | Effect |
|---|---|---|
| `ESP` | `true` | Master ESP switch. |

- Press **`F8`** in-game to toggle; shows/hides **vehicle, ATGM and infantry** boxes together.
- Faction colors: blue = Blue, red = Red, green = Green, yellow = Neutral, white = other; neutralized units turn gray.
- Each label shows name + distance, e.g. `T-80BVM [342m]`.

---

## 5. Usage Notes

1. **This is a cheat.** Use it for single-player / private testing. In some multiplayer/anti-cheat contexts this may violate rules — at your own risk.
2. **Do not run it alongside the four standalone mods.** Remove `InfiniteAmmo.dll`, `Invincible_Tank.dll`, `GHPCESP.dll`, and any older artillery DLL to avoid duplicate patches/scans.
3. **Config changes take effect on restart** (or via the MelonLoader preferences panel; most apply immediately, and artillery parameters apply on the next call).
4. **Changing built-in behavior requires recompiling** (e.g. `ArtilleryFactionAwareEnabled`, volley compression have no settings).
5. **`NoReload` only affects your vehicle.** Don't expect friendly AI to skip reloading.
6. **Turn "No Reload" off before switching ammunition types:** press **`F9`** to disable → switch ammo → press **`F9`** to re-enable if needed (see section 4.3).
7. **Friendly detection uses an authoritative faction cascade.** In custom scenes (e.g. Fulda 1989), if you see "0 units granted", check the log line `[CheatMode] Scene scan finished: ... side=... (via ...)`, which shows the faction source (`SceneController` / `MissionData` / `PlayerUnit`) to help diagnose.
8. **AAR / replay:** instant artillery volleys are deliberately handed back to the vanilla per-round flow during AAR replay to avoid desync.
9. **Compatibility:** written against a specific GHPC build using the publicized assembly and private/internal fields via `AccessTools` reflection. A major game update may break it until adapted.
10. **Backup:** before updating the game, back up `UserData\MelonPreferences.cfg` so you can restore your settings.

---

## 6. Troubleshooting

- **Not loading / no `[CheatMode] Loaded` in log** → verify the DLL is in `Bin\Mods\`, MelonLoader version, and game path references.
- **Startup error "Another instance of CheatMode is already loaded"** → two copies are in `Mods`; delete the extra.
- **ESP invisible** → press F8 to confirm it is on; ensure the target is in view and is not the unit you are in.
- **Enemy artillery still fires** → that is expected. Faction-aware damage only makes the enemy fire *blanks*; if you see enemy shells with actual effect, that battery is a temporary scripted strike (by design it stays vanilla).

---

## 7. Building from Source

Requires MSBuild and the GHPC install (the `.csproj` references the MelonLoader and publicized `Assembly-CSharp` DLLs under the game's `Bin\` folder — adjust the `HintPath`s to your machine).

```
MSBuild.exe CheatMode.sln /p:Configuration=Release
```

Or open `CheatMode.csproj` in Visual Studio and build `Release`.

### Project layout

```
CheatMode/
├── CheatMode.cs                Main entry: settings, faction cascade, ammo scan, ESP rendering
├── CheatAmmoPatches.cs         Infinite ammo & no-reload patches (vehicles, infantry, throwables, emplacements)
├── CheatFireSupportPatches.cs  Artillery & CAS fire-support cheats
├── CheatInvinciblePatches.cs   Invincibility damage-filter patches
├── CheatESPPatches.cs          ESP unit-tracking patches
├── Render.cs                   IMGUI drawing helpers
├── Properties/AssemblyInfo.cs
└── CheatMode.csproj
```

---

## 8. License & Credits

Licensed under the **GNU Affero General Public License v3.0** (see [LICENSE](LICENSE)).

CheatMode incorporates code derived from community mods distributed under the following licenses; please respect their authors:

- [Invincible-Tank](https://github.com/QwertyRyo/Invincible-Tank) — AGPL-3.0 (QwertyRyo)
- [InfiniteAmmo](https://github.com/Bluehawk8908/InfiniteAmmo) — GPL-3.0 (Bluehawk8908)
- [GHPCESP](https://github.com/k4yt3x/GHPCESP) — MIT (K4YT3X)
- [GHPC_Artillery_Rework](https://github.com/QwertyRyo/GHPC_Artillery_Rework) (QwertyRyo) — inspiration for the fire-support/artillery subsystem

**Related documents**

- Full comparison of CheatMode against these four mods (features, implementation differences, fixes/improvements): [docs/COMPARISON.en.md](docs/COMPARISON.en.md)
