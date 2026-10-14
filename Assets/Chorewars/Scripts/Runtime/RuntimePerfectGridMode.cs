using UnityEngine;
using Chorewars.Core;
using Chorewars.Dollhouse;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Perfect Grid: hoover the room in tidy parallel stripes, like mowing a football pitch.
    /// Translucent guide stripes are laid across the floor; each one counts how much of itself you
    /// have actually cleaned, and a stripe that reaches the cleared threshold lights up green.
    /// </summary>
    /// <remarks>
    /// The stripe coverage is read back from the same <see cref="Chorewars.AR.CoverageMap"/> the rest
    /// of the game uses, rather than being tracked separately, so the grid can never disagree with
    /// the coverage ring or the summary. It is sampled on a throttle, not every frame, because each
    /// sample walks the cells inside a stripe rect.
    /// </remarks>
    public sealed class RuntimePerfectGridMode : RuntimeCoverageMode
    {
        private const float StripeWidthMeters = 0.5f;
        private const float ClearedThresholdPercent = 80f;
        private const float MaxGridBonus = 300f;
        private const float ResampleIntervalSeconds = 0.2f;

        private struct Stripe
        {
            public float CentreZ;
            public float CoveragePercent;
            public bool Cleared;
            public Renderer Body;
            public MaterialPropertyBlock Block;
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color IdleColour = new(0.35f, 0.75f, 1f, 0.22f);
        private static readonly Color ClearedColour = new(0.35f, 1f, 0.55f, 0.34f);

        private Stripe[] _stripes;
        private Transform _gridRoot;
        private Material _stripeMaterial;
        private float _alignmentPercent;
        private float _nextResampleAt;
        private bool _announcedPerfect;

        public override string ProfileModeId => "perfect-grid";

        public override string ModeDisplayName => "Perfect Grid";

        protected override void OnModeBegun()
        {
            base.OnModeBegun();
            BuildGrid();
            Resample();
        }

        protected override void OnModeEnded(ChoreResult result)
        {
            if (_gridRoot != null) Destroy(_gridRoot.gameObject);
            base.OnModeEnded(result);
        }

        protected override void OnModeTick(float elapsedSeconds, float coveragePercent)
        {
            if (_stripes == null) return;
            if (Time.time < _nextResampleAt) return;

            Resample();
        }

        private void BuildGrid()
        {
            var bounds = PlayBounds;
            float width = bounds.size.z;
            int count = Mathf.Max(1, Mathf.CeilToInt(width / StripeWidthMeters));

            _gridRoot = new GameObject("PerfectGrid").transform;
            _gridRoot.SetParent(transform, false);

            // One shared translucent material for every stripe; the per-stripe tint rides in a
            // MaterialPropertyBlock so the stripes still batch (same reasoning as the dollhouse).
            _stripeMaterial = PlaceholderMaterials.CreateTransparent(IdleColour, IdleColour.a);

            _stripes = new Stripe[count];
            for (int i = 0; i < count; i++)
            {
                float centreZ = bounds.min.z + (i + 0.5f) * (bounds.size.z / count);
                float stripeDepth = bounds.size.z / count;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"Stripe{i}";
                quad.transform.SetParent(_gridRoot, false);
                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.position = new Vector3(
                    bounds.center.x,
                    bounds.min.y + 0.012f,
                    centreZ);
                quad.transform.localScale = new Vector3(bounds.size.x, stripeDepth, 1f);

                var collider = quad.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = quad.GetComponent<Renderer>();
                renderer.sharedMaterial = _stripeMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                _stripes[i] = new Stripe
                {
                    CentreZ = centreZ,
                    Body = renderer,
                    Block = new MaterialPropertyBlock(),
                    CoveragePercent = 0f
                };
                ApplyTint(i);
            }

            Debug.Log($"[BoreDOOM] Perfect Grid laid out {count} stripes across {bounds.size.z:0.0} m.");
        }

        /// <summary>Reads each stripe's cleanliness straight back out of the coverage map.</summary>
        private void Resample()
        {
            _nextResampleAt = Time.time + ResampleIntervalSeconds;

            var bounds = PlayBounds;
            float stripeDepth = bounds.size.z / Mathf.Max(1, _stripes.Length);
            float total = 0f;
            bool allCleared = true;

            for (int i = 0; i < _stripes.Length; i++)
            {
                var rect = new Rect(
                    bounds.min.x,
                    _stripes[i].CentreZ - stripeDepth * 0.5f,
                    bounds.size.x,
                    stripeDepth);

                float pct = coverageMap != null ? coverageMap.ComputeCoveragePercent(rect) : 0f;
                bool cleared = pct >= ClearedThresholdPercent;

                if (cleared != _stripes[i].Cleared)
                {
                    _stripes[i].Cleared = cleared;
                    ApplyTint(i);
                }

                _stripes[i].CoveragePercent = pct;
                total += pct;
                if (!cleared) allCleared = false;
            }

            _alignmentPercent = _stripes.Length > 0 ? total / _stripes.Length : 0f;

            if (allCleared && !_announcedPerfect)
            {
                _announcedPerfect = true;
                Debug.Log("[BoreDOOM] PERFECT GRID — every stripe cleared.");
            }
        }

        private void ApplyTint(int index)
        {
            var stripe = _stripes[index];
            if (stripe.Body == null || stripe.Block == null) return;

            stripe.Block.SetColor(ColorId, stripe.Cleared ? ClearedColour : IdleColour);
            stripe.Body.SetPropertyBlock(stripe.Block);
        }

        /// <summary>
        /// Grid bonus, up to 300: the average cleanliness of the stripes. It is added on top of the
        /// usual coverage score and the room bonus, so a tidy grid is worth more than a messy sweep.
        /// </summary>
        protected override float GetBonusPoints()
        {
            return base.GetBonusPoints() + (_alignmentPercent / 100f) * MaxGridBonus;
        }
    }
}
