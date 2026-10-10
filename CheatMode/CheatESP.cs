using System.Collections.Generic;
using GHPC;
using GHPC.Camera;
using GHPC.Infantry;
using GHPC.Player;
using UnityEngine;

namespace CheatMode
{
    public partial class CheatModeMod
    {
        private sealed class EspEntry
        {
            public Unit Unit;
            public Transform Transform;
            public GameObject GameObject;
            public bool Infantry;
            public readonly List<Collider> Colliders = new List<Collider>(16);
            public float NextColliderRefresh;
            public float NextBoundsRefresh;
            public float NextLabelRefresh;
            public float RefreshPhase;
            public bool BoundsInitialized;
            public bool HasBounds;
            public bool VisibleAtLastBoundsRefresh;
            public Bounds Bounds;
            public Vector3 RootPosition;
            public Color Color;
            public string Name;
            public int Distance = -1;
            public string Label;
            public string MeasuredLabel;
            public GUIStyle LabelStyle;
            public Vector2 LabelSize;
        }

        // Cache world bounds, not screen coordinates: camera movement must be reflected on
        // every repaint. Collider discovery and label formatting run independently of drawing.
        private readonly List<EspEntry> _espBoxes = new List<EspEntry>(128);
        private readonly Dictionary<Unit, EspEntry> _espColliderCache = new Dictionary<Unit, EspEntry>(128);
        private readonly Plane[] _espPlanes = new Plane[6];
        private readonly Vector3[] _espCorners = new Vector3[8];
        private readonly Vector3[] _espProjectedCorners = new Vector3[8];
        private const float EspHiddenRefreshIntervalSeconds = 0.5f;
        private const float EspLabelRefreshIntervalSeconds = 0.1f;
        private const int EspColliderDiscoveriesPerRepaint = 4;

        private void ResetEspCache()
        {
            _espBoxes.Clear();
            _espColliderCache.Clear();
            _espRefreshTimer = 0f;
        }

        private void RemoveEspUnit(Unit unit)
        {
            // Unity's destroyed objects compare equal to null but still have dictionary keys.
            if (!ReferenceEquals(unit, null))
            {
                _espColliderCache.Remove(unit);
            }
        }

        private void RefreshEspCache()
        {
            _espRefreshTimer -= Time.unscaledDeltaTime;
            if (_espRefreshTimer > 0f)
            {
                return;
            }
            _espRefreshTimer = EspRefreshIntervalSeconds;
            _espBoxes.Clear();

            PlayerInput input = PlayerInput.Instance;
            Unit player = input != null ? input.CurrentPlayerUnit : null;
            if (player == null || !IsFinite(player.transform.position))
            {
                return;
            }

            // ESP has no distance limit; screen visibility is handled during drawing.
            AddEspBoxes(Units, player);
            AddEspBoxes(InfantryUnits, player);
        }

