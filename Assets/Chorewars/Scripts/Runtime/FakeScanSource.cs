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

        private int _emitted;

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

            StopAllCoroutines();
            Finish();
        }

        private IEnumerator RunScan()
        {
            while (_emitted < Plan.Count)
            {
                EmitNext();
                yield return new WaitForSeconds(emitIntervalSeconds);
            }

            Finish();
        }

        /// <summary>
        /// Ends the scan, emitting anything still unemitted first. Stopping early is a legitimate
        /// thing to ask of a source — the device scan offers a "finish" button — so a stop is a
        /// completion, not a cancellation. Guarded so the scan completes exactly once.
        /// </summary>
        private void Finish()
        {
            if (!IsScanning) return;

            while (_emitted < Plan.Count) EmitNext();

            IsScanning = false;
            Progress = 1f;
            ScanCompleted?.Invoke();
        }

        private void EmitNext()
        {
            var room = Plan[_emitted];
            _emitted++;
            room.wallHeight = wallHeightMeters;
            Progress = _emitted / (float)Plan.Count;

            RoomScanned?.Invoke(room);
        }
    }
}
