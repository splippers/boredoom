using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Keeps shaders that code looks up by name (<c>Shader.Find</c>) in the player build.
    /// </summary>
    /// <remarks>
    /// A player build only contains shaders that something in the build references. A name lookup is not a
    /// reference, so in the editor <c>Shader.Find("Unlit/Color")</c> works and on a device it returns null:
    /// the placeholder room throws, and anything built without a shader renders magenta. This asset lives in
    /// a Resources folder, so everything it points at ships. Add a shader here whenever code starts finding
    /// one by name.
    /// </remarks>
    public sealed class RuntimeShaderRefs : ScriptableObject
    {
        public const string ResourcePath = "RuntimeShaderRefs";

        [Tooltip("Shaders found by name at runtime.")]
        public Shader[] shaders;

        [Tooltip("Materials whose keyword variants must survive stripping (e.g. Standard in Fade mode).")]
        public Material[] variantKeepers;
    }
}
