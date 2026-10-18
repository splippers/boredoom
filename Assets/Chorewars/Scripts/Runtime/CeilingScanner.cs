using System.Collections.Generic;
using Chorewars.Diagnostics;
using Chorewars.Voice;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Scan a room by looking at the corners of its ceiling: furniture hides the floor, never the ceiling.
    /// </summary>
    /// <remarks>
    /// Start it with a <c>ceiling-scan.request</c> file holding the room's name. A reticle follows the gaze on the
    /// ceiling; hold it still on a corner and a marker drops. Look back at the first marker to finish, or look down
    /// to cancel. The ceiling height is a guess (2.5 m) unless a <c>ceiling-height.request</c> file says otherwise.
    /// The outline becomes a room in the house map, drawn on the dollhouse and in the real room.
    /// </remarks>
    public sealed class CeilingScanner : MonoBehaviour
    {
        const float DwellSeconds = 1.2f;
        const float SteadyMeters = 0.35f;

        HouseMapDriver _driver;
        Transform _head;
        float _ceilingY = CeilingMath.DefaultCeilingMeters;
        float _nextPoll;
        bool _active;
        string _name;

        readonly List<Vector3> _corners = new List<Vector3>();
        readonly DwellButtonLogic _dwell = new DwellButtonLogic { Seconds = DwellSeconds, DrainSeconds = 0.3f };
        Vector3 _steadyAt;
        bool _haveSteady;

        Transform _reticle;
        LineRenderer _line;
        TextMesh _help;
        readonly List<GameObject> _markers = new List<GameObject>();
        Material _material;

        public bool Active => _active;

        public void Configure(HouseMapDriver driver, Transform head)
        {
            _driver = driver;
            _head = head;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) _material = new Material(shader);
        }

        public void Begin(string roomName)
        {
            _name = roomName;
            _active = true;
            _corners.Clear();
            _dwell.Update(false, 10f);
            _haveSteady = false;
            BuildVisuals();
            TelemetryLogger.Info("Ceiling", $"ceiling scan started for \"{roomName}\" (ceiling {_ceilingY:0.00} m)");
        }

        void Update()
        {
            Poll();
            if (!_active || _head == null) return;

            bool onCeiling = CeilingMath.TryHit(_head.position, _head.forward, _ceilingY, out var hit);

            if (!onCeiling)
            {
                _reticle.gameObject.SetActive(false);
                _dwell.Update(false, Time.unscaledDeltaTime);
                _haveSteady = false;
                // Looking down with some corners marked cancels; with none yet it just waits.
                if (_head.forward.y < -0.35f && _corners.Count > 0) Cancel();
                else SetHelp(_head.position + _head.forward * 1.5f, Prompt());
                return;
            }

            _reticle.gameObject.SetActive(true);
            _reticle.position = hit;

            if (!_haveSteady || Vector3.Distance(_steadyAt, hit) > SteadyMeters)
            {
                _steadyAt = hit;
                _haveSteady = true;
                _dwell.Update(false, 10f);   // moved: start the dwell again
            }

            bool fired = _dwell.Update(true, Time.unscaledDeltaTime);
            SetHelp(hit - Vector3.up * 0.25f, Prompt());
            SetReticle(_dwell.Progress01);

            if (!fired) return;

            if (CeilingMath.ClosesLoop(_corners, hit)) { Finish(); return; }
            AddCorner(hit);
        }

        string Prompt() => _corners.Count == 0 ? "Look at a ceiling corner and hold still"
            : _corners.Count < 3 ? $"{_corners.Count} corner(s). Next corner"
            : $"{_corners.Count} corners. Next corner, or look at the first to finish";

        void AddCorner(Vector3 p)
        {
            _corners.Add(p);
            var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(m.GetComponent<Collider>());
            m.transform.position = p;
            m.transform.localScale = Vector3.one * 0.12f;
            var r = m.GetComponent<MeshRenderer>();
            r.sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.3f, 1f, 0.5f) };
            r.shadowCastingMode = ShadowCastingMode.Off;
            _markers.Add(m);

            _line.positionCount = _corners.Count;
            for (int i = 0; i < _corners.Count; i++) _line.SetPosition(i, _corners[i]);
            TelemetryLogger.Info("Ceiling", $"corner {_corners.Count} at ({p.x:0.0}, {p.z:0.0})");
        }

        void Finish()
        {
            var outline = CeilingMath.Outline(_corners);
            bool ok = _driver != null && _driver.AdoptMeasuredRoom(_name, outline, _ceilingY, _head.position);
            TelemetryLogger.Info("Ceiling", ok
                ? $"ceiling scan done: \"{_name}\" is {outline.width:0.0} x {outline.height:0.0} m"
                : "ceiling scan ended: too small to be a room");
            Teardown();
        }

        void Cancel()
        {
            TelemetryLogger.Info("Ceiling", "ceiling scan cancelled (looked down)");
            Teardown();
        }

        void Teardown()
        {
            _active = false;
            foreach (var m in _markers) if (m != null) Destroy(m);
            _markers.Clear();
            if (_reticle != null) Destroy(_reticle.gameObject);
            if (_line != null) Destroy(_line.gameObject);
            if (_help != null) Destroy(_help.gameObject);
        }

        void BuildVisuals()
        {
            Teardown();
            _active = true;

            var r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(r.GetComponent<Collider>());
            r.name = "CeilingReticle";
            r.transform.localScale = Vector3.one * 0.15f;
            r.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = Color.white };
            _reticle = r.transform;

            var lineGo = new GameObject("CeilingOutline");
            _line = lineGo.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = false;
            _line.startWidth = _line.endWidth = 0.04f;
            _line.positionCount = 0;
            if (_material != null) _line.sharedMaterial = _material;
            _line.startColor = _line.endColor = new Color(0.3f, 1f, 0.5f, 0.9f);

            var helpGo = new GameObject("CeilingHelp");
            _help = helpGo.AddComponent<TextMesh>();
            _help.anchor = TextAnchor.MiddleCenter;
            _help.characterSize = 0.03f;
            _help.fontSize = 64;
            _help.color = Color.white;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                _help.font = font;
                helpGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        void SetReticle(float progress)
        {
            float s = Mathf.Lerp(0.10f, 0.28f, progress);
            _reticle.localScale = Vector3.one * s;
            _reticle.GetComponent<MeshRenderer>().sharedMaterial.color = Color.Lerp(Color.white, new Color(0.3f, 1f, 0.5f), progress);
        }

        void SetHelp(Vector3 at, string text)
        {
            if (_help == null) return;
            _help.text = text;
            _help.transform.position = at;
            _help.transform.rotation = Quaternion.LookRotation(at - _head.position, Vector3.up);
        }

        /// <summary>Files dropped in the app's files folder start a scan or set the ceiling height.</summary>
        void Poll()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.5f;

            var height = System.IO.Path.Combine(Application.persistentDataPath, "ceiling-height.request");
            if (System.IO.File.Exists(height))
            {
                try
                {
                    var text = System.IO.File.ReadAllText(height).Trim();
                    System.IO.File.Delete(height);
                    if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h) && h > 1.8f && h < 5f)
                    {
                        _ceilingY = h;
                        TelemetryLogger.Info("Ceiling", $"ceiling height set to {h:0.00} m");
                    }
                }
                catch { }
            }

            var start = System.IO.Path.Combine(Application.persistentDataPath, "ceiling-scan.request");
            if (System.IO.File.Exists(start))
            {
                string name = string.Empty;
                try { name = System.IO.File.ReadAllText(start).Trim(); System.IO.File.Delete(start); } catch { }
                Begin(name);
            }
        }
    }
}
