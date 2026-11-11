using Chorewars.Diagnostics;
using UnityEngine;

namespace Chorewars.Dollhouse
{
    /// <summary>
    /// Shows both tracked hands as the standard translucent hand model from Unity's XR Hands package, in every scene,
    /// so it is obvious what the headset thinks the hands are doing. The model hides itself when a hand is not tracked
    /// (for instance while a controller is held). Off if a file named "no-hand-outlines" is in the app files folder.
    /// </summary>
    public sealed class HandOutlines : MonoBehaviour
    {
        GameObject _left, _right;
        Material _glove;
        Transform _space;
        float _nextTry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!AndroidPermissions.IsDevice || FindFirstObjectByType<HandOutlines>() != null) return;
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "no-hand-outlines"))) return;
            var go = new GameObject(nameof(HandOutlines));
            DontDestroyOnLoad(go);
            go.AddComponent<HandOutlines>();
        }

        void OnEnable() => GloveStyle.Changed += ApplyColour;
        void OnDisable() => GloveStyle.Changed -= ApplyColour;

        void ApplyColour()
        {
            if (_glove == null)
            {
                var shader = Shader.Find("Chorewars/GloveShaded");
                if (shader == null) { TelemetryLogger.Warn("HandOutlines", "glove shader missing; keeping the default hand material."); return; }
                _glove = new Material(shader);
            }
            _glove.color = GloveStyle.Tint(GloveStyle.Current);
            foreach (var hand in new[] { _left, _right })
            {
                if (hand == null) continue;
                foreach (var renderer in hand.GetComponentsInChildren<Renderer>(true))
                    if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer) renderer.sharedMaterial = _glove;
            }
        }

        void Update()
        {
            if ((_left != null && _right != null) || Time.unscaledTime < _nextTry) return;
            _nextTry = Time.unscaledTime + 1f;
            var cam = Camera.main;
            if (cam == null) return;
            var origin = cam.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            if (origin == null) return;
            var space = origin.Origin != null ? origin.Origin.transform : origin.transform;
            if (space == _space && _left != null && _right != null) return;
            _space = space;
            _left = Spawn(_left, "HandVisual/Prefabs/Left Hand Tracking");
            _right = Spawn(_right, "HandVisual/Prefabs/Right Hand Tracking");
            ApplyColour();
        }

        GameObject Spawn(GameObject existing, string path)
        {
            if (existing != null) { existing.transform.SetParent(_space, false); return existing; }
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) { TelemetryLogger.Warn("HandOutlines", "hand model missing: " + path); return null; }
            var hand = Instantiate(prefab, _space, false);
            hand.name = prefab.name;
            TelemetryLogger.Info("HandOutlines", "hand model placed: " + prefab.name);
            return hand;
        }
    }
}
