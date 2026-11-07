using Chorewars.AR;
using UnityEngine;

namespace Chorewars.UI
{
    /// <summary>Stationary, gaze-selectable scan controls. Does not require the PC voice relay.</summary>
    public sealed class HouseScanPanel : MonoBehaviour
    {
        [Header("World-space layout")]
        [SerializeField] private float distanceMeters = 1.4f;
        [SerializeField] private float yawOffsetDegrees = -38f;
        [SerializeField] private float canvasScale = 0.0012f;

        private HouseScanController _controller;
        private Camera _camera;
        private RectTransform _canvasRect;
        private GameObject _details;
        private UnityEngine.UI.Text _status;
        private UnityEngine.UI.Button _setup;
        private UnityEngine.UI.Button _retry;
        private GameObject _launcher;
        private Font _font;
        private bool _expanded;
        private readonly Chorewars.HandsFree.GazePanelPlacement _placement =
            new Chorewars.HandsFree.GazePanelPlacement { Anchored = true };

        public bool IsExpanded => _expanded;

        public void Configure(HouseScanController controller, Camera camera)
        {
            if (_controller != null) _controller.Changed -= Refresh;
            _controller = controller;
            _camera = camera;
            if (_canvasRect == null) Build();
            if (_controller != null) _controller.Changed += Refresh;
            Refresh();
        }

        public void Show()
        {
            _expanded = true;
            ApplyVisibility();
            _placement.Release();
            Refresh();
        }

        public void Hide()
        {
            _expanded = false;
            ApplyVisibility();
            _placement.Release();
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.Changed -= Refresh;
        }

        private void Build()
        {
            ModeQuickSwitchPanel.EnsureVrEventSystem();
            var canvasGo = new GameObject("HouseScanCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            _canvasRect = canvasGo.GetComponent<RectTransform>();
            _canvasRect.localScale = Vector3.one * canvasScale;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            canvas.sortingOrder = 20;
            ModeQuickSwitchPanel.TryInstallVrRaycaster(canvasGo);
            var pointer = canvasGo.AddComponent<Chorewars.HandsFree.HeadGazePointer>();
            pointer.Eye = _camera;
            pointer.Raycaster = canvasGo.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _launcher = MakeButton(canvasGo.transform, "HouseScanLauncher", "HOUSE SCAN", Vector2.zero,
                new Vector2(320f, 64f), Show).gameObject;
            _details = new GameObject("ScanDetails", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            _details.transform.SetParent(canvasGo.transform, false);
            var rect = _details.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var background = _details.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.025f, 0.04f, 0.07f, 0.95f);
            background.raycastTarget = false;

            _status = MakeText(_details.transform, "ScanStatus", "", new Vector2(0f, 40f), new Vector2(628f, 284f), 21);
            _status.alignment = TextAnchor.UpperLeft;
            _setup = MakeButton(_details.transform, "OpenSpaceSetup", "OPEN SPACE SETUP", new Vector2(-218f, -153f),
                new Vector2(204f, 62f), () => { if (_controller != null) _controller.RequestSceneCapture(); });
            _retry = MakeButton(_details.transform, "RetryRead", "RETRY READ", new Vector2(0f, -153f),
                new Vector2(204f, 62f), () => { if (_controller != null) _controller.RetryRead(); });
            MakeButton(_details.transform, "CloseScanPanel", "CLOSE", new Vector2(218f, -153f),
                new Vector2(204f, 62f), Hide);
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (_canvasRect == null) return;
            _canvasRect.sizeDelta = _expanded ? new Vector2(680f, 400f) : new Vector2(320f, 64f);
            _details.SetActive(_expanded);
            _launcher.SetActive(!_expanded);
        }

        private void Refresh()
        {
            if (_status == null) return;
            string text = _controller != null ? _controller.Diagnostics : "Spatial services unavailable.";
            if (_status.text != text) _status.text = text;
            bool enabled = _controller != null && !_controller.CaptureBusy;
            _setup.interactable = enabled;
            _retry.interactable = enabled;
        }

        private void LateUpdate()
        {
            if (_canvasRect == null || _camera == null) return;
            _placement.Hold(_canvasRect, _camera.transform, distanceMeters, -0.05f,
                yawOffsetDegrees: yawOffsetDegrees);
        }

        private UnityEngine.UI.Text MakeText(Transform parent, string objectName, string value,
            Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private UnityEngine.UI.Button MakeButton(Transform parent, string objectName, string label,
            Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.12f, 0.3f, 0.4f, 1f);
            image.raycastTarget = true;
            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            MakeText(go.transform, "Label", label, Vector2.zero, size - new Vector2(12f, 8f), 20);
            return button;
        }
    }
}
