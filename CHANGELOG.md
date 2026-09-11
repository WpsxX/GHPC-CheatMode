# Changelog

All notable changes to the **CheatMode** all-in-one mod.

## [1.6.3] — 2026-09-12

### Removed
- **The whole fire-support / artillery subsystem.** `CheatFireSupportPatches.cs` and `CheatCASPatches.cs` are gone, together with the seven settings that drove them (`InfiniteFireSupport`, `FireSupportNoCooldown`, `ArtilleryVolleyRounds`, `ArtilleryTimeToTarget`, `ArtilleryAccuracy`, `CasAccuracy`, `CasSpreadTargets`), the two always-on built-in behaviors (faction-aware artillery damage and volley compression) and the `ResolvePlayerFaction()` helper only they used. CheatMode no longer patches `ArtilleryBattery`, `FireMissionManager`, `CasSupportManager`, `CASController` or `CASHardpoint` at all, so it can no longer fight another artillery/CAS mod over the same objects - **use a dedicated fire-support mod for those features.**
- The `[GHPC_Artillery_Rework]` attribution and the artillery section of the upstream comparison were dropped with the subsystem.

### Notes
- Invincibility, infinite ammo / no-reload and ESP are untouched. Existing configs keep working: the removed keys are simply ignored, and a fresh `MelonPreferences.cfg` no longer writes them.

## [1.6.2] — 2026-09-09

### Added
- **`CasAccuracy`** — CAS launch dispersion as a fraction of the weapon's natural spread: `1.0` = natural, `0.5` = half, `0.1` = 10%, `-1.0` (or any `<= 0`) = zero spread (rounds follow the aim line exactly). `CasAccuracyPatch` scales `CASHardpoint._launchDeviation` for the duration of one `Fire()` call and restores it afterwards, so no prefab is touched and nothing accumulates. Applies to every CAS attack type (bombs, rockets, guns).
- **`CasSpreadTargets`** — a batch of CAS planes no longer all lock the same target. Vanilla lets every plane independently pick the "best score + closest to the called position" target, so sorties called together converge on one vehicle. After `CASController.SearchForTarget` has chosen `FinalTarget`, a plane whose target is already claimed by another live plane is re-routed to the nearest unclaimed enemy — first from its own spotted list (visibility and attackability already filtered by vanilla), then from every live enemy within 3 km of the called position that it actually has a weapon for. `EnterState(TurnTowardTarget)` is re-run so weapon choice and release distance follow the new target, and every decision is logged (`[CheatMode] CAS spread: ...`). Only changes anything when several enemies are in reach.

### Fixed
- **Ammunition-type switching while "No Reload" is enabled** — previously documented as a limitation, now fixed. Vanilla only stores the new type in `QueuedClipType` and loads it from `ResumeClipLoadIfEmpty()`, which calls `FeedNewClip()` only when the clip is empty; because "no reload" keeps the player's clip permanently full, a switch never took effect. `NoReloadAmmoSwitchPatch` (postfix on `AmmoFeed.SetNextClipType`) now issues one `FeedNewClip()` after a real type change: the no-reload patch turns that into an immediate clip swap (no reload flow, no reload animation) and the patch also performs the exclusive-item toggle vanilla would have done in `FinishClipReload`. Only the player's currently controlled vehicle is affected; an unchanged type, an in-progress reload or a disabled cheat keeps vanilla behavior.
- **One extra round was added when switching ammunition types.** `RefillVehicleLoadedClip` now takes `keepChamberedRound`: when a round is already in the breech (the old type, kept across a switch), the queue is filled to `Capacity - 1` so "clip + breech" equals exactly one full clip instead of `Capacity + 1`. The pre-fire refill still fills the whole clip, because that chambered round is about to be fired.

## [1.6.1] — 2026-09-08

### Fixed
- **Rounds losing their ranged-fuse ("zeroing") data at sustained/high rates of fire.** When a `LiveRound` is taken from the object pool, `RestoreFromPool()` only raises `_needsRestart`, then `Init() -> resetLocalValues()` clears `_rangedFuseCountdown` to `0`; the per-round fuse value is written in `Start()`, which vanilla defers until the next `DoUpdate()`. Because ammunition feeds push the next round immediately (`AutoFeed -> FeedNewRound()` inside `WeaponFired`), a following `Init()` could land inside that window and re-clear the pending state, so the affected round satisfied its fuse instantly and detonated far short of the intended point — an occasional "rounds land way too close" symptom while firing fast. `LiveRoundInitFuseFixPatch` now runs the pending restart at the end of `Init()`, so every round leaves `Init()` holding its own correct fuse value that later rounds cannot clear. No reload behaviour, feed timing or ballistics are changed.

## [1.6.0] — 2026-09-08
Initial public/release snapshot derived from the `CheatMode` integration project. This release:

- **Documentation** — Translated every code comment into clean English and prepared a GitHub-ready project (README in EN & zh-CN, comparison report, usage tutorials, AGPL-3.0 license).
- **Clarified a "No Reload" limitation** — while `NoReload` is enabled the player cannot switch ammunition types (the current clip is refilled and reload/type-switch is skipped). Clearly marked in the in-game on-screen hint and in both usage guides / READMEs: press **F9** to disable → switch ammo → re-enable if desired. *(Fixed in 1.6.2: ammunition switching now works while "No Reload" is on.)*
- **Feature set (v1.6.0)** — the integrated mod exposes the full surface below:

### Invincibility
- Self and friendly invincibility exposed as two independent settings (`SelfInvincible`, `FriendlyInvincible`).

### Ammo
- Infinite ammo for player &/or friendly vehicles, infantry, throwables and crew-served emplacements.
- "No reload" now applies to the player's currently controlled vehicle only (friendly AI keep vanilla reload); toggled with **F9**.

### Fire support / artillery
- Unlimited player-side artillery / CAS calls.
- No cooldown for player-side artillery & CAS.
- Faction-aware artillery damage (always on): player fires live, enemy fires blanks.
- `ArtilleryVolleyRounds` (1–128 or -1 = vanilla), `ArtilleryTimeToTarget` (ratio / instant), `ArtilleryAccuracy` (dispersion ratio / zero).

### ESP
- Unified **F8** toggle for vehicles / ATGM / infantry; labels show distance.

### Fixes & hardening vs. upstream mods
- Never pollute battery template fields → no cumulative scaling bug.
- Script-planned strikes & temporary batteries always stay vanilla.
- NoReload restricted to player vehicle (behavioral realism for friendly AI).
- Replenish only the clips actually consumed (reserve never inflates).
- Removed fragile `"Malyutka ammo"` name-based special case.
- Pervasive null-guards, AAR-replay protection, ammo-rack-fire protection.
- Single instance / single `PatchAll()` (no duplicate patches across four mods).
- Authoritative faction cascade shared across all subsystem decisions.

### Breaking / notes for upgraders
- If migrating from the four standalone mods, **remove** `InfiniteAmmo.dll`, `Invincible_Tank.dll` and `GHPCESP.dll` from `Bin\Mods\`.

[Unreleased]: planned but not committed.
