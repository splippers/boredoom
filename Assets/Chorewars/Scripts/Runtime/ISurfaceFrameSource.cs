using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// One captured look at the floor: where the camera was, which way it pointed, and what it saw.
    /// </summary>
    /// <remarks>
    /// Deliberately a plain struct with no Unity types beyond the texture, so a fake source is as
    /// easy to write as the real one and both go through the same projection maths below.
    /// </remarks>
    public struct SurfaceFrame
    {
        /// <summary>False when the source has nothing to offer yet (no camera, no permission, no pose).</summary>
        public bool Valid;

        /// <summary>Camera position, world space.</summary>
        public Vector3 Position;

        /// <summary>Camera orientation, world space. Forward is where the camera looks.</summary>
        public Quaternion Rotation;

        /// <summary>Vertical field of view in degrees — Unity's convention, so 60 for a 60 degree lens.</summary>
        public float VerticalFieldOfViewDegrees;

        /// <summary>Width divided by height of the captured image.</summary>
        public float Aspect;

        /// <summary>The image. Must be readable: the bake reads it on the CPU.</summary>
        public Texture2D Image;

        /// <summary>Height of the plane the image is projected onto.</summary>
        public float FloorY;
    }

    /// <summary>
    /// Where the bake gets its pictures. The only seam between "a headset that can see" and
    /// "anything else", so the whole pipeline runs in the editor against a synthetic camera and on
    /// device against the real one without a branch anywhere downstream.
    /// </summary>
    public interface ISurfaceFrameSource
    {
        /// <summary>Takes a picture now. Returns <c>Valid = false</c> rather than throwing.</summary>
        SurfaceFrame Capture();

        /// <summary>Human-readable, for the log line when a device is not producing frames.</summary>
        string Describe { get; }
    }

    /// <summary>
    /// A pinhole camera, shared by the thing that renders a synthetic frame and the thing that
    /// projects a real one back onto the floor.
    /// </summary>
    /// <remarks>
    /// Both directions of one projection live here on purpose. If the render and the bake each had
    /// their own copy of this maths, "the floor now shows what the camera saw" would be a claim about
    /// two sets of conventions happening to agree; sharing the functions makes it a property of the
    /// code. Viewport coordinates follow Unity's: u and v run 0..1, v increasing upwards.
    /// </remarks>
    public static class PinholeCamera
    {
        /// <summary>Unit direction through a viewport point, in world space.</summary>
        public static Vector3 RayDirection(Quaternion rotation, float verticalFovDegrees, float aspect, float u, float v)
        {
            float tanV = Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * Mathf.Max(aspect, 1e-4f);

            var local = new Vector3((u * 2f - 1f) * tanH, (v * 2f - 1f) * tanV, 1f);
            return rotation * local;
        }

        /// <summary>
        /// Where a world point lands in the image. False when it is behind the camera or outside the
        /// frame — the caller skips it rather than clamping, because clamping would smear the edge
        /// texels of the image across the floor.
        /// </summary>
        public static bool TryWorldToViewport(
            Vector3 world, Vector3 origin, Quaternion rotation,
            float verticalFovDegrees, float aspect, out float u, out float v)
        {
            u = 0f;
            v = 0f;

            var local = Quaternion.Inverse(rotation) * (world - origin);
            if (local.z <= 1e-4f) return false;

            float tanV = Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * Mathf.Max(aspect, 1e-4f);

            u = 0.5f + (local.x / local.z) / (2f * tanH);
            v = 0.5f + (local.y / local.z) / (2f * tanV);

            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        /// <summary>Intersects a ray with the horizontal plane at <paramref name="planeY"/>.</summary>
        public static bool TryGroundHit(Vector3 origin, Vector3 direction, float planeY, out Vector3 hit)
        {
            hit = default;

            if (Mathf.Abs(direction.y) < 1e-5f) return false;

            float t = (planeY - origin.y) / direction.y;
            if (t <= 0f) return false;

            hit = origin + direction * t;
            return true;
        }
    }
}
