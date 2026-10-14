using System;
using System.Collections;
using System.Collections.Generic;
using Chorewars.Dollhouse;
using UnityEngine;
using UnityEngine.XR;

namespace Chorewars.Runtime
{
    /// <summary>
    /// The "you finished a room" beat. Listens to <see cref="DollhouseManager.RoomCleaned"/> and, for
    /// each room that crosses the clean threshold, fires a glow pulse on the miniature, a confetti
    /// burst, a floating "+points" popup, a haptic tick and an optional beat of slow motion.
    /// </summary>
    /// <remarks>
    /// All effects are pooled and preallocated. The bonus points shown here are the same ones the
    /// mode adds to the final score, so the popup never contradicts the summary screen.
    /// Everything is guarded: no XR device, no haptics; disabled, no celebration at all.
    /// </remarks>
    public sealed class RoomCleanCelebration : MonoBehaviour
    {
        private const int ConfettiCount = 24;
        private const int MaxConfetti = 64;
        private const float ConfettiLife = 0.9f;
        private const int MaxAuras = 4;
        private const int MaxPopups = 4;

        private static readonly Color[] ConfettiPalette =
        {
            new Color(1f, 0.85f, 0.25f),
            new Color(0.35f, 1f, 0.60f),
            new Color(0.35f, 0.85f, 1f),
            new Color(1f, 1f, 1f),
            new Color(1f, 0.55f, 0.20f),
        };

        private DollhouseManager _dollhouse;
        private Camera _camera;

        private Sprite _softSprite;
        private Sprite _squareSprite;

        private SpriteRenderer[] _auras;
        private float[] _auraBornAt;
        private float[] _auraSize;
        private bool[] _auraActive;
        private Vector3[] _auraCentre;
        private int _nextAura;

        private Transform[] _confetti;
        private SpriteRenderer[] _confettiRenderer;
        private Vector3[] _confettiVelocity;
        private float[] _confettiBornAt;
        private float[] _confettiSpin;
        private bool[] _confettiActive;
        private int _nextConfetti;

        private TextMesh[] _popups;
        private Vector3[] _popupOrigin;
        private float[] _popupBornAt;
        private bool[] _popupActive;
        private int _nextPopup;

        private readonly List<InputDevice> _hapticDevices = new();
        private bool _slowMoRunning;

        /// <summary>Points awarded (and shown) per cleaned room.</summary>
        public int PointsPerRoom { get; set; } = 150;

        /// <summary>Rooms that have reached the clean threshold so far.</summary>
        public int RoomsCleaned { get; private set; }

        /// <summary>Bonus added to the final score, matching what the popups displayed.</summary>
        public int BonusPoints => RoomsCleaned * PointsPerRoom;

        /// <summary>Total confetti pieces spawned so far (diagnostics).</summary>
        public int TotalConfetti { get; private set; }

        /// <summary>Master switch; when false rooms clean silently.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Brief slow motion on a room clean. Off disables any time-scale meddling.</summary>
        public bool SlowMotionEnabled { get; set; } = true;

        public void Configure(DollhouseManager dollhouse, Camera camera)
        {
            if (_dollhouse != null) _dollhouse.RoomCleaned -= OnRoomCleaned;

            _dollhouse = dollhouse;
            _camera = camera;

            if (_dollhouse != null) _dollhouse.RoomCleaned += OnRoomCleaned;
        }

        private void Awake()
        {
            _softSprite = ChoreJuice.MakeRadialSprite(32);
            _squareSprite = MakeSquareSprite(8);

            BuildAuras();
            BuildConfetti();
            BuildPopups();
        }

        private void OnDisable()
        {
            if (_dollhouse != null) _dollhouse.RoomCleaned -= OnRoomCleaned;

            // Never leave the game slowed down if we are torn down mid-celebration.
            if (_slowMoRunning)
            {
                Time.timeScale = 1f;
                _slowMoRunning = false;
            }
        }

        private void OnRoomCleaned(string roomId, Transform roomRoot)
        {
            if (!Enabled) return;

            RoomsCleaned++;

            var bounds = MeasureRoom(roomRoot);
            var centre = bounds.center;

            SpawnAura(centre, bounds.size);
            SpawnConfetti(centre);
            SpawnPopup(centre);
            PulseHaptics();

            if (SlowMotionEnabled && !_slowMoRunning)
                StartCoroutine(SlowMo(0.45f, 0.22f));

            Debug.Log($"[RoomCleanCelebration] {roomId} clean — +{PointsPerRoom} (x{RoomsCleaned})");
        }

