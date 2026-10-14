using System;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Placeholder "cleaned area" feedback: paints visited cells onto a texture applied to the
    /// floor. Purely cosmetic — scoring is driven by <c>CoverageMap</c>. Freshly cleaned texels
    /// flash bright and then settle to the clean tint, so progress reads as a sparkle rather than
    /// a silently changing texture.
    /// </summary>
    public sealed class CoverageFloorVisualizer : MonoBehaviour
    {
        private const int BrushRadius = 2;
        private const float FlashDecayPerSecond = 4.5f;
        private static readonly Color32 Uncleaned = new Color32(38, 42, 48, 255);
        private static readonly Color32 Cleaned = new Color32(90, 220, 160, 255);
        private static readonly Color32 FlashTint = new Color32(235, 255, 245, 255);

        private Texture2D _texture;
        private Color32[] _pixels;
        private Color32[] _display;
        private float[] _flash;
        private Bounds _bounds;
        private int _resolution = 64;
        private bool _dirty;
        private bool _flashing;

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
            _display = new Color32[_pixels.Length];
            _flash = new float[_pixels.Length];
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = Uncleaned;
            Array.Copy(_pixels, _display, _pixels.Length);
            _texture.SetPixels32(_display);
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

            bool paintedNew = false;

            for (int y = cy - BrushRadius; y <= cy + BrushRadius; y++)
            {
                if (y < 0 || y >= _resolution) continue;
                for (int x = cx - BrushRadius; x <= cx + BrushRadius; x++)
                {
                    if (x < 0 || x >= _resolution) continue;

                    int i = y * _resolution + x;

                    // Only genuinely new texels flash. Re-flashing already-clean ones would keep
                    // the whole brush hot and the texture re-uploading whenever the tool stands
                    // still — exactly the per-frame cost we want to avoid on Quest.
                    var px = _pixels[i];
                    if (px.r == Cleaned.r && px.g == Cleaned.g && px.b == Cleaned.b) continue;

                    _pixels[i] = Cleaned;
                    _flash[i] = 1f;
                    paintedNew = true;
                }
            }

            if (!paintedNew) return;

            _flashing = true;
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (_texture == null) return;

            if (_flashing)
            {
                float decay = Time.deltaTime * FlashDecayPerSecond;
                bool stillFlashing = false;

                for (int i = 0; i < _pixels.Length; i++)
                {
                    float f = _flash[i];
                    if (f <= 0f) continue;

                    f = Mathf.Max(0f, f - decay);
                    _flash[i] = f;
                    if (f > 0f) stillFlashing = true;

                    _display[i] = f > 0f
                        ? (Color32)Color32.Lerp(_pixels[i], FlashTint, f)
                        : _pixels[i];
                }

                _flashing = stillFlashing;
                _dirty = true;
            }

            if (!_dirty) return;
            _dirty = false;
            _texture.SetPixels32(_display);
            _texture.Apply(false);
        }
    }
}
