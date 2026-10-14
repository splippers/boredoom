using System.Collections.Generic;
using Chorewars.AR;
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

        private readonly List<Vector3> _vertices = new();
        private float _startedAt;

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

            // Pace the bar from time, or from mesh arrival if meshes are actually coming in. Never
            // reach the top until the scan is stopped, so "100%" always means "done".
            float byTime = (Time.unscaledTime - _startedAt) / Mathf.Max(1f, assumedScanSeconds);
            float byMesh = spatialMeshTracker != null ? spatialMeshTracker.MeshCount / 40f : 0f;

            Progress = Mathf.Clamp01(Mathf.Max(byTime, byMesh)) * 0.95f;
        }

        private void ExtractAndEmitRooms()
        {
            _vertices.Clear();
            if (spatialMeshTracker != null) spatialMeshTracker.CollectWorldVertices(_vertices);

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

            Debug.Log($"[QuestScanSource] Scan complete — {rooms.Count} room(s) from {_vertices.Count} captured vertices.");
        }

        private RoomScan BuildFallbackRoom()
        {
            float half = Mathf.Max(0.5f, fallbackRoomSizeMeters) * 0.5f;

            return new RoomScan
            {
                roomId = "Room-001",
                boundsXZ = new Rect(-half, -half, half * 2f, half * 2f),
                floorY = 0f,
                wallHeight = 2.4f
            };
        }
    }
}
