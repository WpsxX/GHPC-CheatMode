# Changelog

All notable changes to the **CheatMode** all-in-one mod.

## [1.6.0] — 2026-09-08
Initial public/release snapshot derived from the `CheatMode` integration project. This release:

- **Documentation** — Translated every code comment into clean English and prepared a GitHub-ready project (README in EN & zh-CN, comparison report, usage tutorials, AGPL-3.0 license).
- **Clarified a "No Reload" limitation** — while `NoReload` is enabled the player cannot switch ammunition types (the current clip is refilled and reload/type-switch is skipped). Clearly marked in the in-game on-screen hint and in both usage guides / READMEs: press **F9** to disable → switch ammo → re-enable if desired.
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
