using UnityEngine;

namespace Chorewars.Dollhouse
{
    /// <summary>
    /// Lets a dollhouse room's floor draw the live floor's texture, so the miniature shows the same
    /// photographs the room does rather than a second copy of them.
    /// </summary>
    /// <remarks>
    /// The live texture spans the whole play area, but a miniature room is only its own slice of it.
    /// Drawn with the mesh's own UVs it would stretch the entire play area across one room, so each
    /// room needs a material whose scale and offset land its floor on the part of the play area that
    /// room actually occupies.
    ///
    /// Note whose footprint that is: the miniature sits at the dollhouse, tens of centimetres from
    /// where the room really is, so the mapping cannot be derived from where the miniature floor is.
    /// It has to be derived from the room's world rectangle. Getting that wrong is not obvious by
    /// looking — the miniature still shows a plausible floor, just the wrong part of it.
    ///
    /// The one thing taken from the mesh is which way its UVs run. Unity's cube top face is mirrored,
    /// and solving for that beats assuming it: the fit fails loudly rather than silently transposing
    /// the miniature floor on a Unity version that reorders the cube.
    /// </remarks>
    public static class DollhouseFloorTexture
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        /// <summary>An unlit material showing <paramref name="texture"/>, or null with no shader.</summary>
        public static Material CreateMaterial(Texture texture)
        {
            var shader = Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                Debug.LogWarning("[Dollhouse] 'Unlit/Texture' is missing; the miniature keeps its tint.");
                return null;
            }

            var material = new Material(shader) { name = "DollhouseRoomFloor" };
            if (texture != null) material.SetTexture(MainTexId, texture);
            return material;
        }

        /// <summary>
        /// Reads the local position and mesh UV of each corner of the floor's top face. Fixed for the
        /// life of the mesh, so read it once and keep it: reading it per frame would copy the mesh's
        /// vertex arrays every time.
        /// </summary>
        public static bool TryReadFloorFace(Renderer floor, out Vector3[] localXZ, out Vector2[] uv)
        {
            localXZ = null;
            uv = null;

            var filter = floor != null ? floor.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return false;

            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            var normals = mesh.normals;
            var triangles = mesh.triangles;

            if (vertices == null || uvs == null || normals == null || triangles == null) return false;
            if (uvs.Length != vertices.Length || normals.Length != vertices.Length) return false;

            var corners = new int[4];
            int count = 0;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                // The top face is the only one that is flat and upwards, which is the one being
                // looked at: the room is read from above.
                if (normals[triangles[i]].y < 0.9f) continue;
                if (normals[triangles[i + 1]].y < 0.9f) continue;
                if (normals[triangles[i + 2]].y < 0.9f) continue;

                for (int k = 0; k < 3; k++)
                {
                    int index = triangles[i + k];
                    if (Contains(corners, count, index)) continue;
                    if (count >= corners.Length) return false;

                    corners[count++] = index;
                }
            }

            if (count < 3) return false;

            localXZ = new Vector3[count];
            uv = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                localXZ[i] = vertices[corners[i]];
                uv[i] = uvs[corners[i]];
            }

            return true;
        }

        /// <summary>
        /// Solves the scale and offset that carry each mesh UV to the texture coordinate given for
        /// it, and checks the answer against every corner rather than the two it was solved from.
        /// </summary>
        /// <remarks>
        /// Only a scale and offset can be expressed, so the fit needs each mesh UV axis to run along
        /// one texture axis. A cube face does that. If it ever does not, the check fails and the room
        /// keeps its tint instead of drawing something wrong.
        /// </remarks>
        public static bool TrySolve(Vector2[] uv, Vector2[] targets, out Vector2 scale, out Vector2 offset)
        {
            scale = Vector2.one;
            offset = Vector2.zero;

            if (uv == null || targets == null) return false;
            if (uv.Length != targets.Length || uv.Length < 3) return false;

            Vector2 solvedScale = Vector2.one;
            Vector2 solvedOffset = Vector2.zero;

            if (!SolveAxis(uv, targets, 0, ref solvedScale, ref solvedOffset)) return false;
            if (!SolveAxis(uv, targets, 1, ref solvedScale, ref solvedOffset)) return false;

            for (int i = 0; i < uv.Length; i++)
            {
                var got = new Vector2(
                    uv[i].x * solvedScale.x + solvedOffset.x,
                    uv[i].y * solvedScale.y + solvedOffset.y);

                if (Mathf.Abs(got.x - targets[i].x) > 1e-3f) return false;
                if (Mathf.Abs(got.y - targets[i].y) > 1e-3f) return false;
            }

            scale = solvedScale;
            offset = solvedOffset;
            return true;
        }

        private static bool SolveAxis(
            Vector2[] uv,
            Vector2[] targets,
            int axis,
            ref Vector2 scale,
            ref Vector2 offset)
        {
            int low = 0, high = 0;

            for (int i = 1; i < uv.Length; i++)
            {
                if (uv[i][axis] < uv[low][axis]) low = i;
                if (uv[i][axis] > uv[high][axis]) high = i;
            }

            float span = uv[high][axis] - uv[low][axis];
            if (Mathf.Abs(span) < 1e-5f) return false;

            float s = (targets[high][axis] - targets[low][axis]) / span;
            float o = targets[low][axis] - s * uv[low][axis];

            var newScale = scale;
            var newOffset = offset;
            newScale[axis] = s;
            newOffset[axis] = o;
            scale = newScale;
            offset = newOffset;

            return true;
        }

        private static bool Contains(int[] values, int count, int value)
        {
            for (int i = 0; i < count; i++)
            {
                if (values[i] == value) return true;
            }

            return false;
        }
    }
}
