using System;
using HarmonyLib;
using GHPC;
using GHPC.Infantry;
using GHPC.Infantry.Weapons;
using GHPC.Weaponry;
using GHPC.Weapons;
using UnityEngine;

namespace CheatMode
{
    /// <summary>
    /// Reload-start patch, applied before AmmoFeed.FeedNewClip. For weapons managed by this mod
    /// (player + friendlies), if the ammo rack no longer holds a matching clip at the moment a
    /// reload begins (e.g. the unit was only marked mid-mission, or the rack was emptied by some
    /// other cause), one logical clip is added on the spot so the reload loop can always start.
    /// Without this the game refuses to reload whenever the reserve rack is empty, leaving the AI
    /// stuck on that weapon -- which is why friendly vehicles would otherwise go quiet mid-fight.
    /// </summary>
    [HarmonyPatch(typeof(GHPC.Weapons.AmmoFeed), "FeedNewClip")]
    public static class AmmoFeedEnsureClipPatch
    {
        private static void Prefix(GHPC.Weapons.AmmoFeed __instance)
        {
            if (!CheatModeMod.Managed(__instance))
            {
                return;
            }

            GHPC.Weapons.AmmoRack readyRack = __instance.ReadyRack;
            AmmoType.AmmoClip queuedClipType = __instance.QueuedClipType;
            if (readyRack == null || queuedClipType == null || !readyRack.RackSafeToUse)
            {
                return;
            }
            if (!readyRack.HasClipWithPattern(queuedClipType))
            {
                readyRack.AddInvisibleClip(queuedClipType);
            }
        }
    }

    /// <summary>
    /// No-reload patch, applied before AmmoFeed.FeedNewClip. When enabled, only the vehicle the
    /// player currently controls skips the reload flow after its clip empties; instead its current
    /// feed queue (the clip) is refilled in place. Friendly AI vehicles reload normally even if they
    /// carry a marker (and enjoy infinite ammo). Because the firing patch keeps the clip full after
    /// every shot, FeedNewClip is normally never called; this patch acts as a safety net covering
    /// manual reloads, ammunition-type switches and abnormal firing loops that trigger a reload.
    /// </summary>
    [HarmonyPatch(typeof(GHPC.Weapons.AmmoFeed), "FeedNewClip")]
    public static class NoReloadFeedNewClipPatch
    {
        private static bool Prefix(GHPC.Weapons.AmmoFeed __instance)
        {
            if (!CheatModeMod.NoReloadEnabled || !CheatModeMod.Managed(__instance)
                || !CheatModeMod.IsPlayerVehicleComponent(__instance))
            {
                return true; // Disabled / not managed by this mod / not the player vehicle: keep the vanilla reload flow.
            }

            // If already reloading (e.g. a leftover state from when the setting was just enabled), let vanilla Update handle it.
            if (__instance.Reloading)
            {
                return true;
            }

            AmmoType.AmmoClip queuedClipType = __instance.QueuedClipType;
            if (queuedClipType == null)
            {
                return true;
            }

            // keepChamberedRound: an ammunition-type switch / manual reload can leave the old type's
            // round in the breech, so fill only Capacity - 1 into the queue and keep "clip + breech"
            // equal to exactly one full clip (otherwise the switch would add an extra round).
            CheatModeMod.RefillVehicleLoadedClip(__instance, queuedClipType, true);

            // When the manual-reload command / fallback reload path is triggered, the breech may be empty;
            // if auto-feeding is on, immediately start the round-feeding loop so the turret does not sit
            // "armed but unable to fire".
            if (__instance.AutoFeed && __instance.AmmoTypeInBreech == null && !__instance.Cycling)
            {
                __instance.FeedNewRound();
            }

            return false; // Skip the vanilla FeedNewClip and never enter the Reloading state.
        }
    }