        private static Bounds MeasureRoom(Transform roomRoot)
        {
            if (roomRoot == null) return new Bounds(Vector3.zero, Vector3.one * 0.1f);

            var renderers = roomRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(roomRoot.position, Vector3.one * 0.1f);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private void Update()
        {
            AnimateAuras();
            AnimateConfetti();
            AnimatePopups();
        }

        // ---- Glow pulse -------------------------------------------------------------------

        private void BuildAuras()
        {
            _auras = new SpriteRenderer[MaxAuras];
            _auraBornAt = new float[MaxAuras];
            _auraSize = new float[MaxAuras];
            _auraActive = new bool[MaxAuras];
            _auraCentre = new Vector3[MaxAuras];

            var container = new GameObject("Auras");
            container.transform.SetParent(transform, false);

            for (int i = 0; i < MaxAuras; i++)
            {
                var go = new GameObject($"Aura{i}");
                go.transform.SetParent(container.transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _softSprite;
                sr.color = new Color(0.45f, 1f, 0.7f, 0f);
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;

                go.SetActive(false);
                _auras[i] = sr;
            }
        }

        private void SpawnAura(Vector3 centre, Vector3 size)
        {
            int index = _nextAura;
            _nextAura = (_nextAura + 1) % MaxAuras;

            var sr = _auras[index];
            if (sr == null) return;

            float extent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

            _auraCentre[index] = centre;
            _auraSize[index] = Mathf.Max(0.05f, extent);
            _auraBornAt[index] = Time.unscaledTime;
            _auraActive[index] = true;

            sr.transform.position = centre;
            sr.gameObject.SetActive(true);
        }

        private void AnimateAuras()
        {
            if (_auras == null) return;

            float now = Time.unscaledTime;

            for (int i = 0; i < _auras.Length; i++)
            {
                if (!_auraActive[i]) continue;

                float t = (now - _auraBornAt[i]) / 0.7f;
                if (t >= 1f)
                {
                    _auraActive[i] = false;
                    if (_auras[i] != null) _auras[i].gameObject.SetActive(false);
                    continue;
                }

                if (t < 0f) t = 0f;

                var sr = _auras[i];
                if (sr == null) continue;

                // Grow past the room and fade back out; a sine hump reads as a bloom, not a wipe.
                float size = Mathf.Lerp(_auraSize[i] * 0.6f, _auraSize[i] * 1.9f, t);
                float alpha = Mathf.Sin(t * Mathf.PI) * 0.45f;

                // Nudge toward the camera so the glow is not swallowed by the room geometry.
                var toCamera = _camera != null ? (_camera.transform.position - _auraCentre[i]).normalized : Vector3.zero;

                sr.transform.position = _auraCentre[i] + toCamera * 0.02f;
                sr.transform.localScale = Vector3.one * size;
                sr.color = new Color(0.45f, 1f, 0.7f, alpha);
            }
        }

        // ---- Confetti ---------------------------------------------------------------------

        private void BuildConfetti()
        {
            _confetti = new Transform[MaxConfetti];
            _confettiRenderer = new SpriteRenderer[MaxConfetti];
            _confettiVelocity = new Vector3[MaxConfetti];
            _confettiBornAt = new float[MaxConfetti];
            _confettiSpin = new float[MaxConfetti];
            _confettiActive = new bool[MaxConfetti];

            var container = new GameObject("Confetti");
            container.transform.SetParent(transform, false);

            for (int i = 0; i < MaxConfetti; i++)
            {
                var go = new GameObject($"Bit{i:00}");
                go.transform.SetParent(container.transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _squareSprite;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;

                go.SetActive(false);
                _confetti[i] = go.transform;
                _confettiRenderer[i] = sr;
            }
        }

        private void SpawnConfetti(Vector3 centre)
        {
            if (_confetti == null) return;

            float now = Time.unscaledTime;

            for (int n = 0; n < ConfettiCount; n++)
            {
                int index = _nextConfetti;
                _nextConfetti = (_nextConfetti + 1) % MaxConfetti;

                var t = _confetti[index];
                if (t == null) continue;

                // Miniature scale: the whole house is ~0.13 m, so pieces travel a few centimetres.
                var direction = UnityEngine.Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) + 0.6f;

                _confettiVelocity[index] = direction.normalized * UnityEngine.Random.Range(0.25f, 0.55f);
                _confettiBornAt[index] = now;
                _confettiSpin[index] = UnityEngine.Random.Range(-540f, 540f);
                _confettiActive[index] = true;

                t.position = centre + UnityEngine.Random.insideUnitSphere * 0.02f;
                t.localScale = Vector3.one * UnityEngine.Random.Range(0.008f, 0.016f);
                t.rotation = UnityEngine.Random.rotation;

                if (_confettiRenderer[index] != null)
                    _confettiRenderer[index].color = ConfettiPalette[n % ConfettiPalette.Length];

                t.gameObject.SetActive(true);
                TotalConfetti++;
            }
        }

        private void AnimateConfetti()
        {
            if (_confetti == null) return;

            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < _confetti.Length; i++)
            {
                if (!_confettiActive[i]) continue;

                float t = (now - _confettiBornAt[i]) / ConfettiLife;
                if (t >= 1f)
                {
                    _confettiActive[i] = false;
                    if (_confetti[i] != null) _confetti[i].gameObject.SetActive(false);
                    continue;
                }

                if (t < 0f) t = 0f;

                var tr = _confetti[i];
                if (tr == null) continue;

                _confettiVelocity[i] += Vector3.down * (1.2f * dt);
                tr.position += _confettiVelocity[i] * dt;
                tr.Rotate(0f, 0f, _confettiSpin[i] * dt, Space.Self);

                var sr = _confettiRenderer[i];
                if (sr != null)
                {
                    var c = sr.color;
                    c.a = Mathf.Clamp01((1f - t) * 2.5f);
                    sr.color = c;
                }
            }
        }

        // ---- Floating "+points" -----------------------------------------------------------

        private void BuildPopups()
        {
            _popups = new TextMesh[MaxPopups];
            _popupOrigin = new Vector3[MaxPopups];
            _popupBornAt = new float[MaxPopups];
            _popupActive = new bool[MaxPopups];

            var container = new GameObject("Popups");
            container.transform.SetParent(transform, false);

            for (int i = 0; i < MaxPopups; i++)
            {
                var go = new GameObject($"Popup{i}");
                go.transform.SetParent(container.transform, false);

                var text = go.AddComponent<TextMesh>();
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.font = font;
                text.fontSize = 64;
                text.characterSize = 0.012f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = new Color(1f, 0.88f, 0.35f);

                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null && font != null) mr.sharedMaterial = font.material;

                go.SetActive(false);
                _popups[i] = text;
            }
        }

