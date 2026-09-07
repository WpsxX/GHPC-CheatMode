using System;
using System.Reflection;
using GHPC;
using GHPC.Equipment;
using GHPC.Humans;
using GHPC.Player;
using GHPC.Utility;
using HarmonyLib;
using MelonLogger = MelonLoader.MelonLogger;
using UnityEngine;

namespace CheatMode
{
    /// <summary>
    /// Invincibility damage filter. Based on the settings it independently protects the player's own
    /// unit and friendly AI units.
    /// </summary>
    internal static class CheatDamageFilter
    {
        public static GHPC.Unit CurrentPlayerUnit
        {
            get
            {
                try
                {
                    GHPC.Player.PlayerInput playerInput = GHPC.Player.PlayerInput.Instance;
                    return (playerInput != null) ? playerInput.CurrentPlayerUnit : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        internal static bool SelfInvincibleEnabled
        {
            get { return CheatModeMod.SelfInvincible != null && CheatModeMod.SelfInvincible.Value; }
        }

        internal static bool FriendlyInvincibleEnabled
        {
            get { return CheatModeMod.FriendlyInvincible != null && CheatModeMod.FriendlyInvincible.Value; }
        }

        public static bool IsProtectedUnit(GHPC.IUnit unit)
        {
            if (unit == null)
            {
                return false;
            }

            GHPC.Unit playerUnit = CurrentPlayerUnit;
            bool isSelf = playerUnit != null && ReferenceEquals(unit, playerUnit);

            if (isSelf)
            {
                return SelfInvincibleEnabled;
            }

            return playerUnit != null && FriendlyInvincibleEnabled && unit.Allegiance == playerUnit.Allegiance;
        }

        public static bool IsProtectedComponent(GHPC.Equipment.DestructibleComponent component)
        {
            if (component == null)
            {
                return false;
            }

            GHPC.Unit unit = component.Unit;
            if (unit == null)
            {
                unit = GHPC.Utility.CodeUtils.FindComponentInParentFollowAware<GHPC.Unit>(component.transform);
            }

            return IsProtectedUnit(unit);
        }

        public static bool IsProtectedHuman(GHPC.Humans.Human human)
        {
            if (human == null)
            {
                return false;
            }

            foreach (GHPC.Humans.BodyPart part in Enum.GetValues(typeof(GHPC.Humans.BodyPart)))
            {
                GHPC.Equipment.IDestructible component = human.GetBodyPartDestructible(part);
                if (component != null && IsProtectedUnit(component.Unit))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetHitUnit(Collider cObject, GHPC.AI.Interfaces.ITarget targetStruck, out GHPC.IUnit unit)
        {
            unit = null;

            if (targetStruck != null)
            {
                unit = targetStruck.Owner;
            }

            if (unit == null && cObject != null)
            {
                GHPC.IArmor armor = cObject.GetComponent(typeof(GHPC.IArmor)) as GHPC.IArmor;
                unit = (armor != null) ? armor.Unit : null;

                if (unit == null)
                {
                    GHPC.Equipment.DestructibleComponent destructible = cObject.GetComponent<GHPC.Equipment.DestructibleComponent>();
                    unit = (destructible != null) ? destructible.Unit : null;
                }

                if (unit == null)
                {
                    unit = GHPC.Utility.CodeUtils.FindComponentInParentFollowAware<GHPC.Unit>(cObject.transform);
                }
            }

            return unit != null;
        }
    }

    [HarmonyPatch]
    public static class Patch_LiveRound_PenCheck
    {
        private static readonly FieldInfo _framePosField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_framePos");
        private static readonly FieldInfo _frameDirField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_frameDir");
        private static readonly FieldInfo _frameDataField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_frameData");
        private static readonly FieldInfo _lastFramePositionField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_lastFramePosition");
        private static readonly FieldInfo _jetActiveField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_jetActive");
        private static readonly FieldInfo _impactNormalField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_impactNormal");
        private static readonly FieldInfo _impactObjectField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_impactObject");
        private static readonly FieldInfo _materialHitField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_materialHit");
        private static readonly FieldInfo _impactSkipDecalField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_impactSkipDecal");
        private static readonly FieldInfo _hitSolidObjectField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_hitSolidObject");
        private static readonly FieldInfo _heHitNormalRhaField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_heHitNormalRha");
        private static readonly FieldInfo _heHitNormalField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_heHitNormal");
        private static readonly FieldInfo _armedField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_armed");
        private static readonly FieldInfo _impactTimeField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_impactTime");
        private static readonly FieldInfo _trueInitialPositionField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_trueInitialPosition");
        private static readonly FieldInfo _parentUnitField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_parentUnit");
        private static readonly FieldInfo _penetrationLevelField = AccessTools.Field(typeof(GHPC.Weapons.LiveRound), "_penetrationLevel");

        private static readonly MethodInfo _addEventMethod = AccessTools.Method(typeof(GHPC.Weapons.LiveRound), "AddEvent", new[] { typeof(string) });
        private static readonly MethodInfo _reportShotTraceFrameMethod = AccessTools.Method(typeof(GHPC.Weapons.LiveRound), "reportShotTraceFrame");
        private static readonly MethodInfo _logHitMethod = AccessTools.Method(typeof(GHPC.Weapons.LiveRound), "logHit", new[] { typeof(Vector3) });
        private static readonly MethodInfo _resolveImpactAudioMethod = AccessTools.Method(typeof(GHPC.Weapons.LiveRound), "ResolveImpactAudio", new[] { typeof(float) });

        private static bool _warnedOnce;

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Weapons.LiveRound), "penCheck");
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Weapons.LiveRound __instance, ref bool __result, Collider cObject, Vector3 surfaceNormal, Vector3 roundPath, Vector3 impactPoint, GHPC.AI.Interfaces.ITarget targetStruck)
        {
            if (!CheatDamageFilter.TryGetHitUnit(cObject, targetStruck, out GHPC.IUnit hitUnit))
            {
                return true;
            }

            if (!CheatDamageFilter.IsProtectedUnit(hitUnit))
            {
                return true;
            }

            // Vanilla passes through these colliders; let it do so, otherwise enemy rounds
            // disappear on large helper colliders before reaching the actual armor plate.
            if (ShouldLetVanillaHandlePassThrough(__instance, cObject, hitUnit))
            {
                return true;
            }

            NegateHit(__instance, cObject, surfaceNormal, impactPoint, roundPath, hitUnit);

            __result = false;
            return false;
        }

        private static bool ShouldLetVanillaHandlePassThrough(GHPC.Weapons.LiveRound round, Collider cObject, GHPC.IUnit hitUnit)
        {
            if (cObject == null)
            {
                return true;
            }

            // Vanilla handles detection zones first and always lets the round continue.
            if (cObject.CompareTag("Detection"))
            {
                return true;
            }

            GHPC.IArmor armor = cObject.GetComponent(typeof(GHPC.IArmor)) as GHPC.IArmor;

            // Vanilla explicitly skips the shooter's own armor so a round never collides
            // with the vehicle that fired it. Preserve that behavior.
            if (!round.IsSpall && armor != null && round.Shooter != null && (object)hitUnit == (object)round.Shooter)
            {
                return true;
            }

            if (armor != null)
            {
                return false;
            }

            // Compartment colliders are damaging entry points (NotifyPenetrated / overpressure)
            // and must be suppressed for allied vehicles.
            if (cObject.CompareTag("Compartment"))
            {
                return false;
            }

            // Vanilla treats Target-tagged colliders as pass-through AAR markers.
            if (cObject.CompareTag("Target"))
            {
                return true;
            }

            GHPC.PhysicsHelpers.ProxyHitZone proxy = cObject.GetComponent<GHPC.PhysicsHelpers.ProxyHitZone>();
            if (proxy != null)
            {
                // Only stop the round when this proxy would actually route damage into a
                // DestructibleComponent or a Compartment. Plain helper proxies pass through.
                return !(proxy.DestructibleComponent || proxy.Compartment);
            }

            // Generic DestructibleComponent hits are damaged by vanilla and then passed
            // through. Our DestructibleComponent patches block that damage, so we let the
            // round continue to the armor/compartment behind the component.
            if (cObject.GetComponent<GHPC.Equipment.DestructibleComponent>() != null)
            {
                return true;
            }

            // Any other collider is also a vanilla pass-through case.
            return true;
        }

        private static void NegateHit(GHPC.Weapons.LiveRound round, Collider cObject, Vector3 surfaceNormal, Vector3 impactPoint, Vector3 roundPath, GHPC.IUnit hitUnit)
        {
            try
            {
                GHPC.IArmor armor = cObject.GetComponent(typeof(GHPC.IArmor)) as GHPC.IArmor;

                // Mirror the arming-distance check from the real penCheck. Without this,
                // HE/HEAT rounds would never become "armed" when they are stopped early.
                if (_armedField != null)
                {
                    bool armed = (bool)_armedField.GetValue(round);
                    if (!armed)
                    {
                        float armingDistance = round.Info.ArmingDistance;
                        if (armingDistance == 0f)
                        {
                            armed = true;
                        }
                        else
                        {
                            Vector3 trueInitialPosition = (_trueInitialPositionField != null)
                                ? (Vector3)_trueInitialPositionField.GetValue(round)
                                : round.transform.position;
                            armed = Vector3.Distance(impactPoint, trueInitialPosition) >= armingDistance;
                        }
                        _armedField.SetValue(round, armed);
                    }
                }

                if (_impactTimeField != null)
                {
                    float impactTime = (float)_impactTimeField.GetValue(round);
                    if (impactTime <= 0f)
                    {
                        float missionTime = GHPC.SceneController.MissionTime;
                        _impactTimeField.SetValue(round, missionTime);
                        round.Story.ImpactTime = missionTime;
                    }
                }

                bool jetActive = false;
                if (_jetActiveField != null)
                {
                    jetActive = (bool)_jetActiveField.GetValue(round);
                }

                // Set impact visual state so Detonate()/doImpactEffect() spawns the correct
                // impact VFX and decal instead of using default/null values.
                if (_impactObjectField != null)
                {
                    _impactObjectField.SetValue(round, (armor != null) ? cObject.gameObject : null);
                }
                if (_materialHitField != null)
                {
                    _materialHitField.SetValue(round, (armor != null)
                        ? armor.SurfaceMaterial
                        : GHPC.Effects.ParticleEffectsManager.SurfaceMaterial.Steel);
                }
                if (_impactSkipDecalField != null)
                {
                    _impactSkipDecalField.SetValue(round, (armor != null) ? armor.NoImpactDecals : false);
                }
                if (_impactNormalField != null)
                {
                    _impactNormalField.SetValue(round, surfaceNormal);
                }
                if (_framePosField != null)
                {
                    _framePosField.SetValue(round, impactPoint);
                }
                if (_frameDirField != null)
                {
                    _frameDirField.SetValue(round, roundPath);
                }
                if (_lastFramePositionField != null)
                {
                    _lastFramePositionField.SetValue(round, impactPoint);
                }
                if (_parentUnitField != null && _parentUnitField.GetValue(round) == null)
                {
                    _parentUnitField.SetValue(round, hitUnit);
                }

                GHPC.Weapons.ShotInfo.ShotFrameData frameData = new GHPC.Weapons.ShotInfo.ShotFrameData
                {
                    VehicleStruck = hitUnit,
                    ObjectStruck = (armor != null) ? cObject.gameObject : null,
                    ArmorStruck = armor,
                    IsKill = false,
                    IsDamaging = false,
                    HitSomething = true,
                    IsSpall = round.IsSpall,
                    IsJet = jetActive
                };

                if (_frameDataField != null)
                {
                    _frameDataField.SetValue(round, frameData);
                }

                // logHit mirrors the real penCheck: it records ShotInfo.Distance and
                // Story.ImpactPosition, and for HEAT it starts the warhead jet/blast exactly
                // as vanilla would.
                if (_logHitMethod != null)
                {
                    _logHitMethod.Invoke(round, new object[] { impactPoint });
                }

                if (_hitSolidObjectField != null)
                {
                    _hitSolidObjectField.SetValue(round, true);
                }

                // For HE/HESH rounds the caller (LiveRound.DoUpdate) uses _heHitNormalRha
                // to decide whether the fuze has enough resistance to detonate/scab. Use the
                // plate's Sabot rating so the round still detonates on the outside of an
                // allied vehicle without damaging it.
                bool isExplosive = ((int)round.Info.Category == 2); // AmmoCategory.Explosive
                if (isExplosive)
                {
                    if (_heHitNormalRhaField != null)
                    {
                        _heHitNormalRhaField.SetValue(round, (armor != null) ? armor.SabotRha : 9999f);
                    }
                    if (_heHitNormalField != null)
                    {
                        _heHitNormalField.SetValue(round, surfaceNormal);
                    }
                }

                if (_penetrationLevelField != null)
                {
                    // Non-penetrating hit. Level 1 gives a full-caliber impact mark; level 0
                    // is reserved for cases where the armor vastly overmatches the round.
                    _penetrationLevelField.SetValue(round, 1);
                }

                if (!round.IsSpall && _resolveImpactAudioMethod != null)
                {
                    _resolveImpactAudioMethod.Invoke(round, new object[] { (armor != null) ? armor.SabotRha : 0f });
                }

                if (_addEventMethod != null)
                {
                    _addEventMethod.Invoke(round, new object[] { "Hit friendly unit, shot negated." });
                }

                if (_reportShotTraceFrameMethod != null)
                {
                    _reportShotTraceFrameMethod.Invoke(round, null);
                }
            }
            catch (Exception ex)
            {
                if (!_warnedOnce)
                {
                    _warnedOnce = true;
                    MelonLogger.Msg("[CheatMode] Failed to write negated-hit trace data: " + ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// Defense-in-depth block for every projectile-damage entry point on destructible
    /// components. This covers direct hits, spall, SimpleRound damage and proxy hit zones.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_DestructibleComponent_ApplyProjectileDamage
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Equipment.DestructibleComponent), "ApplyProjectileDamage",
                new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(GHPC.ResistanceType), typeof(float), typeof(bool) });
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Equipment.DestructibleComponent __instance)
        {
            return !CheatDamageFilter.IsProtectedComponent(__instance);
        }
    }

    /// <summary>
    /// Private float overload used by the proxy hit-zone system
    /// (DestructibleComponent.HandleProxyColliderStruck).
    /// </summary>
    [HarmonyPatch]
    public static class Patch_DestructibleComponent_ApplyProjectileDamage_Float
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Equipment.DestructibleComponent), "ApplyProjectileDamage", new[] { typeof(float) });
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Equipment.DestructibleComponent __instance)
        {
            return !CheatDamageFilter.IsProtectedComponent(__instance);
        }
    }

    /// <summary>
    /// Blocks HE/HEAT blast overpressure damage against allied components.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_DestructibleComponent_ApplyOverpressureDamage
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Equipment.DestructibleComponent), "ApplyOverpressureDamage",
                new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(float) });
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Equipment.DestructibleComponent __instance)
        {
            return !CheatDamageFilter.IsProtectedComponent(__instance);
        }
    }

    /// <summary>
    /// Blocks blast shock damage against allied components.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_DestructibleComponent_ApplyBlastShockDamage
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Equipment.DestructibleComponent), "ApplyBlastShockDamage", new[] { typeof(float) });
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Equipment.DestructibleComponent __instance)
        {
            return !CheatDamageFilter.IsProtectedComponent(__instance);
        }
    }

    /// <summary>
    /// If a round still reaches an allied compartment (non-armored collider, open top, etc.),
    /// suppress the penetration notification so crew panic/bailout and overpressure are not
    /// triggered for allied vehicles.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_Compartment_NotifyPenetrated
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Effects.Compartment), "NotifyPenetrated");
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Effects.Compartment __instance)
        {
            GHPC.Unit unit = GHPC.Utility.CodeUtils.FindComponentInParentFollowAware<GHPC.Unit>(__instance.transform);
            return !CheatDamageFilter.IsProtectedUnit(unit);
        }
    }

    /// <summary>
    /// Additional safety net for HE/HEAT blast overpressure being inserted directly into
    /// an allied compartment without going through NotifyPenetrated first.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_Compartment_InsertOverpressure
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Effects.Compartment), "InsertOverpressure", new[] { typeof(float), typeof(Vector3) });
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Effects.Compartment __instance)
        {
            GHPC.Unit unit = GHPC.Utility.CodeUtils.FindComponentInParentFollowAware<GHPC.Unit>(__instance.transform);
            return !CheatDamageFilter.IsProtectedUnit(unit);
        }
    }

    /// <summary>
    /// Blocks direct Human.Kill calls for allied infantry/crew. Some systems (vehicle
    /// collision, gore, crew compartment detachment) kill humans directly instead of going
    /// through DestructibleComponent damage methods.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_Human_Kill
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Humans.Human), "Kill");
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Humans.Human __instance)
        {
            return !CheatDamageFilter.IsProtectedHuman(__instance);
        }
    }

    /// <summary>
    /// Blocks direct health reductions through Human.SetPartHealth / InjurePart for allied
    /// humans. Anti-personnel grenades call SetPartHealth(BodyPart.Torso, 0f) directly.
    /// Healing/raising health is still allowed.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_Human_SetPartHealth
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Humans.Human), "SetPartHealth", new[] { typeof(GHPC.Humans.BodyPart), typeof(float) });
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Humans.Human __instance, GHPC.Humans.BodyPart part, float healthPercent)
        {
            if (!CheatDamageFilter.IsProtectedHuman(__instance))
            {
                return true;
            }

            GHPC.Equipment.IDestructible component = __instance.GetBodyPartDestructible(part);
            if (component != null && healthPercent < component.HealthPercent)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Anti-personnel grenades directly kill infantry via Human.SetPartHealth and then
    /// apply ragdoll force. Skip the entire damaging call for allied infantry.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_AntiPersonnelGrenadeExplosionBehaviour_DamageInfantryUnit
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Weaponry.AntiPersonnelGrenadeExplosionBehaviour), "DamageInfantryUnit",
                new[] { typeof(Vector3), typeof(Vector3), typeof(GHPC.Infantry.InfantryUnit) });
        }

        [HarmonyPrefix]
        static bool Prefix(Vector3 position, Vector3 direction, GHPC.Infantry.InfantryUnit infantryUnit)
        {
            return !CheatDamageFilter.IsProtectedUnit(infantryUnit);
        }
    }

    /// <summary>
    /// Prevents allied infantry from being killed by vehicle collisions (run over).
    /// Low-speed nudging is left intact.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_InfantryUnit_OnCollisionWithVehicle
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Infantry.InfantryUnit), "OnCollisionWithVehicle");
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Infantry.InfantryUnit __instance, Vector3 closestPosition, Vector3 velocity)
        {
            if (CheatDamageFilter.IsProtectedUnit(__instance) && !__instance.HasEmplacement)
            {
                Vector3 relativeVelocity = velocity - __instance.Velocity;
                if (relativeVelocity.sqrMagnitude >= 10.0 && velocity.sqrMagnitude >= 10.0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Prevents allied infantry from being killed when a limb is dismembered.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_InfantryUnit_GoreOnLimbDismembered
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Infantry.InfantryUnit), "GoreOnLimbDismembered");
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Infantry.InfantryUnit __instance)
        {
            return !CheatDamageFilter.IsProtectedUnit(__instance);
        }
    }

    /// <summary>
    /// Prevents KillAllRemainingCrew from killing crew members of allied vehicles.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_CrewManager_KillAllRemainingCrew
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Crew.CrewManager), "KillAllRemainingCrew");
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Crew.CrewManager __instance)
        {
            return !CheatDamageFilter.IsProtectedUnit(__instance.Unit);
        }
    }

    /// <summary>
    /// Final safety nets: allied units must never be marked destroyed or incapacitated
    /// even if some future damage path bypasses every component-level guard.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_Unit_NotifyDestroyed
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Unit), "NotifyDestroyed");
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Unit __instance)
        {
            return !CheatDamageFilter.IsProtectedUnit(__instance);
        }
    }

    [HarmonyPatch]
    public static class Patch_Unit_NotifyIncapacitated
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GHPC.Unit), "NotifyIncapacitated");
        }

        [HarmonyPrefix]
        static bool Prefix(GHPC.Unit __instance)
        {
            return !CheatDamageFilter.IsProtectedUnit(__instance);
        }
    }

}
