using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GHPC;
using GHPC.AI.Platoons;
using GHPC.Camera;
using GHPC.Infantry;
using GHPC.Infantry.Weapons;
using GHPC.Player;
using GHPC.State;
using GHPC.Vehicle;
using GHPC.Weaponry;
using GHPC.Weapons;
using GHPC.World;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CheatMode.CheatModeMod), "CheatMode", "1.6.4", "CheatMode")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace CheatMode
{
    /// <summary>
    /// Marker component. Attached to units (vehicles / infantry) that receive infinite ammo,
    /// as well as to infantry crew-served weapon emplacements. Its purposes are:
    /// 1. Prevent the same unit from being processed twice;
    /// 2. Identify seeded objects for the ammo patches. Eligibility is also checked against
    ///    current settings and ownership, since markers outlive player switches and toggles.
    /// </summary>
    public class CheatMarker : MonoBehaviour
    {
    }

    public partial class CheatModeMod : MelonMod
    {
        public static CheatModeMod Instance;

        private const float RescanIntervalSeconds = 2.5f;
        private const float EspRefreshIntervalSeconds = 0.05f;

        private static MelonPreferences_Category _prefs;

        // Settings
        internal static MelonPreferences_Entry<bool> SelfInvincible;
        internal static MelonPreferences_Entry<bool> FriendlyInvincible;
        internal static MelonPreferences_Entry<bool> SelfInfiniteAmmo;
        internal static MelonPreferences_Entry<bool> FriendlyInfiniteAmmo;
        internal static MelonPreferences_Entry<bool> NoReload;
        internal static MelonPreferences_Entry<bool> ESPEnabled;

        // ESP unit lists
        public List<Unit> Units = new List<Unit>();
        public List<Unit> InfantryUnits = new List<Unit>();
        internal readonly List<InfantryEmplacementHolder> EmplacementHolders = new List<InfantryEmplacementHolder>();

        private float _rescanTimer;
        private float _espRefreshTimer;

        // Ammo-feed queue reflection refs / setters used for "no reload"
        private static readonly AccessTools.FieldRef<GHPC.Weapons.AmmoFeed, Queue<AmmoType>> VehicleClipMainRef =
            AccessTools.FieldRefAccess<GHPC.Weapons.AmmoFeed, Queue<AmmoType>>("_feedClipMain");
        private static readonly AccessTools.FieldRef<GHPC.Weapons.AmmoFeed, Queue<AmmoType>> VehicleClipAuxRef =
            AccessTools.FieldRefAccess<GHPC.Weapons.AmmoFeed, Queue<AmmoType>>("_feedClipAux");
        private static readonly AccessTools.FieldRef<GHPC.Weapons.AmmoFeed, bool> VehicleAuxFeedModeRef =
            AccessTools.FieldRefAccess<GHPC.Weapons.AmmoFeed, bool>("_auxFeedMode");
        private static readonly System.Reflection.MethodInfo VehicleLoadedClipTypeSetter =
            AccessTools.PropertySetter(typeof(GHPC.Weapons.AmmoFeed), "LoadedClipType");

        /// <summary>
        /// Refills the given ammunition feed's current clip queue.
        /// When <paramref name="keepChamberedRound"/> is true and a round is already chambered (an
        /// ammo-type switch keeps the old type's round in the breech), the queue is filled to
        /// Capacity - 1 so "clip + breech" still equals exactly one full clip; otherwise the switch
        /// would silently add one extra round. Keep it false for the pre-fire refill, where that
        /// chambered round is about to be fired.
        /// </summary>
        internal static void RefillVehicleLoadedClip(GHPC.Weapons.AmmoFeed feed, AmmoType.AmmoClip clipType, bool keepChamberedRound = false)
        {
            if (feed == null || clipType == null || clipType.Capacity <= 0
                || clipType.MinimalPattern == null || clipType.MinimalPattern.Length == 0)
            {
                return;
            }

            Queue<AmmoType> loadedClip = VehicleAuxFeedModeRef(feed) ? VehicleClipAuxRef(feed) : VehicleClipMainRef(feed);
            if (loadedClip == null)
            {
                return;
            }

            int rounds = clipType.Capacity;
            if (keepChamberedRound && feed.AmmoTypeInBreech != null)
            {
                rounds = Math.Max(0, rounds - 1);
            }

            loadedClip.Clear();
            for (int i = 0; i < rounds; i++)
            {
                int num = i % clipType.MinimalPattern.Length;
                loadedClip.Enqueue(clipType.MinimalPattern[num].AmmoType);
            }

            if (feed.LoadedClipType == null || !feed.LoadedClipType.Equals(clipType))
            {
                VehicleLoadedClipTypeSetter.Invoke(feed, new object[] { clipType });
            }
        }

        public override void OnInitializeMelon()
        {
            if (Instance != null)
            {
                MelonLogger.Error("Another instance of CheatMode is already loaded");
                return;
            }

            Instance = this;

            _prefs = MelonPreferences.CreateCategory("CheatMode", "Cheat Mode");
            SelfInvincible = _prefs.CreateEntry("SelfInvincible", true, "Self unit: invincible");
            FriendlyInvincible = _prefs.CreateEntry("FriendlyInvincible", true, "Friendly AI units: invincible");
            SelfInfiniteAmmo = _prefs.CreateEntry("SelfInfiniteAmmo", true, "Self unit: infinite ammo");
            FriendlyInfiniteAmmo = _prefs.CreateEntry("FriendlyInfiniteAmmo", true, "Friendly AI units: infinite ammo");
            NoReload = _prefs.CreateEntry("NoReload", true, "Player vehicle: no reloading (F10 toggles; friendly AI unaffected)");

            ESPEnabled = _prefs.CreateEntry("ESP", true, "ESP on/off (F9 toggles)");

            HarmonyInstance.PatchAll();
            MelonLogger.Msg("[CheatMode] Loaded. F9 = ESP on/off. F10 = no reload on/off. Settings are in MelonPreferences.cfg -> [CheatMode]");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            StateController.RunOrDefer(GameState.GameReady, new GameStateEventHandler(OnGameReady), GameStatePriority.Medium);
            ResetEspCache();
            _rescanTimer = 0f;
        }

        public override void OnUpdate()
        {
            // F9: toggle vehicles, ATGM and infantry ESP together (on / off).
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (ESPEnabled != null)
                {
                    ESPEnabled.Value = !ESPEnabled.Value;
                    MelonLogger.Msg("[CheatMode] ESP " + (ESPEnabled.Value ? "Enabled" : "Disabled"));
                }
            }

            // F10: toggle vehicle-weapon "no reload" (refill the clip immediately instead of reloading) on / off.
            if (Input.GetKeyDown(KeyCode.F10))
            {
                if (NoReload != null)
                {
                    NoReload.Value = !NoReload.Value;
                    MelonLogger.Msg("[CheatMode] No Reload " + (NoReload.Value ? "Enabled" : "Disabled"));
                }
            }

            // Periodically rescan to pick up reinforcement units and emplacements that appear only after the scene has loaded.
            if (StateController.IsGameStateTriggered(GameState.GameReady))
            {
                _rescanTimer -= Time.deltaTime;
                if (_rescanTimer <= 0f)
                {
                    _rescanTimer = RescanIntervalSeconds;
                    ApplyInfiniteAmmo(false);
                }
            }

            // Purge destroyed units from the ESP lists.
            for (int i = Units.Count - 1; i >= 0; i--)
            {
                if (Units[i] == null)
                {
                    RemoveEspUnit(Units[i]);
                    Units.RemoveAt(i);
                }
            }
            for (int i = InfantryUnits.Count - 1; i >= 0; i--)
            {
                if (InfantryUnits[i] == null)
                {
                    RemoveEspUnit(InfantryUnits[i]);
                    InfantryUnits.RemoveAt(i);
                }
            }

            if (ESPEnabled != null && ESPEnabled.Value)
            {
                RefreshEspCache();
            }
            else
            {
                _espBoxes.Clear();
                _espRefreshTimer = 0f;
            }
        }

        public override void OnGUI()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }
            // Show the on-screen hint while "no reload" is on; hide it when off (same style as the ESP header).
            if (NoReload != null && NoReload.Value)
            {
                Render.DrawString(new Vector2(5, 20), "CheatMode No Reload Enabled", Color.red, false);
            }

            if (ESPEnabled == null || !ESPEnabled.Value)
            {
                return;
            }

            Render.DrawString(new Vector2(5, 2), "CheatMode ESP Enabled", Color.red, false);
            DrawCachedEsp();
        }

        private IEnumerator OnGameReady(GameState _)
        {
            // Bootstrap once; Awake patches track reinforcements without periodic global scans.
            foreach (InfantryEmplacementHolder holder in GameObject.FindObjectsByType<InfantryEmplacementHolder>(FindObjectsSortMode.None))
            {
                RegisterEmplacementHolder(holder);
            }
            ApplyInfiniteAmmo(true);
            yield break;
        }

        // ---------------------------------------------------------------
        // Shared methods for infinite ammo / no reload
        // ---------------------------------------------------------------

        internal static bool SelfAmmoEnabled
        {
            get { return SelfInfiniteAmmo != null && SelfInfiniteAmmo.Value; }
        }

        internal static bool FriendlyAmmoEnabled
        {
            get { return FriendlyInfiniteAmmo != null && FriendlyInfiniteAmmo.Value; }
        }

        internal static bool NoReloadEnabled
        {
            get { return NoReload != null && NoReload.Value; }
        }

        internal static bool Managed(MonoBehaviour component)
        {
            if (component == null || (!SelfAmmoEnabled && !FriendlyAmmoEnabled))
            {
                return false;
            }
            Unit owner = component.GetComponentInParent<Unit>();
            if (owner != null)
            {
                return ManagedUnit(owner);
            }
            InfantryEmplacementHolder holder = component.GetComponentInParent<InfantryEmplacementHolder>();
            if (holder == null || holder.GetComponent<CheatMarker>() == null)
            {
                return false;
            }
            UnitInfoBroker vehicle = holder.AttachedVehicleInfoBroker;
            if (vehicle != null && IsAmmoEligibleUnit(vehicle.Unit))
            {
                return true;
            }
            InfantryEmplacement[] emplacements = holder.Emplacements;
            if (emplacements != null)
            {
                foreach (InfantryEmplacement emplacement in emplacements)
                {
                    if (emplacement != null && IsAmmoEligibleUnit(emplacement.AssignedUnit))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static bool ManagedUnit(Unit unit)
        {
            // Most firing attempts are from unmarked enemies. Reject them before faction/platoon work.
            return (SelfAmmoEnabled || FriendlyAmmoEnabled) && unit != null
                && unit.GetComponent<CheatMarker>() != null && IsAmmoEligibleUnit(unit);
        }

        private static bool IsAmmoEligibleUnit(Unit unit)
        {
            if (unit == null || unit.Neutralized || (!SelfAmmoEnabled && !FriendlyAmmoEnabled))
            {
                return false;
            }
            PlayerInput input = PlayerInput.Instance;
            Unit player = input != null ? input.CurrentPlayerUnit : null;
            if (unit == player)
            {
                return SelfAmmoEnabled;
            }
            if (!FriendlyAmmoEnabled)
            {
                return false;
            }
            string source;
            if (unit.Allegiance == ResolveFriendlyFaction(player, out source))
            {
                return true;
            }
            PlatoonData platoon = player != null ? PlatoonManager.GetPlatoonDataByUnit(player) : null;
            return platoon != null && platoon.Units != null && platoon.Units.Contains(unit);
        }

        internal void RegisterEmplacementHolder(InfantryEmplacementHolder holder)
        {
            if (holder != null && !EmplacementHolders.Contains(holder))
            {
                EmplacementHolders.Add(holder);
            }
        }

        /// <summary>
        /// Whether the given component (a weapon / feed system) belongs to the vehicle the player
        /// currently controls. Used to make "no reload" apply only to the player: friendly AI
        /// vehicles that carry a CheatMarker (and enjoy infinite ammo) still reload normally.
        /// </summary>
        internal static bool IsPlayerVehicleComponent(MonoBehaviour component)
        {
            if (component == null)
            {
                return false;
            }

            PlayerInput playerInput = PlayerInput.Instance;
            Unit playerUnit = (playerInput != null) ? playerInput.CurrentPlayerUnit : null;
            if (playerUnit == null)
            {
                return false;
            }

            Vehicle vehicle = component.GetComponentInParent<Vehicle>();
            return vehicle != null && vehicle.gameObject == playerUnit.gameObject;
        }

        // ---------------------------------------------------------------
        // Infinite-ammo scan logic (derived from InfiniteAmmo)
        // ---------------------------------------------------------------

        internal static Faction ResolveFriendlyFaction(Unit playerUnit, out string factionSource)
        {
            // Resolve the player's faction (authoritative cascade).
            Faction friendlyFaction = Faction.Neutral;
            factionSource = "none";
            if (SceneController.TargetSpawningFaction != Faction.Neutral)
            {
                friendlyFaction = SceneController.TargetSpawningFaction;
                factionSource = "SceneController";
            }
            else if (MissionStateController.Instance != null
                && MissionStateController.Instance.MissionSceneMeta != null
                && MissionStateController.Instance.MissionSceneMeta.DynamicMetadata != null
                && MissionStateController.Instance.MissionSceneMeta.DynamicMetadata.MissionData != null
                && MissionStateController.Instance.MissionSceneMeta.DynamicMetadata.MissionData.PlayerFaction != Faction.Neutral)
            {
                friendlyFaction = MissionStateController.Instance.MissionSceneMeta.DynamicMetadata.MissionData.PlayerFaction;
                factionSource = "MissionData";
            }
            else if (playerUnit != null && playerUnit.Allegiance != Faction.Neutral)
            {
                friendlyFaction = playerUnit.Allegiance;
                factionSource = "PlayerUnit";
            }
            else if (playerUnit != null)
            {
                friendlyFaction = playerUnit.Allegiance;
                factionSource = "PlayerUnit(Neutral)";
            }
            else
            {
                friendlyFaction = Faction.Neutral;
                factionSource = "fallback";
            }

            return friendlyFaction;
        }

        private void ApplyInfiniteAmmo(bool logSummary)
        {
            for (int i = EmplacementHolders.Count - 1; i >= 0; i--)
            {
                if (EmplacementHolders[i] == null)
                {
                    EmplacementHolders.RemoveAt(i);
                }
            }
            PlayerInput playerInput = PlayerInput.Instance;
            Unit playerUnit = (playerInput != null) ? playerInput.CurrentPlayerUnit : null;

            bool selfAmmo = SelfAmmoEnabled;
            bool friendlyAmmo = FriendlyAmmoEnabled;
            if (!selfAmmo && !friendlyAmmo)
            {
                return;
            }

            string factionSource;
            Faction friendlyFaction = ResolveFriendlyFaction(playerUnit, out factionSource);

            int enabledCount = 0;

            // 1. The player's own unit (separate toggle).
            if (selfAmmo && playerUnit != null && !playerUnit.Neutralized && playerUnit.GetComponent<CheatMarker>() == null)
            {
                SeedUnit(playerUnit);
                playerUnit.gameObject.AddComponent<CheatMarker>();
                enabledCount++;
            }

            if (friendlyAmmo)
            {
                // 2. Every live unit of the friendly faction.
                List<Unit>[] liveUnits = SceneUnitsManager.AllLiveUnitsByFaction;
                int factionIndex = (int)friendlyFaction;
                List<Unit> friendlies = liveUnits != null && factionIndex >= 0 && factionIndex < liveUnits.Length
                    ? liveUnits[factionIndex] : null;
                if (friendlies != null)
                {
                    foreach (Unit unit in friendlies)
                    {
                        if (unit == null || unit.GetComponent<CheatMarker>() != null || unit.Neutralized)
                        {
                            continue;
                        }
                        if (ReferenceEquals(unit, playerUnit) && !selfAmmo)
                        {
                            continue;
                        }

                        SeedUnit(unit);
                        unit.gameObject.AddComponent<CheatMarker>();
                        enabledCount++;
                    }
                }

                // 3. Members of the player's platoon (extra safety net).
                if (playerUnit != null)
                {
                    PlatoonData platoon = PlatoonManager.GetPlatoonDataByUnit(playerUnit);
                    if (platoon != null && platoon.Units != null)
                    {
                        foreach (Unit member in platoon.Units)
                        {
                            if (member == null || member.GetComponent<CheatMarker>() != null || member.Neutralized)
                            {
                                continue;
                            }
                            if (ReferenceEquals(member, playerUnit) && !selfAmmo)
                            {
                                continue;
                            }

                            SeedUnit(member);
                            member.gameObject.AddComponent<CheatMarker>();
                            enabledCount++;
                        }
                    }
                }

                // 4. Infantry crew-served weapon emplacements.
                foreach (InfantryEmplacementHolder holder in EmplacementHolders)
                {
                    if (holder == null || !holder.gameObject.activeInHierarchy
                        || holder.GetComponent<CheatMarker>() != null || !IsHolderFriendly(holder, friendlyFaction))
                    {
                        continue;
                    }

                    RefillWeaponsUnder(holder.transform);
                    holder.gameObject.AddComponent<CheatMarker>();
                    enabledCount++;
                }
            }

            if (logSummary)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("[CheatMode] Scene scan finished: ").Append(enabledCount).Append(" unit(s) granted infinite ammo.");
                sb.Append(" side=").Append(friendlyFaction).Append(" (via ").Append(factionSource).Append(")");
                sb.Append("; player=");
                if (playerUnit == null)
                {
                    sb.Append("none");
                }
                else
                {
                    sb.Append(playerUnit.UniqueName).Append(" [").Append(playerUnit.Allegiance).Append('/').Append(playerUnit.GetType().Name).Append(']');
                }
                MelonLogger.Msg(sb.ToString());
            }
        }

        private static bool IsHolderFriendly(InfantryEmplacementHolder holder, Faction playerFaction)
        {
            UnitInfoBroker attachedVehicle = holder.AttachedVehicleInfoBroker;
            if (attachedVehicle != null && attachedVehicle.Unit != null && attachedVehicle.Unit.Allegiance == playerFaction)
            {
                return true;
            }

            InfantryEmplacement[] emplacements = holder.Emplacements;

            if (emplacements != null)
            {
                foreach (InfantryEmplacement emplacement in emplacements)
                {
                    InfantryUnit assigned = (emplacement != null) ? emplacement.AssignedUnit : null;
                    if (assigned != null && assigned.Allegiance == playerFaction)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void SeedUnit(Unit unit)
        {
            Vehicle vehicle = unit as Vehicle;
            if (vehicle != null)
            {
                EnsureSeedAmmo(vehicle);
            }

            InfantryUnit infantry = unit as InfantryUnit;
            if (infantry != null)
            {
                RefillWeaponsUnder(infantry.transform);
                InfantryEmplacement emplacement = infantry.Emplacement;
                InfantryEmplacementHolder holder = (emplacement != null) ? emplacement.EmplacementHolder : null;
                if (holder != null && holder.GetComponent<CheatMarker>() == null)
                {
                    RefillWeaponsUnder(holder.transform);
                    holder.gameObject.AddComponent<CheatMarker>();
                }
            }
        }

        internal static void RefillWeaponsUnder(Transform root)
        {
            if (root == null)
            {
                return;
            }
            foreach (InfantryWeaponSystem weapon in root.GetComponentsInChildren<InfantryWeaponSystem>(true))
            {
                InfantryWeaponAmmoPatch.RefillWeapon(weapon);
            }
            foreach (InfantryThrowableWeaponSystem throwable in root.GetComponentsInChildren<InfantryThrowableWeaponSystem>(true))
            {
                InfantryThrowableAmmoPatch.RefillThrowable(throwable);
            }
        }

        private static void EnsureSeedAmmo(Vehicle vehicle)
        {
            LoadoutManager loadoutManager = vehicle.LoadoutManager;
            if (loadoutManager == null || loadoutManager.RackLoadouts == null)
            {
                return;
            }

            foreach (LoadoutManager.RackLoadout rackLoadout in loadoutManager.RackLoadouts)
            {
                if (rackLoadout == null || rackLoadout.Rack == null)
                {
                    continue;
                }

                GHPC.Weapons.AmmoRack rack = rackLoadout.Rack;
                AmmoType.AmmoClip[] clipTypes = rack.ClipTypes;
                if (clipTypes == null || clipTypes.Length == 0)
                {
                    continue;
                }

                foreach (AmmoType.AmmoClip clipType in clipTypes)
                {
                    if (clipType != null && !rack.HasClipWithPattern(clipType))
                    {
                        rack.AddInvisibleClip(clipType);
                    }
                }
            }
        }

    }
}
