using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Editor-friendly scan source: emits a small fixed plan of rooms over a few seconds so the
    /// whole scan -> dollhouse pipeline can be exercised without a headset.
    /// </summary>
    public sealed class FakeScanSource : MonoBehaviour, IScanSource
    {
        public event System.Action<RoomScan> RoomScanned;
        public event System.Action ScanCompleted;

        [SerializeField] private float emitIntervalSeconds = 1.2f;
        [SerializeField] private float wallHeightMeters = 2.4f;

        private static readonly List<RoomScan> Plan = new List<RoomScan>
        {
            new RoomScan { roomId = "Room-001", boundsXZ = new Rect(-2.5f, -2.5f, 2.8f, 2.6f) },
            new RoomScan { roomId = "Room-002", boundsXZ = new Rect(0.3f, -2.4f, 2.2f, 1.8f) },
            new RoomScan { roomId = "Room-003", boundsXZ = new Rect(-2.2f, 0.2f, 2.0f, 2.0f) },
            new RoomScan { roomId = "Room-004", boundsXZ = new Rect(0.1f, -0.3f, 2.4f, 2.2f) },
        };

        public float Progress { get; private set; }
        public bool IsScanning { get; private set; }

        public Bounds? KnownBounds
        {
            get
            {
                if (Plan.Count == 0) return null;

                var bounds = Plan[0].ToBounds();
                for (int i = 1; i < Plan.Count; i++) bounds.Encapsulate(Plan[i].ToBounds());
                return bounds;
            }
        }

        public void Begin()
        {
            if (IsScanning) return;

            IsScanning = true;
            Progress = 0f;
            StartCoroutine(RunScan());
        }

        public void Stop()
        {
            if (!IsScanning) return;

            IsScanning = false;
            StopAllCoroutines();
        }

        private IEnumerator RunScan()
        {
            int emitted = 0;

            while (emitted < Plan.Count)
            {
                var room = Plan[emitted];
                room.wallHeight = wallHeightMeters;
                emitted++;
                Progress = emitted / (float)Plan.Count;

                RoomScanned?.Invoke(room);
                yield return new WaitForSeconds(emitIntervalSeconds);
            }

            Progress = 1f;
            IsScanning = false;
            ScanCompleted?.Invoke();
        }
    }
}
