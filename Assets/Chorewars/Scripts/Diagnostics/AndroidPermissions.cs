using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.Diagnostics
{
    /// <summary>
    /// Asks the headset for the runtime permissions the game needs, and remembers the answer.
    /// </summary>
    /// <remarks>
    /// Declaring a permission in the manifest is only half of it on Android: a permission the user has
    /// to consent to is *denied until the app asks*, and the overlay that asks is the app's job. The
    /// build declared <c>com.oculus.permission.USE_SCENE</c> (for room geometry) and
    /// <c>android.permission.RECORD_AUDIO</c> (for ORAC's voice), but nothing ever requested them, so
    /// both were denied and meshing had nothing to read. That is the whole of why the device scan
    /// returned no vertices.
    ///
    /// Everything here is a no-op off-device, and the editor reports the permissions as not applicable
    /// rather than "denied" — an editor is not a device that refused.
    ///
    /// This is a *request*, not a guarantee: the player can still say no, and the code that needs the
    /// permission has to cope with that (the scan already falls back to the play-space boundary).
    /// </remarks>
    public static class AndroidPermissions
    {
        /// <summary>Room geometry (the scene model) — what meshing needs to produce any triangles.</summary>
        public const string Scene = "com.oculus.permission.USE_SCENE";

        /// <summary>Microphone, for ORAC's voice input.</summary>
        public const string Microphone = "android.permission.RECORD_AUDIO";

        /// <summary>
        /// Not requested yet: the pseudo-photogrammetry slice's passthrough camera. Declared here so the
        /// name is in one place when that slice needs it. Requesting it now would ask the player for
        /// access to the headset cameras before anything reads them.
        /// </summary>
        public const string HeadsetCamera = "horizonos.permission.HEADSET_CAMERA";

        /// <summary>What this build asks for at launch, in the order it asks.</summary>
        public static readonly string[] Wanted = { Scene, Microphone };

        // Held for the lifetime of the app because the callbacks are invoked on a later frame than the
        // request: a callbacks object with no reference can be collected before its own answer arrives.
        static readonly Dictionary<string, UnityEngine.Android.PermissionCallbacks> Pending =
            new Dictionary<string, UnityEngine.Android.PermissionCallbacks>();

        static readonly Dictionary<string, bool> Granted = new Dictionary<string, bool>();

        /// <summary>True on a real Android device; false in the editor and on any other desktop build.</summary>
        public static bool IsDevice =>
            !Application.isEditor && Application.platform == RuntimePlatform.Android;

        /// <summary>
        /// Requests everything in <see cref="Wanted"/> that is not already held. Safe to call more than
        /// once: the OS coalesces the prompts, and a permission already answered is not asked again.
        /// </summary>
        public static void RequestAll()
        {
            if (!IsDevice)
            {
                TelemetryLogger.Info("Permissions", "Not a device — runtime permissions are not applicable.");
                return;
            }

            foreach (var permission in Wanted) Request(permission);

            TelemetryLogger.Info("Permissions", "Requested: " + Summary());
        }

        /// <summary>Requests one permission, unless it is already granted or already asked for.</summary>
        public static void Request(string permission)
        {
            if (!IsDevice || string.IsNullOrEmpty(permission)) return;
            if (Pending.ContainsKey(permission)) return;

            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
            {
                Granted[permission] = true;
                return;
            }

            var callbacks = new UnityEngine.Android.PermissionCallbacks();

            callbacks.PermissionGranted += name =>
            {
                Granted[name] = true;
                TelemetryLogger.Info("Permissions", name + " granted.");
            };

            callbacks.PermissionDenied += name =>
            {
                Granted[name] = false;
                TelemetryLogger.Warn("Permissions", name + " denied — the feature that needs it will not work. " +
                                                   "If it cannot be asked again it has to be turned on in the headset settings.");
            };

            Pending[permission] = callbacks;
            UnityEngine.Android.Permission.RequestUserPermission(permission, callbacks);
        }

        /// <summary>Whether a permission is known to be granted. Unknown and refused both read false.</summary>
        public static bool IsGranted(string permission)
            => !string.IsNullOrEmpty(permission) && Granted.TryGetValue(permission, out var granted) && granted;

        /// <summary>Room geometry is available to read. The scan checks this before expecting meshes.</summary>
        public static bool SceneGranted => IsGranted(Scene);

        /// <summary>
        /// "scene=granted microphone=denied" — one line a person can read, used by the run header and by
        /// the logs. Off-device it says so rather than reporting a refusal that never happened.
        /// </summary>
        public static string Summary()
        {
            if (!IsDevice) return "n/a (not a device)";

            var parts = new List<string>(Wanted.Length + 1);

            foreach (var permission in Wanted)
                parts.Add(ShortName(permission) + "=" + State(permission));

            return string.Join(" ", parts);
        }

        static string State(string permission)
        {
            if (IsGranted(permission)) return "granted";
            if (Granted.TryGetValue(permission, out var granted)) return granted ? "granted" : "denied";

            return Pending.ContainsKey(permission) ? "asked" : "not asked";
        }

        /// <summary>Trims a permission id to the part that names it: the last dotted segment, lowercased.</summary>
        static string ShortName(string permission)
        {
            if (string.IsNullOrEmpty(permission)) return "?";

            int dot = permission.LastIndexOf('.');
            if (dot < 0 || dot == permission.Length - 1) return permission;

            // "USE_SCENE" -> "scene", "RECORD_AUDIO" -> "audio": drop a leading verb where there is one.
            var tail = permission.Substring(dot + 1);
            if (tail.StartsWith("USE_")) tail = tail.Substring(4);
            if (tail.StartsWith("RECORD_")) tail = "audio";

            return tail.ToLowerInvariant();
        }
    }
}
