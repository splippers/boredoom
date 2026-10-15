using UnityEngine;
using Chorewars.AR;

namespace Chorewars.HandsFree
{
    /// <summary>
    /// The first layer of hands-free: the loop needs no input at all.
    ///
    /// The player is holding a hoover, so nobody is going to press "finish scan". This
    /// watches the room and ends the scan when the room stops growing - and it is the
    /// weakest source in HandsFreeInput, so a player who says otherwise always wins.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public class AutoScanDirector : MonoBehaviour
    {
        public bool Enabled = true;

        /// <summary>How long the room must stop changing before the scan is called done.</summary>
        public float SettleSeconds = 4f;

        /// <summary>Never call it done during the first few seconds of a scan.</summary>
        public float MinScanSeconds = 6f;

        /// <summary>A hard stop, so a scan left running while the player goes for tea ends.</summary>
        public float MaxScanSeconds = 300f;

        /// <summary>Change in coverage (percent, plus a little per mesh) that counts as growth.</summary>
        public float GrowthEpsilon = 0.25f;

        public HandsFreeInput Input;

        HouseScanSessionController _scan;
        SpatialMeshTracker _mesh;

        float _stableFor;
        float _lastCoverage = -1f;
        int _lastMeshes = -1;

        public string LastReason { get; private set; }

        void Awake()
        {
            if (_scan == null) _scan = FindAnyObjectByType<HouseScanSessionController>();
            if (_mesh == null) _mesh = FindAnyObjectByType<SpatialMeshTracker>();
        }

        void Update()
        {
            if (!Enabled || _scan == null) return;

            if (!_scan.IsScanning)
            {
                _stableFor = 0f;
                _lastCoverage = -1f;
                _lastMeshes = -1;
                return;
            }

            float coverage = _scan.CoveragePercent;
            int meshes = _mesh != null ? _mesh.MeshCount : 0;

            float grew = Mathf.Abs(coverage - _lastCoverage) + Mathf.Abs(meshes - _lastMeshes) * 0.01f;
            _lastCoverage = coverage;
            _lastMeshes = meshes;

            if (grew > GrowthEpsilon)
            {
                _stableFor = 0f;
                return;
            }

            _stableFor += Time.unscaledDeltaTime;

            bool settled = _stableFor >= SettleSeconds && _scan.ElapsedSeconds >= MinScanSeconds;
            bool timedOut = _scan.ElapsedSeconds >= MaxScanSeconds;
            if (!settled && !timedOut) return;

            LastReason = settled ? "the room stopped growing" : "the scan ran long";
            _stableFor = 0f;
            _scan.StopScan();

            if (Input != null)
            {
                Input.Submit(new IntentRequest
                {
                    Intent = ChorewarsIntent.FinishScan,
                    Source = IntentSource.Auto,
                    Argument = LastReason,
                    Confidence = settled ? 0.8f : 0.5f
                });
            }
        }
    }
}
