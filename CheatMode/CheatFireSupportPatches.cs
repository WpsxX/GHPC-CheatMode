using System;
using System.Collections.Generic;
using System.Reflection;
using GHPC;
using GHPC.Weaponry.CAS;
using GHPC.Weapons.Artillery;
using HarmonyLib;
using UnityEngine;

namespace CheatMode
{
    /// <summary>
    /// Fire-support cheats (all are pure configuration options: no hotkeys, no on-screen hints):
    /// 1. InfiniteFireSupport -- the round counts of the player faction's artillery / CAS are never
    ///    depleted;
    /// 2. FireSupportNoCooldown -- fire support has no cooldown:
    ///    - Artillery: vanilla cooldown bookkeeping is preserved (CooldownManager panel entries need a
    ///      cooldown &gt; 0 for the whole mission to run the "Incoming" state normally); a DoUpdate
    ///      postfix clears the residual cooldown left after a volley ends, so another call can be made
    ///      as soon as the volley is done and the panel returns to Ready;
    ///    - CAS: the RemainingCooldown write is intercepted (the CAS panel uses a fixed-duration timer
    ///      and shows Ready as soon as the cooldown is 0), so another call can be made without waiting;
    /// 3. Faction-aware damage (always on, no settings toggle): the player faction's artillery fires
    ///    live rounds (with damage), while the enemy faction's artillery fires blanks (no damage); to
    ///    disable it you must edit CheatModeMod.ArtilleryFactionAwareEnabled;
    /// 4. ArtilleryTimeToTarget -- arrival-time percentage (player-faction batteries and on-call calls
    ///    only): 1.0 = vanilla, 0.5 = half the time, 0.1 = 10% of vanilla, -1.0 (or any &lt;=0) = instant.
    ///    Note: a strike's total arrival time = first-round delay (_onCallImpactDelay)
    ///    + volley spread time ((rounds-1) x per-round interval _interShotDelaySeconds)
    ///    + shell flight time (spawn point ~346 m above the target / slant range).
    ///    When not instant, only the first-round delay is shortened; the shells still land one volley
    ///    at a time with the vanilla interval (see point 5).
    /// 5. Volley compression (built-in behavior, hidden, no settings toggle): enabled automatically only
    ///    for "instant arrival" -- every per-round interval is zeroed and all of the volley's rounds are
    ///    fired on the same frame the call succeeds (a true single-volley strike), and the panel's
    ///    DelayAmount is zeroed accordingly. Other arrival times are not compressed.
    /// 6. ArtilleryVolleyRounds -- rounds per volley: -1 (default) = use the battery's own count,
    ///    otherwise each volley fires a fixed number of rounds (1..128). The count takes effect on the
    ///    per-call quota _currentShotQuota in DoSingleShot, and the number fired "all at once" for
    ///    instant arrival follows it as well.
    /// 7. ArtilleryAccuracy -- artillery accuracy (dispersion-radius ratio, player-faction batteries
    ///    only): 1.0 = vanilla spread, smaller = more accurate (0.1 = 10% of vanilla), -1.0 (or any
    ///    &lt;=0) = zero dispersion, every round lands on the same point.
    ///
    /// Items 2a/4/5/6/7 all affect only player-faction batteries; enemy-faction batteries and
    /// script-planned strikes keep the vanilla behavior.
    ///
    /// Faction detection: a battery is located by checking whether it belongs to the blue / red arrays
    /// of FireMissionManager / CasSupportManager. Script-planned one-off strikes (temporary batteries
    /// with ignoreCooldown == true, which belong to no faction array) always keep the vanilla behavior,
    /// so campaign scripted events are never disturbed.
    /// </summary>
    public static class CheatFireSupportPatches
    {
        private static CasSupportManager _casManagerCache;

        private static CasSupportManager GetCasSupportManager()
        {
            if (_casManagerCache == null)
            {
                _casManagerCache = UnityEngine.Object.FindObjectOfType<CasSupportManager>();
            }
            return _casManagerCache;
        }

