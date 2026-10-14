using System;
using System.Collections.Generic;
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

        /// <summary>Raised with a mode id when the player picks a chore from the mode menu.</summary>
        public event Action<string> ModeSelected;

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

        private GameObject _modePanel;
        private Text _modeStatusText;
        private readonly List<ModeRow> _modeRows = new();

        /// <summary>One selectable chore on the mode menu.</summary>
        private sealed class ModeRow
        {
            public string Id;
            public Image Background;
            public Text Name;
        }

        private GameObject _summaryRoot;
        private Text _summaryText;
        private Text _summaryTitleText;
        private Text _summaryPbText;
        private float _summaryPbPunch;
        private float _summaryRevealAt;
        private float _summaryHideAt = -1f;

        // Last values written to the chore panel. The panel is driven every frame, but the labels
        // only need rebuilding when the whole number they show actually changes — otherwise the
        // string interpolation allocates garbage every single frame on device.
        private int _lastCoveragePercent = int.MinValue;
        private int _lastTimerSecond = int.MinValue;
        private string _lastGrade;

        /// <summary>Row layout for the per-room breakdown: built once, shown/hidden per session.</summary>
        private sealed class RoomBar
        {
            public GameObject Root;
            public Text Label;
            public Text Percent;
            public RectTransform Fill;
            public float BarWidth;
            public float Target;
        }

        private const int MaxSummaryRooms = 6;
        private readonly List<RoomBar> _roomBars = new();

        private bool _placed;

        public void Build(Camera camera)
        {
            _camera = camera;

            var canvasGo = new GameObject("HudCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = camera;
            canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(640f, 480f);
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

            // Rebuild the label only when the whole number actually changes. Assigning the
            // interpolated string every frame would allocate continuously on device.
            int rounded = Mathf.Clamp(Mathf.RoundToInt(percent), 0, 100);
            if (rounded == _lastCoveragePercent) return;

            _lastCoveragePercent = rounded;
            Assign(_coverageRingText, $"{rounded}%");
        }

        /// <summary>Live score. The ticker counts up toward it rather than snapping.</summary>
        public void SetPoints(int points)
        {
            _targetPoints = points;
        }

        public void SetTimer(float secondsRemaining)
        {
            // Only rewrite the label when the displayed second changes.
            int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
            if (seconds == _lastTimerSecond) return;

            _lastTimerSecond = seconds;
            Assign(_timerText, $"Time left {seconds}s");
        }

        public void SetGrade(string grade)
        {
            if (grade == _lastGrade) return;

            _lastGrade = grade;
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
            ShowSummary(result, null, -1, false);
        }

        /// <summary>
        /// Rich end-of-session summary: grade reveal, score, a personal-best compare and an animated
        /// per-room breakdown.
        /// </summary>
        /// <param name="roomCoverage">Per-room final coverage (id → 0–100); null hides the bars.</param>
        /// <param name="previousBest">Score to beat, or -1 to hide the compare line entirely.</param>
        /// <param name="isNewBest">True when this run beat <paramref name="previousBest"/>.</param>
        public void ShowSummary(
            ChoreResult result,
            IReadOnlyDictionary<string, float> roomCoverage,
            int previousBest,
            bool isNewBest)
        {
            if (_summaryRoot == null) return;

            var grade = result != null && !string.IsNullOrEmpty(result.grade) ? result.grade : "-";
            var percent = result != null && result.session != null ? result.session.coveragePercent : 0f;
            var points = result != null ? result.totalPoints : 0;

            Assign(_summaryTitleText, "CHORE COMPLETE");
            Assign(_summaryGradeText, grade);
            Assign(_summaryText, $"Clean {percent:0}%      Score {points}{FormatDuration(result)}");

            if (_summaryPbText != null)
            {
                if (previousBest < 0)
                {
                    _summaryPbText.text = string.Empty;
                }
                else if (previousBest == 0)
                {
                    _summaryPbText.text = "First run \u2014 set the bar!";
                    _summaryPbText.color = new Color(0.75f, 0.82f, 0.9f);
                }
                else if (isNewBest)
                {
                    _summaryPbText.text = $"NEW BEST!  beat {previousBest}   +{points - previousBest}";
                    _summaryPbText.color = new Color(1f, 0.85f, 0.32f);
                }
                else
                {
                    int delta = points - previousBest;

                    _summaryPbText.text = delta switch
                    {
                        > 0 => $"Best {previousBest}   +{delta}",
                        0 => $"Best {previousBest}   matched",
                        _ => $"Best {previousBest}   {delta}"
                    };
                    _summaryPbText.color = new Color(0.75f, 0.82f, 0.9f);
                }
            }

            PopulateRoomBars(roomCoverage);

            // Kick off the slam, the screen-space punch and the staggered bar reveal.
            _gradeSlam = 1f;
            _summaryPunch = 1f;
            _summaryPbPunch = isNewBest ? 1f : 0f;
            _summaryRevealAt = Time.unscaledTime;

            _summaryRoot.SetActive(true);
            _summaryHideAt = Time.unscaledTime + 16f;
        }

        /// <summary>Fills in the per-room rows, hiding any that are unused this session.</summary>
        private void PopulateRoomBars(IReadOnlyDictionary<string, float> roomCoverage)
        {
            int index = 0;

            if (roomCoverage != null && roomCoverage.Count > 0)
            {
                // Sort so the rows read in a stable order rather than dictionary order.
                var ids = new List<string>(roomCoverage.Keys);
                ids.Sort();

                foreach (var id in ids)
                {
                    if (index >= _roomBars.Count) break;

                    var bar = _roomBars[index];
                    bar.Root.SetActive(true);
                    bar.Target = Mathf.Clamp(roomCoverage[id], 0f, 100f);

                    Assign(bar.Label, id);
                    Assign(bar.Percent, $"{bar.Target:0}%");

                    // Start empty; AnimateSummaryBars grows it so the rows fill in sequence.
                    bar.Fill.sizeDelta = new Vector2(0f, bar.Fill.sizeDelta.y);
                    index++;
                }
            }

            for (; index < _roomBars.Count; index++) _roomBars[index].Root.SetActive(false);
        }

        private static string FormatDuration(ChoreResult result)
        {
            if (result?.session == null || !result.session.endTimeUtc.HasValue) return string.Empty;

            var seconds = (float)(result.session.endTimeUtc.Value - result.session.startTimeUtc).TotalSeconds;
            if (seconds <= 0f) return string.Empty;

            int m = (int)seconds / 60;
            int s = (int)seconds % 60;
            return m > 0 ? $"      {m}m {s:D2}s" : $"      {s}s";
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

        // ---- Mode menu --------------------------------------------------------------------

        private static readonly Color ModeIdle = new(0.14f, 0.16f, 0.22f, 0.92f);
        private static readonly Color ModeSelectedColour = new(0.18f, 0.46f, 0.30f, 1f);

        /// <summary>
        /// Shows the chore menu. Built lazily on first use so it can read the mode catalog at the
        /// moment it is actually needed rather than at HUD construction.
        /// </summary>
        public void ShowModePanel(string title, string status)
        {
            if (_modePanel == null) BuildModePanel();

            Assign(_modeStatusText, status);
            var titleText = _modePanel != null ? _modePanel.transform.Find("Title")?.GetComponent<Text>() : null;
            Assign(titleText, title);
            _modePanel.SetActive(true);
        }

        public void HideModePanel()
        {
            if (_modePanel != null) _modePanel.SetActive(false);
        }

        /// <summary>Highlights the chosen row so the player can see what they are about to run.</summary>
        public void SetSelectedMode(string modeId)
        {
            foreach (var row in _modeRows)
            {
                if (row.Background == null) continue;
                row.Background.color = row.Id == modeId ? ModeSelectedColour : ModeIdle;
            }
        }

        private void BuildModePanel()
        {
            var panelGo = new GameObject("ModePanel", typeof(RectTransform));
            panelGo.transform.SetParent(_canvas.transform, false);

            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var paper = MakeImage(panelGo.transform, "Paper", new Color(0.05f, 0.06f, 0.09f, 0.80f));
            Stretch(paper.GetComponent<RectTransform>());

            var title = MakeText(panelGo.transform, "Title", 46, TextAnchor.UpperLeft, Color.white);
            SetRect(title.rectTransform, new Vector2(28f, -16f), new Vector2(600f, 56f));
            title.text = "CHOOSE YOUR CHORE";

            _modeStatusText = MakeText(panelGo.transform, "Status", 26, TextAnchor.UpperLeft, new Color(0.82f, 0.88f, 1f));
            SetRect(_modeStatusText.rectTransform, new Vector2(28f, -74f), new Vector2(600f, 40f));

            var modes = RuntimeModeCatalog.Modes;
            float y = -120f;
            const float rowHeight = 54f;
            const float rowGap = 8f;

            for (int i = 0; i < modes.Count; i++)
            {
                var info = modes[i];
                float top = y - i * (rowHeight + rowGap);

                var rowGo = MakeImage(panelGo.transform, $"Mode_{info.Id}", ModeIdle);
                var rowRect = rowGo.GetComponent<RectTransform>();
                SetRect(rowRect, new Vector2(28f, top), new Vector2(584f, rowHeight));

                var button = rowGo.AddComponent<Button>();
                button.targetGraphic = rowGo.GetComponent<Image>();

                string id = info.Id;
                button.onClick.AddListener(() =>
                {
                    SetSelectedMode(id);
                    ModeSelected?.Invoke(id);
                });

                var name = MakeText(rowGo.transform, "Name", 30, TextAnchor.MiddleLeft, Color.white);
                SetRect(name.rectTransform, new Vector2(16f, 0f), new Vector2(250f, rowHeight));
                name.text = info.DisplayName;

                var blurb = MakeText(rowGo.transform, "Blurb", 21, TextAnchor.MiddleRight, new Color(0.78f, 0.84f, 0.95f));
                var blurbRect = blurb.rectTransform;
                blurbRect.anchorMin = new Vector2(0f, 0f);
                blurbRect.anchorMax = new Vector2(1f, 1f);
                blurbRect.offsetMin = new Vector2(270f, 0f);
                blurbRect.offsetMax = new Vector2(-16f, 0f);
                blurb.text = info.Blurb;

                _modeRows.Add(new ModeRow { Id = info.Id, Background = rowGo.GetComponent<Image>(), Name = name });
            }

            _modePanel = panelGo;
            panelGo.SetActive(false);
        }

        private void BuildSummary(Transform parent)
        {
            _summaryRoot = MakeImage(parent, "Summary", new Color(0f, 0f, 0f, 0.78f));
            Stretch(_summaryRoot.GetComponent<RectTransform>());

            _summaryTitleText = MakeText(_summaryRoot.transform, "Title", 34, TextAnchor.MiddleCenter, new Color(0.72f, 0.94f, 0.78f));
            CentreElement(_summaryTitleText.rectTransform, new Vector2(0f, 198f), new Vector2(620f, 44f));

            // Big grade letter on its own element so it can slam in independently of the details.
            _summaryGradeText = MakeText(_summaryRoot.transform, "GradeLetter", 130, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.4f));
            CentreElement(_summaryGradeText.rectTransform, new Vector2(0f, 104f), new Vector2(400f, 150f));
            _summaryGradeText.text = "-";

            _summaryText = MakeText(_summaryRoot.transform, "SummaryText", 30, TextAnchor.MiddleCenter, Color.white);
            CentreElement(_summaryText.rectTransform, new Vector2(0f, 14f), new Vector2(620f, 40f));

            _summaryPbText = MakeText(_summaryRoot.transform, "PbText", 28, TextAnchor.MiddleCenter, new Color(0.75f, 0.82f, 0.9f));
            CentreElement(_summaryPbText.rectTransform, new Vector2(0f, -30f), new Vector2(620f, 36f));

            BuildSummaryRoomBars(_summaryRoot.transform);

            _summaryRoot.SetActive(false);
        }

        /// <summary>Builds the per-room rows once; ShowSummary only shows/hides and retargets them.</summary>
        private void BuildSummaryRoomBars(Transform parent)
        {
            const float rowHeight = 34f;
            const float labelWidth = 150f;
            const float barLeft = 160f;
            const float barWidth = 320f;
            const float percentLeft = 496f;

            for (int i = 0; i < MaxSummaryRooms; i++)
            {
                var rowGo = new GameObject($"RoomBar{i}", typeof(RectTransform));
                rowGo.transform.SetParent(parent, false);

                var rowRect = rowGo.GetComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.5f, 0.5f);
                rowRect.anchorMax = new Vector2(0.5f, 0.5f);
                rowRect.pivot = new Vector2(0f, 0.5f);
                // Anchored at the canvas centre; rows start at x -310 so they span the panel width.
                rowRect.anchoredPosition = new Vector2(-310f, -72f - i * rowHeight);
                rowRect.sizeDelta = new Vector2(620f, rowHeight - 6f);

                var label = MakeText(rowGo.transform, "Label", 24, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 0.96f));
                SetRect(label.rectTransform, Vector2.zero, new Vector2(labelWidth, rowHeight - 6f));

                var back = MakeImage(rowGo.transform, "Back", new Color(1f, 1f, 1f, 0.14f));
                var backRect = back.GetComponent<RectTransform>();
                backRect.anchorMin = new Vector2(0f, 0.5f);
                backRect.anchorMax = new Vector2(0f, 0.5f);
                backRect.pivot = new Vector2(0f, 0.5f);
                backRect.anchoredPosition = new Vector2(barLeft, 0f);
                backRect.sizeDelta = new Vector2(barWidth, 18f);

                var fill = MakeImage(rowGo.transform, "Fill", new Color(0.36f, 0.9f, 0.6f));
                var fillRect = fill.GetComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0f, 0.5f);
                fillRect.anchorMax = new Vector2(0f, 0.5f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.anchoredPosition = new Vector2(barLeft, 0f);
                fillRect.sizeDelta = new Vector2(0f, 18f);

                var percent = MakeText(rowGo.transform, "Percent", 24, TextAnchor.MiddleRight, new Color(0.86f, 0.9f, 0.96f));
                var percentRect = percent.rectTransform;
                percentRect.anchorMin = new Vector2(0f, 0.5f);
                percentRect.anchorMax = new Vector2(0f, 0.5f);
                percentRect.pivot = new Vector2(0f, 0.5f);
                percentRect.anchoredPosition = new Vector2(percentLeft, 0f);
                percentRect.sizeDelta = new Vector2(84f, rowHeight - 6f);

                _roomBars.Add(new RoomBar
                {
                    Root = rowGo,
                    Label = label,
                    Percent = percent,
                    Fill = fillRect,
                    BarWidth = barWidth
                });

                rowGo.SetActive(false);
            }
        }

        /// <summary>Centre-anchored element placement, shared by the summary text block.</summary>
        private static void CentreElement(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
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
            AnimateSummaryBars();

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

            // The personal-best line gets its own flourish when the run actually beat the record.
            if (_summaryPbPunch > 0f)
            {
                _summaryPbPunch = Mathf.Max(0f, _summaryPbPunch - dt * 2.2f);

                if (_summaryPbText != null)
                    _summaryPbText.rectTransform.localScale = Vector3.one * (1f + _summaryPbPunch * 0.22f);
            }
        }

        /// <summary>
        /// Grows the per-room bars one after another after the summary appears. Cheap: only touches
        /// the rects of rows that are actually visible, and does nothing at all once settled.
        /// </summary>
        private void AnimateSummaryBars()
        {
            if (_summaryRoot == null || !_summaryRoot.activeSelf) return;

            float since = Time.unscaledTime - _summaryRevealAt;

            for (int i = 0; i < _roomBars.Count; i++)
            {
                var bar = _roomBars[i];
                if (bar.Root == null || !bar.Root.activeSelf || bar.Fill == null) continue;

                // Staggered start so the rows read in sequence rather than all at once.
                float start = 0.25f + i * 0.13f;
                float local = Mathf.Clamp01((since - start) / 0.45f);

                // Ease-out so the fill decelerates into its final value.
                float eased = 1f - (1f - local) * (1f - local);
                float shown = Mathf.Lerp(0f, bar.Target, eased);

                var size = bar.Fill.sizeDelta;
                bar.Fill.sizeDelta = new Vector2(bar.BarWidth * (shown / 100f), size.y);
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
