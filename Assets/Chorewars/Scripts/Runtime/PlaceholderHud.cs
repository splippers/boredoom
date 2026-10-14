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

        private Camera _camera;
        private Canvas _canvas;

        private GameObject _chorePanel;
        private Text _coverageText;
        private Text _timerText;
        private Text _gradeText;
        private Text _sourceText;
        private Text _comboText;
        private float _coveragePunch;
        private float _comboPunch;

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
            Assign(_coverageText, $"{percent:0}% CLEAN");
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

            if (combo < 2)
            {
                _comboText.gameObject.SetActive(false);
                return;
            }

            _comboText.gameObject.SetActive(true);
            Assign(_comboText, combo >= 10 ? $"STREAK x{combo}  \u2022 FIRE" : $"STREAK x{combo}");
            _comboPunch = 1f;
        }

        /// <summary>One-frame scale punch on the coverage readout; called for every new cell.</summary>
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

            Assign(_summaryText, $"CHORE COMPLETE\n\nGrade {grade}\nClean {percent:0}%\nScore {points}");

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
            rect.offsetMin = new Vector2(0f, -210f);
            rect.offsetMax = Vector2.zero;

            var paper = MakeImage(panelGo.transform, "Paper", new Color(0.06f, 0.05f, 0.04f, 0.55f));
            Stretch(paper.GetComponent<RectTransform>());

            _coverageText = MakeText(panelGo.transform, "Coverage", 46, TextAnchor.UpperLeft, Color.white);
            SetRect(_coverageText.rectTransform, new Vector2(20f, -12f), new Vector2(600f, 56f));

            // Right-anchored with a top-right pivot so the streak punch scales from that corner.
            _comboText = MakeText(panelGo.transform, "Combo", 32, TextAnchor.UpperRight, new Color(1f, 0.72f, 0.25f));
            var comboRect = _comboText.rectTransform;
            comboRect.anchorMin = new Vector2(1f, 1f);
            comboRect.anchorMax = new Vector2(1f, 1f);
            comboRect.pivot = new Vector2(1f, 1f);
            comboRect.anchoredPosition = new Vector2(-20f, -16f);
            comboRect.sizeDelta = new Vector2(300f, 46f);
            _comboText.text = string.Empty;
            _comboText.gameObject.SetActive(false);

            _timerText = MakeText(panelGo.transform, "Timer", 34, TextAnchor.UpperLeft, new Color(0.82f, 0.88f, 1f));
            SetRect(_timerText.rectTransform, new Vector2(20f, -74f), new Vector2(600f, 40f));

            _gradeText = MakeText(panelGo.transform, "Grade", 34, TextAnchor.UpperLeft, new Color(1f, 0.87f, 0.5f));
            SetRect(_gradeText.rectTransform, new Vector2(20f, -120f), new Vector2(600f, 40f));

            _sourceText = MakeText(panelGo.transform, "Source", 22, TextAnchor.UpperLeft, new Color(0.7f, 0.76f, 0.85f));
            SetRect(_sourceText.rectTransform, new Vector2(20f, -162f), new Vector2(600f, 36f));

            _chorePanel = panelGo;
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

            _summaryText = MakeText(_summaryRoot.transform, "SummaryText", 40, TextAnchor.MiddleCenter, Color.white);
            Stretch(_summaryText.rectTransform);

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

            _coveragePunch = Mathf.Max(0f, _coveragePunch - dt * 5f);
            _comboPunch = Mathf.Max(0f, _comboPunch - dt * 4f);

            if (_coverageText != null)
                _coverageText.rectTransform.localScale = Vector3.one * (1f + _coveragePunch * 0.22f);

            if (_comboText != null)
                _comboText.rectTransform.localScale = Vector3.one * (1f + _comboPunch * 0.35f);
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