        /// <summary>Resolves the faction an artillery battery belongs to: blue/red, or Neutral when not found (temporary scripted battery).</summary>
        private static Faction GetBatteryFaction(ArtilleryBattery battery)
        {
            if (battery == null)
            {
                return Faction.Neutral;
            }

            FireMissionManager manager = FireMissionManager.Instance;
            if (manager == null)
            {
                return Faction.Neutral;
            }

            if (manager.BlueArtilleryBatteries != null && Array.IndexOf(manager.BlueArtilleryBatteries, battery) >= 0)
            {
                return Faction.Blue;
            }
            if (manager.RedArtilleryBatteries != null && Array.IndexOf(manager.RedArtilleryBatteries, battery) >= 0)
            {
                return Faction.Red;
            }
            return Faction.Neutral;
        }

        /// <summary>Resolves the faction a CAS support airframe belongs to: blue/red, or Neutral when not found.</summary>
        private static Faction GetAirframeFaction(CasAirframeUnit airframe)
        {
            if (airframe == null)
            {
                return Faction.Neutral;
            }

            CasSupportManager manager = GetCasSupportManager();
            if (manager == null)
            {
                return Faction.Neutral;
            }

            if (manager.BlueCasAirframes != null && Array.IndexOf(manager.BlueCasAirframes, airframe) >= 0)
            {
                return Faction.Blue;
            }
            if (manager.RedCasAirframes != null && Array.IndexOf(manager.RedCasAirframes, airframe) >= 0)
            {
                return Faction.Red;
            }
            return Faction.Neutral;
        }

        private static bool IsPlayerFactionBattery(ArtilleryBattery battery)
        {
            Faction batteryFaction = GetBatteryFaction(battery);
            if (batteryFaction == Faction.Neutral)
            {
                return false;
            }
            return batteryFaction == CheatModeMod.ResolvePlayerFaction();
        }

        private static bool IsPlayerFactionAirframe(CasAirframeUnit airframe)
        {
            Faction airframeFaction = GetAirframeFaction(airframe);
            if (airframeFaction == Faction.Neutral)
            {
                return false;
            }
            return airframeFaction == CheatModeMod.ResolvePlayerFaction();
        }

        /// <summary>
        /// Artillery fire-mission patch (SendFireMission, the only entry point that actually deducts
        /// rounds / assigns the parameters):
        /// 1. Unlimited rounds: the current remaining count is recorded before firing, then restored
        ///    exactly after a successful call -- no matter how many (if any) the vanilla logic deducts,
        ///    the remaining count stays constant and never grows;
        /// 2-5. Items are all applied in the Postfix by rewriting this call's "current value" fields
        ///    (_currentShotQuota / _currentInterShotTime / _currentRadius / TimeUntilImpactSeconds /
        ///    RemainingDelay). Because vanilla reassigns these fields on every call, they never pollute
        ///    the battery template and never accumulate ("each use getting faster / more accurate").
        ///    Only player-faction batteries are affected; enemy batteries and script-planned strikes keep
        ///    the vanilla behavior.
        /// 2. Arrival time (ArtilleryTimeToTarget, percentage): 1.0 = vanilla; &lt;1 cancels the first-round
        ///    delay and only scales the per-round interval; &lt;=0 (-1.0) = zero interval + the whole volley
        ///    fired on the same frame;
        /// 3. Rounds per volley (ArtilleryVolleyRounds): overrides _currentShotQuota;
        /// 4. Accuracy (ArtilleryAccuracy): scales _currentRadius by the ratio, zeroed when &lt;=0.
        /// </summary>
        [HarmonyPatch(typeof(ArtilleryBattery), "SendFireMission")]
        public static class ArtillerySendFireMissionPatch
        {
            private static readonly AccessTools.FieldRef<ArtilleryBattery, int> MissionsRef =
                AccessTools.FieldRefAccess<ArtilleryBattery, int>("_missionsAvailable");
            // The "current value" fields for this volley (vanilla SendFireMission reassigns them on each
            // call): only these are rewritten -- never the battery template fields (_shots /
            // _interShotDelaySeconds / _randomDispersionRadiusMeters / _onCallImpactDelay) -- so there is
            // no "accumulating scale with each use" problem.
            private static readonly AccessTools.FieldRef<ArtilleryBattery, int> ShotQuotaRef =
                AccessTools.FieldRefAccess<ArtilleryBattery, int>("_currentShotQuota");
            private static readonly AccessTools.FieldRef<ArtilleryBattery, float> InterShotTimeRef =
                AccessTools.FieldRefAccess<ArtilleryBattery, float>("_currentInterShotTime");
            private static readonly AccessTools.FieldRef<ArtilleryBattery, float> RadiusRef =
                AccessTools.FieldRefAccess<ArtilleryBattery, float>("_currentRadius");
            private static readonly AccessTools.FieldRef<ArtilleryBattery, int> ShotCounterRef =
                AccessTools.FieldRefAccess<ArtilleryBattery, int>("_shotCounter");
            // Auto-properties { get; private set; }: write their backing fields directly.
            private static readonly AccessTools.FieldRef<ArtilleryBattery, float> TimeUntilImpactRef =
                AccessTools.FieldRefAccess<ArtilleryBattery, float>("<TimeUntilImpactSeconds>k__BackingField");
            private static readonly AccessTools.FieldRef<ArtilleryBattery, float> RemainingDelayRef =
                AccessTools.FieldRefAccess<ArtilleryBattery, float>("<RemainingDelay>k__BackingField");

