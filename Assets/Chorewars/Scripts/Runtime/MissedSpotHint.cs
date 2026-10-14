using UnityEngine;
using Chorewars.AR;

namespace Chorewars.Runtime
{
    /// <summary>
    /// "You missed a bit" — a faint pulsing marker parked over the largest still-uncleaned patch of
    /// floor. Queries <see cref="CoverageMap"/> on a slow interval (never per frame) so the search
    /// cost stays off the hot path.
    /// </summary>
    /// <remarks>
    /// Deliberately restrained: it only appears once the player has made real progress, only when
    /// there is a cluster big enough to be worth mentioning, and it fades out as the player closes
    /// in. Everything is one pooled marker plus one label.
    /// </remarks>
    public sealed class MissedSpotHint : MonoBehaviour
    {
        private const float FadeSpeed = 2.5f;
        private const float MaxMarkerDiameter = 1.4f;
        private const float MinMarkerDiameter = 0.35f;

        private static readonly Color HintTint = new Color(1f, 0.86f, 0.42f);

        private CoverageMap _map;
        private Camera _camera;
        private float _floorY;

        private SpriteRenderer _marker;
        private TextMesh _label;

        private float _nextQueryAt;
        private float _startedAt = -1f;
        private float _visible;      // 0..1 fade
        private float _markerDiameter = MinMarkerDiameter;
        private bool _hasTarget;
        private Vector3 _target;

        /// <summary>Query cadence. The flood fill is cheap but there is no reason to run it per frame.</summary>
        public float QueryIntervalSeconds { get; set; } = 1.5f;

        /// <summary>Minimum cluster size worth pointing at, in coverage cells.</summary>
        public int MinClusterCells { get; set; } = 8;

        /// <summary>Coverage (%) before hints are allowed to appear, so it never nags at the start.</summary>
        public float MinimumCoveragePercent { get; set; } = 15f;

        /// <summary>Master switch for this hint, combined with the global
        /// <see cref="JuiceSettings.Enabled"/> switch.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>How many times a cluster has been surfaced (diagnostics).</summary>
        public int HintCount { get; private set; }

        /// <summary>Whether the marker is currently fading in / on screen.</summary>
        public bool IsShowing => _visible > 0.01f;

        /// <summary>Current world position the marker is parked over (diagnostics).</summary>
        public Vector3 CurrentTarget => _target;

        public void Configure(CoverageMap map, Camera camera, float floorY)
        {
            _map = map;
            _camera = camera;
            _floorY = floorY;
        }

        private void Awake()
        {
            var container = new GameObject("MissedSpot");
            container.transform.SetParent(transform, false);

            var markerGo = new GameObject("Marker");
            markerGo.transform.SetParent(container.transform, false);

            _marker = markerGo.AddComponent<SpriteRenderer>();
            _marker.sprite = PlaceholderHud.RingSprite();
            _marker.color = new Color(HintTint.r, HintTint.g, HintTint.b, 0f);
            _marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _marker.receiveShadows = false;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(container.transform, false);

            _label = labelGo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _label.font = font;
            _label.fontSize = 64;
            _label.characterSize = 0.01f;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = new Color(HintTint.r, HintTint.g, HintTint.b, 0f);
            _label.text = "you missed a bit";

            var labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (labelRenderer != null && font != null) labelRenderer.sharedMaterial = font.material;
        }

        private void Update()
        {
            if (_map == null) return;

            float now = Time.unscaledTime;
            if (_startedAt < 0f) _startedAt = now;

            bool wants = false;

            if (Enabled && now >= _nextQueryAt && now - _startedAt > 3f)
            {
                _nextQueryAt = now + Mathf.Max(0.25f, QueryIntervalSeconds);

                float coverage = _map.ComputeCoveragePercent();

                // Once the floor is essentially done there is nothing to point at.
                if (coverage >= MinimumCoveragePercent && coverage < 99f &&
                    _map.TryFindLargestUnvisitedCluster(out var centre, out var cells, MinClusterCells))
                {
                    _target = new Vector3(centre.x, _floorY + 0.015f, centre.z);
                    _hasTarget = true;
                    wants = true;

                    // Roughly size the marker to the patch it is describing.
                    _markerDiameter = Mathf.Clamp(
                        _map.CellSizeMeters * Mathf.Sqrt(cells) * 1.6f,
                        MinMarkerDiameter,
                        MaxMarkerDiameter);

                    HintCount++;
                }
                else
                {
                    _hasTarget = false;
                }
            }

            wants = _hasTarget && Enabled && JuiceSettings.Enabled;

            _visible = Mathf.MoveTowards(_visible, wants ? 1f : 0f, Time.unscaledDeltaTime * FadeSpeed);

            if (_visible <= 0.001f)
            {
                if (_marker != null) _marker.enabled = false;
                if (_label != null) _label.gameObject.SetActive(false);
                return;
            }

            if (_marker != null)
            {
                _marker.enabled = true;
                _marker.transform.position = _target;

                // Slow breathe so it reads as a nudge rather than a flashing warning. Scale is
                // always rebuilt from the stored diameter so it cannot compound frame to frame.
                float breathe = 1f + Mathf.Sin(now * 3.2f) * 0.06f;
                _marker.transform.localScale = Vector3.one * (_markerDiameter * breathe);
                _marker.color = new Color(
                    HintTint.r, HintTint.g, HintTint.b,
                    _visible * (0.18f + Mathf.Sin(now * 3.2f) * 0.07f));
            }

            if (_label != null)
            {
                _label.gameObject.SetActive(true);
                _label.transform.position = _target + Vector3.up * 0.10f;

                if (_camera != null)
                    _label.transform.rotation = Quaternion.LookRotation(
                        _label.transform.position - _camera.transform.position, Vector3.up);

                _label.color = new Color(HintTint.r, HintTint.g, HintTint.b, _visible * 0.55f);
            }
        }
    }
}
