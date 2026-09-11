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

[assembly: MelonInfo(typeof(CheatMode.CheatModeMod), "CheatMode", "1.6.3", "CheatMode")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace CheatMode
{
    /// <summary>
    /// Marker component. Attached to units (vehicles / infantry) that receive infinite ammo,
    /// as well as to infantry crew-served weapon emplacements. Its purposes are:
    /// 1. Prevent the same unit from being processed twice;
    /// 2. Let the infinite-ammo / no-reload patches quickly decide whether an object is
    ///    managed by this mod.
    /// </summary>
    public class CheatMarker : MonoBehaviour
    {
    }

    public class CheatModeMod : MelonMod
    {
        public static CheatModeMod Instance;

        private const float RescanIntervalSeconds = 2.5f;

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

        private float _rescanTimer;

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
            NoReload = _prefs.CreateEntry("NoReload", true, "Player vehicle: no reloading (F9 toggles; friendly AI unaffected)");

            ESPEnabled = _prefs.CreateEntry("ESP", true, "ESP on/off (F8 toggles)");

            HarmonyInstance.PatchAll();
            MelonLogger.Msg("[CheatMode] Loaded. F8 = ESP on/off. F9 = no reload on/off. Settings are in MelonPreferences.cfg -> [CheatMode]");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            StateController.RunOrDefer(GameState.GameReady, new GameStateEventHandler(OnGameReady), GameStatePriority.Medium);
        }

        public override void OnUpdate()
        {
            // F8: toggle vehicles, ATGM and infantry ESP together (on / off).
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (ESPEnabled != null)
                {
                    ESPEnabled.Value = !ESPEnabled.Value;
                    MelonLogger.Msg("[CheatMode] ESP " + (ESPEnabled.Value ? "Enabled" : "Disabled"));
                }
            }

            // F9: toggle vehicle-weapon "no reload" (refill the clip immediately instead of reloading) on / off.
            if (Input.GetKeyDown(KeyCode.F9))
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
                    Units.RemoveAt(i);
                }
            }
            for (int i = InfantryUnits.Count - 1; i >= 0; i--)
            {
                if (InfantryUnits[i] == null)
                {
                    InfantryUnits.RemoveAt(i);
                }
            }
        }

        public override void OnGUI()
        {
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
            DrawUnitsESP(Units);
            DrawUnitsESP(InfantryUnits);
        }

        private IEnumerator OnGameReady(GameState _)
        {
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
            return component != null && component.GetComponentInParent<CheatMarker>() != null;
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

        private void ApplyInfiniteAmmo(bool logSummary)
        {
            PlayerInput playerInput = PlayerInput.Instance;
            Unit playerUnit = (playerInput != null) ? playerInput.CurrentPlayerUnit : null;

            bool selfAmmo = SelfAmmoEnabled;
            bool friendlyAmmo = FriendlyAmmoEnabled;
            if (!selfAmmo && !friendlyAmmo)
            {
                return;
            }

            // Resolve the player's faction (authoritative cascade).
            Faction friendlyFaction = Faction.Neutral;
            string factionSource = "none";
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
                List<Unit> friendlies = SceneUnitsManager.AllLiveUnitsByFaction[(int)friendlyFaction];
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
                InfantryEmplacementHolder[] holders = GameObject.FindObjectsByType<InfantryEmplacementHolder>(FindObjectsSortMode.None);
                foreach (InfantryEmplacementHolder holder in holders)
                {
                    if (holder == null || holder.GetComponent<CheatMarker>() != null || !IsHolderFriendly(holder, friendlyFaction))
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

        // ---------------------------------------------------------------
        // ESP rendering
        // ---------------------------------------------------------------

        private void DrawUnitsESP(List<Unit> unitsToDraw)
        {
            if (PlayerInput.Instance == null)
            {
                return;
            }

            Camera mainCamera = CameraManager.MainCam;
            if (mainCamera == null)
            {
                return;
            }

            foreach (Unit unit in unitsToDraw)
            {
                if (unit == null || unit == PlayerInput.Instance.CurrentPlayerUnit)
                {
                    continue;
                }

                (Vector3 componentTopPos, Vector3 componentBottomPos, float componentWidth) = GetComponentDimensions(unit, false, mainCamera);

                if (componentTopPos.z > 0f && componentBottomPos.z > 0f)
                {
                    Color highlightColor;
                    switch (unit.Allegiance)
                    {
                        case Faction.Blue:
                            highlightColor = Color.blue;
                            break;
                        case Faction.Red:
                            highlightColor = Color.red;
                            break;
                        case Faction.Green:
                            highlightColor = Color.green;
                            break;
                        case Faction.Neutral:
                            highlightColor = Color.yellow;
                            break;
                        default:
                            highlightColor = Color.white;
                            break;
                    }

                    if (unit.Neutralized)
                    {
                        highlightColor = Color.gray;
                    }

                    Unit playerUnit = PlayerInput.Instance.CurrentPlayerUnit;
                    string label = unit.FriendlyName;
                    if (playerUnit != null)
                    {
                        float distance = Vector3.Distance(playerUnit.transform.position, unit.transform.position);
                        label = unit.FriendlyName + " [" + distance.ToString("F0") + "m]";
                    }

                    DrawBoxESP(componentBottomPos, componentTopPos, highlightColor, componentWidth, true, label);
                }
            }
        }

        private (Vector3, Vector3, float) GetComponentDimensions(Component component, bool pivotPoint, Camera camera)
        {
            if (component == null || camera == null)
            {
                return (Vector3.zero, Vector3.zero, 0f);
            }

            Vector3 componentPos = component.transform.position;

            Collider[] colliders = component.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0)
            {
                return (componentPos, componentPos, 0f);
            }

            Bounds combinedBounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                combinedBounds.Encapsulate(colliders[i].bounds);
            }

            Vector3 componentTopPos = componentPos;
            Vector3 componentBottomPos = componentPos;

            if (pivotPoint)
            {
                float pivotOffsetY = componentPos.y - combinedBounds.center.y;
                componentTopPos.y = combinedBounds.max.y + pivotOffsetY;
                componentBottomPos.y = combinedBounds.min.y + pivotOffsetY;
            }
            else
            {
                componentTopPos.y = combinedBounds.max.y;
                componentBottomPos.y = combinedBounds.min.y;
            }

            Vector3[] corners = new Vector3[8];
            Vector3 center = combinedBounds.center;
            Vector3 extents = combinedBounds.extents;

            corners[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
            corners[1] = center + new Vector3(-extents.x, -extents.y, extents.z);
            corners[2] = center + new Vector3(-extents.x, extents.y, -extents.z);
            corners[3] = center + new Vector3(-extents.x, extents.y, extents.z);
            corners[4] = center + new Vector3(extents.x, -extents.y, -extents.z);
            corners[5] = center + new Vector3(extents.x, -extents.y, extents.z);
            corners[6] = center + new Vector3(extents.x, extents.y, -extents.z);
            corners[7] = center + new Vector3(extents.x, extents.y, extents.z);

            Vector3[] screenCorners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                screenCorners[i] = camera.WorldToScreenPoint(corners[i]);
            }

            float minScreenX = float.MaxValue;
            float maxScreenX = float.MinValue;
            foreach (Vector3 screenCorner in screenCorners)
            {
                minScreenX = Mathf.Min(minScreenX, screenCorner.x);
                maxScreenX = Mathf.Max(maxScreenX, screenCorner.x);
            }

            float widthScreen = maxScreenX - minScreenX;
            return (camera.WorldToScreenPoint(componentTopPos), camera.WorldToScreenPoint(componentBottomPos), widthScreen);
        }

        private void DrawBoxESP(Vector3 bottomPos, Vector3 topPos, Color color, float width, bool snapLine, string label)
        {
            bottomPos = ScaleVector(bottomPos);
            topPos = ScaleVector(topPos);

            if (bottomPos.x < 0 || bottomPos.x > Screen.width || bottomPos.y < 0 || bottomPos.y > Screen.height)
            {
                return;
            }

            topPos.y = Screen.height - topPos.y;
            bottomPos.y = Screen.height - bottomPos.y;

            float height = topPos.y - bottomPos.y;
            if (width < Math.Abs(height))
            {
                width = Math.Abs(height);
            }

            Render.DrawBox(bottomPos.x - (width / 2), bottomPos.y, width, height, color, 2f, label);

            if (snapLine)
            {
                Render.DrawLine(new Vector2(Screen.width / 2, Screen.height / 2), new Vector2(bottomPos.x, bottomPos.y), color, 2f);
            }
        }

        public static Vector3 ScaleVector(Vector3 position)
        {
            return new Vector3(
                position.x / CameraManager.MainCam.pixelWidth * Screen.width,
                position.y / CameraManager.MainCam.pixelHeight * Screen.height,
                position.z
            );
        }
    }
}