            private static readonly MethodInfo DoSingleShotMethod =
                AccessTools.Method(typeof(ArtilleryBattery), "DoSingleShot")
                ?? typeof(ArtilleryBattery).GetMethod("DoSingleShot", BindingFlags.Instance | BindingFlags.NonPublic);

            private sealed class CallState
            {
                public int MissionsOriginal = -1;
                public bool Apply; // Player faction + on-call call: apply custom parameters to this volley.
            }

            /// <summary>
            /// The Prefix does only two things: record the remaining round count (for unlimited fire
            /// support), and decide whether this call should apply custom parameters. All parameter
            /// rewriting happens in the Postfix.
            /// </summary>
            private static void Prefix(ArtilleryBattery __instance, bool ignoreCooldown, out CallState __state)
            {
                __state = new CallState();
                if (__instance == null)
                {
                    return;
                }

                // 1) Unlimited rounds (player-faction batteries only).
                if (CheatModeMod.InfiniteFireSupportEnabled && IsPlayerFactionBattery(__instance))
                {
                    __state.MissionsOriginal = MissionsRef(__instance);
                }

                // 2) Custom parameters only apply to player-faction batteries;
                //    enemy-faction batteries and script-planned one-off strikes keep the vanilla behavior.
                __state.Apply = !ignoreCooldown && IsPlayerFactionBattery(__instance);
            }

            /// <summary>
            /// Logs a line for each player-issued call: the vanilla parameters of "this volley" plus the
            /// rewritten values. Under normal conditions the vanilla numbers for every call should be
            /// identical (not shrinking over successive calls). Search key "[CheatMode] artillery" in the
            /// MelonLoader console / Latest.log.
            /// </summary>
            private static void LogBatteryProfile(
                ArtilleryBattery battery,
                float scale,
                float accuracy,
                int roundSetting,
                float vanillaImpact,
                float vanillaInterShot,
                float vanillaRadius,
                int vanillaQuota,
                float newImpact,
                float newInterShot,
                float newRadius,
                int newQuota)
            {
                if (battery == null)
                {
                    return;
                }

                MelonLoader.MelonLogger.Msg(string.Format(
                    "[CheatMode] artillery '{0}': vanilla impact={1:0.###}s interShot={2:0.###}s radius={3:0.##}m rounds={4} | scale={5:0.###} accuracy={6:0.###} volleyRounds={7} -> impact={8:0.###}s interShot={9:0.###}s radius={10:0.##}m rounds={11} (spread={12:0.##}s)",
                    battery.FriendlyName,
                    vanillaImpact,
                    vanillaInterShot,
                    vanillaRadius,
                    vanillaQuota,
                    scale,
                    accuracy,
                    roundSetting,
                    newImpact,
                    newInterShot,
                    newRadius,
                    newQuota,
                    (float)Mathf.Max(0, newQuota - 1) * newInterShot));
            }

