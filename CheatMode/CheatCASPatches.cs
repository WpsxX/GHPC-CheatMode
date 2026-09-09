using System;
using System.Collections.Generic;
using System.Reflection;
using GHPC;
using GHPC.Vehicle;
using GHPC.World;
using HarmonyLib;
using UnityEngine;

namespace CheatMode
{
    /// <summary>
    /// CAS (fixed-wing / helicopter fire support) cheats:
    ///
    /// 1. CasAccuracy - accuracy, expressed as a fraction of the weapon's own launch dispersion
    ///    (same semantics as ArtilleryAccuracy): 1.0 = the weapon's natural spread, values between
    ///    0 and 1 tighten it proportionally and &lt;=0 (including -1.0) = zero spread, so the rounds
    ///    follow the aim line exactly ("hit the target position"). Implemented by temporarily
    ///    scaling CASHardpoint._launchDeviation (the vanilla random launch-angle deviation, in
    ///    degrees) for the duration of one Fire() call - no prefab edits, nothing cumulative.
    ///
    /// 2. CasSpreadTargets - multi-plane target spreading: a batch of CAS planes no longer all lock
    ///    the same target. Vanilla lets every plane independently pick the "best score + closest to
    ///    the called position" target, so sorties called together always converge on one vehicle.
    ///    This patch adjudicates after SearchForTarget has chosen FinalTarget:
    ///      - if that target is already claimed by another live plane, pick an unclaimed one;
    ///      - candidates come from the plane's own spotted list first (vanilla already filtered
    ///        visibility and attackability);
    ///      - when that list has no spare, the search widens to every live enemy unit within 3 km
    ///        of the called position, skipping anything the plane has no weapon for
    ///        (GetIdealAttackType == Inert).
    /// </summary>
    public static class CheatCASPatches
    {
        /// <summary>
        /// CAS accuracy patch, applied before CASHardpoint.Fire (every weapon release / trigger
        /// pull): scales this release's random launch deviation by CasAccuracy and restores the
        /// original value afterwards.
        /// </summary>
        [HarmonyPatch(typeof(CASHardpoint), "Fire")]
        public static class CasAccuracyPatch
        {
            private static readonly AccessTools.FieldRef<CASHardpoint, float> DeviationRef =
                AccessTools.FieldRefAccess<CASHardpoint, float>("_launchDeviation");

            private struct DeviationState
            {
                public bool Scaled;
                public float Original;
            }

            private static void Prefix(CASHardpoint __instance, out DeviationState __state)
            {
                __state = default(DeviationState);
                if (__instance == null)
                {
                    return;
                }

                float scale = CheatModeMod.CasAccuracyScale;
                if (scale >= 1f)
                {
                    return; // Vanilla.
                }

                __state.Original = DeviationRef(__instance);
                __state.Scaled = true;
                DeviationRef(__instance) = (scale <= 0f) ? 0f : Mathf.Max(0f, __state.Original * scale);
            }

            private static void Postfix(CASHardpoint __instance, DeviationState __state)
            {
                if (__instance == null || !__state.Scaled)
                {
                    return;
                }

                DeviationRef(__instance) = __state.Original;
            }
        }

        /// <summary>
        /// CAS multi-plane spreading patch, applied after CASController.SearchForTarget: vanilla has
        /// already chosen FinalTarget (both the "spotted now" and the "last known position" paths are
        /// done), so this is where the target-claim adjudication happens. If the target is changed,
        /// EnterState(TurnTowardTarget) is re-run so _finalAttackType / _releaseDistance and the other
        /// attack parameters are recomputed for the new target.
        /// </summary>
        [HarmonyPatch(typeof(CASController), "SearchForTarget")]
        public static class CasTargetSpreadPatch
        {
            /// <summary>Widened search radius: how far from the called position enemies may still participate.</summary>
            private const float WideSearchRadius = 3000f;

            private static readonly AccessTools.FieldRef<CASController, List<Unit>> SpottedRef =
                AccessTools.FieldRefAccess<CASController, List<Unit>>("_spottedTargetsCurrent");
            private static readonly AccessTools.FieldRef<CASController, Vector3> InterestRef =
                AccessTools.FieldRefAccess<CASController, Vector3>("_interestPoint");
            private static readonly AccessTools.FieldRef<CASController, bool> LastKnownRef =
                AccessTools.FieldRefAccess<CASController, bool>("_targetIsLastKnownPosition");

            private static readonly MethodInfo EnterStateMethod =
                AccessTools.Method(typeof(CASController), "EnterState");
            private static readonly MethodInfo GetIdealAttackTypeMethod =
                AccessTools.Method(typeof(CASController), "GetIdealAttackType");

            /// <summary>Target -> the plane currently attacking it.</summary>
            private static readonly Dictionary<Unit, CASController> Claims = new Dictionary<Unit, CASController>();

            private static void Postfix(CASController __instance)
            {
                if (!CheatModeMod.CasSpreadTargetsEnabled || __instance == null)
                {
                    return;
                }

                Unit chosen = __instance.FinalTarget;
                if (chosen == null)
                {
                    return;
                }

                PruneStaleClaims();
                ReleasePlaneClaims(__instance);

                Unit result = chosen;
                if (IsClaimedByOther(chosen, __instance))
                {
                    Unit alternative = FindAlternativeTarget(__instance, chosen);
                    if (alternative != null)
                    {
                        result = alternative;
                    }
                }

                if (result != chosen)
                {
                    __instance.FinalTarget = result;
                    LastKnownRef(__instance) = false;
                    RecomputeAttackParams(__instance);
                }

                Claims[result] = __instance;

                List<Unit> spotted = SpottedRef(__instance);
                MelonLoader.MelonLogger.Msg(string.Format(
                    "[CheatMode] CAS spread: plane='{0}' spotted={1} claims={2} vanilla='{3}' -> '{4}'",
                    __instance.gameObject.name,
                    (spotted != null) ? spotted.Count : 0,
                    Claims.Count,
                    chosen.FriendlyName,
                    result.FriendlyName));
            }

