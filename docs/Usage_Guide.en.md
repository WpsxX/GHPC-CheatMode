# CheatMode Usage Guide (English)

> Applies to `CheatMode.dll` v1.6.0. Covers **installation, feature reference, and usage notes**.

---

## 1. Installation

### Prerequisites
- A legitimate copy of **Gunner, HEAT, PC!** on Steam.
- **MelonLoader** installed (v0.6.1+ recommended). If not installed, run the official installer:
  `https://github.com/LavaGang/MelonLoader.Installer/releases/latest/download/MelonLoader.Installer.exe`, pick GHPC, and install.

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

> Verify it loaded: the MelonLoader console / `Latest.log` should show
> `[CheatMode] Loaded. F8 = ESP on/off. F9 = no reload on/off. ...`

### Uninstall
- Just delete `CheatMode.dll` from `Bin\Mods\`. No leftover files.

---

## 2. Feature Reference

CheatMode is split into five blocks; each can be toggled independently.

### 1. Invincibility
| Setting | Default | Effect |
|---|---|---|
| `SelfInvincible` | true | The unit you are currently controlling takes no damage and cannot be destroyed. |
| `FriendlyInvincible` | true | Same-faction friendly AI units take no damage and cannot be destroyed. |

- The two tiers are independent: you can have "self invincible + AI fragile" or "self fragile + AI invincible".
- Protection is layered across 18 patch points (penCheck, component damage, overpressure, human kills, run-overs, compartment blowout, unit-destroyed markers, etc.). Blocked hits keep their impact VFX / AAR record intact (shown as a non-penetrating hit rather than vanishing).

### 2. Infinite Ammo
| Setting | Default | Effect |
|---|---|---|
| `SelfInfiniteAmmo` | true | Your unit never runs out of ammo. |
| `FriendlyInfiniteAmmo` | true | Friendly AI (vehicles / infantry / throwables / emplacements) never run out. |

- Covers main guns, autocannons, coaxial/top/RC machine guns, missiles; infantry rifles/MGs/sniper/rocket launchers; grenades; tripod emplacements and their ammo crates.
- Any rack that is emptied is auto-seeded with a logical clip, preventing "AI stuck / goes quiet".
- **Enemy units are unaffected** and consume ammo normally.

### 3. No Reload — player vehicle only
| Setting | Default | Notes |
|---|---|---|
| `NoReload` | true | See below |

- When on, **only the vehicle you currently control** skips the reload flow and refills the clip directly for sustained fire.
- **Friendly AI vehicles are unaffected**: even though they have infinite reserve ammo, they keep the vanilla reload cadence (more realistic, not overpowered).
- Press **`F9`** in-game to toggle; while on, the top-left shows `CheatMode No Reload Enabled`.
- It only manages weapons that belong to your vehicle and that have at least one infinite-ammo toggle active.

> ✅ **Ammunition switching now works while "No Reload" is on (fixed in 1.6.2)**: selecting another type swaps the clip **immediately** (no reload flow, no animation), and "clip + breech" stays exactly one full clip — no extra round is added. The round already in the breech keeps its old type until it is fired.
> (Before 1.6.2 the workflow was: press **`F9`** to disable → switch ammo → press **`F9`** to re-enable.)

### 4. Fire Support / Artillery
| Setting | Default | Effect |
|---|---|---|
| `InfiniteFireSupport` | true | Player-side artillery / CAS **calls never run out**. |
| `FireSupportNoCooldown` | true | Player-side artillery / CAS **have no cooldown** — call again immediately. |
| `ArtilleryVolleyRounds` | -1 | Rounds per volley; `-1` = vanilla; otherwise a fixed 1–128. |
| `ArtilleryTimeToTarget` | 1.0 | Arrival-time ratio; `-1.0`/`<=0` = instant. |
| `ArtilleryAccuracy` | 1.0 | Dispersion ratio; smaller = more accurate; `<=0` = all rounds on one point. |
| `CasAccuracy` | 1.0 | CAS launch dispersion ratio of the weapon's natural spread; smaller = tighter; `<=0` = zero spread (rounds follow the aim line). All CAS attack types. |
| `CasSpreadTargets` | true | CAS planes pick different targets instead of all locking the same one. |

- These fire-support parameters apply **only to batteries/airframes on the player's faction**; enemy batteries and **script-planned story strikes always stay vanilla**, so campaign scripts are never disturbed.
- **Faction-aware artillery damage (built-in, cannot be disabled)**: your faction's artillery fires live (damaging) rounds; the enemy's fires blanks (no damage). To disable, you must edit `ArtilleryFactionAwareEnabled` in `CheatMode.cs` and recompile.
- **Volley compression (built-in)**: when `ArtilleryTimeToTarget <= 0` ("instant"), the whole volley is fired on the same frame (a true single-volley strike) and the panel countdown is zeroed.
- Note: as soon as `ArtilleryTimeToTarget < 1`, the first-round delay is removed (the first round drops immediately); the setting only controls the interval between rounds. Example (2S3, vanilla 30 s first delay, 12 rounds, 3 s interval): `0.5` → no first delay, 1.5 s interval, ~16.5 s to finish; `-1.0` → all 12 rounds drop on the same frame.

### 5. ESP
| Setting | Default | Effect |
|---|---|---|
| `ESP` | true | Master ESP switch. |

- Press **`F8`** in-game to toggle; shows/hides **vehicle, ATGM and infantry** boxes together.
- Faction colors: blue = Blue, red = Red, green = Green, yellow = Neutral, white = other; neutralized units turn gray.
- Each label shows name + distance, e.g. `T-80BVM [342m]`.

---

## 3. Usage Notes

1. **This is a cheat.** Use it for single-player / private testing. In some multiplayer/anti-cheat contexts this may violate rules — at your own risk.
2. **Do not run it alongside the four standalone mods.** Remove `InfiniteAmmo.dll`, `Invincible_Tank.dll`, `GHPCESP.dll`, and any older artillery DLL to avoid duplicate patches/scans.
3. **Config changes take effect on restart** (or via the MelonLoader preferences panel; most apply immediately, and artillery parameters apply on the next call).
4. **Changing built-in behavior requires recompiling** (e.g. `ArtilleryFactionAwareEnabled`, volley compression have no settings).
5. **`NoReload` only affects your vehicle.** Don't expect friendly AI to skip reloading.
6. **Ammunition switching is instant while "No Reload" is on** (since 1.6.2): select the type and the clip is swapped immediately, with no extra round. The chambered round keeps its old type until fired.
7. **Friendly detection uses an authoritative faction cascade.** In custom scenes (e.g. Fulda 1989), if you see "0 units granted", check the log line `[CheatMode] Scene scan finished: ... side=... (via ...)`, which shows the faction source (`SceneController` / `MissionData` / `PlayerUnit`) to help diagnose.
8. **AAR / replay**: instant artillery volleys are deliberately handed back to the vanilla per-round flow during AAR replay to avoid desync.
9. **Compatibility**: written against a specific GHPC build using the publicized assembly and private/internal fields via `AccessTools` reflection. A major game update may break it until adapted.
10. **Backup**: before updating the game, back up `UserData\MelonPreferences.cfg` so you can restore your settings.

---

## Appendix: Quick troubleshooting
- **Not loading / no `[CheatMode] Loaded` in log** → verify the DLL is in `Bin\Mods\`, MelonLoader version, and game path references.
- **Startup error "Another instance of CheatMode is already loaded"** → two copies are in `Mods`; delete the extra.
- **ESP invisible** → press F8 to confirm it is on; ensure the target is in view and is not the unit you are in.
- **Enemy artillery still fires** → that is expected. Faction-aware damage only makes the enemy fire *blanks*; if you see enemy shells with actual effect, that battery is a temporary scripted strike (by design it stays vanilla).
