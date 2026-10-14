using System.Collections.Generic;
using UnityEngine;
using Chorewars.AR;
using Chorewars.Core;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Ghost Run: race your own previous best, replayed as a translucent ribbon with a moving head.
    /// </summary>
    /// <remarks>
    /// The ghost is a real recording — the sparse trail that <see cref="RuntimeCoverageMode"/> saves
    /// into <see cref="PlayerProfile"/> at the end of a run — not a scripted path. To keep the race
    /// honest, the ghost's progress is scored on its own <see cref="CoverageMap"/> with the same bounds
    /// as the player's, fed one recorded point at a time as the playback head passes it. So "you are
    /// ahead" means the player has actually cleaned more floor than the ghost had, at the same elapsed
    /// time, measured the same way — not an estimate from the path length.
    ///
    /// The ghost is your best Ghost Run first, and your best Standard Coverage run if you have not
    /// finished a Ghost Run yet, so the mode has a rival the first time it is played.
    /// </remarks>
    public sealed class RuntimeGhostRunMode : RuntimeCoverageMode
    {
        private const string FallbackGhostKey = "hoover";
        private const float MaxGhostBonus = 240f;
        private const float ObjectiveIntervalSeconds = 0.25f;
        private const float GhostMarkStepMeters = 0.05f;

        private static readonly Color GhostColour = new(0.45f, 0.85f, 1f, 0.35f);
        private static readonly Color GhostHeadColour = new(0.6f, 0.95f, 1f, 0.75f);

        private readonly List<Vector3> _ghostPath = new();

        private string _ghostSourceKey;
        private float _ghostDuration;
        private Transform _ghostRoot;
        private CoverageMap _ghostMap;
        private Transform _ghostHead;
        private int _ghostMarkedUpTo;
        private float _ghostCoveragePercent;
        private float _pacePoints;
        private float _nextObjectiveAt;

        public override string ProfileModeId => "ghost-run";

        public override string ModeDisplayName => "Ghost Run";

        protected override void OnModeBegun()
        {
            base.OnModeBegun();
            LoadGhost();
            BuildGhostVisuals();
        }

        protected override void OnModeEnded(ChoreResult result)
        {
            if (_ghostRoot != null) Destroy(_ghostRoot.gameObject);
            if (_ghostMap != null) Destroy(_ghostMap.gameObject);
            base.OnModeEnded(result);
        }

        protected override void OnModeTick(float elapsedSeconds, float coveragePercent)
        {
            if (_ghostPath.Count < 2 || _ghostDuration <= 0f) return;

            // Advance the playback head, feeding each newly-passed recorded point into the ghost map so
            // the ghost's coverage is measured exactly like the player's.
            float fraction = Mathf.Clamp01(elapsedSeconds / _ghostDuration);
            int target = Mathf.Min(_ghostPath.Count - 1, Mathf.FloorToInt(fraction * (_ghostPath.Count - 1)));

            while (_ghostMarkedUpTo <= target)
            {
                // Mark the segment the ghost travelled getting here, not just the sampled points: the
                // recording stores points at a fixed spacing, and the path between two of them crosses
                // cells neither point lands in. Walking the segment is what makes the ghost's coverage
                // comparable to the dense per-frame marking a live run does.
                var point = _ghostPath[_ghostMarkedUpTo];

                if (_ghostMarkedUpTo > 0)
                {
                    var previous = _ghostPath[_ghostMarkedUpTo - 1];
                    float distance = Vector3.Distance(previous, point);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(distance / GhostMarkStepMeters));

                    for (int step = 1; step <= steps; step++)
                        _ghostMap?.MarkVisitedWorldPosition(Vector3.Lerp(previous, point, (float)step / steps));
                }
                else
                {
                    _ghostMap?.MarkVisitedWorldPosition(point);
                }

                _ghostMarkedUpTo++;
            }

            int head = Mathf.Min(_ghostPath.Count - 1, Mathf.Max(0, _ghostMarkedUpTo - 1));
            if (_ghostHead != null) _ghostHead.position = Flatten(_ghostPath[head]);

            _ghostCoveragePercent = _ghostMap != null ? _ghostMap.ComputeCoveragePercent() : 0f;
            _pacePoints = coveragePercent - _ghostCoveragePercent;

            if (Time.time >= _nextObjectiveAt)
            {
                _nextObjectiveAt = Time.time + ObjectiveIntervalSeconds;

                string lead = _pacePoints >= 0f
                    ? $"AHEAD +{_pacePoints:0}%"
                    : $"BEHIND {_pacePoints:0}%";
                Hud?.SetObjective($"GHOST {_ghostCoveragePercent:0}%   YOU {coveragePercent:0}%   {lead}");
            }
        }

        /// <summary>Being ahead of your own ghost is worth up to 240; being behind costs nothing.</summary>
        protected override float GetBonusPoints()
        {
            float ghostBonus = Mathf.Clamp(_pacePoints, 0f, 40f) / 40f * MaxGhostBonus;
            return base.GetBonusPoints() + ghostBonus;
        }

        private void LoadGhost()
        {
            var profile = PlayerProfile.Instance;
            if (profile == null) return;

            // Prefer the player's own best Ghost Run, then their best Standard Coverage, so there is a
            // rival on the very first Ghost Run.
            if (profile.TryGetBestGhost(ProfileModeId, out var ownPath, out var ownDuration) && ownPath.Count >= 2)
            {
                _ghostSourceKey = ProfileModeId;
                _ghostPath.AddRange(ownPath);
                _ghostDuration = ownDuration;
            }
            else if (profile.TryGetBestGhost(FallbackGhostKey, out var refPath, out var refDuration) && refPath.Count >= 2)
            {
                _ghostSourceKey = FallbackGhostKey;
                _ghostPath.AddRange(refPath);
                _ghostDuration = refDuration;
            }

            if (_ghostPath.Count >= 2)
            {
                Debug.Log($"[BoreDOOM] Ghost Run racing the '{_ghostSourceKey}' ghost — {_ghostPath.Count} points over {_ghostDuration:0.0}s.");
            }
            else
            {
                Hud?.SetObjective("No ghost yet — race your best Standard Coverage run once you have set one.");
                Debug.Log("[BoreDOOM] Ghost Run has no ghost to race yet.");
            }
        }

        private void BuildGhostVisuals()
        {
            if (_ghostPath.Count < 2) return;

            _ghostRoot = new GameObject("GhostRun").transform;
            _ghostRoot.SetParent(transform, false);

            // The ghost's own coverage canvas: same bounds as the player's, fed its recorded points.
            var mapGo = new GameObject("GhostCoverage");
            mapGo.transform.SetParent(_ghostRoot, false);
            _ghostMap = mapGo.AddComponent<CoverageMap>();
            _ghostMap.SetBounds(
                new Vector2(PlayBounds.min.x, PlayBounds.min.z),
                new Vector2(PlayBounds.max.x, PlayBounds.max.z));

            // Flatten the ribbon onto the floor: the recording is at tool height, which reads better as
            // a path drawn on the actual floor.
            var floorPath = new Vector3[_ghostPath.Count];
            for (int i = 0; i < _ghostPath.Count; i++) floorPath[i] = Flatten(_ghostPath[i]);

            var lineGo = new GameObject("GhostRibbon");
            lineGo.transform.SetParent(_ghostRoot, false);
            var line = lineGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = floorPath.Length;
            line.SetPositions(floorPath);
            line.startWidth = 0.06f;
            line.endWidth = 0.06f;
            line.numCapVertices = 2;
            line.sharedMaterial = PlaceholderMaterials.CreateTransparent(GhostColour, GhostColour.a);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "GhostHead";
            head.transform.SetParent(_ghostRoot, false);
            head.transform.localScale = Vector3.one * 0.14f;
            var headCollider = head.GetComponent<Collider>();
            if (headCollider != null) Destroy(headCollider);
            var headRenderer = head.GetComponent<Renderer>();
            headRenderer.sharedMaterial = PlaceholderMaterials.CreateTransparent(GhostHeadColour, GhostHeadColour.a);
            headRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            headRenderer.receiveShadows = false;
            _ghostHead = head.transform;

            // Start the head at the first recorded point rather than at the origin.
            _ghostHead.position = Flatten(_ghostPath[0]);
        }

        private Vector3 Flatten(Vector3 world) => new(world.x, PlayBounds.min.y + 0.05f, world.z);
    }
}
