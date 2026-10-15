using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chorewars.HandsFree
{
    /// <summary>
    /// Head-gaze selection for ordinary world-space uGUI: look at a control and hold.
    ///
    /// This exists because the obvious VR path - a controller laser through OVRRaycaster -
    /// needs a controller, and the player's hands are full. The uGUI GraphicRaycaster is
    /// perfectly capable of hit-testing on its own; all it lacks is something to point with.
    /// Here that something is the middle of the player's view.
    ///
    /// Feedback is deliberately borrowed rather than invented: hovering sets the EventSystem
    /// selection, so a Button shows its own highlighted colour, and the dwell itself is shown
    /// by the reticle ring.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class HeadGazePointer : MonoBehaviour
    {
        public bool Enabled = true;

        public Camera Eye;
        public GraphicRaycaster Raycaster;

        /// <summary>How long to hold a look before it activates, in seconds.</summary>
        public float DwellSeconds = 1.15f;

        /// <summary>A brief glance away does not lose your place.</summary>
        public float ReleaseGrace = 0.35f;

        public bool ShowReticle = true;

        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        PointerEventData _pointer;

        GameObject _hover;
        GameObject _selectedByUs;
        float _dwell;
        float _nextFireAt;

        Image _reticle;
        RectTransform _reticleRect;
        Image _cursor;
        RectTransform _cursorRect;
        float _nextLogAt;
        static Sprite _ringSprite;

        public GameObject Hover { get { return _hover; } }

        public float Dwell01
        {
            get { return DwellSeconds <= 0f ? 0f : Mathf.Clamp01(_dwell / DwellSeconds); }
        }

        void Update()
        {
            if (!Enabled) return;

            var events = EventSystem.current;
            if (events == null) return;
            if (Raycaster == null) return;
            if (Eye == null)
            {
                Eye = Camera.main;
                if (Eye == null) return;
            }

            if (_pointer == null) _pointer = new PointerEventData(events);

            // The middle of the view. In XR that is the seam between the eyes, which
            // ScreenPointToRay maps to the centre of the view frustum - where the head points.
            // The camera's own pixel rect is the reference the raycaster uses, and in XR it is the
            // eye buffer rather than Screen, so aim from the camera, not from Screen.
            _pointer.position = Eye.pixelRect.center;
            _pointer.delta = Vector2.zero;

            var hit = PickControl();
            UpdateCursor();
            LogState(hit);

            if (hit == null)
            {
                if (_dwell > 0f)
                {
                    _dwell -= Time.unscaledDeltaTime / Mathf.Max(0.05f, ReleaseGrace);
                    if (_dwell > 0f)
                    {
                        UpdateReticle();
                        return;
                    }
                }

                _dwell = 0f;
                if (_hover != null) Debug.Log("[gaze] " + name + " hover lost (" + PathOf(_hover) + ")");
                _hover = null;
                UpdateReticle();
                ReleaseSelection(events);
                return;
            }

            if (hit != _hover)
            {
                Debug.Log("[gaze] " + name + " hover -> " + PathOf(hit));
                _hover = hit;
                _dwell = 0f;
                events.SetSelectedGameObject(hit);
                _selectedByUs = hit;
            }

            UpdateReticle();

            if (Time.unscaledTime < _nextFireAt) return;

            _dwell += Time.unscaledDeltaTime;
            if (_dwell < DwellSeconds) return;

            _nextFireAt = Time.unscaledTime + 0.6f;
            _dwell = 0f;
            Activate(hit);
        }

        GameObject PickControl()
        {
            if (_pointer == null) return null;

            _hits.Clear();
            Raycaster.Raycast(_pointer, _hits);

            for (int i = 0; i < _hits.Count; i++)
            {
                var go = _hits[i].gameObject;
                if (go == null) continue;

                var selectable = go.GetComponent<Selectable>();
                if (selectable != null && !selectable.IsInteractable()) continue;

                // Only things that can actually be activated; ignore decorative graphics.
                if (selectable == null && go.GetComponent<IPointerClickHandler>() == null) continue;

                return go;
            }

            return null;
        }

        static string PathOf(GameObject go)
        {
            if (go == null) return "-";
            string path = go.name;
            for (var t = go.transform.parent; t != null && path.Length < 120; t = t.parent) path = t.name + "/" + path;
            return path;
        }

        void Activate(GameObject go)
        {
            if (go == null) return;
            Debug.Log("[gaze] " + name + " ACTIVATE " + PathOf(go));

            var button = go.GetComponent<Button>();
            if (button != null && button.IsInteractable())
            {
                button.onClick.Invoke();
                return;
            }

            if (_pointer != null) ExecuteEvents.Execute(go, _pointer, ExecuteEvents.submitHandler);
        }

        /// <summary>Only ever clear our own selection, so a second pointer on another canvas cannot fight us.</summary>
        void ReleaseSelection(EventSystem events)
        {
            if (_selectedByUs == null) return;
            if (events.currentSelectedGameObject == _selectedByUs) events.SetSelectedGameObject(null);
            _selectedByUs = null;
        }

        // ---- gaze cursor + diagnostics ----------------------------------------------------

        /// <summary>
        /// A small ring where the view centre meets the canvas, shown whether or not anything is under
        /// it. Without it a player cannot tell "nothing is selectable" from "I am not aiming at it".
        /// </summary>
        void UpdateCursor()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || Eye == null) return;

            if (_cursor == null)
            {
                var go = new GameObject("GazeCursor", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                _cursorRect = go.GetComponent<RectTransform>();
                _cursorRect.sizeDelta = new Vector2(28f, 28f);
                _cursor = go.GetComponent<Image>();
                _cursor.sprite = RingSprite();
                _cursor.color = new Color(1f, 1f, 1f, 0.7f);
                _cursor.raycastTarget = false;
            }

            var ray = Eye.ScreenPointToRay(_pointer.position);
            var plane = new Plane(-canvas.transform.forward, canvas.transform.position);
            float enter;
            bool onCanvas = plane.Raycast(ray, out enter);
            _cursor.enabled = onCanvas;
            if (!onCanvas) return;

            _cursorRect.position = ray.GetPoint(enter);
            _cursorRect.SetAsLastSibling();
        }

        /// <summary>Why a gaze does not land is invisible from outside the headset, so say it every couple of seconds.</summary>
        void LogState(GameObject hit)
        {
            if (Time.unscaledTime < _nextLogAt) return;
            _nextLogAt = Time.unscaledTime + 2f;

            string top = _hits.Count > 0 && _hits[0].gameObject != null ? _hits[0].gameObject.name : "-";
            Debug.Log("[gaze] " + name + " px=" + _pointer.position + " cam=" + Eye.pixelWidth + "x" + Eye.pixelHeight
                      + " hits=" + _hits.Count + " top=" + top + " pick=" + (hit != null ? hit.name : "-")
                      + " dwell=" + Dwell01.ToString("0.00"));
        }

        // ---- reticle ----------------------------------------------------------------------

        void UpdateReticle()
        {
            if (!ShowReticle || _hover == null)
            {
                if (_reticle != null) _reticle.enabled = false;
                return;
            }

            if (_reticle == null) BuildReticle();
            if (_reticle == null) return;

            var hoverRect = _hover.transform as RectTransform;
            _reticle.enabled = true;
            _reticle.fillAmount = Dwell01;

            if (hoverRect != null)
                _reticleRect.position = hoverRect.TransformPoint(hoverRect.rect.center);
        }

        void BuildReticle()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("GazeReticle", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);

            _reticleRect = go.GetComponent<RectTransform>();
            _reticleRect.anchorMin = new Vector2(0.5f, 0.5f);
            _reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
            _reticleRect.pivot = new Vector2(0.5f, 0.5f);
            _reticleRect.sizeDelta = new Vector2(64f, 64f);

            _reticle = go.GetComponent<Image>();
            _reticle.sprite = RingSprite();
            _reticle.color = new Color(0.55f, 1f, 0.72f, 0.95f);
            _reticle.raycastTarget = false;
            _reticle.type = Image.Type.Filled;
            _reticle.fillMethod = Image.FillMethod.Radial360;
            _reticle.fillOrigin = (int)Image.Origin360.Top;
            _reticle.fillClockwise = true;
            _reticle.fillAmount = 0f;

            _reticleRect.SetAsLastSibling();
        }

        /// <summary>A thin annulus, generated once, so this component needs no assets.</summary>
        static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;

            const int size = 128;
            const float inner = 0.80f;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "GazeRing",
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

                    float a = Mathf.Clamp01((d - inner) / 0.05f) * Mathf.Clamp01((1f - d) / 0.05f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);

            _ringSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _ringSprite;
        }
    }
}
