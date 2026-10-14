using System;
using UnityEngine;
using UnityEngine.UI;
using Chorewars.Core;
using Chorewars.UI;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Placeholder world-space UI, built with legacy uGUI Text (the project ships no TextMeshPro assets).
    /// Owns two mutually exclusive panels: a room-scan panel (progress bar + clickable START button)
    /// and the in-session chore panel (coverage / time / grade) plus the end-of-session summary.
    /// The overlay is small and translucent so AR passthrough stays readable.
    /// </summary>
    public sealed class PlaceholderHud : MonoBehaviour
    {
        /// <summary>Raised when the player activates the START SCAN button.</summary>
        public event Action StartScanRequested;

        private static Font _font;
        private static Sprite _ringSprite;
        private static Sprite _flameSprite;

        private Camera _camera;
        private Canvas _canvas;

        private GameObject _chorePanel;
        private Text _pointsText;
        private Text _timerText;
        private Text _gradeText;
        private Text _sourceText;
        private Text _comboText;
        private Image _coverageRing;
        private Text _coverageRingText;
        private Image _flame;
        private float _coveragePunch;
        private float _comboPunch;
        private float _flamePulse;

        private int _targetPoints;
        private float _displayedPoints;
        private float _ringFill;
        private float _ringTarget;
        private float _gradeSlam;
        private float _summaryPunch;
        private Text _summaryGradeText;

        private GameObject _scanPanel;
        private Text _scanTitleText;
        private Text _scanStatusText;
        private Text _scanButtonLabel;
        private RectTransform _scanBarFill;
        private float _scanBarFullWidth = 552f;

        private GameObject _summaryRoot;
        private Text _summaryText;
        private float _summaryHideAt = -1f;
        private bool _placed;

        public void Build(Camera camera)
        {
            _camera = camera;

            var canvasGo = new GameObject("HudCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = camera;
            canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(640f, 320f);
            canvasGo.transform.localScale = Vector3.one * 0.0013f;

            // The launch panel already knows how to make a world-space canvas raycastable
            // (OVRRaycaster when Meta UI is present, GraphicRaycaster otherwise) and to supply an
            // EventSystem with the right input module. Reuse it instead of duplicating that logic.
            ModeQuickSwitchPanel.EnsureVrEventSystem();
            ModeQuickSwitchPanel.TryInstallVrRaycaster(canvasGo);

            BuildChorePanel(canvasGo.transform);
            BuildScanPanel(canvasGo.transform);
            BuildSummary(canvasGo.transform);

            SetChorePanelVisible(false);
        }

        // ---- Chore panel ------------------------------------------------------------------

        public void SetCoverage(float percent)
        {
            // The ring and the readout are smoothed/punched in AnimatePunches so this stays cheap
            // and can be called every frame.
            _ringTarget = Mathf.Clamp01(percent / 100f);
            Assign(_coverageRingText, $"{percent:0}%");
        }

        /// <summary>Live score. The ticker counts up toward it rather than snapping.</summary>
        public void SetPoints(int points)
        {
            _targetPoints = points;
        }

        public void SetTimer(float secondsRemaining)
        {
            Assign(_timerText, $"Time left {Mathf.Max(0f, secondsRemaining):0}s");
        }

        public void SetGrade(string grade)
        {
            Assign(_gradeText, $"Grade {grade}");
        }

        public void SetSource(string source)
        {
            Assign(_sourceText, source);
        }

        public void SetChorePanelVisible(bool visible)
        {
            if (_chorePanel != null) _chorePanel.SetActive(visible);
        }

        /// <summary>Shows the consecutive-new-cell streak. Hidden below 2 so it stays an event.</summary>
        public void SetCombo(int combo)
        {
            if (_comboText == null) return;

            bool show = combo >= 2;
            _comboText.gameObject.SetActive(show);

            // The flame only appears once a real run is going, so it reads as an achievement.
            if (_flame != null) _flame.gameObject.SetActive(combo >= 3);

            if (!show) return;

            Assign(_comboText, combo >= 10 ? $"STREAK x{combo}  \u2022 FIRE" : $"STREAK x{combo}");
            _comboPunch = 1f;
            _flamePulse = 1f;
        }

        /// <summary>One-frame scale punch on the coverage ring; called for every new cell.</summary>
        public void PunchCoverage() => _coveragePunch = 1f;

        // ---- Scan panel -------------------------------------------------------------------

        public void ShowScanPanel(string title, string status)
        {
            if (_scanPanel != null) _scanPanel.SetActive(true);
            Assign(_scanTitleText, title);
            Assign(_scanStatusText, status);
        }

        public void HideScanPanel()
        {
            if (_scanPanel != null) _scanPanel.SetActive(false);
        }

        public void SetScanStatus(string status)
        {
            Assign(_scanStatusText, status);
        }

        public void SetScanProgress(float normalised, string status = null)
        {
            normalised = Mathf.Clamp01(normalised);

            if (_scanBarFill != null)
            {
                // Measure the track rather than hard-coding a width, so the fill always reaches
                // the end of the bar regardless of canvas size or padding.
                var track = _scanBarFill.parent as RectTransform;
                float full = track != null ? Mathf.Max(0f, track.rect.width - 4f) : _scanBarFullWidth;

                var size = _scanBarFill.sizeDelta;
                size.x = full * normalised;
                _scanBarFill.sizeDelta = size;
            }

            if (!string.IsNullOrEmpty(status)) SetScanStatus(status);
        }

        public void SetScanButtonLabel(string label)
        {
            Assign(_scanButtonLabel, label);
        }

        // ---- Summary ----------------------------------------------------------------------

        public void ShowSummary(ChoreResult result)
        {
            if (_summaryRoot == null) return;

            var grade = result != null && !string.IsNullOrEmpty(result.grade) ? result.grade : "-";
            var percent = result != null && result.session != null ? result.session.coveragePercent : 0f;
            var points = result != null ? result.totalPoints : 0;

            Assign(_summaryGradeText, grade);
            Assign(_summaryText, $"CHORE COMPLETE\nClean {percent:0}%    Score {points}");

            // Kick off the slam + the screen-space punch.
            _gradeSlam = 1f;
            _summaryPunch = 1f;

            _summaryRoot.SetActive(true);
            _summaryHideAt = Time.unscaledTime + 12f;
        }

        public void HideSummary()
        {
            if (_summaryRoot != null) _summaryRoot.SetActive(false);
            _summaryHideAt = -1f;
        }

        // ---- Build ------------------------------------------------------------------------

        private void BuildChorePanel(Transform parent)
        {
            var panelGo = new GameObject("ChorePanel", typeof(RectTransform));
            panelGo.transform.SetParent(parent, false);

            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -260f);
            rect.offsetMax = Vector2.zero;

            var paper = MakeImage(panelGo.transform, "Paper", new Color(0.06f, 0.05f, 0.04f, 0.55f));
            Stretch(paper.GetComponent<RectTransform>());

            // ---- coverage ring (top right) ----
            // A dim full ring behind a radially-filled one; the fill reads the progress directly.
            MakeRing(panelGo.transform, "RingBack", new Color(1f, 1f, 1f, 0.13f), out _);

            _coverageRing = MakeRing(panelGo.transform, "RingFill", new Color(0.36f, 0.9f, 0.6f), out _);
            _coverageRing.type = Image.Type.Filled;
            _coverageRing.fillMethod = Image.FillMethod.Radial360;
            _coverageRing.fillOrigin = (int)Image.Origin360.Top;
            _coverageRing.fillClockwise = true;
            _coverageRing.fillAmount = 0f;

            _coverageRingText = MakeText(panelGo.transform, "RingText", 54, TextAnchor.MiddleCenter, Color.white);
            var ringTextRect = _coverageRingText.rectTransform;
            ringTextRect.anchorMin = new Vector2(1f, 1f);
            ringTextRect.anchorMax = new Vector2(1f, 1f);
            ringTextRect.pivot = new Vector2(1f, 1f);
            ringTextRect.anchoredPosition = new Vector2(-18f, -14f);
            ringTextRect.sizeDelta = new Vector2(168f, 168f);
            _coverageRingText.text = "0%";

            // ---- left column ----
            _pointsText = MakeText(panelGo.transform, "Points", 50, TextAnchor.UpperLeft, new Color(1f, 0.87f, 0.4f));
            SetRect(_pointsText.rectTransform, new Vector2(20f, -14f), new Vector2(420f, 58f));
            _pointsText.text = "0 pts";

            _timerText = MakeText(panelGo.transform, "Timer", 34, TextAnchor.UpperLeft, new Color(0.82f, 0.88f, 1f));
            SetRect(_timerText.rectTransform, new Vector2(20f, -76f), new Vector2(420f, 40f));

            _gradeText = MakeText(panelGo.transform, "Grade", 34, TextAnchor.UpperLeft, new Color(1f, 0.87f, 0.5f));
            SetRect(_gradeText.rectTransform, new Vector2(20f, -120f), new Vector2(420f, 40f));

            _sourceText = MakeText(panelGo.transform, "Source", 20, TextAnchor.UpperLeft, new Color(0.7f, 0.76f, 0.85f));
            SetRect(_sourceText.rectTransform, new Vector2(20f, -160f), new Vector2(600f, 34f));

            // ---- streak + flame ----
            var flameGo = MakeImage(panelGo.transform, "Flame", new Color(1f, 0.6f, 0.2f));
            _flame = flameGo.GetComponent<Image>();
            var flameRect = _flame.rectTransform;
            flameRect.anchorMin = new Vector2(0f, 1f);
            flameRect.anchorMax = new Vector2(0f, 1f);
            flameRect.pivot = new Vector2(0f, 1f);
            flameRect.anchoredPosition = new Vector2(20f, -196f);
            flameRect.sizeDelta = new Vector2(30f, 42f);
            _flame.sprite = FlameSprite();
            _flame.raycastTarget = false;
            _flame.gameObject.SetActive(false);

            _comboText = MakeText(panelGo.transform, "Combo", 32, TextAnchor.UpperLeft, new Color(1f, 0.72f, 0.25f));
            SetRect(_comboText.rectTransform, new Vector2(58f, -198f), new Vector2(400f, 46f));
            _comboText.text = string.Empty;
            _comboText.gameObject.SetActive(false);

            _chorePanel = panelGo;
        }

        /// <summary>Builds one ring image in the panel's top-right slot.</summary>
        private static Image MakeRing(Transform parent, string name, Color colour, out RectTransform rect)
        {
            var go = MakeImage(parent, name, colour);
            rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-18f, -14f);
            rect.sizeDelta = new Vector2(168f, 168f);

            var image = go.GetComponent<Image>();
            image.sprite = RingSprite();
            image.raycastTarget = false;
            return image;
        }

        private void BuildScanPanel(Transform parent)
        {
            var panelGo = new GameObject("ScanPanel", typeof(RectTransform));
            panelGo.transform.SetParent(parent, false);

            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var paper = MakeImage(panelGo.transform, "Paper", new Color(0.05f, 0.06f, 0.09f, 0.78f));
            Stretch(paper.GetComponent<RectTransform>());

            _scanTitleText = MakeText(panelGo.transform, "Title", 46, TextAnchor.UpperLeft, Color.white);
            SetRect(_scanTitleText.rectTransform, new Vector2(28f, -16f), new Vector2(600f, 56f));

            _scanStatusText = MakeText(panelGo.transform, "Status", 30, TextAnchor.UpperLeft, new Color(0.82f, 0.88f, 1f));
            SetRect(_scanStatusText.rectTransform, new Vector2(20f, -80f), new Vector2(600f, 60f));

            var barBack = MakeImage(panelGo.transform, "ScanBarBack", new Color(1f, 1f, 1f, 0.16f));
            var backRect = barBack.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(1f, 1f);
            backRect.pivot = new Vector2(0.5f, 1f);
            backRect.offsetMin = new Vector2(20f, -160f);
            backRect.offsetMax = new Vector2(-20f, -128f);

            var fill = MakeImage(barBack.transform, "ScanBarFill", new Color(0.36f, 0.82f, 0.52f));
            _scanBarFill = fill.GetComponent<RectTransform>();
            _scanBarFill.anchorMin = new Vector2(0f, 0f);
            _scanBarFill.anchorMax = new Vector2(0f, 1f);
            _scanBarFill.pivot = new Vector2(0f, 0.5f);
            _scanBarFill.offsetMin = new Vector2(2f, 3f);
            _scanBarFill.offsetMax = new Vector2(2f, -3f);
            _scanBarFill.sizeDelta = new Vector2(0f, -6f);

            var buttonGo = MakeImage(panelGo.transform, "StartButton", new Color(0.16f, 0.5f, 0.32f, 1f));
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 20f);
            buttonRect.sizeDelta = new Vector2(340f, 66f);

            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonGo.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;

            var colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colours;

            button.onClick.AddListener(() => StartScanRequested?.Invoke());

            _scanButtonLabel = MakeText(buttonGo.transform, "Label", 34, TextAnchor.MiddleCenter, Color.white);
            Stretch(_scanButtonLabel.rectTransform);
            _scanButtonLabel.text = "START SCAN";

            _scanPanel = panelGo;
            panelGo.SetActive(false);
        }

        private void BuildSummary(Transform parent)
        {
            _summaryRoot = MakeImage(parent, "Summary", new Color(0f, 0f, 0f, 0.72f));
            Stretch(_summaryRoot.GetComponent<RectTransform>());

            // Big grade letter on its own element so it can slam in independently of the details.
            _summaryGradeText = MakeText(_summaryRoot.transform, "GradeLetter", 150, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.4f));
            var gradeRect = _summaryGradeText.rectTransform;
            gradeRect.anchorMin = new Vector2(0.5f, 0.5f);
            gradeRect.anchorMax = new Vector2(0.5f, 0.5f);
            gradeRect.pivot = new Vector2(0.5f, 0.5f);
            gradeRect.anchoredPosition = new Vector2(0f, 46f);
            gradeRect.sizeDelta = new Vector2(400f, 180f);
            _summaryGradeText.text = "-";

            _summaryText = MakeText(_summaryRoot.transform, "SummaryText", 36, TextAnchor.MiddleCenter, Color.white);
            var detailRect = _summaryText.rectTransform;
            detailRect.anchorMin = new Vector2(0.5f, 0.5f);
            detailRect.anchorMax = new Vector2(0.5f, 0.5f);
            detailRect.pivot = new Vector2(0.5f, 0.5f);
            detailRect.anchoredPosition = new Vector2(0f, -74f);
            detailRect.sizeDelta = new Vector2(580f, 170f);

            _summaryRoot.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            if (_canvas != null && _canvas.worldCamera != _camera) _canvas.worldCamera = _camera;

            var cameraTransform = _camera.transform;
            var target = cameraTransform.position
                         + cameraTransform.forward * 1.15f
                         + cameraTransform.up * -0.26f;

            transform.position = _placed
                ? Vector3.Lerp(transform.position, target, Time.unscaledDeltaTime * 10f)
                : target;
            _placed = true;

            transform.rotation = Quaternion.LookRotation(transform.position - cameraTransform.position, Vector3.up);

            AnimatePunches();

            if (_summaryHideAt > 0f && Time.unscaledTime >= _summaryHideAt) HideSummary();
        }

        /// <summary>
        /// Decays the scale punches. Applied as localScale so the Text layout itself is untouched
        /// (no rebuilds, no allocation) and it composes with the panel's own scaling.
        /// </summary>
        private void AnimatePunches()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;

            _coveragePunch = Mathf.Max(0f, _coveragePunch - dt * 5f);
            _comboPunch = Mathf.Max(0f, _comboPunch - dt * 4f);
            _flamePulse = Mathf.Max(0f, _flamePulse - dt * 3f);

            // Coverage ring eases toward the live value (exponential, frame-rate independent) and
            // punches on every new cell.
            _ringFill = Mathf.Lerp(_ringFill, _ringTarget, 1f - Mathf.Exp(-dt * 12f));

            if (_coverageRing != null)
            {
                _coverageRing.fillAmount = _ringFill;
                _coverageRing.rectTransform.localScale = Vector3.one * (1f + _coveragePunch * 0.10f);
            }

            if (_coverageRingText != null)
            {
                _coverageRingText.rectTransform.localScale = Vector3.one * (1f + _coveragePunch * 0.18f);
                _coverageRingText.color = Color.Lerp(Color.white, new Color(0.6f, 1f, 0.78f), _coveragePunch);
            }

            // Points ticker: count toward the target, never allocating a string every frame.
            if (Mathf.Abs(_displayedPoints - _targetPoints) >= 0.5f)
            {
                _displayedPoints = Mathf.MoveTowards(
                    _displayedPoints, _targetPoints,
                    Mathf.Max(1f, Mathf.Abs(_targetPoints - _displayedPoints) * 6f * dt));

                Assign(_pointsText, $"{(int)_displayedPoints} pts");
            }

            if (_comboText != null)
                _comboText.rectTransform.localScale = Vector3.one * (1f + _comboPunch * 0.35f);

            if (_flame != null && _flame.gameObject.activeSelf)
            {
                float pulse = 1f + Mathf.Sin(now * 12f) * 0.12f + _flamePulse * 0.35f;
                _flame.rectTransform.localScale = new Vector3(pulse, pulse * 1.1f, 1f);
                _flame.color = Color.Lerp(
                    new Color(1f, 0.5f, 0.12f),
                    new Color(1f, 0.86f, 0.32f),
                    Mathf.PingPong(now * 4f, 1f));
            }

            // Grade letter slams from oversized down to rest, then the overlay does one punch.
            if (_gradeSlam > 0f)
            {
                _gradeSlam = Mathf.Max(0f, _gradeSlam - dt * 2.4f);
                float k = _gradeSlam * _gradeSlam;

                if (_summaryGradeText != null)
                    _summaryGradeText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 3.4f, k);
            }

            if (_summaryPunch > 0f)
            {
                _summaryPunch = Mathf.Max(0f, _summaryPunch - dt * 3.2f);

                if (_summaryRoot != null)
                    _summaryRoot.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.06f, _summaryPunch);
            }
        }

        /// <summary>An annulus sprite for the coverage ring (transparent inside and out).</summary>
        internal static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;

            const int size = 128;
            const float inner = 0.74f;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HudRing",
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

                    float a = Mathf.Clamp01((d - inner) / 0.06f) * Mathf.Clamp01((1f - d) / 0.06f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);

            _ringSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _ringSprite;
        }

        /// <summary>A soft teardrop for the streak flame.</summary>
        private static Sprite FlameSprite()
        {
            if (_flameSprite != null) return _flameSprite;

            const int size = 64;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HudFlame",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                // ny: 0 at the base, 1 at the tip.
                float ny = (y + 0.5f) / size;
                float halfWidth = 0.62f * Mathf.Sin(Mathf.Pow(ny, 0.75f) * Mathf.PI);

                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) - 0.5f;
                    float a = Mathf.Clamp01((halfWidth - Mathf.Abs(nx)) / 0.10f) * 0.85f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);

            _flameSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _flameSprite;
        }

        private static void Assign(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        private static Font UiFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }

        private static GameObject MakeImage(Transform parent, string name, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = colour;
            return go;
        }

        private static Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = UiFont();
            text.fontSize = size;
            text.alignment = anchor;
            text.color = colour;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
