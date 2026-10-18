using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Chorewars.Core;

namespace Chorewars.UI
{
    /// <summary>
    /// Large world-space launch UI for Quest: IMGUI is not raycastable in VR.
    /// Uses uGUI + Meta OVRRaycaster / OVRInputModule when available so controller lasers work.
    /// </summary>
    public sealed class ModeQuickSwitchPanel : MonoBehaviour
    {
        private static ModeQuickSwitchPanel _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (_instance != null) return;

            var go = new GameObject(nameof(ModeQuickSwitchPanel));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ModeQuickSwitchPanel>();
        }

        private GameObject _canvasRoot;
        private Canvas _canvas;
        private bool _built;
        private bool _buildStarted;

        /// <summary>
        /// Cached HMD/rig camera — never call FindObjectsByType every frame (that can freeze Quest).
        /// </summary>
        private Camera _cachedPlayCamera;

        private static Sprite _pitchStripeSprite;

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _cachedPlayCamera = null;
            ScheduleBuild();
        }

        private void Start()
        {
            ScheduleBuild();
        }

        private void ScheduleBuild()
        {
            if (_built || _buildStarted) return;
            _buildStarted = true;
            StartCoroutine(CoBuildWorldUi());
        }

        private float _nextRuntimeCheckAt;
        private readonly Chorewars.HandsFree.GazePanelPlacement _gazePlacement = new Chorewars.HandsFree.GazePanelPlacement { Anchored = true };

        private IEnumerator CoBuildWorldUi()
        {
            // XR setups often have no Camera.main for the first few frames; wait so World Space UI isn't "black".
            for (var i = 0; i < 180 && GetPlayCamera() == null; i++)
                yield return null;

            BuildWorldUi();
        }

        private void LateUpdate()
        {
            if (_canvasRoot == null) return;

            var cam = GetPlayCamera();
            if (cam == null) return;

            if (_canvas != null)
                _canvas.worldCamera = cam;

            // The runtime flow draws its own menus. This launcher is 1.7 m wide and sits just behind them, so
            // with both up the nearer layer takes every gaze and the other is unreachable, and the text of
            // the two overlaps. The runtime HUD supersedes it; scenes without one still get the launcher.
            if (_canvasRoot.activeSelf && Time.unscaledTime >= _nextRuntimeCheckAt)
            {
                _nextRuntimeCheckAt = Time.unscaledTime + 0.5f;
                if (FindFirstObjectByType<Chorewars.Runtime.PlaceholderHud>(FindObjectsInactive.Include) != null)
                {
                    _canvasRoot.SetActive(false);
                    Debug.Log("[launcher] runtime HUD present - launcher panel hidden");
                }
            }

            // This panel is all buttons, so it holds still for head gaze to reach them; a copy that
            // followed the head would keep the same button under the middle of the view forever.
            _gazePlacement.Hold(_canvasRoot.transform, cam.transform, 1.65f, 0.08f);
        }

        private Camera GetPlayCamera()
        {
            if (_cachedPlayCamera != null && _cachedPlayCamera.isActiveAndEnabled)
                return _cachedPlayCamera;

            _cachedPlayCamera = FindBestCameraSlow();
            return _cachedPlayCamera;
        }

        private static Camera FindBestCameraSlow()
        {
            if (Camera.main != null) return Camera.main;

            try
            {
                var tagged = GameObject.FindGameObjectWithTag("MainCamera");
                if (tagged != null && tagged.TryGetComponent<Camera>(out var tc) && tc.enabled)
                    return tc;
            }
            catch
            {
                // Tag missing in project — ignore.
            }

            var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            if (cams == null || cams.Length == 0) return null;

            Camera best = null;
            var bestScore = int.MinValue;
            foreach (var c in cams)
            {
                if (c == null || !c.enabled) continue;

                var score = 0;
                if (c.CompareTag("MainCamera")) score += 1000;
                if (c.stereoTargetEye == StereoTargetEyeMask.Both) score += 100;
                score += Mathf.RoundToInt(c.depth * 10f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            return best;
        }

        private void BuildWorldUi()
        {
            if (_built) return;
            _built = true;

            EnsureVrEventSystem();

            _canvasRoot = new GameObject("ModeQuickSwitchCanvas");
            _canvasRoot.transform.SetParent(transform, false);

            _canvas = _canvasRoot.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = GetPlayCamera();

            // Canvas already adds RectTransform — do not AddComponent<RectTransform> again (Android log:
            // "Can't add component RectTransform ... already added"), which breaks BuildWorldUi.
            var rt = _canvasRoot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1280, 860);

            var scaler = _canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            TryInstallVrRaycaster(_canvasRoot);

            // Controllers are not a given here - the player's hands are usually full - so the
            // same GraphicRaycaster is driven by head gaze instead of by a controller laser.
            // Harmless when a Meta raycaster was installed instead: it simply finds no
            // GraphicRaycaster and idles.
            var gaze = _canvasRoot.AddComponent<Chorewars.HandsFree.HeadGazePointer>();
            gaze.Eye = GetPlayCamera();
            gaze.Raycaster = _canvasRoot.GetComponent<GraphicRaycaster>();

            var panel = CreateUiChild(_canvasRoot.transform, "Panel", stretch: true);

            var pitchBg = CreateUiChild(panel.transform, "PitchStripe", stretch: true);
            var pitchImg = pitchBg.gameObject.AddComponent<Image>();
            BuildPitchBackground(pitchImg);

            AddPitchWhiteLines(panel);

            var content = CreateUiChild(panel.transform, "Content", stretch: true);
            content.offsetMin = new Vector2(44, 44);
            content.offsetMax = new Vector2(-44, -44);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 12);
            layout.spacing = 22;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            AddSwosLeagueBanner(content);
            AddSwosMainTitle(content);
            AddSubtitle(content,
                "MAIN MENU — pick your fixture. Laser + trigger to select.");
            AddBigButton(content, "HooverMode", "HOME — HOOVER FC", SwosKitRed());
            AddBigButton(content, "MowingMode", "AWAY — MOWING UNITED", SwosKitBlue());
            AddBigButton(content, "__RELOAD__", "REPLAY MATCH (reload scene)", SwosKitNeutral());

            var hint = CreateUiChild(content.transform, "Hint", stretch: false);
            var hintRt = hint.GetComponent<RectTransform>();
            hintRt.sizeDelta = new Vector2(0, 96);
            var hintText = hint.gameObject.AddComponent<Text>();
            hintText.font = GetUiFont();
            hintText.fontSize = 26;
            hintText.color = new Color(0.92f, 0.95f, 0.78f, 0.92f);
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintText.verticalOverflow = VerticalWrapMode.Overflow;
            hintText.raycastTarget = false;
            hintText.text =
                "8-bit pitch vibes — controller laser on a kit bar, then trigger.\n" +
                "(Hands-only pinch UI needs a hand ray; coming next.)";

            var closeRow = CreateUiChild(content.transform, "CloseRow", stretch: false);
            var closeRt = closeRow.GetComponent<RectTransform>();
            closeRt.sizeDelta = new Vector2(0, 76);
            var closeBtn = closeRow.gameObject.AddComponent<Button>();
            var closeImg = closeRow.gameObject.AddComponent<Image>();
            closeImg.color = new Color(0.12f, 0.12f, 0.14f, 1f);
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(() => _canvasRoot.SetActive(false));

            var closeTextGo = new GameObject("Label");
            closeTextGo.transform.SetParent(closeRow, false);
            var closeTextRt = closeTextGo.AddComponent<RectTransform>();
            closeTextRt.anchorMin = Vector2.zero;
            closeTextRt.anchorMax = Vector2.one;
            closeTextRt.offsetMin = Vector2.zero;
            closeTextRt.offsetMax = Vector2.zero;
            var closeText = closeTextGo.AddComponent<Text>();
            closeText.font = hintText.font;
            closeText.fontSize = 32;
            closeText.fontStyle = FontStyle.Bold;
            closeText.color = new Color(1f, 0.92f, 0.35f, 1f);
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.text = "FULL TIME — hide panel";
            ApplySwosTextOutline(closeText, 2f);

            _canvasRoot.transform.localScale = Vector3.one * 0.00135f;

            pitchBg.transform.SetAsFirstSibling();
            content.transform.SetAsLastSibling();
        }

        private static void BuildPitchBackground(Image pitchImg)
        {
            pitchImg.raycastTarget = false;

            var sprite = GetPitchStripeSprite();
            if (sprite != null)
            {
                pitchImg.sprite = sprite;
                pitchImg.type = Image.Type.Tiled;
                pitchImg.color = Color.white;
            }
            else
            {
                pitchImg.sprite = null;
                pitchImg.type = Image.Type.Simple;
                pitchImg.color = new Color(0.13f, 0.46f, 0.22f, 1f);
            }
        }

        private static Font GetUiFont() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        private static Sprite GetPitchStripeSprite()
        {
            if (_pitchStripeSprite != null) return _pitchStripeSprite;

            const int w = 64;
            const int h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;

            var g1 = new Color(0.14f, 0.48f, 0.22f, 1f);
            var g2 = new Color(0.11f, 0.40f, 0.19f, 1f);

            for (int y = 0; y < h; y++)
            {
                bool stripe = (y / 6) % 2 == 0;
                var c = stripe ? g1 : g2;
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, c);
            }

            // Keep CPU-readable: makeNoLongerReadable breaks some Android / XR builds for runtime sprites.
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            try
            {
                _pitchStripeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }
            catch
            {
                _pitchStripeSprite = null;
            }

            return _pitchStripeSprite;
        }

        private static void AddPitchWhiteLines(RectTransform panel)
        {
            const float m = 14f;
            const float thick = 9f;
            var white = new Color(0.96f, 0.97f, 0.94f, 0.93f);

            void Strip(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
            {
                var r = CreateUiChild(panel.transform, name, stretch: true);
                r.anchorMin = anchorMin;
                r.anchorMax = anchorMax;
                r.offsetMin = offsetMin;
                r.offsetMax = offsetMax;
                var img = r.gameObject.AddComponent<Image>();
                img.color = white;
                img.raycastTarget = false;
            }

            Strip("TouchlineTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(m, -thick), new Vector2(-m, 0f));
            Strip("TouchlineBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(m, 0f), new Vector2(-m, thick));
            Strip("TouchlineLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, m), new Vector2(thick, -m));
            Strip("TouchlineRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thick, m), new Vector2(0f, -m));

            var mid = CreateUiChild(panel.transform, "HalfwayLine", stretch: false);
            mid.anchorMin = new Vector2(0.5f, 0.5f);
            mid.anchorMax = new Vector2(0.5f, 0.5f);
            mid.pivot = new Vector2(0.5f, 0.5f);
            mid.sizeDelta = new Vector2(780f, thick * 0.65f);
            var midImg = mid.gameObject.AddComponent<Image>();
            midImg.color = white;
            midImg.raycastTarget = false;
        }

        private static void AddSwosLeagueBanner(RectTransform parent)
        {
            var rt = CreateUiChild(parent, "LeagueBanner", stretch: false);
            rt.sizeDelta = new Vector2(0, 52);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = GetUiFont();
            t.fontSize = 30;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(1f, 0.95f, 0.45f, 1f);
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.text = "★ SENSIBLE CHORES LEAGUE ★";
            ApplySwosTextOutline(t, 2f);
        }

        private static void AddSwosMainTitle(RectTransform parent)
        {
            var rt = CreateUiChild(parent, "MainTitle", stretch: false);
            rt.sizeDelta = new Vector2(0, 132);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = GetUiFont();
            t.fontSize = 96;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(1f, 0.96f, 0.2f, 1f);
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.text = "BOREDOOM\n<size=42>CUP '26</size>";

            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.78f);
            sh.effectDistance = new Vector2(6f, -6f);

            ApplySwosTextOutline(t, 3f);
        }

        private static void ApplySwosTextOutline(Text t, float dist)
        {
            var o = t.gameObject.GetComponent<Outline>();
            if (o == null) o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.02f, 0.02f, 0.02f, 0.95f);
            o.useGraphicAlpha = true;
            o.effectDistance = new Vector2(dist, -dist);
        }

        private static Color SwosKitRed() => new Color(0.78f, 0.12f, 0.14f, 1f);
        private static Color SwosKitBlue() => new Color(0.1f, 0.22f, 0.72f, 1f);
        private static Color SwosKitNeutral() => new Color(0.22f, 0.24f, 0.28f, 1f);

        private static RectTransform CreateUiChild(Transform parent, string name, bool stretch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            return rect;
        }

        private static void AddSubtitle(RectTransform parent, string text)
        {
            var rt = CreateUiChild(parent, "Subtitle", stretch: false);
            rt.sizeDelta = new Vector2(0, 82);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = GetUiFont();
            t.fontSize = 32;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(0.93f, 0.98f, 0.88f, 0.96f);
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            ApplySwosTextOutline(t, 1.5f);
        }

        private void AddBigButton(RectTransform parent, string sceneKey, string label, Color bg)
        {
            var row = CreateUiChild(parent, $"Btn_{label}", stretch: false);
            row.sizeDelta = new Vector2(0, 122);

            var img = row.gameObject.AddComponent<Image>();
            img.color = bg;

            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(bg.r * 1.08f, bg.g * 1.08f, bg.b * 1.08f, 1f);
            colors.pressedColor = new Color(bg.r * 0.85f, bg.g * 0.85f, bg.b * 0.85f, 1f);
            btn.colors = colors;

            string sceneName = sceneKey;
            if (sceneKey == "__RELOAD__")
            {
                btn.onClick.AddListener(() =>
                {
                    var active = SceneManager.GetActiveScene().name;
                    SwitchTo(active);
                });
            }
            else
            {
                btn.onClick.AddListener(() => SwitchTo(sceneName));
            }

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(row, false);
            var tr = textGo.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            var txt = textGo.AddComponent<Text>();
            txt.font = GetUiFont();
            txt.fontSize = 36;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.text = label;
            ApplySwosTextOutline(txt, 2f);
        }

        private static void SwitchTo(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return;

            BootstrapSceneRouter.SetLastModeSceneName(sceneName);
            if (Application.CanStreamedLevelBeLoaded(sceneName))
                SceneManager.LoadScene(sceneName);
        }

        public static void EnsureVrEventSystem()
        {
            var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            EventSystem es = systems.Length > 0 ? systems[0] : null;
            for (int i = 1; i < systems.Length; i++)
            {
                if (systems[i] != null)
                    Destroy(systems[i].gameObject);
            }

            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
                DontDestroyOnLoad(esGo);
            }

            // If a module is already present, keep it. Destroy() is deferred to the end of the
            // frame, so destroying the module and immediately re-adding it makes the "already has
            // module" check inside the Try* helpers see the doomed component and skip adding a
            // replacement — which would leave the EventSystem with no input module at all when a
            // second caller (e.g. the placeholder HUD) ensures the EventSystem in the same frame.
            if (es.GetComponent<BaseInputModule>() != null) return;

            if (!TryAddVrInputModule(es.gameObject) && !TryAddInputSystemUiInputModule(es.gameObject))
                es.gameObject.AddComponent<StandaloneInputModule>();
        }

        private static bool TryAddInputSystemUiInputModule(GameObject go)
        {
            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem", throwOnError: false);
            if (t == null) return false;
            if (go.GetComponent(t) != null) return true;
            go.AddComponent(t);
            return true;
        }

        public static void TryInstallVrRaycaster(GameObject canvasGo)
        {
            foreach (var gr in canvasGo.GetComponents<GraphicRaycaster>())
                Destroy(gr);

            if (!TryAddOvrRaycaster(canvasGo))
                canvasGo.AddComponent<GraphicRaycaster>();
        }

        private static bool TryAddVrInputModule(GameObject go)
        {
            var t = Type.GetType("UnityEngine.EventSystems.OVRInputModule, Oculus.VR", throwOnError: false)
                    ?? Type.GetType("UnityEngine.EventSystems.OVRInputModule, Meta.XR", throwOnError: false)
                    ?? FindMetaUiTypeByAssemblyLookup("UnityEngine.EventSystems.OVRInputModule");
            if (t == null) return false;
            if (go.GetComponent(t) != null) return true;
            go.AddComponent(t);
            return true;
        }

        private static bool TryAddOvrRaycaster(GameObject go)
        {
            var t = Type.GetType("UnityEngine.EventSystems.OVRRaycaster, Oculus.VR", throwOnError: false)
                    ?? Type.GetType("UnityEngine.EventSystems.OVRRaycaster, Meta.XR", throwOnError: false)
                    ?? FindMetaUiTypeByAssemblyLookup("UnityEngine.EventSystems.OVRRaycaster");
            if (t == null) return false;
            if (go.GetComponent(t) != null) return true;
            go.AddComponent(t);
            return true;
        }

        /// <summary>
        /// Resolves Meta UI types with cheap <see cref="Assembly.GetType(string)"/> calls only.
        /// Do NOT use <c>Assembly.GetTypes()</c> on every assembly — that can take seconds on Quest and trigger ANR.
        /// </summary>
        private static Type FindMetaUiTypeByAssemblyLookup(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n;
                try
                {
                    n = asm.GetName().Name ?? string.Empty;
                }
                catch
                {
                    continue;
                }

                if (n.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("Meta", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                try
                {
                    var t = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
                    if (t != null) return t;
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }
    }
}
