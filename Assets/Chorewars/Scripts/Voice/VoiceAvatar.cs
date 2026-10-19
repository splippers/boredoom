using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chorewars.Voice
{
    /// <summary>
    /// A floating, stylised head with curly fiery-red hair that is Claudia's face in the headset: it blinks, looks
    /// at the player, moves its mouth with the voice, tilts to listen and glances up while thinking.
    /// </summary>
    /// <remarks>
    /// Built entirely from code (spheres, merged into a handful of meshes so it costs few draw calls) and drawn
    /// with the same flat unlit colour the panel uses. Facing is +Z; the owner turns it to the player.
    /// </remarks>
    public sealed class VoiceAvatar
    {
        const float Rx = 0.075f, Ry = 0.095f, Rz = 0.082f;   // head ellipsoid

        public readonly Transform Root;
        readonly Transform _head;
        readonly Transform _lidL, _lidR, _browL, _browR, _eyes, _lower, _inner;
        readonly Vector3 _lowerRest, _innerRest, _browLRest, _browRRest, _lidLRest, _lidRRest;

        float _mouth, _blink, _nextBlink = 2f, _t, _nextGlance, _glanceX, _glanceY;

        static Mesh s_sphere;
        static Material Mat(Color c) =>
            new Material(Shader.Find("Unlit/Color")) { color = c };

        public VoiceAvatar(Transform parent)
        {
            Root = new GameObject("VoiceAvatar").transform;
            Root.SetParent(parent, false);
            _head = new GameObject("Head").transform;
            _head.SetParent(Root, false);

            var skin = new Color(0.91f, 0.73f, 0.62f);
            var skinShade = new Color(0.80f, 0.60f, 0.50f);
            var white = new Color(0.97f, 0.97f, 0.97f);
            var lipRed = new Color(0.72f, 0.14f, 0.24f);
            var mouthDark = new Color(0.22f, 0.04f, 0.07f);
            var iris = new Color(0.30f, 0.16f, 0.08f);
            var black = new Color(0.02f, 0.02f, 0.03f);
            var gold = new Color(0.95f, 0.78f, 0.25f);

            // ----- static face and neck -----
            var face = new List<Part>
            {
                P(0, 0, 0, Rx, Ry, Rz),                                  // head
                P(0, -0.082f, 0.004f, 0.036f, 0.06f, 0.034f),            // neck
                P(0, -0.062f, 0.04f, 0.045f, 0.03f, 0.04f),              // chin and jaw
                P(0, -0.012f, F(0, -0.012f) + 0.009f, 0.0085f, 0.011f, 0.0095f), // nose
            };
            AddMesh("Face", face, skin);

            var shade = new List<Part>
            {
                P(-0.0085f, -0.022f, F(0, -0.022f) + 0.005f, 0.0045f, 0.0045f, 0.004f), // nostrils
                P(0.0085f, -0.022f, F(0, -0.022f) + 0.005f, 0.0045f, 0.0045f, 0.004f),
                P(0, -0.108f, 0.02f, 0.04f, 0.03f, 0.035f),              // under the chin
            };
            AddMesh("Shade", shade, skinShade);

            AddMesh("Blush", new List<Part>
            {
                P(-0.043f, -0.022f, F(-0.043f, -0.022f) + 0.001f, 0.015f, 0.011f, 0.004f),
                P(0.043f, -0.022f, F(0.043f, -0.022f) + 0.001f, 0.015f, 0.011f, 0.004f),
            }, new Color(0.98f, 0.58f, 0.58f));

            AddMesh("Earrings", new List<Part>
            {
                P(-0.078f, -0.03f, 0.0f, 0.006f, 0.006f, 0.006f),
                P(0.078f, -0.03f, 0.0f, 0.006f, 0.006f, 0.006f),
                P(-0.078f, -0.042f, 0.0f, 0.004f, 0.004f, 0.004f),
                P(0.078f, -0.042f, 0.0f, 0.004f, 0.004f, 0.004f),
            }, gold);

            // ----- eyes -----
            _eyes = new GameObject("Eyes").transform;
            _eyes.SetParent(_head, false);
            foreach (float s in new[] { -1f, 1f })
            {
                float x = 0.032f * s, y = 0.014f, z = F(x, y);
                var eye = new GameObject(s < 0 ? "EyeL" : "EyeR").transform;
                eye.SetParent(_head, false);
                eye.localPosition = new Vector3(x, y, z - 0.003f);
                AddMesh("White", new List<Part> { P(0, 0, 0, 0.0125f, 0.0105f, 0.006f) }, white, eye);
                var look = new GameObject("Look").transform;
                look.SetParent(eye, false);
                look.name = "Look";
                AddMesh("Iris", new List<Part> { P(0, 0, 0.0045f, 0.0072f, 0.0072f, 0.003f) }, iris, look);
                AddMesh("Pupil", new List<Part> { P(0, 0, 0.0068f, 0.0036f, 0.0036f, 0.002f) }, black, look);
                AddMesh("Glint", new List<Part> { P(0.0022f, 0.0025f, 0.0085f, 0.0014f, 0.0014f, 0.001f) }, white, look);

                var lid = new GameObject("Lid").transform;
                lid.SetParent(_head, false);
                lid.localPosition = new Vector3(x, y + 0.011f, z + 0.0005f);
                AddMesh("Lid", new List<Part> { P(0, 0, 0, 0.0135f, 0.0112f, 0.0066f) }, skin, lid);
                AddMesh("Lash", new List<Part> { P(0, -0.0105f, 0.0012f, 0.0145f, 0.0017f, 0.005f), P(s * 0.0125f, -0.0095f, 0.0005f, 0.003f, 0.0028f, 0.004f) }, black, lid);
                if (s < 0) { _lidL = lid; _lidLRest = lid.localPosition; _browL = null; }
                else { _lidR = lid; _lidRRest = lid.localPosition; }
            }
            _browL = Brow("BrowL", -1f); _browLRest = _browL.localPosition;
            _browR = Brow("BrowR", 1f); _browRRest = _browR.localPosition;

            // ----- mouth -----
            float my = -0.046f, mz = F(0, my);
            _inner = new GameObject("Inner").transform;
            _inner.SetParent(_head, false);
            _inner.localPosition = new Vector3(0, my - 0.004f, mz + 0.001f);
            AddMesh("Inner", new List<Part> { P(0, 0, 0, 0.016f, 0.008f, 0.003f) }, mouthDark, _inner);
            _innerRest = _inner.localPosition;

            var upper = new GameObject("UpperLip").transform;
            upper.SetParent(_head, false);
            upper.localPosition = new Vector3(0, my + 0.0035f, mz + 0.003f);
            AddMesh("Upper", new List<Part>
            {
                P(-0.007f, 0, 0, 0.0095f, 0.0042f, 0.0055f), P(0.007f, 0, 0, 0.0095f, 0.0042f, 0.0055f),
                P(-0.0155f, 0.0012f, -0.001f, 0.004f, 0.0035f, 0.004f), P(0.0155f, 0.0012f, -0.001f, 0.004f, 0.0035f, 0.004f),
            }, lipRed, upper);

            _lower = new GameObject("LowerLip").transform;
            _lower.SetParent(_head, false);
            _lower.localPosition = new Vector3(0, my - 0.0045f, mz + 0.003f);
            _lowerRest = _lower.localPosition;
            AddMesh("Lower", new List<Part> { P(0, 0, 0, 0.0135f, 0.0052f, 0.0058f) }, lipRed, _lower);

            // ----- hair -----
            BuildHair();
        }

        // ---------------- building ----------------

        struct Part { public Vector3 pos, scale; }
        static Part P(float x, float y, float z, float sx, float sy, float sz) =>
            new Part { pos = new Vector3(x, y, z), scale = new Vector3(sx, sy, sz) };

        /// <summary>Z of the head's front surface at (x, y).</summary>
        static float F(float x, float y)
        {
            float v = 1f - (x / Rx) * (x / Rx) - (y / Ry) * (y / Ry);
            return Rz * Mathf.Sqrt(Mathf.Max(0f, v));
        }

        Transform Brow(string name, float side)
        {
            var b = new GameObject(name).transform;
            b.SetParent(_head, false);
            float x = 0.033f * side, y = 0.043f;
            b.localPosition = new Vector3(x, y, F(x, y) + 0.002f);
            b.localRotation = Quaternion.Euler(0, 0, -side * 9f);
            AddMesh("Brow", new List<Part> { P(0, 0, 0, 0.0165f, 0.0032f, 0.0045f) }, new Color(0.04f, 0.03f, 0.04f), b);
            return b;
        }

        void BuildHair()
        {
            var rng = new System.Random(7);
            float R() => (float)rng.NextDouble();
            var dark = new List<Part>();
            var mid = new List<Part>();
            var glossy = new List<Part>();

            // crown and back: curls thick over everything behind the face
            for (int i = 0; i < 150; i++)
            {
                var d = new Vector3(R() * 2 - 1, R() * 2 - 0.55f, R() * 2 - 1.15f);
                if (d.sqrMagnitude < 0.01f) continue;
                d.Normalize();
                if (d.z > 0.25f && d.y < 0.55f) continue;   // keep the face clear
                float k = 1.12f + R() * 0.12f;
                float r = 0.017f + R() * 0.014f;
                var part = P(d.x * Rx * k, d.y * Ry * k, d.z * Rz * k, r, r, r);
                (i % 3 == 0 ? glossy : i % 2 == 0 ? mid : dark).Add(part);
            }

            // hairline curls across the forehead
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.Lerp(-82f, 82f, i / 10f) * Mathf.Deg2Rad;
                float e = (46f + R() * 8f) * Mathf.Deg2Rad;
                float r = 0.016f + R() * 0.007f;
                var part = P(Mathf.Sin(a) * Mathf.Cos(e) * Rx * 1.12f, Mathf.Sin(e) * Ry * 1.1f + 0.004f,
                             Mathf.Cos(a) * Mathf.Cos(e) * Rz * 1.12f, r, r, r);
                (i % 2 == 0 ? dark : mid).Add(part);
            }

            // ringlets: spirals that fall past the shoulders on both sides
            foreach (float s in new[] { -1f, 1f })
            {
                for (int k = 0; k < 5; k++)
                {
                    float a = (82f + k * 20f) * Mathf.Deg2Rad;           // from the temple round towards the back
                    float phase0 = R() * 6.28f;
                    for (int j = 0; j < 11; j++)
                    {
                        float t = j / 10f;
                        float ph = phase0 + j * 1.3f;
                        float y = 0.015f - t * 0.2f;
                        float spread = 1.08f + t * 0.25f;
                        float x = s * Mathf.Sin(a) * Rx * spread + Mathf.Cos(ph) * 0.013f;
                        float z = Mathf.Cos(a) * Rz * 1.05f + Mathf.Sin(ph) * 0.013f - t * 0.012f;
                        float r = 0.0185f - t * 0.003f;
                        (j % 3 == 0 ? glossy : j % 2 == 0 ? mid : dark).Add(P(x, y, z, r, r, r));
                    }
                }
            }

            AddMesh("HairDark", dark, new Color(0.45f, 0.08f, 0.02f));
            AddMesh("HairMid", mid, new Color(0.75f, 0.17f, 0.04f));
            AddMesh("HairGloss", glossy, new Color(0.98f, 0.45f, 0.12f));
        }

        void AddMesh(string name, List<Part> parts, Color colour, Transform parent = null)
        {
            if (parts.Count == 0) return;
            if (s_sphere == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s_sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(tmp);
            }

            var combine = new CombineInstance[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                combine[i].mesh = s_sphere;
                combine[i].transform = Matrix4x4.TRS(parts[i].pos, Quaternion.identity, parts[i].scale * 2f);   // primitive radius is 0.5
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(combine, true, true);

            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _head, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mat(colour);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ---------------- life ----------------

        /// <param name="speaking">How loud the voice is right now (RMS).</param>
        /// <param name="hearing">The player is talking.</param>
        /// <param name="thinking">Claude is working out an answer.</param>
        public void Tick(float dt, float speakingRms, bool hearing, bool thinking)
        {
            _t += dt;
            _mouth = MouthMotion.Next(_mouth, speakingRms, dt);
            bool speaking = speakingRms > 0.004f;

            // float and sway
            Root.localPosition = new Vector3(0, Mathf.Sin(_t * 1.3f) * 0.012f, 0);
            float nod = speaking ? Mathf.Sin(_t * 6f) * 2.5f * _mouth : 0f;
            float tilt = hearing ? 9f : Mathf.Sin(_t * 0.7f) * 2.5f;
            float turn = Mathf.Sin(_t * 0.5f) * 4f;
            _head.localRotation = Quaternion.Euler(nod + (thinking ? -4f : 0f), turn, tilt);

            // blink
            if (_t >= _nextBlink) { _blink = 1e-3f; _nextBlink = _t + 2f + Mathf.Abs(Mathf.Sin(_t * 12.9898f)) * 3.5f; }
            float close = 0f;
            if (_blink > 0f)
            {
                _blink += dt / 0.16f;
                close = _blink < 0.5f ? _blink * 2f : Mathf.Max(0f, 2f - _blink * 2f);
                if (_blink >= 1f) _blink = 0f;
            }
            SetLid(_lidL, _lidLRest, close);
            SetLid(_lidR, _lidRRest, close);

            // eyes: up and to one side while thinking, small drifts otherwise
            if (_t >= _nextGlance)
            {
                _nextGlance = _t + 0.8f + Mathf.Abs(Mathf.Sin(_t * 78.233f)) * 2.2f;
                _glanceX = (Mathf.Sin(_t * 91.7f)) * 0.0022f;
                _glanceY = (Mathf.Sin(_t * 41.3f)) * 0.0014f;
            }
            float gx = thinking ? 0.0032f : _glanceX;
            float gy = thinking ? 0.0030f : _glanceY;
            foreach (Transform look in _head.GetComponentsInChildren<Transform>())
                if (look.name == "Look") look.localPosition = Vector3.Lerp(look.localPosition, new Vector3(gx, gy, 0), 10f * dt);

            // brows: lifted to listen and to think
            float lift = (hearing ? 0.006f : 0f) + (thinking ? 0.009f : 0f) + (speaking ? 0.002f * _mouth : 0f);
            _browL.localPosition = Vector3.Lerp(_browL.localPosition, _browLRest + new Vector3(0, lift, 0), 8f * dt);
            _browR.localPosition = Vector3.Lerp(_browR.localPosition, _browRRest + new Vector3(0, lift + (thinking ? 0.003f : 0f), 0), 8f * dt);

            // mouth
            _lower.localPosition = _lowerRest + new Vector3(0, -0.017f * _mouth, -0.001f * _mouth);
            _inner.localScale = new Vector3(1f - 0.15f * _mouth, 0.35f + 1.9f * _mouth, 1f);
            _inner.localPosition = _innerRest + new Vector3(0, -0.0085f * _mouth, 0);
        }

        static void SetLid(Transform lid, Vector3 rest, float close)
        {
            // open: a thin sliver above the eye. closed: pulled down over it.
            lid.localPosition = rest + new Vector3(0, -close * 0.0108f, 0.0003f * close);
            lid.localScale = new Vector3(1f, Mathf.Lerp(0.35f, 1f, close), 1f);
        }
    }
}
