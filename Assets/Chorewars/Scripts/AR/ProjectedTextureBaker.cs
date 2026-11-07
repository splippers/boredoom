using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Chorewars.AR
{
    public sealed class ProjectedTextureBaker : MonoBehaviour
    {
        private static ProjectedTextureBaker _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Chorewars.Core.CleanExperience.IsActive) return;
            if (_instance != null) return;
            var go = new GameObject(nameof(ProjectedTextureBaker));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ProjectedTextureBaker>();
        }

        [SerializeField] private SpatialMeshTracker meshTracker;
        [SerializeField] private Camera captureCamera;
        [SerializeField] private Shader projectedShader;

        private Material _material;

        private void Awake()
        {
            if (meshTracker == null) meshTracker = FindAnyObjectByType<SpatialMeshTracker>();
            if (captureCamera == null) captureCamera = Camera.main;
        }

        public bool TryBakeOnce(out string pngPath)
        {
            pngPath = null;

            if (meshTracker == null) meshTracker = FindAnyObjectByType<SpatialMeshTracker>();
            if (captureCamera == null) captureCamera = Camera.main;
            if (captureCamera == null) return false;

            if (projectedShader == null) projectedShader = Shader.Find("Chorewars/ProjectedTexture");
            if (projectedShader == null) return false;

            if (_material == null) _material = new Material(projectedShader);

            // Capture a frame by rendering the camera into a texture.
            // (Avoid ScreenCapture APIs; they are not available in some player/assembly configurations.)
            var tex = CaptureCameraFrame(captureCamera, 1024, 1024);
            if (tex == null) return false;

            // Save the frame so you can pull it later (useful for debugging / future multi-frame blending).
            var ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            pngPath = Path.Combine(Application.persistentDataPath, $"projected-frame-{ts}.png");
            try
            {
                if (TryEncodeToPng(tex, out var bytes) && bytes != null && bytes.Length > 0)
                    File.WriteAllBytes(pngPath, bytes);
                else
                    pngPath = null;
            }
            catch
            {
                // non-fatal; projection can still work
                pngPath = null;
            }

            _material.SetTexture("_MainTex", tex);
            _material.SetMatrix("_ProjectVP", captureCamera.projectionMatrix * captureCamera.worldToCameraMatrix);

            ApplyMaterialToSpatialMeshes(_material);
            return true;
        }

        private static Texture2D CaptureCameraFrame(Camera cam, int width, int height)
        {
            if (cam == null) return null;

            var prevRt = cam.targetTexture;
            var prevActive = RenderTexture.active;

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            cam.targetTexture = prevRt;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            return tex;
        }

        private static bool TryEncodeToPng(Texture2D tex, out byte[] bytes)
        {
            bytes = null;
            if (tex == null) return false;

            // Avoid a hard dependency on UnityEngine.ImageConversionModule (EncodeToPNG),
            // which may not be present in some Unity 6 player configurations.
            try
            {
                var t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", throwOnError: false);
                if (t == null) return false;

                var m = t.GetMethod("EncodeToPNG", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Texture2D) }, null);
                if (m == null) return false;

                bytes = m.Invoke(null, new object[] { tex }) as byte[];
                return bytes != null;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyMaterialToSpatialMeshes(Material mat)
        {
            // Mesh GameObjects are created under meshParent if configured; we can safely re-skin all MeshRenderers.
            var renderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (r.gameObject.name.StartsWith("SpatialMesh_", StringComparison.Ordinal))
                    r.sharedMaterial = mat;
            }
        }
    }
}

