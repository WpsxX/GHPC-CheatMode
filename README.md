# CheatMode for Gunner, HEAT, PC!

<div align="center">

[![English](https://img.shields.io/badge/English-0969da?style=flat-square)](README.md)
[![简体中文](https://img.shields.io/badge/简体中文-2ea44f?style=flat-square)](README.zh-CN.md)

**English** · [简体中文](README.zh-CN.md) 

</div>

> ## ⚠️ Fire support has been REMOVED (v1.6.3)
>
> **The entire fire-support / artillery subsystem is gone in v1.6.3.** Unlimited calls, no cooldown, arrival
> time, volley size, artillery dispersion, CAS accuracy and CAS target spreading are **no longer part of
> CheatMode**, and the two built-in behaviors (faction-aware artillery damage, instant-volley compression) went
> with them. The settings below were deleted as well — old configs simply have those keys ignored:
> `InfiniteFireSupport`, `FireSupportNoCooldown`, `ArtilleryVolleyRounds`, `ArtilleryTimeToTarget`,
> `ArtilleryAccuracy`, `CasAccuracy`, `CasSpreadTargets`.
>
> CheatMode no longer patches `ArtilleryBattery`, `FireMissionManager`, `CasSupportManager`, `CASController` or
> `CASHardpoint` at all, so it cannot fight another artillery/CAS mod over the same objects any more.
> **Use a dedicated fire-support mod for those features.**
>
> **Invincibility, infinite ammo / no-reload and ESP are unaffected.**

An all-in-one MelonLoader cheat mod for [Gunner, HEAT, PC!](https://store.steampowered.com/app/1705180/Gunner_HEAT_PC/) that merges **invincibility, infinite ammo / no-reload and ESP** into a **single DLL**, adds **player-vs-friendly granularity**, and is carefully scoped so enemy units and scripted campaign events keep their vanilla behavior.

Built by integrating and hardening the ideas behind three community mods: [InfiniteAmmo](https://github.com/Bluehawk8908/InfiniteAmmo) · [Invincible-Tank](https://github.com/QwertyRyo/Invincible-Tank) · [GHPCESP](https://github.com/k4yt3x/GHPCESP). See the [detailed comparison](docs/COMPARISON.en.md).

> ⚠️ **Cheat mod.** Intended for single-player / private testing only. Not for competitive multiplayer.

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
- **ESP** — toggle vehicles, ATGM and infantry boxes together with **F8**; labels show distance.

All of the above can be switched on/off individually in `MelonPreferences.cfg`.

---

## 2. Installation

> Applies to `CheatMode.dll` v1.6.3.

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
3. **(Important)** If `Mods` still contains the standalone **`InfiniteAmmo.dll`**, **`Invincible_Tank.dll`**, **`GHPCESP.dll`**, **remove or disable them**. CheatMode already covers these features; having both causes duplicate Harmony patches and duplicate scene scans.
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

> ✅ **Ammunition switching works while "No Reload" is on (fixed in 1.6.2):** selecting another type now swaps the clip **immediately** — `NoReloadAmmoSwitchPatch` issues one `FeedNewClip()` after a real type change, which the no-reload patch turns into an instant clip swap (no reload flow, no reload animation), and it keeps "clip + breech" equal to exactly one full clip (no extra round). Only the *current* clip changes; the round already in the breech keeps its old type until it is fired. (Before 1.6.2 you had to press **`F9`** off → switch → on again.)

### 4.4 ESP

| Setting | Default | Effect |
|---|---|---|
| `ESP` | `true` | Master ESP switch. |

- Press **`F8`** in-game to toggle; shows/hides **vehicle, ATGM and infantry** boxes together.
- Faction colors: blue = Blue, red = Red, green = Green, yellow = Neutral, white = other; neutralized units turn gray.
- Each label shows name + distance, e.g. `T-80BVM [342m]`.

---

## 5. Usage Notes

1. **This is a cheat.** Use it for single-player / private testing. In some multiplayer/anti-cheat contexts this may violate rules — at your own risk.
2. **Do not run it alongside the three standalone mods.** Remove `InfiniteAmmo.dll`, `Invincible_Tank.dll` and `GHPCESP.dll` to avoid duplicate patches/scans.
3. **Config changes take effect on restart** (or via the MelonLoader preferences panel; most apply immediately).
4. **Changing built-in behavior requires recompiling** (built-in behaviors have no settings entries).
5. **`NoReload` only affects your vehicle.** Don't expect friendly AI to skip reloading.
6. **Ammunition switching is instant while "No Reload" is on** (since 1.6.2): just select the type — the clip is swapped immediately and no extra round is added. The round already in the breech keeps its old type until it is fired.
7. **Friendly detection uses an authoritative faction cascade.** In custom scenes (e.g. Fulda 1989), if you see "0 units granted", check the log line `[CheatMode] Scene scan finished: ... side=... (via ...)`, which shows the faction source (`SceneController` / `MissionData` / `PlayerUnit`) to help diagnose.
8. **Compatibility:** written against a specific GHPC build using the publicized assembly and private/internal fields via `AccessTools` reflection. A major game update may break it until adapted.
9. **Backup:** before updating the game, back up `UserData\MelonPreferences.cfg` so you can restore your settings.

---

## 6. Troubleshooting

- **Not loading / no `[CheatMode] Loaded` in log** → verify the DLL is in `Bin\Mods\`, MelonLoader version, and game path references.
- **Startup error "Another instance of CheatMode is already loaded"** → two copies are in `Mods`; delete the extra.
- **ESP invisible** → press F8 to confirm it is on; ensure the target is in view and is not the unit you are in.

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

**Related documents**

- Full comparison of CheatMode against these three mods (features, implementation differences, fixes/improvements): [docs/COMPARISON.en.md](docs/COMPARISON.en.md)
