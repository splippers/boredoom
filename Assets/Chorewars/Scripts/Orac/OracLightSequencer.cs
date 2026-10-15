using UnityEngine;

namespace Chorewars.Orac
{
    /// <summary>
    /// The internal light matrix. Orac's lights flash in a regular, non-random chase -
    /// never a glittery twinkle. Rate and colour are the mood made visible.
    /// </summary>
    public class OracLightSequencer : MonoBehaviour
    {
        static readonly Color DormantColor = new Color(0.02f, 0.03f, 0.03f, 1f);
        static readonly Color Cool = new Color(0.15f, 0.95f, 0.75f, 1f);
        static readonly Color Hot = new Color(1f, 0.55f, 0.15f, 1f);
        static readonly Color Sulky = new Color(0.55f, 0.10f, 0.10f, 1f);

        Renderer[] _lights;
        MaterialPropertyBlock _block;
        OracMood _mood = OracMood.Dormant;

        float _chase;
        float _pulse;

        public void Bind(Renderer[] lights)
        {
            _lights = lights;
            if (_block == null) _block = new MaterialPropertyBlock();
        }

        public void SetMood(OracMood mood) { _mood = mood; }

        /// <summary>A speech-synced flicker, fired on each utterance.</summary>
        public void Pulse() { _pulse = 1f; }

        void Update()
        {
            if (_lights == null || _lights.Length == 0 || _block == null) return;

            float rate = RateFor(_mood);
            _chase += Time.deltaTime * rate;
            _pulse = Mathf.Max(0f, _pulse - Time.deltaTime * 2.2f);

            int n = _lights.Length;
            int head = Mathf.FloorToInt(_chase);
            float frac = _chase - head;
            int idx = ((head % n) + n) % n;
            int prev = ((idx - 1) % n + n) % n;

            Color baseColor = ColorFor(_mood);
            float glow = GlowFor(_mood);

            for (int i = 0; i < n; i++)
            {
                float v = glow;
                if (i == idx) v += 1f;
                else if (i == prev) v += (1f - frac);
                v = Mathf.Clamp01(v + _pulse * 0.5f);

                _block.SetColor("_Color", baseColor * v);
                _lights[i].SetPropertyBlock(_block);
            }
        }

        static float RateFor(OracMood mood)
        {
            switch (mood)
            {
                case OracMood.Dormant: return 0f;
                case OracMood.Idle: return 1.2f;
                case OracMood.Attentive: return 2.2f;
                case OracMood.Processing: return 9f;
                case OracMood.Answering: return 4f;
                case OracMood.Indignant: return 12f;
                case OracMood.Sulking: return 0.4f;
                default: return 1f;
            }
        }

        static float GlowFor(OracMood mood)
        {
            switch (mood)
            {
                case OracMood.Dormant: return 0f;
                case OracMood.Idle: return 0.30f;
                case OracMood.Attentive: return 0.45f;
                case OracMood.Processing: return 0.35f;
                case OracMood.Answering: return 0.55f;
                case OracMood.Indignant: return 0.70f;
                case OracMood.Sulking: return 0.18f;
                default: return 0.3f;
            }
        }

        static Color ColorFor(OracMood mood)
        {
            switch (mood)
            {
                case OracMood.Dormant: return DormantColor;
                case OracMood.Indignant: return Hot;
                case OracMood.Sulking: return Sulky;
                default: return Cool;
            }
        }
    }
}
