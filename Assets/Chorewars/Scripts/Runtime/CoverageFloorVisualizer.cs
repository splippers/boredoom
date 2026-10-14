using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Placeholder "cleaned area" feedback: paints visited cells onto a texture applied to the
    /// floor. Purely cosmetic — scoring is driven by <c>CoverageMap</c>.
    /// </summary>
    public sealed class CoverageFloorVisualizer : MonoBehaviour
    {
        private const int BrushRadius = 2;
        private static readonly Color32 Uncleaned = new Color32(38, 42, 48, 255);
        private static readonly Color32 Cleaned = new Color32(90, 220, 160, 255);

        private Texture2D _texture;
        private Color32[] _pixels;
        private Bounds _bounds;
        private int _resolution = 64;
        private bool _dirty;

        public void Build(Bounds bounds, Renderer floorRenderer, int resolution = 64)
        {
            _bounds = bounds;
            _resolution = Mathf.Clamp(resolution, 8, 256);

            _texture = new Texture2D(_resolution, _resolution, TextureFormat.RGBA32, false)
            {
                name = "CoverageFloor",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            _pixels = new Color32[_resolution * _resolution];
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = Uncleaned;
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);

            if (floorRenderer == null) return;

            var shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = "CoverageFloorMat", mainTexture = _texture };
            floorRenderer.sharedMaterial = material;
        }

        public void Mark(Vector3 worldPosition)
        {
            if (_texture == null) return;

            float u = Mathf.InverseLerp(_bounds.min.x, _bounds.max.x, worldPosition.x);
            float v = Mathf.InverseLerp(_bounds.min.z, _bounds.max.z, worldPosition.z);
            if (u < 0f || u > 1f || v < 0f || v > 1f) return;

            int cx = Mathf.Clamp((int)(u * (_resolution - 1)), 0, _resolution - 1);
            int cy = Mathf.Clamp((int)(v * (_resolution - 1)), 0, _resolution - 1);

            for (int y = cy - BrushRadius; y <= cy + BrushRadius; y++)
            {
                if (y < 0 || y >= _resolution) continue;
                for (int x = cx - BrushRadius; x <= cx + BrushRadius; x++)
                {
                    if (x < 0 || x >= _resolution) continue;
                    _pixels[y * _resolution + x] = Cleaned;
                }
            }

            _dirty = true;
        }

        private void LateUpdate()
        {
            if (!_dirty || _texture == null) return;
            _dirty = false;
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }
    }
}
