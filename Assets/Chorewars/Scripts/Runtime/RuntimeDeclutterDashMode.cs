using System.Collections.Generic;
using UnityEngine;
using Chorewars.Core;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Declutter Dash: get round the room and clear the clutter before the clock runs out. Each item
    /// you reach pops and scores; clearing the lot pays a completion bonus.
    /// </summary>
    /// <remarks>
    /// The clutter is placed procedurally inside the scanned <see cref="RuntimeCoverageMode.PlayBounds"/>
    /// — the Inspector-authored version falls back to eight random items in a fixed -3..3 box in the
    /// editor. Placement is seeded from the session count so each run is a fresh layout but any given
    /// run is reproducible for testing.
    ///
    /// The reach check is measured from the tracked tool, which is the runtime's stand-in for the
    /// player. On device that becomes the controller or hand transform.
    /// </remarks>
    public sealed class RuntimeDeclutterDashMode : RuntimeCoverageMode
    {
        private const int ItemCount = 8;
        private const int PointsPerItem = 50;
        private const int ClearAllBonus = 250;
        private const float ReachMeters = 0.6f;
        private const float PopSeconds = 0.22f;
        private const float ClearHoldSeconds = 0.35f;

        private static readonly Color[] ClutterColours =
        {
            new(0.85f, 0.45f, 0.30f, 1f),
            new(0.45f, 0.60f, 0.85f, 1f),
            new(0.80f, 0.75f, 0.35f, 1f),
            new(0.70f, 0.50f, 0.80f, 1f)
        };

        private sealed class ClutterItem
        {
            public Transform Root;
            public Vector3 Position;
            public bool Collected;
            public float PopStartedAt;
        }

        private readonly List<ClutterItem> _items = new();
        private Transform _clutterRoot;
        private int _collected;
        private float _nextObjectiveAt;
        private float _clearedAt = -1f;

        public override string ProfileModeId => "declutter-dash";

        public override string ModeDisplayName => "Declutter Dash";

        protected override void OnModeBegun()
        {
            base.OnModeBegun();
            SpawnClutter();
            UpdateObjective();
        }

        protected override void OnModeEnded(ChoreResult result)
        {
            if (_clutterRoot != null) Destroy(_clutterRoot.gameObject);
            base.OnModeEnded(result);
        }

        protected override void OnModeTick(float elapsedSeconds, float coveragePercent)
        {
            CollectReachable();
            AnimatePops();

            if (Time.time >= _nextObjectiveAt) UpdateObjective();

            // Hold for a beat after the last item so the completed score reaches the HUD and the final
            // pop plays out before the summary covers the screen.
            if (_items.Count > 0 && _collected >= _items.Count && _clearedAt < 0f) _clearedAt = Time.time;
            if (_clearedAt >= 0f && Time.time >= _clearedAt + ClearHoldSeconds) End();
        }

        /// <summary>Each item scores, and clearing the room pays a completion bonus.</summary>
        protected override float GetBonusPoints()
        {
            float score = _collected * PointsPerItem;
            if (_items.Count > 0 && _collected >= _items.Count) score += ClearAllBonus;
            return base.GetBonusPoints() + score;
        }

        private void SpawnClutter()
        {
            var bounds = PlayBounds;

            _clutterRoot = new GameObject("Clutter").transform;
            _clutterRoot.SetParent(transform, false);

            // Seed from the session count: a fresh layout each run, but reproducible for a given run.
            var profile = PlayerProfile.Instance;
            int seed = (profile != null ? profile.TotalSessions : 0) * 7919 + 13;
            var random = new System.Random(seed);

            // Keep clear of the room edges and of the origin, where the tool starts.
            float margin = 0.5f;
            float minX = bounds.min.x + margin, maxX = bounds.max.x - margin;
            float minZ = bounds.min.z + margin, maxZ = bounds.max.z - margin;

            for (int i = 0; i < ItemCount; i++)
            {
                Vector3 position = Vector3.zero;
                bool placeable = false;

                for (int attempt = 0; attempt < 12 && !placeable; attempt++)
                {
                    float x = Mathf.Lerp(minX, maxX, (float)random.NextDouble());
                    float z = Mathf.Lerp(minZ, maxZ, (float)random.NextDouble());
                    position = new Vector3(x, bounds.min.y, z);
                    placeable = new Vector2(x, z).magnitude > 0.8f;
                }

                _items.Add(new ClutterItem
                {
                    Root = BuildItem(position, i).transform,
                    Position = position
                });
            }

            Debug.Log($"[BoreDOOM] Declutter Dash placed {ItemCount} clutter items (seed {seed}).");
        }

        private GameObject BuildItem(Vector3 floorPosition, int index)
        {
            var go = new GameObject($"Clutter{index}");
            go.transform.SetParent(_clutterRoot, false);
            go.transform.position = floorPosition + Vector3.up * 0.16f;

            var colour = ClutterColours[index % ClutterColours.Length];

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.26f, 0.26f, 0.26f);
            body.transform.localRotation = Quaternion.Euler(18f, index * 37f, 12f);
            var collider = body.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var material = PlaceholderMaterials.CreateOpaque(colour);
            var renderer = body.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go;
        }

        private void CollectReachable()
        {
            if (ToolDriver == null) return;

            var player = ToolDriver.transform.position;

            foreach (var item in _items)
            {
                if (item.Collected) continue;

                float distance = Vector2.Distance(
                    new Vector2(player.x, player.z),
                    new Vector2(item.Position.x, item.Position.z));

                if (distance > ReachMeters) continue;

                item.Collected = true;
                item.PopStartedAt = Time.time;
                _collected++;
                UpdateObjective();
            }
        }

        /// <summary>Shrinks a collected item out of existence, then removes it.</summary>
        private void AnimatePops()
        {
            foreach (var item in _items)
            {
                if (!item.Collected || item.Root == null) continue;

                float t = (Time.time - item.PopStartedAt) / PopSeconds;
                if (t >= 1f)
                {
                    Destroy(item.Root.gameObject);
                    item.Root = null;
                    continue;
                }

                item.Root.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, t);
                item.Root.Rotate(Vector3.up, 720f * Time.deltaTime, Space.World);
            }
        }

        private void UpdateObjective()
        {
            _nextObjectiveAt = Time.time + 0.25f;
            int remaining = _items.Count - _collected;

            if (_items.Count > 0 && remaining <= 0)
                Hud?.SetObjective($"CLUTTER CLEARED   +{ClearAllBonus + _collected * PointsPerItem}");
            else
                Hud?.SetObjective($"CLUTTER {remaining} left   (+{_collected * PointsPerItem})");
        }
    }
}
