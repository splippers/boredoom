using System.Collections.Generic;
using Chorewars.AR;
using Chorewars.Diagnostics;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Device scan source: drives the OpenXR <see cref="SpatialMeshTracker"/>, then turns the captured
    /// geometry into rooms with <see cref="MeshRoomExtractor"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately free of Quest-only compile dependencies so the project still builds and runs in
    /// the editor. If the runtime exposes no meshing, or the scan captures nothing, the scan still
    /// yields one sensible room rather than failing — a player standing in a room they cannot scan
    /// should still get to play, and the log says plainly that it was a fallback.
    /// </remarks>
    public sealed class QuestScanSource : MonoBehaviour, IScanSource
    {
        public event System.Action<RoomScan> RoomScanned;
        public event System.Action ScanCompleted;

        [SerializeField] private SpatialMeshTracker spatialMeshTracker;

        [Tooltip("Roughly how long a full scan takes, used only to pace the progress bar.")]
        [SerializeField] private float assumedScanSeconds = 20f;

        [Tooltip("Side length of the single room emitted when no meshing is available.")]
        [SerializeField] private float fallbackRoomSizeMeters = 3f;

        [Tooltip("How long the mesh must stop growing before an unattended scan is considered done.")]
        [SerializeField] private float settleSeconds = 6f;

        [Tooltip("Never finish from settling before this long, so a pause is not mistaken for the end.")]
        [SerializeField] private float minimumScanSeconds = 10f;

        [Tooltip("Hard limit: a scan that never settles still finishes here.")]
        [SerializeField] private float maximumScanSeconds = 60f;

        private readonly List<Vector3> _vertices = new();
        private float _startedAt;
        private int _lastMeshCount = -1;
        private float _lastGrowthAt;

        public float Progress { get; private set; }
        public bool IsScanning { get; private set; }

        // A live scan does not know its extent until meshes arrive.
        public Bounds? KnownBounds => null;

        public void Configure(SpatialMeshTracker tracker)
        {
            spatialMeshTracker = tracker;
        }

        public void Begin()
        {
            if (IsScanning) return;

            IsScanning = true;
            Progress = 0f;
            _startedAt = Time.unscaledTime;

            if (spatialMeshTracker != null)
            {
                try { spatialMeshTracker.StartScanning(); }
                catch (System.Exception e) { Debug.LogWarning($"[QuestScanSource] StartScanning failed: {e.Message}"); }
            }
        }

        public void Stop()
        {
            if (!IsScanning) return;

            IsScanning = false;
            Progress = 1f;

            if (spatialMeshTracker != null)
            {
                try { spatialMeshTracker.StopScanning(); }
                catch (System.Exception e) { Debug.LogWarning($"[QuestScanSource] StopScanning failed: {e.Message}"); }
            }

            ExtractAndEmitRooms();
            ScanCompleted?.Invoke();
        }

        private void Update()
        {
            if (!IsScanning) return;

            float elapsed = Time.unscaledTime - _startedAt;
            int meshCount = spatialMeshTracker != null ? spatialMeshTracker.MeshCount : 0;

            if (meshCount != _lastMeshCount)
            {
                _lastMeshCount = meshCount;
                _lastGrowthAt = Time.unscaledTime;
            }

            // Pace the bar from time, or from mesh arrival if meshes are actually coming in. Never
            // reach the top until the scan is stopped, so "100%" always means "done".
            float byTime = elapsed / Mathf.Max(1f, assumedScanSeconds);
            float byMesh = meshCount / 40f;

            Progress = Mathf.Clamp01(Mathf.Max(byTime, byMesh)) * 0.95f;

            if (ShouldFinish(elapsed, meshCount)) Stop();
        }

        /// <summary>
        /// Decides when an unattended scan is finished. The player can always finish by hand; this is
        /// the safety net for when they walk away. A scan is done when the space has stopped growing:
        /// either meshes are arriving and have now paused, or none ever arrived and there is nothing
        /// left to wait for. A pause only counts after a minimum, so a quiet moment while someone is
        /// still moving around is not mistaken for the end, and a hard limit stops the rest.
        /// </summary>
        private bool ShouldFinish(float elapsed, int meshCount)
        {
            if (elapsed >= maximumScanSeconds) return true;
            if (elapsed < minimumScanSeconds) return false;

            // Nothing is coming: the runtime exposes no meshing boundary to wait for.
            if (meshCount == 0) return elapsed >= assumedScanSeconds;

            return Time.unscaledTime - _lastGrowthAt >= settleSeconds;
        }

        private void ExtractAndEmitRooms()
        {
            _vertices.Clear();
            if (spatialMeshTracker != null) spatialMeshTracker.CollectWorldVertices(_vertices);

            // The count the mesh slice lives or dies by, declared where it is known. Zero vertices is
            // the silent fallback — the runtime produced no geometry — so it is judged WayOff rather
            // than waved through, and it reaches MQ3.log where the report can see it (a bare Debug.Log
            // only reaches logcat, which the telemetry pull does not read).
            ExpectationRegistry.Expect(
                "scanVertices", _vertices.Count,
                ExpectationRange.AtLeast(1), ExpectationRange.AtLeast(0),
                "vertices", "the room scan captured geometry from the environment");

            List<RoomScan> rooms = null;

            if (_vertices.Count > 0)
            {
                try
                {
                    rooms = MeshRoomExtractor.Extract(_vertices);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[QuestScanSource] Room extraction failed: {e.Message}");
                }
            }

            if (rooms == null || rooms.Count == 0)
            {
                Debug.LogWarning("[QuestScanSource] No rooms could be extracted from the scan — falling back to a single default room.");
                rooms = new List<RoomScan> { BuildFallbackRoom() };
            }

            foreach (var room in rooms) RoomScanned?.Invoke(room);

            TelemetryLogger.Info("Scan",
                $"scan complete — {rooms.Count} room(s) from {_vertices.Count} captured vertices.");
            Debug.Log($"[QuestScanSource] Scan complete — {rooms.Count} room(s) from {_vertices.Count} captured vertices.");
        }

        /// <summary>
        /// The room to play in when the scan could see nothing. The guardian boundary is tried first,
        /// because it is a real measurement of the space the player has — the runtime already knows it
        /// and it costs nothing. Only when there is no boundary either does the room become a box of
        /// an assumed size, which is a guess in the same breath as any other.
        /// </summary>
        private RoomScan BuildFallbackRoom()
        {
            if (PlaySpaceBoundary.TryGetRect() is Rect boundary)
            {
                Debug.Log($"[QuestScanSource] No meshing available — using the play-space boundary instead " +
                          $"({boundary.width:0.0}x{boundary.height:0.0} m at {boundary.x:0.0},{boundary.y:0.0}).");

                return new RoomScan
                {
                    roomId = "Play Space",
                    boundsXZ = boundary,
                    floorY = 0f,
                    wallHeight = 2.4f,
                    assumed = true
                };
            }

            float half = Mathf.Max(0.5f, fallbackRoomSizeMeters) * 0.5f;
            Debug.Log($"[QuestScanSource] No meshing and no play-space boundary — falling back to an " +
                      $"assumed {half * 2f:0.0}x{half * 2f:0.0} m room.");

            return new RoomScan
            {
                roomId = "Room-001",
                boundsXZ = new Rect(-half, -half, half * 2f, half * 2f),
                floorY = 0f,
                wallHeight = 2.4f,
                assumed = true
            };
        }
    }
}
