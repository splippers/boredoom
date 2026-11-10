using System.Collections.Generic;
using Chorewars.Diagnostics;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Chorewars.Dollhouse
{
    /// <summary>
    /// Draws both tracked hands as chunky white cartoon gloves (puffy fingers, palm blob, flared cuff) in every scene, so it is obvious
    /// what the headset thinks the hands are doing. Off if a file named "no-hand-outlines" is in the app files folder.
    /// </summary>
    public sealed class HandOutlines : MonoBehaviour
    {
        // XRHandJointID values per finger, each chain starting at the wrist.
        static readonly int[][] Chains =
        {
            new[] { 3, 4, 5, 6 },           // thumb
            new[] { 7, 8, 9, 10, 11 },      // index
            new[] { 12, 13, 14, 15, 16 },   // middle
            new[] { 17, 18, 19, 20, 21 },   // ring
            new[] { 22, 23, 24, 25, 26 }    // little
        };

        sealed class Hand
        {
            public Transform[] Bones = new Transform[24];
            public int BoneCount;
            public Transform[] Dots = new Transform[27];
            public Transform Cuff;
            public Transform Root;
        }

        readonly Hand _left = new Hand(), _right = new Hand();
        readonly List<XRHandSubsystem> _subsystems = new List<XRHandSubsystem>();
        XRHandSubsystem _hands;
        Transform _space;
        Material _material;
        float _nextLookup, _nextLog;
        bool _loggedTracked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!AndroidPermissions.IsDevice || FindFirstObjectByType<HandOutlines>() != null) return;
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "no-hand-outlines"))) return;
            var go = new GameObject(nameof(HandOutlines));
            DontDestroyOnLoad(go);
            go.AddComponent<HandOutlines>();
        }

        void Awake()
        {
            _material = new Material(Shader.Find("Sprites/Default"));
            Build(_left, "Left", new Color(0.1f, 1f, 1f, 1f));
            Build(_right, "Right", new Color(1f, 0.3f, 0.9f, 1f));
        }

        static readonly Color Glove = new Color(1f, 1f, 1f, 1f);
        static readonly Color Seam = new Color(0.9f, 0.92f, 0.96f, 1f);

        Transform Part(Hand hand, PrimitiveType type, string name, Color colour)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(hand.Root, false);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", colour);
            renderer.SetPropertyBlock(block);
            return go.transform;
        }

        void Build(Hand hand, string name, Color unused)
        {
            hand.Root = new GameObject(name + " glove").transform;
            hand.Root.SetParent(transform, false);
            for (int c = 0; c < Chains.Length; c++)
                for (int k = 0; k + 1 < Chains[c].Length; k++)
                    hand.Bones[hand.BoneCount++] = Part(hand, PrimitiveType.Cylinder, "finger" + c + "_" + k, Seam);
            for (int id = 2; id <= 26; id++)
            {
                bool knuckleInPalm = id == 3 || id == 7 || id == 12 || id == 17 || id == 22;
                if (knuckleInPalm) continue;
                hand.Dots[id] = Part(hand, PrimitiveType.Sphere, "joint" + id, Glove);
            }
            hand.Cuff = Part(hand, PrimitiveType.Cylinder, "cuff", Glove);
            hand.Root.gameObject.SetActive(false);
        }

        static float Radius(int id, bool thumb) => thumb ? 0.0125f : 0.0105f;

        void Update()
        {
            if (Time.unscaledTime >= _nextLookup && (_hands == null || !_hands.running))
            {
                _nextLookup = Time.unscaledTime + 1f;
                _hands = null; _subsystems.Clear(); SubsystemManager.GetSubsystems(_subsystems);
                foreach (var s in _subsystems) if (s.running) { _hands = s; break; }
            }
            if (_space == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var origin = cam.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
                    _space = origin != null ? (origin.Origin != null ? origin.Origin.transform : origin.transform) : null;
                }
            }
        }

        void LateUpdate()
        {
            if (_hands == null || !_hands.running) { _left.Root.gameObject.SetActive(false); _right.Root.gameObject.SetActive(false); return; }
            bool l = Draw(_hands.leftHand, _left), r = Draw(_hands.rightHand, _right);
            if (Time.unscaledTime >= _nextLog && (l || r) != _loggedTracked)
            {
                _loggedTracked = l || r;
                _nextLog = Time.unscaledTime + 2f;
                TelemetryLogger.Info("HandOutlines", "hands drawn: left=" + l + " right=" + r);
            }
        }

        bool Draw(XRHand hand, Hand view)
        {
            bool tracked = hand.isTracked;
            view.Root.gameObject.SetActive(tracked);
            if (!tracked) return false;
            var points = new Vector3[27];
            var ok = new bool[27];
            Quaternion palmRotation = Quaternion.identity;
            for (int id = 1; id <= 26; id++)
            {
                if (!hand.GetJoint((XRHandJointID)id).TryGetPose(out var pose)) continue;
                var world = PalmMiniatureController.ToWorldPose(_space, pose);
                points[id] = world.position; ok[id] = true;
                if (id == 2) palmRotation = world.rotation;
            }
            // Fingers: puffy tubes between joints, round joints on top.
            int b = 0;
            for (int c = 0; c < Chains.Length; c++)
            {
                bool thumb = c == 0;
                float width = (thumb ? 0.027f : 0.022f);
                for (int k = 0; k + 1 < Chains[c].Length; k++, b++)
                {
                    int a = Chains[c][k], z = Chains[c][k + 1];
                    var bone = view.Bones[b];
                    bool show = ok[a] && ok[z];
                    bone.gameObject.SetActive(show);
                    if (!show) continue;
                    Vector3 d = points[z] - points[a];
                    float len = d.magnitude;
                    if (len < 0.0005f) { bone.gameObject.SetActive(false); continue; }
                    bone.position = (points[a] + points[z]) * 0.5f;
                    bone.rotation = Quaternion.FromToRotation(Vector3.up, d);
                    bone.localScale = new Vector3(width, len * 0.5f, width);
                }
                for (int k = 1; k < Chains[c].Length; k++)
                {
                    int id = Chains[c][k];
                    var dot = view.Dots[id];
                    dot.gameObject.SetActive(ok[id]);
                    if (!ok[id]) continue;
                    dot.position = points[id];
                    float r = Radius(id, thumb) * (k == Chains[c].Length - 1 ? 1.05f : 1f);
                    dot.localScale = Vector3.one * (r * 2f);
                }
            }
            // Palm: a flat rounded blob on the palm joint, turned with the hand.
            var palm = view.Dots[2];
            palm.gameObject.SetActive(ok[2]);
            if (ok[2])
            {
                palm.position = points[2];
                palm.rotation = palmRotation;
                palm.localScale = new Vector3(0.085f, 0.045f, 0.095f);
            }
            // Cuff: a flared tube at the wrist pointing back along the forearm.
            view.Cuff.gameObject.SetActive(ok[1] && ok[2]);
            if (ok[1] && ok[2])
            {
                Vector3 back = (points[1] - points[2]).normalized;
                view.Cuff.position = points[1] + back * 0.012f;
                view.Cuff.rotation = Quaternion.FromToRotation(Vector3.up, back);
                view.Cuff.localScale = new Vector3(0.07f, 0.02f, 0.07f);
            }
            return true;
        }
    }
}