        private void AddEspBoxes(List<Unit> units, Unit player)
        {
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit == null || unit == player || !unit.gameObject.activeInHierarchy)
                {
                    continue;
                }
                EspEntry entry;
                if (!_espColliderCache.TryGetValue(unit, out entry))
                {
                    entry = new EspEntry
                    {
                        Unit = unit,
                        Transform = unit.transform,
                        GameObject = unit.gameObject,
                        Infantry = unit is InfantryUnit,
                        RefreshPhase = (unit.GetInstanceID() & 255) / 256f
                    };
                    _espColliderCache.Add(unit, entry);
                }
                _espBoxes.Add(entry);
            }
        }

        private static bool TryGetEspBounds(EspEntry entry, Vector3 root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool infantry = entry.Infantry;
            List<Collider> colliders = entry.Colliders;
            bool hasBounds = false;
            for (int i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy
                    || !collider.transform.IsChildOf(entry.Transform))
                {
                    continue;
                }
                Bounds candidate = collider.bounds;
                Vector3 size = candidate.size;
                float distanceSquared = (candidate.center - root).sqrMagnitude;
                if (!IsFinite(candidate.center) || !IsFinite(size) || !IsFinite(distanceSquared)
                    || size.x <= 0f || size.y <= 0f || size.z <= 0f
                    || size.x > (infantry ? 6f : 300f) || size.y > (infantry ? 8f : 300f)
                    || size.z > (infantry ? 6f : 300f)
                    || distanceSquared > (infantry ? 100f : 250000f))
                {
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = candidate;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(candidate);
                }
            }

            Vector3 combined = bounds.size;
            if (infantry && (!hasBounds || combined.x > 5f || combined.y > 6f || combined.z > 5f
                || (bounds.center - root).sqrMagnitude > 64f))
            {
                // Includes the case where every collider has invalid ragdoll bounds.
                bounds = new Bounds(root + Vector3.up * 0.9f, new Vector3(1f, 1.8f, 1f));
                return true;
            }
            return hasBounds && IsFinite(bounds.center) && IsFinite(combined)
                && combined.x <= 500f && combined.y <= 500f && combined.z <= 500f;
        }

        private void DrawCachedEsp()
        {
            Camera camera = CameraManager.MainCam;
            PlayerInput input = PlayerInput.Instance;
            Unit player = input != null ? input.CurrentPlayerUnit : null;
            if (camera == null || player == null || camera.pixelWidth <= 0 || camera.pixelHeight <= 0)
            {
                return;
            }
            GeometryUtility.CalculateFrustumPlanes(camera, _espPlanes);
            // Read camera/screen state once. Projection below is managed matrix math,
            // avoiding ten WorldToViewportPoint engine calls for every visible unit.
            Matrix4x4 view = camera.worldToCameraMatrix;
            Matrix4x4 viewProjection = camera.projectionMatrix * view;
            float near = Mathf.Max(camera.nearClipPlane, 0.001f);
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            float now = Time.unscaledTime;
            Vector3 playerPosition = player.transform.position;
            if (!IsFinite(playerPosition))
            {
                return;
            }
            int discoveryBudget = EspColliderDiscoveriesPerRepaint;
            for (int i = 0; i < _espBoxes.Count; i++)
            {
                EspEntry entry = _espBoxes[i];
                Unit unit = entry.Unit;
                if (unit == null || unit == player || !entry.GameObject.activeInHierarchy)
                {
                    continue;
                }
                // Follow moving units at the display frame rate, between bounds refreshes.
                Vector3 root = entry.Transform.position;
                if (!IsFinite(root))
                {
                    continue;
                }
                Bounds bounds = entry.Bounds;
                bounds.center += root - entry.RootPosition;
                // Conservative first-discovery bounds cover the existing collider validation
                // limits. They are only a culling hint and are never drawn as a box.
                Bounds probe = entry.HasBounds ? bounds
                    : new Bounds(root, Vector3.one * (entry.Infantry ? 22f : 1300f));
                probe.Expand(entry.Infantry ? 1f : 4f);
                bool visible = GeometryUtility.TestPlanesAABB(_espPlanes, probe);
                bool discovered = false;
                if (visible && discoveryBudget > 0
                    && (!entry.BoundsInitialized || now >= entry.NextColliderRefresh))
                {
                    unit.GetComponentsInChildren<Collider>(true, entry.Colliders);
                    discoveryBudget--;
                    discovered = true;
                    entry.NextColliderRefresh = now + RescanIntervalSeconds
                        * (0.8f + entry.RefreshPhase * 0.4f);
                }
                if (!entry.BoundsInitialized && !discovered)
                {
                    continue;
                }
                bool refreshedBounds = false;
                if (discovered || now >= entry.NextBoundsRefresh
                    || (visible && !entry.VisibleAtLastBoundsRefresh))
                {
                    bool firstRefresh = !entry.BoundsInitialized;
                    entry.HasBounds = TryGetEspBounds(entry, root, out bounds);
                    entry.BoundsInitialized = true;
                    refreshedBounds = true;
                    entry.Bounds = bounds;
                    entry.RootPosition = root;
                    entry.VisibleAtLastBoundsRefresh = visible;
                    float interval = visible ? EspRefreshIntervalSeconds : EspHiddenRefreshIntervalSeconds;
                    // Phase the first refresh, then preserve the interval independently per unit.
                    entry.NextBoundsRefresh = now + interval
                        * (firstRefresh ? 0.5f + entry.RefreshPhase * 0.5f : 1f);
                }
                if (!entry.HasBounds || (!visible && !refreshedBounds))
                {
                    continue;
                }
                if (!GeometryUtility.TestPlanesAABB(_espPlanes, bounds))
                {
                    continue;
                }
                Rect rect;
                if (!EspGeometry.TryProjectBounds(view, viewProjection, near, screenWidth, screenHeight,
                    bounds, root, _espCorners, _espProjectedCorners, out rect))
                {
                    continue;
                }
                // An off-screen unit becoming visible must refresh immediately, rather than
                // waiting for the slower hidden-unit interval to expire.
                if (!visible)
                {
                    entry.NextBoundsRefresh = 0f;
                }
                UpdateEspLabel(entry, root, playerPosition, now);
                Render.DrawBox(rect.x, rect.y, rect.width, rect.height, entry.Color, 2f,
                    entry.Label, entry.LabelSize);
            }
        }

        private static void UpdateEspLabel(EspEntry entry, Vector3 root, Vector3 playerPosition, float now)
        {
            Unit unit = entry.Unit;
            switch (unit.Allegiance)
            {
                case Faction.Blue: entry.Color = Color.blue; break;
                case Faction.Red: entry.Color = Color.red; break;
                case Faction.Green: entry.Color = Color.green; break;
                case Faction.Neutral: entry.Color = Color.yellow; break;
                default: entry.Color = Color.white; break;
            }
            if (unit.Neutralized)
            {
                entry.Color = Color.gray;
            }
            if (entry.Label == null || now >= entry.NextLabelRefresh)
            {
                float distanceSquared = (root - playerPosition).sqrMagnitude;
                if (IsFinite(distanceSquared))
                {
                    int distance = Mathf.RoundToInt(Mathf.Sqrt(distanceSquared));
                    string name = unit.FriendlyName;
                    if (entry.Label == null || distance != entry.Distance || name != entry.Name)
                    {
                        entry.Distance = distance;
                        entry.Name = name;
                        entry.Label = name + " [" + distance + "m]";
                    }
                }
                entry.NextLabelRefresh = now + EspLabelRefreshIntervalSeconds;
            }
            GUIStyle style = Render.StringStyle;
            if (entry.MeasuredLabel != entry.Label || entry.LabelStyle != style)
            {
                entry.LabelSize = Render.MeasureString(entry.Label);
                entry.MeasuredLabel = entry.Label;
                entry.LabelStyle = style;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
