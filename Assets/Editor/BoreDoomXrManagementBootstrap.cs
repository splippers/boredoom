using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

/// <summary>
/// Creates XR Plug-in Management assets when they are missing from the repo (broken GUID refs).
/// Enables OpenXR <b>Meta Quest Support</b> + <b>Oculus Touch Controller Profile</b> for Android — required for Quest to present frames (otherwise the HMD can stay black).
/// </summary>
[InitializeOnLoad]
public static class BoreDoomXrManagementBootstrap
{
    const string XrRoot = "Assets/XR";
    const string SettingsFolder = XrRoot + "/Settings";
    const string LoadersFolder = XrRoot + "/Loaders";
    const string PerBuildAssetPath = SettingsFolder + "/XRGeneralSettingsPerBuildTarget.asset";
    const string OpenXrLoaderPath = LoadersFolder + "/Open XR Loader.asset";

    static BoreDoomXrManagementBootstrap()
    {
        EditorApplication.delayCall += EnsureXrManagementAssetsExist;
    }

    static void EnsureXrManagementAssetsExist()
    {
        try
        {
            EnsureFolders();

            var loaderPath = EnsureOpenXrLoaderAsset();
            var loader = AssetDatabase.LoadAssetAtPath<XRLoader>(loaderPath);
            if (loader == null)
            {
                Debug.LogError("[BoreDoom] Failed to load OpenXR Loader at " + loaderPath);
                return;
            }

            var perBuild = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(PerBuildAssetPath);
            if (perBuild == null)
            {
                perBuild = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perBuild, PerBuildAssetPath);
            }

            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                if (!perBuild.HasSettingsForBuildTarget(group))
                    perBuild.CreateDefaultSettingsForBuildTarget(group);
                if (!perBuild.HasManagerSettingsForBuildTarget(group))
                    perBuild.CreateDefaultManagerSettingsForBuildTarget(group);
            }

            if (!IsAndroidLoaderConfigured(perBuild))
            {
                foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
                {
                    var mgr = perBuild.ManagerSettingsForBuildTarget(group);
                    if (mgr == null)
                        continue;
                    while (mgr.activeLoaders.Count > 0)
                        mgr.TryRemoveLoader(mgr.activeLoaders[0]);
                    mgr.TryAddLoader(loader);
                    mgr.automaticLoading = true;
                    mgr.automaticRunning = true;
                }

                foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
                {
                    var gs = perBuild.SettingsForBuildTarget(group);
                    if (gs != null)
                        gs.InitManagerOnStart = true;
                }

                EditorUtility.SetDirty(perBuild);
                AssetDatabase.SaveAssets();

                EditorBuildSettings.RemoveConfigObject(XRGeneralSettings.settingsKey);
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perBuild, true);
            }
            else
            {
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perBuild, true);
            }

            TryEnsureOpenXrPackageSettings();
            EnableQuestOpenXrAndroidFeatures();
            AssetDatabase.Refresh();

            if (!IsAndroidLoaderConfigured(perBuild))
                Debug.LogWarning("[BoreDoom] Android XR loader list still empty — check XR Plug-in Management.");
            else
                Debug.Log("[BoreDoom] XR Plug-in Management OK. Quest OpenXR features updated — rebuild & install Android APK.");
        }
        catch (Exception e)
        {
            Debug.LogError("[BoreDoom] XR bootstrap failed: " + e);
        }
    }

    static bool IsAndroidLoaderConfigured(XRGeneralSettingsPerBuildTarget perBuild)
    {
        var mgr = perBuild.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        return mgr != null && mgr.activeLoaders != null && mgr.activeLoaders.Count > 0;
    }

    static void EnableQuestOpenXrAndroidFeatures()
    {
        OpenXRFeatureSetManager.InitializeFeatureSets();

        foreach (var fs in OpenXRFeatureSetManager.FeatureSetsForBuildTarget(BuildTargetGroup.Android))
        {
            if (fs.featureIds == null)
                continue;
            foreach (var fid in fs.featureIds)
            {
                if (!string.IsNullOrEmpty(fid) &&
                    fid.IndexOf("metaquest", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fs.isEnabled = true;
                    break;
                }
            }
        }

        OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(BuildTargetGroup.Android);

        var android = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (android == null)
        {
            Debug.LogWarning("[BoreDoom] OpenXR Android settings missing — open Project Settings > XR Plug-in Management > OpenXR.");
            return;
        }

        bool changed = false;

        var mq = android.GetFeature<MetaQuestFeature>();
        if (mq != null)
        {
            if (!mq.enabled)
            {
                mq.enabled = true;
                changed = true;
            }
        }
        else
        {
            Debug.LogWarning(
                "[BoreDoom] Meta Quest Support feature not found on OpenXR Android settings. In Editor: Project Settings > XR Plug-in Management > OpenXR > Android > Feature Groups, enable Meta Quest Support.");
        }

        var touch = android.GetFeature<OculusTouchControllerProfile>();
        if (touch != null && !touch.enabled)
        {
            touch.enabled = true;
            changed = true;
        }

        // AR Foundation session + meshing (from com.unity.xr.meta-openxr). Session is a dependency of
        // every other AR feature; Meshing is what turns the runtime's XR_FB_triangle_mesh into the
        // XRMeshSubsystem that SpatialMeshTracker reads, and enabling it is also what makes the
        // package add com.oculus.permission.USE_SCENE / USE_ANCHOR_API to the Android manifest
        // (see ModifyAndroidManifest.cs in that package). Both are absent when the package is not
        // installed, hence the null checks.
        var arSession = android.GetFeature<UnityEngine.XR.OpenXR.Features.Meta.ARSessionFeature>();
        if (arSession != null && !arSession.enabled)
        {
            arSession.enabled = true;
            changed = true;
        }

        var arMesh = android.GetFeature<UnityEngine.XR.OpenXR.Features.Meta.ARMeshFeature>();
        if (arMesh != null && !arMesh.enabled)
        {
            arMesh.enabled = true;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(android);
            var path = AssetDatabase.GetAssetPath(android);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("[BoreDoom] Enabled Meta Quest Support and/or Oculus Touch Controller Profile for OpenXR (Android).");
        }
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(XrRoot))
            AssetDatabase.CreateFolder("Assets", "XR");
        if (!AssetDatabase.IsValidFolder(LoadersFolder))
            AssetDatabase.CreateFolder(XrRoot, "Loaders");
        if (!AssetDatabase.IsValidFolder(SettingsFolder))
            AssetDatabase.CreateFolder(XrRoot, "Settings");
    }

    static string EnsureOpenXrLoaderAsset()
    {
        var full = Path.Combine(Directory.GetCurrentDirectory(), OpenXrLoaderPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full))
            return OpenXrLoaderPath;

        var instance = ScriptableObject.CreateInstance<OpenXRLoader>();
        AssetDatabase.CreateAsset(instance, OpenXrLoaderPath);
        AssetDatabase.SaveAssets();
        return OpenXrLoaderPath;
    }

    static void TryEnsureOpenXrPackageSettings()
    {
        const string editorAsm = "Unity.XR.OpenXR.Editor";
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name != editorAsm)
                continue;
            var t = assembly.GetType("UnityEditor.XR.OpenXR.OpenXRPackageSettings");
            if (t == null)
                return;
            var m = t.GetMethod("GetOrCreateInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            m?.Invoke(null, null);
            return;
        }
    }
}
