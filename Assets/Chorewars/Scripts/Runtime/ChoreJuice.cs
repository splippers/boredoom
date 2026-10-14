using UnityEngine;
using Chorewars.AR;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Presentation layer for the moment-to-moment "feel" of cleaning. Subscribes to
    /// <see cref="CoverageMap.CellFirstVisited"/> and, for every genuinely new cell:
    /// spawns a pooled scale-pop puff on the floor, advances a combo counter and plays a
    /// procedural tick whose pitch rises with the combo.
    /// </summary>
    /// <remarks>
    /// Everything is preallocated and pooled so the hot path (a few cells per frame) allocates
    /// nothing, which matters on Quest. Juice is decorative: if it is disabled or misconfigured,
    /// the core coverage loop is unaffected.
    /// </remarks>
    public sealed class ChoreJuice : MonoBehaviour
    {
        private const int MaxPuffs = 64;
        private const float PuffLifeSeconds = 0.38f;
        private const float PuffStartScale = 0.15f;
        private const float PuffEndScale = 0.55f;
        private const float ComboWindowSeconds = 0.55f;
        private const int MaxComboSemitones = 14;

        private static readonly Color PuffTint = new Color(0.45f, 1f, 0.72f, 1f);

        private CoverageMap _map;
        private PlaceholderHud _hud;
        private float _floorY;

        private SpriteRenderer[] _puffs;
        private float[] _puffBornAt;
        private bool[] _puffActive;
        private int _nextPuff;

        private AudioSource _audio;
        private AudioClip _tickClip;

        private int _combo;
        private float _lastCellAt = float.NegativeInfinity;

        /// <summary>Total puffs spawned. Handy for probing without reaching into internals.</summary>
        public int TotalPops { get; private set; }

        /// <summary>Current consecutive-new-cell streak (0 when the streak has lapsed).</summary>
        public int Combo => _combo;

        /// <summary>When false the controller ignores cells entirely (core loop keeps running).
        /// Also gated by the global <see cref="JuiceSettings.Enabled"/> switch.</summary>
        public bool Enabled { get; set; } = true;

        public void Configure(CoverageMap map, PlaceholderHud hud, float floorY)
        {
            if (_map != null) _map.CellFirstVisited -= OnCellFirstVisited;

            _map = map;
            _hud = hud;
            _floorY = floorY;

            // Subscribe here rather than in OnEnable: AddComponent runs Awake/OnEnable before the
            // caller has had a chance to Configure us, so the map is still null at that point.
            if (_map != null) _map.CellFirstVisited += OnCellFirstVisited;
        }

        private void OnDisable()
        {
            if (_map != null) _map.CellFirstVisited -= OnCellFirstVisited;
        }

        private void Awake()
        {
            BuildPuffPool();
            BuildAudio();
        }

        private void OnCellFirstVisited(Vector3 worldCentre)
        {
            if (!Enabled || !JuiceSettings.Enabled) return;

            var now = Time.unscaledTime;
            _combo = now - _lastCellAt <= ComboWindowSeconds ? _combo + 1 : 1;
            _lastCellAt = now;

            SpawnPuff(worldCentre, now);
            PlayTick();

            if (_hud != null)
            {
                _hud.SetCombo(_combo);
                _hud.PunchCoverage();
            }
        }

        private void Update()
        {
            AnimatePuffs();

            // The streak lapses on its own so the counter drops back to nothing when the player
            // stops making new progress (rather than sitting at a stale high number).
            if (_combo > 0 && Time.unscaledTime - _lastCellAt > ComboWindowSeconds)
            {
                _combo = 0;
                if (_hud != null) _hud.SetCombo(0);
            }
        }

        // ---- Puffs ------------------------------------------------------------------------

        private void BuildPuffPool()
        {
            _puffs = new SpriteRenderer[MaxPuffs];
            _puffBornAt = new float[MaxPuffs];
            _puffActive = new bool[MaxPuffs];

            var sprite = MakeRadialSprite(32);
            var container = new GameObject("Puffs");
            container.transform.SetParent(transform, false);

            for (int i = 0; i < MaxPuffs; i++)
            {
                var go = new GameObject($"Puff{i:00}");
                go.transform.SetParent(container.transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = PuffTint;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;

                go.SetActive(false);
                _puffs[i] = sr;
            }
        }

        private void SpawnPuff(Vector3 worldCentre, float now)
        {
            int index = _nextPuff;
            _nextPuff = (_nextPuff + 1) % MaxPuffs;

            var sr = _puffs[index];
            if (sr == null) return;

            var colour = PuffTint;
            colour.a = 1f;

            sr.transform.position = new Vector3(worldCentre.x, _floorY + 0.02f, worldCentre.z);
            sr.transform.localScale = Vector3.one * PuffStartScale;
            sr.color = colour;
            sr.gameObject.SetActive(true);

            _puffBornAt[index] = now;
            _puffActive[index] = true;
            TotalPops++;
        }

        private void AnimatePuffs()
        {
            if (_puffs == null) return;

            float now = Time.unscaledTime;

            for (int i = 0; i < _puffs.Length; i++)
            {
                if (!_puffActive[i]) continue;

                float t = (now - _puffBornAt[i]) / PuffLifeSeconds;
                if (t >= 1f)
                {
                    _puffActive[i] = false;
                    if (_puffs[i] != null) _puffs[i].gameObject.SetActive(false);
                    continue;
                }

                if (t < 0f) t = 0f;

                var sr = _puffs[i];
                if (sr == null) continue;

                // Ease-out growth + fade; a small rise keeps the puff from looking pasted flat.
                float ease = 1f - (1f - t) * (1f - t);
                float scale = Mathf.Lerp(PuffStartScale, PuffEndScale, ease);

                var colour = PuffTint;
                colour.a = Mathf.Pow(1f - t, 1.6f) * 0.75f;

                sr.transform.localScale = Vector3.one * scale;
                sr.transform.position += Vector3.up * (Time.deltaTime * 0.10f);
                sr.color = colour;
            }
        }

        internal static Sprite MakeRadialSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "JuicePuff",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - radius) / radius;
                    float dy = (y + 0.5f - radius) / radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);

            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---- Audio ------------------------------------------------------------------------

        private void BuildAudio()
        {
            try
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f; // 2D: the player is always "in" the sound
                _audio.volume = 0.5f;
                _tickClip = MakeTickClip();
            }
            catch (System.Exception e)
            {
                // Headless/batch editors have no audio device; juice must never break the loop.
                Debug.LogWarning($"BoreDoom/ChoreJuice: audio unavailable — {e.Message}");
                _audio = null;
                _tickClip = null;
            }
        }

        private void PlayTick()
        {
            if (_audio == null || _tickClip == null) return;

            // One semitone per combo step: reads as a rising arpeggio while the streak holds.
            int steps = Mathf.Clamp(_combo - 1, 0, MaxComboSemitones);
            _audio.pitch = Mathf.Pow(1.0594631f, steps);
            _audio.PlayOneShot(_tickClip, 1f);
        }

        /// <summary>A short descending blip — closer to a satisfying "tick" than a flat sine.</summary>
        private static AudioClip MakeTickClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.09f;
            const float startHz = 1300f;
            const float endHz = 620f;

            int samples = Mathf.Max(1, (int)(sampleRate * duration));
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float frequency = Mathf.Lerp(startHz, endHz, t);
                float envelope = Mathf.Exp(-9f * t);

                // Phase is integrated because the frequency sweeps over time.
                float phase = 2f * Mathf.PI * frequency * (i / (float)sampleRate);
                data[i] = Mathf.Sin(phase) * envelope * 0.32f;
            }

            var clip = AudioClip.Create("JuiceTick", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