    /// <summary>
    /// Ammunition-type switching while "no reload" is on, applied after AmmoFeed.SetNextClipType.
    ///
    /// Vanilla only stores the new type in QueuedClipType and then lets ResumeClipLoadIfEmpty()
    /// decide whether to load it - and that method only calls FeedNewClip() when the clip is empty.
    /// Because "no reload" keeps the player's clip permanently full, the new type never got loaded,
    /// so ammunition could not be switched while the cheat was enabled.
    ///
    /// This postfix issues one FeedNewClip() after a real type change: it is caught by
    /// NoReloadFeedNewClipPatch, which swaps the loaded clip to QueuedClipType immediately (no
    /// reload flow, no reload animation), and it also performs the exclusive-item toggle that
    /// vanilla would have done in FinishClipReload for weapons with
    /// ExclusivesChangeBeforeReload == false. Only the player's currently controlled vehicle is
    /// affected; an unchanged type, an in-progress reload or a disabled cheat keeps vanilla behavior.
    /// </summary>
    [HarmonyPatch(typeof(GHPC.Weapons.AmmoFeed), "SetNextClipType")]
    public static class NoReloadAmmoSwitchPatch
    {
        private static readonly System.Reflection.MethodInfo ToggleExclusiveItemsMethod =
            AccessTools.Method(typeof(GHPC.Weapons.AmmoFeed), "ToggleExclusiveItems");

