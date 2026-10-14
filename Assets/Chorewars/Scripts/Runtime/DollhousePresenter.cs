using System.Collections;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Reveal animation for the scan -> dollhouse pipeline. "Present" lifts the finished miniature
    /// into the player's view for the scan-complete moment; "Dock" parks it low and to one side so
    /// it stops occluding the real room while the chore is played.
    /// The motion is driven from the miniature's renderer bounds, so it works wherever the
    /// DollhouseManager decides to put its origin, and it only ever translates/scales (never rotates).
    /// </summary>
    public sealed class DollhousePresenter : MonoBehaviour
    {
        [Header("Present (scan complete)")]
        [SerializeField] private float presentDistanceMeters = 0.85f;
        [SerializeField] private float presentDropMeters = 0.10f;
        [SerializeField] private float presentScale = 1f;

        [Header("Dock (during play)")]
        [SerializeField] private float dockDistanceMeters = 0.65f;
        [SerializeField] private float dockRightMeters = 0.34f;
        [SerializeField] private float dockDropMeters = 0.30f;
        [SerializeField] private float dockScale = 0.5f;

        private Coroutine _motion;

        /// <summary>Lift the miniature into view, centred a little below eye level.</summary>
        public void Present(Transform camera, Transform root, float duration = 1.4f)
        {
            if (root == null) return;

            var target = OffsetFromCamera(camera, presentDistanceMeters, 0f, -presentDropMeters);
            Play(root, target ?? root.position, presentScale, duration);
        }

        /// <summary>Park the miniature low and to the player's right, out of the way.</summary>
        public void Dock(Transform camera, Transform root, float duration = 1f)
        {
            if (root == null) return;

            var target = OffsetFromCamera(camera, dockDistanceMeters, dockRightMeters, -dockDropMeters);
            Play(root, target ?? root.position, dockScale, duration);
        }

        private static Vector3? OffsetFromCamera(Transform camera, float forward, float right, float up)
        {
            if (camera == null) return null;

            return camera.position
                   + camera.forward * forward
                   + camera.right * right
                   + camera.up * up;
        }

        private void Play(Transform root, Vector3 centreTarget, float targetScale, float duration)
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion = StartCoroutine(Animate(root, centreTarget, targetScale, Mathf.Max(0.05f, duration)));
        }

        private static IEnumerator Animate(Transform root, Vector3 centreTarget, float targetScale, float duration)
        {
            float startScale = root.localScale.x;
            var centreLocal = ContentCentreLocal(root);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                float scale = Mathf.Lerp(startScale, targetScale, t);

                root.localScale = Vector3.one * scale;
                root.position = centreTarget - centreLocal * scale;

                yield return null;
            }

            root.localScale = Vector3.one * targetScale;
            root.position = centreTarget - centreLocal * targetScale;
        }

        /// <summary>Centre of everything currently rendered under <paramref name="root"/>, in root-local space.</summary>
        private static Vector3 ContentCentreLocal(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return Vector3.zero;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            return root.InverseTransformPoint(bounds.center);
        }
    }
}
