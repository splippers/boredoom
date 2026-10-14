using System.Collections.Generic;
using UnityEngine;
using Chorewars.AR;
using Chorewars.Core;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Chaos Assessment: don't clean a thing — survey the room and come away with a mess report.
    /// Each room carries a chaos rating; walking it to the assessment threshold logs the room and
    /// banks its rating, and a full-house survey pays a completion bonus.
    /// </summary>
    /// <remarks>
    /// The Inspector-authored version rates a room by spatial-mesh density (triangles per square
    /// metre) straight off the scanned meshes. The editor's fake scan produces rectangles, not meshes,
    /// so there is nothing to measure yet. Rather than invent a number and pass it off as a reading,
    /// the chaos rating here is an explicit, clearly-labelled stand-in: a stable per-room value seeded
    /// from the room id, so a given room always reads the same and the mode is testable. <see
    /// cref="ChaosFor"/> is the single place to swap in the real mesh-density measure when the device
    /// scan provides meshes — the rest of the mode only consumes the number.
    /// </remarks>
    public sealed class RuntimeChaosAssessmentMode : RuntimeCoverageMode
    {
        /// <summary>
        /// A room counts as assessed once you have walked enough of it to survey it. Deliberately well
        /// below the dollhouse's 95% "cleaned" threshold: this mode is about reading the room, not
        /// scrubbing it, so a survey should complete long before a clean would.
        /// </summary>
        private const float AssessedThresholdPercent = 60f;
        private const float ChaosPointsPerLevel = 20f;
        private const float FullSurveyBonus = 150f;
        private const float ObjectiveIntervalSeconds = 0.4f;
        private const float FinishHoldSeconds = 0.4f;

        private static readonly Color TidyColour = new(0.30f, 0.90f, 0.45f, 0.85f);
        private static readonly Color ChaoticColour = new(0.95f, 0.35f, 0.20f, 0.85f);

        private readonly Dictionary<string, float> _chaos = new();
        private readonly HashSet<string> _assessed = new();
        private readonly List<Transform> _markers = new();
        private readonly List<float> _markerBaseY = new();
        private Transform _markerRoot;
        private float _nextObjectiveAt;
        private float _finishedAt = -1f;
        private float _bob;

        public override string ProfileModeId => "chaos-assessment";

        public override string ModeDisplayName => "Chaos Assessment";

        protected override void OnModeBegun()
        {
            base.OnModeBegun();
            BuildMarkers();
        }

        protected override void OnModeEnded(ChoreResult result)
        {
            Debug.Log($"[BoreDOOM] Chaos report — house reading {AverageChaos():0.0}/10: {ChaosLabel(AverageChaos())}");

            if (_markerRoot != null) Destroy(_markerRoot.gameObject);
            base.OnModeEnded(result);
        }

        protected override void OnModeTick(float elapsedSeconds, float coveragePercent)
        {
            AssessRooms();
            BobMarkers();

            if (Time.time >= _nextObjectiveAt) UpdateObjective();

            var rooms = pipeline != null ? pipeline.Rooms : null;
            if (rooms != null && rooms.Count > 0 && _assessed.Count >= rooms.Count && _finishedAt < 0f)
                _finishedAt = Time.time;

            if (_finishedAt >= 0f && Time.time >= _finishedAt + FinishHoldSeconds) End();
        }

        /// <summary>Each assessed room banks its chaos rating; a full survey pays a completion bonus.</summary>
        protected override float GetBonusPoints()
        {
            float score = 0f;
            foreach (var roomId in _assessed)
                if (_chaos.TryGetValue(roomId, out float chaos)) score += chaos * ChaosPointsPerLevel;

            var rooms = pipeline != null ? pipeline.Rooms : null;
            if (rooms != null && rooms.Count > 0 && _assessed.Count >= rooms.Count) score += FullSurveyBonus;

            return base.GetBonusPoints() + score;
        }

        private void AssessRooms()
        {
            if (pipeline == null || dollhouse == null) return;

            foreach (var room in pipeline.Rooms)
            {
                if (_assessed.Contains(room.roomId)) continue;

                if (dollhouse.RoomFinalCoverage.TryGetValue(room.roomId, out float coverage)
                    && coverage >= AssessedThresholdPercent)
                {
                    _assessed.Add(room.roomId);
                    Debug.Log($"[BoreDOOM] Chaos Assessment: {room.roomId} assessed — chaos {ChaosFor(room.roomId):0.0}/10.");
                }
            }
        }

        private void BuildMarkers()
        {
            var rooms = pipeline != null ? pipeline.Rooms : null;
            if (rooms == null || rooms.Count == 0) return;

            _markerRoot = new GameObject("ChaosMarkers").transform;
            _markerRoot.SetParent(transform, false);

            foreach (var room in rooms)
            {
                float chaos = ChaosFor(room.roomId);
                _chaos[room.roomId] = chaos;

                var rect = room.boundsXZ;
                var centre = new Vector3(rect.x + rect.width * 0.5f, room.floorY + 0.35f, rect.y + rect.height * 0.5f);

                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = $"Chaos_{room.roomId}";
                marker.transform.SetParent(_markerRoot, false);
                marker.transform.position = centre;
                marker.transform.localScale = Vector3.one * 0.16f;

                var collider = marker.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = marker.GetComponent<Renderer>();
                renderer.sharedMaterial = PlaceholderMaterials.CreateTransparent(
                    Color.Lerp(TidyColour, ChaoticColour, Mathf.InverseLerp(1f, 10f, chaos)), 0.85f);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                _markers.Add(marker.transform);
                _markerBaseY.Add(centre.y);
            }

            Debug.Log($"[BoreDOOM] Chaos Assessment reading {rooms.Count} rooms (stand-in chaos ratings).");
        }

        private void BobMarkers()
        {
            if (_markers.Count == 0) return;

            _bob += Time.deltaTime * 2.4f;
            float offset = Mathf.Sin(_bob) * 0.05f;

            for (int i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                if (marker == null) continue;

                float baseY = i < _markerBaseY.Count ? _markerBaseY[i] : marker.position.y;
                var position = marker.position;
                marker.position = new Vector3(position.x, baseY + offset, position.z);
            }
        }

        private void UpdateObjective()
        {
            _nextObjectiveAt = Time.time + ObjectiveIntervalSeconds;

            var rooms = pipeline != null ? pipeline.Rooms : null;
            int total = rooms != null ? rooms.Count : 0;
            if (total == 0) return;

            string worstId = null;
            float worst = 0f;
            foreach (var room in rooms)
            {
                if (_chaos.TryGetValue(room.roomId, out float chaos) && chaos > worst)
                {
                    worst = chaos;
                    worstId = room.roomId;
                }
            }

            Hud?.SetObjective($"ASSESSED {_assessed.Count}/{total}   messiest: {worstId} ({worst:0.0}/10)");
        }

        private float AverageChaos()
        {
            if (_chaos.Count == 0) return 0f;
            float sum = 0f;
            foreach (var value in _chaos.Values) sum += value;
            return sum / _chaos.Count;
        }

        private static string ChaosLabel(float chaos) => chaos switch
        {
            >= 9f => "DOOMED",
            >= 7f => "chaos adjacent",
            >= 5f => "lived in (honest)",
            >= 3f => "mostly fine",
            _ => "minimalist, suspicious"
        };

        /// <summary>
        /// Stand-in chaos rating, stable per room id. This is where the real spatial-mesh density
        /// measure belongs once the device scan yields meshes; everything else already consumes it.
        /// </summary>
        private static float ChaosFor(string roomId)
        {
            int hash = 17;
            foreach (char c in roomId) hash = unchecked(hash * 31 + c);

            // Avalanche before folding into the range. Without this, ids one character apart —
            // Room-001, Room-002 — differ by one in the hash and land at near-identical readings,
            // which makes the "which room is worst" readout useless.
            hash = unchecked(hash ^ (hash >> 15));
            hash = unchecked(hash * (int)0x85EBCA6B);
            hash = unchecked(hash ^ (hash >> 13));

            return 1f + Mathf.Abs(hash % 90) / 10f;   // 1.0 .. 9.9
        }
    }
}