        private static void Postfix(GHPC.Weapons.AmmoFeed __instance)
        {
            if (__instance == null)
            {
                return;
            }
            if (!CheatModeMod.NoReloadEnabled || !CheatModeMod.Managed(__instance)
                || !CheatModeMod.IsPlayerVehicleComponent(__instance))
            {
                return;
            }
            if (__instance.Reloading)
            {
                return; // Already reloading: let the vanilla flow finish.
            }

            AmmoType.AmmoClip queued = __instance.QueuedClipType;
            if (queued == null)
            {
                return;
            }
            if (__instance.LoadedClipType != null && __instance.LoadedClipType.Equals(queued))
            {
                return; // Same type, or the FeedNewClip patch already swapped it.
            }

            // Trigger one clip swap: NoReloadFeedNewClipPatch turns it into "load QueuedClipType now".
            __instance.FeedNewClip();

            // Weapons with ExclusivesChangeBeforeReload == false toggle their exclusive items inside
            // FinishClipReload, which is skipped here because no real reload happens; do it manually.
            if (!__instance.ExclusivesChangeBeforeReload && ToggleExclusiveItemsMethod != null)
            {
                try
                {
                    ToggleExclusiveItemsMethod.Invoke(__instance, new object[] { queued });
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Error("[CheatMode] failed to toggle exclusive ammo items: " + ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// No-reload firing patch, applied before AmmoFeed.WeaponFired. Only for the vehicle the player
    /// currently controls: the current clip is refilled before each shot. Vanilla WeaponFired then
    /// clears the chambered round and feeds the next one via FeedNewRound, so the player's clip count
    /// never reaches zero and FeedNewClip / the reload progress bar is never triggered. Friendly AI
    /// vehicles are unaffected (their clips empty and reload normally, while still enjoying infinite
    /// reserve ammo).
    /// </summary>
    [HarmonyPatch(typeof(GHPC.Weapons.AmmoFeed), "WeaponFired")]
    public static class NoReloadWeaponFiredPatch
    {
        private static void Prefix(GHPC.Weapons.AmmoFeed __instance, AmmoType ammoType, LiveRound round)
        {
            if (!CheatModeMod.NoReloadEnabled || !CheatModeMod.Managed(__instance)
                || !CheatModeMod.IsPlayerVehicleComponent(__instance))
            {
                return;
            }

            AmmoType.AmmoClip clipType = __instance.LoadedClipType != null ? __instance.LoadedClipType : __instance.QueuedClipType;
            if (clipType == null)
            {
                return;
            }

            CheatModeMod.RefillVehicleLoadedClip(__instance, clipType);
        }
    }

    /// <summary>
    /// Reload-complete patch, applied around AmmoFeed.FinishClipReload. It records how many clips the
    /// vanilla FinishClipReload actually removed from the reserve rack, then replenishes exactly that
    /// same number -- never more -- so the reserve stock does not mysteriously grow.
    /// </summary>
    [HarmonyPatch(typeof(GHPC.Weapons.AmmoFeed), "FinishClipReload")]
    public static class CheatModeReplenishPatch
    {
        internal static readonly AccessTools.FieldRef<GHPC.Weapons.AmmoRack, bool> VisualSlotSetupRef =
            AccessTools.FieldRefAccess<GHPC.Weapons.AmmoRack, bool>("_didVisualSlotSetup");

        private static readonly AccessTools.FieldRef<GHPC.Weapons.AmmoFeed, AmmoType.AmmoClip> QueuedClipTypeLockedInRef =
            AccessTools.FieldRefAccess<GHPC.Weapons.AmmoFeed, AmmoType.AmmoClip>("_queuedClipTypeLockedIn");

        /// <summary>
        /// Replenishes one clip back onto the ammo rack. For racks with visible rounds it first tries to
        /// refill an empty slot (keeping the visual consistent); when all slots are full, visual setup is
        /// incomplete, or the rack is purely logical, only the logical stock is replenished.
        /// </summary>
        internal static void ReplenishClip(GHPC.Weapons.AmmoRack readyRack, AmmoType.AmmoClip clipType)
        {
            if (readyRack == null || clipType == null || !readyRack.RackSafeToUse)
            {
                return;
            }

            if (readyRack.UseVisibleRounds && VisualSlotSetupRef(readyRack))
            {
                int clipsBefore = readyRack.StoredClips.Count;
                readyRack.AddClipToAnySlot(clipType);
                if (readyRack.StoredClips.Count == clipsBefore)
                {
                    readyRack.AddInvisibleClip(clipType);
                }
            }
            else
            {
                readyRack.AddInvisibleClip(clipType);
            }
        }

        private static int CountClips(GHPC.Weapons.AmmoRack readyRack, AmmoType.AmmoClip clipType)
        {
            if (readyRack == null || readyRack.StoredClips == null || clipType == null)
            {
                return 0;
            }

            int count = 0;
            foreach (AmmoType.AmmoClip storedClip in readyRack.StoredClips)
            {
                if (storedClip != null && storedClip.Equals(clipType))
                {
                    count++;
                }
            }
            return count;
        }

        private static void Prefix(GHPC.Weapons.AmmoFeed __instance, out int __state)
        {
            __state = 0;

            if (!CheatModeMod.Managed(__instance))
            {
                return;
            }

            GHPC.Weapons.AmmoRack readyRack = __instance.ReadyRack;
            AmmoType.AmmoClip queuedClipType = QueuedClipTypeLockedInRef(__instance);
            if (queuedClipType == null)
            {
                queuedClipType = __instance.QueuedClipType;
            }
            if (readyRack == null || queuedClipType == null)
            {
                return;
            }

            __state = CountClips(readyRack, queuedClipType);
        }

        private static void Postfix(GHPC.Weapons.AmmoFeed __instance, int __state)
        {
            if (!CheatModeMod.Managed(__instance))
            {
                return;
            }

            GHPC.Weapons.AmmoRack readyRack = __instance.ReadyRack;
            AmmoType.AmmoClip loadedClipType = __instance.LoadedClipType != null ? __instance.LoadedClipType : __instance.QueuedClipType;
            if (readyRack == null || loadedClipType == null)
            {
                return;
            }

            // When the ammo rack is on fire / overheating, the game itself does not draw rounds
            // (TryFinalFeedNewClip refuses), so do not replenish either -- we must not feed a burning rack.
            if (!readyRack.RackSafeToUse)
            {
                return;
            }

            int consumed = __state - CountClips(readyRack, loadedClipType);
            for (int i = 0; i < consumed; i++)
            {
                ReplenishClip(readyRack, loadedClipType);
            }
        }
    }

    /// <summary>
    /// Dual-feed weapons (the BMP-2's 2A42, some anti-aircraft guns, etc.) prefetch the other belt from
    /// the rack via FeedNonSelectedClip when switching ammunition types. This patch covers that
    /// consumption point, otherwise the secondary belt would gradually run dry after switching.
    /// </summary>
    [HarmonyPatch(typeof(GHPC.Weapons.AmmoFeed), "FeedNonSelectedClip")]
    public static class AmmoFeedDualFeedPatch
    {
        private static readonly AccessTools.FieldRef<GHPC.Weapons.AmmoFeed, bool> AuxFeedModeRef =
            AccessTools.FieldRefAccess<GHPC.Weapons.AmmoFeed, bool>("_auxFeedMode");

        private static void Postfix(GHPC.Weapons.AmmoFeed __instance)
        {
            if (!CheatModeMod.Managed(__instance))
            {
                return;
            }

            GHPC.Weapons.AmmoRack readyRack = __instance.ReadyRack;
            if (readyRack == null || readyRack.ClipTypes == null || readyRack.ClipTypes.Length < 2 || !readyRack.RackSafeToUse)
            {
                return;
            }

            // The clip that was just taken is necessarily the one on the "non-selected" side.
            AmmoType.AmmoClip consumedType = AuxFeedModeRef(__instance) ? readyRack.ClipTypes[0] : readyRack.ClipTypes[1];
            if (consumedType != null && !readyRack.HasClipWithPattern(consumedType))
            {
                readyRack.AddInvisibleClip(consumedType);
            }
        }
    }

    /// <summary>
    /// Infinite-ammo patch for infantry weapons (rifles / squad machine guns / sniper rifles / rocket
    /// launchers, etc.), applied before TryFire. For every infantryman managed by this mod, the magazine
    /// and reserve are refilled before each firing attempt. Because the magazine never empties, the
    /// infantryman never enters a reload flow that consumes reserve / ammo-crate ammunition, enabling
    /// sustained fully-automatic fire. It also heals abnormal states such as "no ammo from the start" or
    /// "reload interrupted mid-way".
    /// </summary>
    [HarmonyPatch(typeof(InfantryWeaponSystem), "TryFire")]
    public static class InfantryWeaponAmmoPatch
    {
        private static readonly AccessTools.FieldRef<InfantryWeaponSystem, int> CurrentClipAmmoRef =
            AccessTools.FieldRefAccess<InfantryWeaponSystem, int>("_currentClipAmmo");

        private static readonly AccessTools.FieldRef<InfantryWeaponSystem, int> ClipCountRef =
            AccessTools.FieldRefAccess<InfantryWeaponSystem, int>("_clipCount");

        private static readonly AccessTools.FieldRef<InfantryWeaponSystem, int> MaxClipCountRef =
            AccessTools.FieldRefAccess<InfantryWeaponSystem, int>("_maxClipCount");

        private static readonly AccessTools.FieldRef<InfantryWeaponSystem, bool> CanFireRef =
            AccessTools.FieldRefAccess<InfantryWeaponSystem, bool>("_canFire");

        private static void Prefix(InfantryWeaponSystem __instance)
        {
            if (__instance == null)
            {
                return;
            }

            InfantryUnit unit = __instance.InfantryUnit;
            bool managed = (unit != null && unit.GetComponent<CheatMarker>() != null)
                || (unit == null && __instance.GetComponentInParent<CheatMarker>() != null);
            if (!managed)
            {
                return;
            }

            RefillWeapon(__instance);
        }

        /// <summary>Refills an infantry weapon: full magazine, at least 1 reserve clip (without shrinking the current count), and restores the can-fire state.</summary>
        internal static void RefillWeapon(InfantryWeaponSystem weapon)
        {
            if (weapon == null)
            {
                return;
            }
            AmmoType.AmmoClip clipType = weapon.ClipType;
            if (clipType == null || clipType.Capacity <= 0)
            {
                return;
            }

            CurrentClipAmmoRef(weapon) = clipType.Capacity;
            ClipCountRef(weapon) = Math.Max(ClipCountRef(weapon), Math.Max(1, MaxClipCountRef(weapon)));
            CanFireRef(weapon) = true;
        }
    }

    /// <summary>
    /// Infinite-ammo patch for infantry throwables (grenades / rifle grenades / anti-tank grenades),
    /// applied before TryThrow. For every infantryman managed by this mod, the count is refilled before
    /// each throw.
    /// </summary>
    [HarmonyPatch(typeof(InfantryThrowableWeaponSystem), "TryThrow")]
    public static class InfantryThrowableAmmoPatch
    {
        private static readonly AccessTools.FieldRef<InfantryThrowableWeaponSystem, int> CurrentAvailableRef =
            AccessTools.FieldRefAccess<InfantryThrowableWeaponSystem, int>("_currentAvailable");

        private static readonly AccessTools.FieldRef<InfantryThrowableWeaponSystem, int> MaxAmountRef =
            AccessTools.FieldRefAccess<InfantryThrowableWeaponSystem, int>("_maxAmount");

        private static void Prefix(InfantryThrowableWeaponSystem __instance)
        {
            if (__instance == null)
            {
                return;
            }

            InfantryUnit unit = __instance.InfantryUnit;
            bool managed = (unit != null && unit.GetComponent<CheatMarker>() != null)
                || (unit == null && __instance.GetComponentInParent<CheatMarker>() != null);
            if (!managed)
            {
                return;
            }

            RefillThrowable(__instance);
        }

        /// <summary>Refills a throwable weapon's count to its maximum.</summary>
        internal static void RefillThrowable(InfantryThrowableWeaponSystem throwable)
        {
            if (throwable == null)
            {
                return;
            }
            CurrentAvailableRef(throwable) = MaxAmountRef(throwable);
        }
    }

    /// <summary>
    /// Infinite-ammo patch for crew-served weapon ammo crates (AmmoSupplies, e.g. the crate beside a
    /// tripod-mounted machine gun). For a managed emplacement, taking ammo always succeeds without
    /// reducing the crate's stock. This is the final safety net behind the infantry weapon / throwable
    /// patches: no matter which code path draws from an ammo crate, it always finds ammo available.
    /// </summary>
    [HarmonyPatch(typeof(AmmoSupplies), "TryTakeClips")]
    public static class AmmoSuppliesClipsPatch
    {
        private static bool Prefix(AmmoSupplies __instance, int clipsToTake, ref int clipsTaken, ref bool __result)
        {
            if (!CheatModeMod.Managed(__instance))
            {
                return true; // Not managed by this mod: keep the original logic.
            }
            if (clipsToTake <= 0)
            {
                clipsTaken = 0;
                __result = false;
                return false;
            }
            clipsTaken = clipsToTake;
            __result = true;
            return false; // Skip the original method: succeed directly without deducting stock.
        }
    }

    /// <summary>Grenade / rifle-grenade portion of the crew-served weapon ammo crates; same as above.</summary>
    [HarmonyPatch(typeof(AmmoSupplies), "TryTakeGrenades")]
    public static class AmmoSuppliesGrenadesPatch
    {
        private static bool Prefix(AmmoSupplies __instance, int grenadesToTake, ref int grenadesTaken, ref bool __result)
        {
            if (!CheatModeMod.Managed(__instance))
            {
                return true; // Not managed by this mod: keep the original logic.
            }
            if (grenadesToTake <= 0)
            {
                grenadesTaken = 0;
                __result = false;
                return false;
            }
            grenadesTaken = grenadesToTake;
            __result = true;
            return false; // Skip the original method: succeed directly without deducting stock.
        }
    }

    /// <summary>
    /// Fixes pooled rounds losing their per-round fuse ("zeroing") data at high rates of fire.
    ///
    /// Vanilla sequence for a round taken from the object pool:
    ///   1. RestoreFromPool() only raises the private _needsRestart flag;
    ///   2. Init() -> resetLocalValues() clears the ranged fuse (_rangedFuseCountdown = 0,
    ///      _rangedFuseActive = false);
    ///   3. the real per-round fuse value is written in Start(), which vanilla defers until the
    ///      next DoUpdate() (it restarts there when _needsRestart is still set).
    ///
    /// That leaves a window in which the live round is already spawned but still carries a zeroed
    /// fuse. Ammunition feeds push the next round immediately (vanilla AutoFeed calls
    /// FeedNewRound() inside WeaponFired), so at sustained/high rates of fire a following Init()
    /// can land inside that window and re-clear the pending state; the affected round then
    /// satisfies its fuse instantly and detonates far short of the intended point.
    ///
    /// Running the pending restart right after Init() writes the fuse value in the same call, so
    /// every round leaves Init() holding its own correct value that later rounds cannot clear.
    /// Nothing else about firing is touched: no reload behaviour, no feed timing, no ballistics.
    /// </summary>
    [HarmonyPatch(typeof(GHPC.Weapons.LiveRound), "Init")]
    public static class LiveRoundInitFuseFixPatch
    {
        private static readonly AccessTools.FieldRef<GHPC.Weapons.LiveRound, bool> NeedsRestartRef =
            AccessTools.FieldRefAccess<GHPC.Weapons.LiveRound, bool>("_needsRestart");

        private static void Postfix(GHPC.Weapons.LiveRound __instance)
        {
            if (__instance == null)
            {
                return;
            }

            // Only pooled rounds pending a restart are affected; a fresh round has already had
            // its fuse written by its own Start().
            if (!NeedsRestartRef(__instance))
            {
                return;
            }

            // Restart() clears _needsRestart and runs Start(), which restores
            // _rangedFuseCountdown from Info.RangedFuseTime. Vanilla's own DoUpdate check then
            // sees _needsRestart == false and will not run it a second time.
            __instance.Restart();
        }
    }
}