            private static void Postfix(ArtilleryBattery __instance, bool __result, CallState __state)
            {
                if (__instance == null || __state == null)
                {
                    return;
                }

                if (__state.MissionsOriginal >= 0 && __result)
                {
                    MissionsRef(__instance) = __state.MissionsOriginal;
                }

                if (!__result || !__state.Apply)
                {
                    return;
                }

                // The original method has already populated this volley's parameters; here only those
                // "current value" fields are overridden -- the battery template is untouched, so every use
                // starts from the same baseline and never gets faster / more accurate over time.
                float scale = CheatModeMod.TimeToTargetScale;
                float accuracy = CheatModeMod.AccuracyScale;
                int roundSetting = CheatModeMod.VolleyRounds;
                bool instant = scale <= 0f; // Instant arrival: zero interval + the whole volley fired in one frame.

                float vanillaImpact = TimeUntilImpactRef(__instance);
                float vanillaInterShot = InterShotTimeRef(__instance);
                float vanillaRadius = RadiusRef(__instance);
                int vanillaQuota = ShotQuotaRef(__instance);

                float newImpact = vanillaImpact;
                float newInterShot = vanillaInterShot;
                float newRadius = vanillaRadius;
                int newQuota = vanillaQuota;

                // a) Rounds per volley (custom count).
                if (roundSetting > 0)
                {
                    newQuota = roundSetting;
                }

                // b) Accuracy (dispersion radius): smaller = more accurate; <=0 (including -1.0) = zero
                //    dispersion, every round on the same point.
                if (accuracy < 1f)
                {
                    newRadius = (accuracy <= 0f) ? 0f : Mathf.Max(0f, vanillaRadius * accuracy);
                }

                // c) Arrival time: <1 always cancels the first-round delay and scales the interval by the
                //    percentage; instant arrival zeroes the interval.
                if (scale < 1f)
                {
                    newImpact = 0f;
                    newInterShot = instant ? 0f : Mathf.Max(0f, vanillaInterShot * scale);
                }

                try
                {
                    if (newQuota != vanillaQuota)
                    {
                        ShotQuotaRef(__instance) = newQuota;
                    }
                    if (newRadius != vanillaRadius)
                    {
                        RadiusRef(__instance) = newRadius;
                    }
                    if (scale < 1f)
                    {
                        TimeUntilImpactRef(__instance) = newImpact;
                        InterShotTimeRef(__instance) = newInterShot;
                        // The panel's "incoming" timer = first-round delay (0) + spread time, matching the
                        // actual ballistics.
                        RemainingDelayRef(__instance) = (float)Mathf.Max(0, newQuota - 1) * newInterShot;
                    }
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Error("[CheatMode] failed to apply artillery tweaks: " + ex.Message);
                }

                LogBatteryProfile(__instance, scale, accuracy, roundSetting, vanillaImpact, vanillaInterShot,
                    vanillaRadius, vanillaQuota, newImpact, newInterShot, newRadius, newQuota);

                if (instant)
                {
                    FireAllRoundsNow(__instance);
                }
            }

            /// <summary>
            /// Instant arrival: on the same frame the call succeeds, spawn all remaining rounds of this
            /// volley at once and push _shotCounter up to the quota -- the next frame's DoUpdate then
            /// finishes cleanly (IsFiring = false, RemainingDelay = 0, and the panel never gets stuck on
            /// "Incoming").
            /// </summary>
            private static void FireAllRoundsNow(ArtilleryBattery battery)
            {
                if (battery == null || DoSingleShotMethod == null)
                {
                    return;
                }
                if (AarController.InAar)
                {
                    return; // AAR replay: hand control back to the vanilla per-round flow.
                }

                try
                {
                    int quota = ShotQuotaRef(battery);
                    for (int i = ShotCounterRef(battery); i < quota; i++)
                    {
                        DoSingleShotMethod.Invoke(battery, null);
                    }
                    ShotCounterRef(battery) = quota;
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Error("[CheatMode] Artillery instant burst failed: " + ex.Message);
                }
            }
        }

        /// CAS sortie patch. Every time CasSupportManager successfully dispatches a support aircraft, it
        /// calls CasAirframeUnit.ReduceMissionsAvailable to deduct one sortie. When enabled and the
        /// airframe belongs to the player's faction, the entire method is skipped so the airframe is
        /// never depleted.
        /// </summary>
        [HarmonyPatch(typeof(CasAirframeUnit), "ReduceMissionsAvailable")]
        public static class CasUnlimitedMissionsPatch
        {
            private static bool Prefix(CasAirframeUnit __instance)
            {
                if (!CheatModeMod.InfiniteFireSupportEnabled)
                {
                    return true; // Cheat disabled: apply the vanilla deduction.
                }
                return !IsPlayerFactionAirframe(__instance);
            }
        }