        private void SpawnPopup(Vector3 centre)
        {
            int index = _nextPopup;
            _nextPopup = (_nextPopup + 1) % MaxPopups;

            var text = _popups[index];
            if (text == null) return;

            text.text = $"+{PointsPerRoom}";
            text.color = new Color(1f, 0.88f, 0.35f, 1f);

            _popupOrigin[index] = centre + Vector3.up * 0.05f;
            _popupBornAt[index] = Time.unscaledTime;
            _popupActive[index] = true;

            text.transform.position = _popupOrigin[index];
            text.gameObject.SetActive(true);
        }

        private void AnimatePopups()
        {
            if (_popups == null) return;

            float now = Time.unscaledTime;

            for (int i = 0; i < _popups.Length; i++)
            {
                if (!_popupActive[i]) continue;

                float t = (now - _popupBornAt[i]) / 1.1f;
                if (t >= 1f)
                {
                    _popupActive[i] = false;
                    if (_popups[i] != null) _popups[i].gameObject.SetActive(false);
                    continue;
                }

                if (t < 0f) t = 0f;

                var text = _popups[i];
                if (text == null) continue;

                text.transform.position = _popupOrigin[i] + Vector3.up * (0.06f * t);

                if (_camera != null)
                    text.transform.rotation = Quaternion.LookRotation(
                        text.transform.position - _camera.transform.position, Vector3.up);

                var c = text.color;
                c.a = Mathf.Clamp01(1f - Mathf.Pow(t, 3f));
                text.color = c;
            }
        }

        private static Sprite MakeSquareSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "JuiceConfetti",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);

            tex.SetPixels32(pixels);
            tex.Apply(false);

            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---- Feel -------------------------------------------------------------------------

        private IEnumerator SlowMo(float scale, float duration)
        {
            _slowMoRunning = true;

            float previous = Time.timeScale;
            Time.timeScale = Mathf.Clamp(scale, 0.05f, 1f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Time.timeScale = previous <= 0f ? 1f : previous;
            _slowMoRunning = false;
        }

        /// <summary>Short haptic tick on any held controllers. No-op without XR hardware.</summary>
        private void PulseHaptics()
        {
            try
            {
                _hapticDevices.Clear();
                InputDevices.GetDevicesWithCharacteristics(
                    InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand,
                    _hapticDevices);

                for (int i = 0; i < _hapticDevices.Count; i++)
                {
                    var device = _hapticDevices[i];
                    if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
                        device.SendHapticImpulse(0u, 0.6f, 0.15f);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"BoreDoom/RoomCleanCelebration: haptics unavailable — {e.Message}");
            }
        }
    }
}
