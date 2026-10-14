using UnityEngine;
using Chorewars.AR;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Device scan source. Today this starts/stops the existing <see cref="SpatialMeshTracker"/>
    /// and reports progress, but does not yet extract rooms (that is the MRUK/OVRScene or
    /// mesh-clustering milestone). It deliberately has no Quest-only compile dependencies so the
    /// project still builds and runs in the editor.
    /// </summary>
    public sealed class QuestScanSource : MonoBehaviour, IScanSource
    {
        public event System.Action<RoomScan> RoomScanned;
        public event System.Action ScanCompleted;

        [SerializeField] private SpatialMeshTracker spatialMeshTracker;

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

            ScanCompleted?.Invoke();
        }

        private void Update()
        {
            if (!IsScanning) return;

            // Room extraction is not wired yet; keep progress moving so UI can show activity.
            Progress = Mathf.Clamp01(Progress + Time.deltaTime * 0.1f);
        }
    }
}