        /// <summary>
        /// Artillery no-cooldown patch. It does not block writes to RemainingCooldown -- doing so would
        /// break CooldownManager's panel tracking (an entry whose cooldown drops to 0 during a volley is
        /// removed early, freezing the button on "Incoming" and preventing any later call). The correct
        /// approach keeps vanilla cooldown bookkeeping (cooldown &gt; 0 for the whole volley, panel showing
        /// "Incoming"), then a DoUpdate postfix zeroes the residual cooldown when NOT firing -- as soon
        /// as a volley ends the cooldown is 0, the panel returns to Ready on the next frame, and another
        /// call can be made immediately.
        /// </summary>
        [HarmonyPatch(typeof(ArtilleryBattery), "DoUpdate")]
        public static class ArtilleryNoCooldownPatch
        {
            // RemainingCooldown is an auto-property { get; private set; }; write its backing field directly.
            private static readonly AccessTools.FieldRef<ArtilleryBattery, float> RemainingCooldownRef =
                AccessTools.FieldRefAccess<ArtilleryBattery, float>("<RemainingCooldown>k__BackingField");

            private static void Postfix(ArtilleryBattery __instance)
            {
                if (!CheatModeMod.FireSupportNoCooldownEnabled || __instance == null || __instance.IsFiring)
                {
                    return;
                }
                if (!IsPlayerFactionBattery(__instance))
                {
                    return; // Enemy battery: keep the vanilla cooldown.
                }

                RemainingCooldownRef(__instance) = 0f;
            }
        }

        /// <summary>
        /// CAS no-cooldown patch. CasAirframeUnit.RemainingCooldown's setter is written whenever an
        /// airframe is dispatched (ResetCooldown, 120 s) and when an aircraft returns. When enabled and
        /// the airframe belongs to the player's faction, all writes are blocked so the cooldown stays 0:
        /// no waiting after a call, ready to call again at any time.
        /// </summary>
        [HarmonyPatch(typeof(CasAirframeUnit), "RemainingCooldown", MethodType.Setter)]
        public static class CasNoCooldownPatch
        {
            private static bool Prefix(CasAirframeUnit __instance)
            {
                if (!CheatModeMod.FireSupportNoCooldownEnabled)
                {
                    return true; // Cheat disabled: allow the vanilla cooldown write.
                }
                return !IsPlayerFactionAirframe(__instance);
            }
        }

        /// <summary>
        /// Faction-aware damage patch, applied to ArtilleryBattery.DoSingleShot (the spawn point of every
        /// round). When enabled, a player-faction battery spawns live rounds (which deal damage), while
        /// an enemy-faction battery skips spawning (firing blanks, no damage); temporary scripted
        /// batteries (Neutral) keep the vanilla behavior. Skipping DoSingleShot does not disturb the
        /// volley timing (DoUpdate still advances _shotCounter, and the panel shows "Incoming" and ends
        /// normally).
        /// </summary>
        [HarmonyPatch(typeof(ArtilleryBattery), "DoSingleShot")]
        public static class ArtilleryFactionAwareDamagePatch
        {
            private static bool Prefix(ArtilleryBattery __instance)
            {
                if (!CheatModeMod.ArtilleryFactionAwareEnabled || __instance == null)
                {
                    return true;
                }

                Faction batteryFaction = GetBatteryFaction(__instance);
                if (batteryFaction != Faction.Blue && batteryFaction != Faction.Red)
                {
                    return true; // Temporary scripted battery: keep the vanilla behavior.
                }

                Faction playerFaction = CheatModeMod.ResolvePlayerFaction();
                if (playerFaction != Faction.Blue && playerFaction != Faction.Red)
                {
                    return true; // Player faction cannot be determined: conservatively keep the vanilla behavior.
                }

                return batteryFaction == playerFaction; // Player faction fires; enemy faction fires blanks.
            }
        }
    }
}
