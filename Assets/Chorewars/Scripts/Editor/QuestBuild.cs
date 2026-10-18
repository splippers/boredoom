using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Meta;

namespace Chorewars.Build
{
    /// <summary>
    /// Quest 3 player settings and the APK build entry point.
    /// </summary>
    /// <remarks>
    /// Nothing here runs on its own. The project's ProjectSettings are the developer's, so this only
    /// touches them when someone asks for a build — by menu item, or by calling <see cref="BuildApk"/>
    /// from a script. That keeps a normal editor session from silently rewriting player settings.
    ///
    /// The build needs the Unity <b>Android Build Support</b> module (with the SDK/NDK) installed for
    /// this editor. If it is missing, the validation below says so rather than failing halfway through
    /// a build.
    /// </remarks>
    public static class QuestBuild
    {
        public const string DefaultOutputPath = "Builds/BoreDOOM-Quest.apk";
        public const string ScenePath = "Assets/Chorewars/Scenes/HooverMode.unity";
        public const string AndroidApplicationId = "com.boredoom.chorewars";

        public static bool IsAndroidBuildSupported =>
            BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

        [MenuItem("BoreDOOM/Quest/Validate build readiness")]
        public static void ValidateFromMenu() => Debug.Log("[BoreDOOM] Quest readiness — " + Validate());

        [MenuItem("BoreDOOM/Quest/Build APK")]
        public static void BuildFromMenu()
        {
            var report = BuildApk(DefaultOutputPath);
            Debug.Log("[BoreDOOM] Quest build — " + report);
        }

        /// <summary>A one-line summary of everything that decides whether an APK can be built.</summary>
        public static string Validate()
        {
            var sb = new StringBuilder();

            sb.Append("androidModule=").Append(IsAndroidBuildSupported);
            sb.Append(" activeTarget=").Append(EditorUserBuildSettings.activeBuildTarget);
            sb.Append(" arch=").Append(PlayerSettings.Android.targetArchitectures);
            sb.Append(" scripting=").Append(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android));
            sb.Append(" minSdk=").Append(PlayerSettings.Android.minSdkVersion);
            sb.Append(" apis=").Append(string.Join("/", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)));
            sb.Append(" linear=").Append(PlayerSettings.colorSpace == ColorSpace.Linear);
            sb.Append(" appId=").Append(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            sb.Append(" sceneExists=").Append(File.Exists(ScenePath));
            sb.Append(" internetPermission=").Append(PlayerSettings.Android.forceInternetPermission);
            sb.Append(" toolchain=[").Append(QuestToolchain.Summary()).Append(']');

            return sb.ToString();
        }

        /// <summary>Applies the Quest player settings, then builds <paramref name="outputPath"/>.</summary>
        public static string BuildApk(string outputPath = DefaultOutputPath)
        {
            if (!IsAndroidBuildSupported)
                return "cannot build: " + QuestToolchain.Advice() + " State: " + Validate();

            if (!File.Exists(ScenePath))
                return "cannot build: scene not found at " + ScenePath;

            ApplyQuestPlayerSettings();

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                // Unity's summary.totalSize counts uncompressed and intermediate build output and
                // reports a 39.6 MB APK as roughly 794 MB. Anything that consumes this figure - the
                // build log, pull-telemetry, an agent reading either - would be WAY_OFF by
                // construction. Report the artefact's real size on disk, and keep Unity's number
                // only if the file cannot be stat-ed.
                long bytes = (long)summary.totalSize;
                try
                {
                    if (File.Exists(outputPath)) bytes = new FileInfo(outputPath).Length;
                }
                catch (Exception)
                {
                }

                return $"built {outputPath} ({bytes / (1024f * 1024f):0.0} MB on disk, {bytes} bytes) in {summary.totalTime.TotalSeconds:0}s";
            }

            return $"build {summary.result}: {summary.totalErrors} error(s). See the console for details.";
        }

        /// <summary>
        /// The settings a Quest 3 build needs: ARM64, IL2CPP, a modern minimum SDK, Vulkan with an
        /// OpenGLES3 fallback, and linear colour. Internet permission is on because ChoreWars Battle
        /// talks to a peer over UDP.
        /// </summary>
        /// <summary>
        /// Turns on "Camera Image Support" in Meta's Camera (Passthrough) feature. Without it the camera
        /// system runs but never hands over an image, so TryAcquireLatestCpuImage quietly returns false.
        /// The setting lives in a generated settings asset that is not committed, so the build sets it.
        /// </summary>
        static void EnableCameraImages()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var feature = settings != null ? settings.GetFeature<ARCameraFeature>() : null;
            if (feature == null)
            {
                Debug.LogWarning("[BoreDOOM] Meta Camera (Passthrough) feature not found; camera images stay off.");
                return;
            }

            if (feature.cameraImageSupportEnabled) return;

            feature.cameraImageSupportEnabled = true;
            EditorUtility.SetDirty(feature);
        }

        public static void ApplyQuestPlayerSettings()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidApplicationId);

            // 32 (Android 12L) because the Meta Quest Camera feature, which the camera probe uses, refuses to
            // build below it. A Quest 3 runs Android 14, so nothing is lost.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);

            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            PlayerSettings.colorSpace = ColorSpace.Linear;

            EnableCameraImages();

            AssetDatabase.SaveAssets();
            Debug.Log("[BoreDOOM] Applied Quest player settings — " + Validate());
        }
    }
}