            private static bool IsClaimedByOther(Unit target, CASController plane)
            {
                if (target == null)
                {
                    return false;
                }
                return Claims.TryGetValue(target, out CASController other) && other != null && other != plane;
            }

            /// <summary>
            /// Finds a replacement target: the nearest unclaimed one from the plane's own spotted
            /// list first, then any live enemy unit near the called position.
            /// </summary>
            private static Unit FindAlternativeTarget(CASController plane, Unit exclude)
            {
                Vector3 interest = InterestRef(plane);

                // 1) The plane's own spotted list (vanilla already filtered visibility + attackability).
                Unit best = FindNearest(plane, SpottedRef(plane), interest, exclude, float.MaxValue);
                if (best != null)
                {
                    return best;
                }

                // 2) Widened: every live enemy unit near the called position.
                List<Unit>[] allUnits = SceneUnitsManager.AllLiveUnitsByFaction;
                if (allUnits == null)
                {
                    return null;
                }

                Unit wideBest = null;
                float wideBestDistance = WideSearchRadius;
                for (int f = 0; f < allUnits.Length; f++)
                {
                    Faction faction = (Faction)f;
                    if (faction == Faction.Neutral || faction == plane.unitFaction)
                    {
                        continue; // Enemies only.
                    }
                    List<Unit> list = allUnits[f];
                    if (list == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        Unit candidate = list[i];
                        if (candidate == null || candidate.Neutralized || candidate == exclude)
                        {
                            continue;
                        }
                        if (IsClaimedByOther(candidate, plane))
                        {
                            continue;
                        }
                        if (!CanPlaneAttack(plane, candidate))
                        {
                            continue; // No weapon for it - vanilla would end the run instead.
                        }

                        float distance = Vector3.Distance(candidate.Center.position, interest);
                        if (distance < wideBestDistance)
                        {
                            wideBestDistance = distance;
                            wideBest = candidate;
                        }
                    }
                }
                return wideBest;
            }

            private static Unit FindNearest(CASController plane, List<Unit> candidates, Vector3 interest, Unit exclude, float maxDistance)
            {
                if (candidates == null || candidates.Count == 0)
                {
                    return null;
                }

                Unit best = null;
                float bestDistance = maxDistance;
                for (int i = 0; i < candidates.Count; i++)
                {
                    Unit candidate = candidates[i];
                    if (candidate == null || candidate == exclude || candidate.Neutralized)
                    {
                        continue;
                    }
                    if (IsClaimedByOther(candidate, plane))
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(candidate.Center.position, interest);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = candidate;
                    }
                }
                return best;
            }

            /// <summary>Whether this plane has an attack type for the target (GetIdealAttackType != Inert).</summary>
            private static bool CanPlaneAttack(CASController plane, Unit unit)
            {
                if (GetIdealAttackTypeMethod == null)
                {
                    return true; // Reflection unavailable: be permissive.
                }
                try
                {
                    object result = GetIdealAttackTypeMethod.Invoke(plane, new object[] { unit });
                    return !CASAttackType.Inert.Equals(result);
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>Re-runs EnterState(TurnTowardTarget) so the attack parameters match the new target.</summary>
            private static void RecomputeAttackParams(CASController plane)
            {
                if (EnterStateMethod == null)
                {
                    return;
                }
                try
                {
                    ParameterInfo[] parameters = EnterStateMethod.GetParameters();
                    if (parameters.Length != 1)
                    {
                        return;
                    }
                    object state = Enum.Parse(parameters[0].ParameterType, "TurnTowardTarget");
                    EnterStateMethod.Invoke(plane, new object[] { state });
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Error("[CheatMode] CAS target switch failed to recompute attack params: " + ex.Message);
                }
            }

            /// <summary>Releases the targets this plane claimed earlier (called when it re-picks one).</summary>
            private static void ReleasePlaneClaims(CASController plane)
            {
                List<Unit> release = null;
                foreach (KeyValuePair<Unit, CASController> pair in Claims)
                {
                    if (pair.Value == plane)
                    {
                        if (release == null)
                        {
                            release = new List<Unit>();
                        }
                        release.Add(pair.Key);
                    }
                }
                if (release != null)
                {
                    for (int i = 0; i < release.Count; i++)
                    {
                        Claims.Remove(release[i]);
                    }
                }
            }

            /// <summary>Drops stale claims: destroyed target, destroyed plane, or a plane no longer holding that target.</summary>
            private static void PruneStaleClaims()
            {
                List<Unit> stale = null;
                foreach (KeyValuePair<Unit, CASController> pair in Claims)
                {
                    Unit target = pair.Key;
                    CASController plane = pair.Value;
                    if (target == null || plane == null || plane.FinalTarget != target)
                    {
                        if (stale == null)
                        {
                            stale = new List<Unit>();
                        }
                        stale.Add(target);
                    }
                }
                if (stale != null)
                {
                    for (int i = 0; i < stale.Count; i++)
                    {
                        Claims.Remove(stale[i]);
                    }
                }
            }
        }
    }
}
