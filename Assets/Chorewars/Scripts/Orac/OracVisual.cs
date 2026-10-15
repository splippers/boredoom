using UnityEngine;

namespace Chorewars.Orac
{
    /// <summary>
    /// The body: a transparent perspex box on a dark base, a 4x3 matrix of internal
    /// lights, a tumbler key in a slot on top, and a slow floating bob. Placeholder-grade
    /// geometry built from primitives; no scene wiring, no external assets.
    /// </summary>
    public class OracVisual : MonoBehaviour
    {
        public const float Width = 0.30f;
        public const float Height = 0.24f;
        public const float Depth = 0.20f;

        public Transform Key { get; private set; }
        public OracLightSequencer Sequencer { get; private set; }

        bool _built;
        Vector3 _home;
        float _bobSeed;
        float _keyBlend;                 // 0 = key out, 1 = key fully inserted
        Vector3 _keyOutLocal;
        Vector3 _keyInLocal;

        public void Build()
        {
            if (_built) return;
            _built = true;

            _home = transform.localPosition;
            _bobSeed = Random.value * 10f;

            var glass = MakeMaterial("ORAC Perspex", FindShader("Standard"),
                new Color(0.55f, 0.80f, 0.85f, 0.18f), true);
            var frame = MakeMaterial("ORAC Frame", FindShader("Unlit/Color"),
                new Color(0.06f, 0.07f, 0.08f, 1f), false);
            var lightMat = MakeMaterial("ORAC Light", FindShader("Unlit/Color"),
                Color.black, false);
            var keyMat = MakeMaterial("ORAC Key", FindShader("Unlit/Color"),
                new Color(1f, 0.60f, 0.15f, 1f), false);

            // Dark base and the perspex shell.
            Primitive(PrimitiveType.Cube, "Base", new Vector3(0f, -0.145f, 0f),
                new Vector3(Width + 0.02f, 0.04f, Depth + 0.02f), frame);
            Primitive(PrimitiveType.Cube, "Shell", new Vector3(0f, 0.02f, 0f),
                new Vector3(Width, Height, Depth), glass);

            // Internal light matrix: 4 across, 3 down, on the front face.
            var lights = new Renderer[12];
            int k = 0;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    float x = -0.105f + col * 0.070f;
                    float y = 0.010f + row * 0.045f;
                    var go = Primitive(PrimitiveType.Sphere, "Light" + k,
                        new Vector3(x, y, 0.096f), Vector3.one * 0.016f, lightMat);
                    lights[k] = go.GetComponent<Renderer>();
                    k++;
                }
            }

            // Key slot and the tumbler key.
            Primitive(PrimitiveType.Cube, "KeySlot", new Vector3(0.105f, 0.135f, 0f),
                new Vector3(0.03f, 0.012f, 0.03f), frame);
            var key = Primitive(PrimitiveType.Cube, "Key", new Vector3(0.105f, 0.215f, 0f),
                new Vector3(0.018f, 0.05f, 0.018f), keyMat);
            Key = key.transform;
            _keyInLocal = new Vector3(0.105f, 0.130f, 0f);
            _keyOutLocal = new Vector3(0.105f, 0.215f, 0f);

            Sequencer = gameObject.GetComponent<OracLightSequencer>();
            if (Sequencer == null) Sequencer = gameObject.AddComponent<OracLightSequencer>();
            Sequencer.Bind(lights);
        }

        public void SetKeyInserted(bool inserted)
        {
            _keyBlendTarget = inserted ? 1f : 0f;
        }

        float _keyBlendTarget;

        void Update()
        {
            if (!_built) return;

            // The float.
            Vector3 p = _home;
            p.y += Mathf.Sin(Time.time * 0.9f + _bobSeed) * 0.015f;
            transform.localPosition = p;

            // Turn to face whoever is speaking (yaw only).
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 to = cam.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f)
                {
                    var look = Quaternion.LookRotation(-to.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look,
                        Time.deltaTime * 1.5f);
                }
            }

            // The key.
            _keyBlend = Mathf.MoveTowards(_keyBlend, _keyBlendTarget, Time.deltaTime * 2.5f);
            if (Key != null)
                Key.localPosition = Vector3.Lerp(_keyOutLocal, _keyInLocal, _keyBlend);
        }

        GameObject Primitive(PrimitiveType type, string name, Vector3 localPos,
            Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && mat != null) renderer.sharedMaterial = mat;
            return go;
        }

        static Shader FindShader(string name)
        {
            var s = Shader.Find(name);
            if (s == null) s = Shader.Find("Unlit/Color");
            return s;
        }

        static Material MakeMaterial(string name, Shader shader, Color color, bool transparent)
        {
            var mat = new Material(shader);
            mat.name = name;
            mat.color = color;
            if (transparent && shader != null && shader.name == "Standard")
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            return mat;
        }
    }
}
