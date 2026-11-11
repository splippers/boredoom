using System.Collections;
using Chorewars.Core;
using Chorewars.Diagnostics;
using UnityEngine;

namespace Chorewars.UI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class IntroController : MonoBehaviour
    {
        [SerializeField] private Texture2D _logo;
        [SerializeField] private AudioClip _introMusic;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.25f;
        [SerializeField] private float _fadeSeconds = 0.8f;
        private AudioSource _audio;
        private Camera _camera;
        private RectTransform _canvas;
        private UnityEngine.UI.Button _continue;
        private UnityEngine.UI.Text _muteText;
        private readonly UnityEngine.UI.Button[] _gloveSwatches = new UnityEngine.UI.Button[4];
        private bool _transitioning;
        private bool _paused;
        private bool _playbackStarted;
        private readonly Chorewars.HandsFree.GazePanelPlacement _placement =
            new Chorewars.HandsFree.GazePanelPlacement { Anchored = true };

        private void RefreshGloveSwatches()
        {
            for (int i = 0; i < _gloveSwatches.Length; i++)
            {
                if (_gloveSwatches[i] == null) continue;
                bool chosen = Chorewars.Dollhouse.GloveStyle.All[i] == Chorewars.Dollhouse.GloveStyle.Current;
                _gloveSwatches[i].transform.localScale = Vector3.one * (chosen ? 1.18f : 1f);
                var text = _gloveSwatches[i].GetComponentInChildren<UnityEngine.UI.Text>();
                text.fontStyle = chosen ? FontStyle.Bold : FontStyle.Normal;
            }
        }
        private void OnDestroy() => Chorewars.Dollhouse.GloveStyle.Changed -= RefreshGloveSwatches;

        public bool IsMuted => _audio != null && _audio.mute;
        public bool IsTransitioning => _transitioning;

        private void Awake()
        {
            _audio = GetComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.loop = true;
            _audio.volume = _volume;
            _audio.clip = _introMusic;
        }

        private IEnumerator Start()
        {
            while ((_camera = Camera.main) == null) yield return null;
            _canvas = CleanMenu.CreateCanvas(transform, _camera, new Vector2(640f, 940f));
            CleanMenu.Background(_canvas);
            if (_logo != null)
            {
                var graphic = new GameObject("BoreDOOM Logo", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                graphic.transform.SetParent(_canvas, false);
                float ratio = (float)_logo.width / Mathf.Max(1, _logo.height);
                var size = ratio >= 1f ? new Vector2(520f, 520f / ratio) : new Vector2(520f * ratio, 520f);
                CleanMenu.Place(graphic.GetComponent<RectTransform>(), new Vector2(0f, 135f), size);
                var image = graphic.GetComponent<UnityEngine.UI.RawImage>();
                image.texture = _logo;
                image.raycastTarget = false;
            }
            CleanMenu.Text(_canvas, "Welcome", "Welcome to BoreDOOM!", new Vector2(0f, -170f), new Vector2(580f, 54f), 30);
            CleanMenu.Text(_canvas, "GloveLabel", "Pick your glove colour", new Vector2(0f, -232f), new Vector2(580f, 40f), 24);
            for (int i = 0; i < Chorewars.Dollhouse.GloveStyle.All.Length; i++)
            {
                var colour = Chorewars.Dollhouse.GloveStyle.All[i];
                var swatch = CleanMenu.Button(_canvas, "Glove" + colour, Chorewars.Dollhouse.GloveStyle.Label(colour), new Vector2(-225f + 150f * i, -298f), new Vector2(140f, 64f), () => Chorewars.Dollhouse.GloveStyle.Set(colour));
                var tint = Chorewars.Dollhouse.GloveStyle.Tint(colour);
                swatch.targetGraphic.color = tint;
                var label = swatch.GetComponentInChildren<UnityEngine.UI.Text>();
                label.fontSize = 20;
                label.color = colour == Chorewars.Dollhouse.GloveColour.Black ? Color.white : new Color(0.1f, 0.1f, 0.12f);
                _gloveSwatches[i] = swatch;
            }
            Chorewars.Dollhouse.GloveStyle.Changed += RefreshGloveSwatches;
            RefreshGloveSwatches();
            _continue = CleanMenu.Button(_canvas, "Continue", "CONTINUE", new Vector2(-150f, -400f), new Vector2(280f, 76f), Continue);
            var mute = CleanMenu.Button(_canvas, "Mute", "MUTE", new Vector2(150f, -400f), new Vector2(280f, 76f), ToggleMute);
            _muteText = mute.GetComponentInChildren<UnityEngine.UI.Text>();
            StartCoroutine(PlayIntroMusic());
            TelemetryLogger.Info("CleanIntro", "Intro ready: selected logo/music; no Claudia or custom scan flow.");
        }

        private IEnumerator PlayIntroMusic()
        {
            if (_introMusic == null) { TelemetryLogger.Warn("CleanIntro", "Intro music reference missing."); yield break; }
            _introMusic.LoadAudioData();
            float waiting = 0f;
            while (_introMusic.loadState != AudioDataLoadState.Loaded || _paused)
            {
                if (_transitioning) yield break;
                if (_introMusic.loadState == AudioDataLoadState.Failed || waiting > 5f)
                {
                    TelemetryLogger.Warn("CleanIntro", "Intro audio failed to load: " + _introMusic.loadState);
                    yield break;
                }
                if (!_paused) waiting += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_transitioning) yield break;
            _audio.clip = _introMusic;
            _audio.Play();
            _playbackStarted = true;
            yield return null;
            LogAudio("start");
        }

        private void LogAudio(string reason)
        {
            if (_audio == null) return;
            TelemetryLogger.Info("CleanIntro", $"audio {reason}: loaded={(_introMusic != null ? _introMusic.loadState.ToString() : "missing")}, playing={_audio.isPlaying}, samples={_audio.timeSamples}, volume={_audio.volume:0.00}, mute={_audio.mute}, listenerVolume={AudioListener.volume:0.00}, listenerPaused={AudioListener.pause}");
        }

        public void ToggleMute()
        {
            if (_audio == null) return;
            _audio.mute = !_audio.mute;
            if (_muteText != null) _muteText.text = _audio.mute ? "UNMUTE" : "MUTE";
            LogAudio("mute changed");
        }

        public void Continue()
        {
            if (_transitioning) return;
            _transitioning = true;
            if (_continue != null) _continue.interactable = false;
            StartCoroutine(FadeAndContinue());
        }

        private IEnumerator FadeAndContinue()
        {
            float start = _audio.volume;
            float elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _audio.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _fadeSeconds)));
                yield return null;
            }
            _audio.Stop();
            UnityEngine.SceneManagement.SceneManager.LoadScene(CleanExperience.SetupScene);
        }

        private void LateUpdate()
        {
            if (_canvas != null && _camera != null) _placement.Hold(_canvas, _camera.transform, 1.5f, 0f);
        }
        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (_audio == null || _transitioning || !_playbackStarted) return;
            if (paused) _audio.Pause();
            else
            {
                _audio.UnPause();
                if (!_audio.isPlaying && _introMusic != null && _introMusic.loadState == AudioDataLoadState.Loaded) _audio.Play();
            }
            LogAudio(paused ? "pause" : "resume");
        }
        private void OnDisable() { if (_audio != null) _audio.Stop(); }
    }

    /// <summary>Shared uGUI construction for the two clean screens, using existing gaze conventions.</summary>
    public static class CleanMenu
    {
        public static RectTransform CreateCanvas(Transform parent, Camera camera, Vector2 size)
        {
            ModeQuickSwitchPanel.EnsureVrEventSystem();
            var go = new GameObject("CleanCanvas", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * 0.0012f;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            ModeQuickSwitchPanel.TryInstallVrRaycaster(go);
            var pointer = go.AddComponent<Chorewars.HandsFree.HeadGazePointer>();
            pointer.Eye = camera;
            pointer.Raycaster = go.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            return rect;
        }
        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
        public static void Background(RectTransform rect)
        {
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.01f, 0.015f, 0.025f, 0.28f);
            background.raycastTarget = false;
        }
        public static UnityEngine.UI.Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), position, size);
            var text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }
        public static UnityEngine.UI.Button Button(Transform parent, string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), position, size);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.4f, 0.16f, 0.025f);
            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            Text(go.transform, "Label", label, Vector2.zero, size - new Vector2(12f, 8f), 25);
            return button;
        }
    }
}
