using System;
using Chorewars.Diagnostics;
using Unity.XR.CoreUtils;
using UnityEngine;
using UObject = UnityEngine.Object;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
#endif

namespace Chorewars.Bootstrap
{
    /// <summary>
    /// Prototype scenes were saved as empty YAML (no Camera / XR rig), which yields a black HMD on Quest
    /// even though Unity starts. This builds a minimal tracked rig once and keeps it across scene loads.
    /// Failures here run before the first scene; any exception can prevent rendering — use fallbacks + logs.
    /// </summary>
    public static class QuestXrRigBootstrap
    {
        public const string LogPrefix = "BoreDoom/Startup";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureXrRigBeforeFirstScene()
        {
            SubsystemHealth.BootstrapRig = SubsystemStatus.Initialising;
            TelemetryLogger.Info("Bootstrap", "BeforeSceneLoad (first scene not loaded yet).");
            Debug.Log($"{LogPrefix}: BeforeSceneLoad (first scene not loaded yet).");

            try
            {
                if (UObject.FindObjectsByType<XROrigin>(FindObjectsInactive.Include).Length > 0)
                {
                    SubsystemHealth.BootstrapRig = SubsystemStatus.Ok;
                    SubsystemHealth.ArCamera = SubsystemStatus.Ok;
                    TelemetryLogger.Info("Bootstrap", "XROrigin already present, skipping auto rig.");
                    Debug.Log($"{LogPrefix}: XROrigin already present, skipping auto rig.");
                    return;
                }

                BuildXrRig();
                SubsystemHealth.BootstrapRig = SubsystemStatus.Ok;
                SubsystemHealth.ArCamera = SubsystemStatus.Ok;
                TelemetryLogger.Info("Bootstrap", "XR rig created.");
                Debug.Log($"{LogPrefix}: XR rig created.");
            }
            catch (Exception e)
            {
                SubsystemHealth.BootstrapRig = SubsystemStatus.Failed;
                TelemetryLogger.Error("Bootstrap", "XR rig failed, using minimal camera only.", e);
                Debug.LogError($"{LogPrefix}: XR rig failed, using minimal camera only.\n{e}");
                BuildMinimalCamera();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCameraAfterFirstScene()
        {
            var scene = SceneManager.GetActiveScene();
            var cameras = UObject.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            var line = $"AfterSceneLoad scene=\"{scene.name}\" cameras={cameras.Length}.";
            TelemetryLogger.Info("Bootstrap", line);
            Debug.Log($"{LogPrefix}: {line}");

            if (cameras.Length == 0)
            {
                TelemetryLogger.Warn("Bootstrap", "No camera after first scene load — creating fallback.");
                Debug.LogWarning($"{LogPrefix}: No camera after first scene load — creating fallback.");
                BuildMinimalCamera();
            }

            AttachRuntimeDiagnostics();
        }

        private static void AttachRuntimeDiagnostics()
        {
            var diagGo = new GameObject("BoreDoom Xr Diagnostics");
            diagGo.AddComponent<XrHeadsetDiagnostics>();
            UObject.DontDestroyOnLoad(diagGo);
        }

        private static void BuildXrRig()
        {
            var xrOriginGo = new GameObject("XR Origin");
            var xrOrigin = xrOriginGo.AddComponent<XROrigin>();

            var floorOffset = new GameObject("Camera Floor Offset");
            floorOffset.transform.SetParent(xrOriginGo.transform, false);

            var camGo = new GameObject("Main Camera");
            try
            {
                camGo.tag = "MainCamera";
            }
            catch (Exception e)
            {
                TelemetryLogger.Warn("Bootstrap", $"Could not set MainCamera tag: {e.Message}");
                Debug.LogWarning($"{LogPrefix}: Could not set MainCamera tag ({e.Message}).");
            }

            camGo.transform.SetParent(floorOffset.transform, false);

            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.stereoTargetEye = StereoTargetEyeMask.Both;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.1f, 1f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 500f;

            // The game is AR: the player must always see their real surroundings. On the headset the camera
            // is cleared to transparent and an ARCameraManager turns passthrough on (the Meta camera feature
            // creates the passthrough composition layer). The ARSession starts the subsystems, camera and
            // meshing alike. The editor has no passthrough, so it keeps the dark background.
            if (!Application.isEditor)
            {
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camGo.AddComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();

                var sessionGo = new GameObject("AR Session");
                sessionGo.AddComponent<UnityEngine.XR.ARFoundation.ARSession>();
                UObject.DontDestroyOnLoad(sessionGo);
                TelemetryLogger.Info("Bootstrap", "Passthrough: ARSession + ARCameraManager added, camera cleared transparent.");
            }

#if ENABLE_INPUT_SYSTEM
            var poseDriver = camGo.AddComponent<TrackedPoseDriver>();
            poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            poseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            var posAction = new InputAction(type: InputActionType.PassThrough, expectedControlType: "Vector3");
            posAction.AddBinding("<XRHMD>/centerEyePosition");
            var rotAction = new InputAction(type: InputActionType.PassThrough, expectedControlType: "Quaternion");
            rotAction.AddBinding("<XRHMD>/centerEyeRotation");
            posAction.Enable();
            rotAction.Enable();
            poseDriver.positionInput = new InputActionProperty(posAction);
            poseDriver.rotationInput = new InputActionProperty(rotAction);
#endif

            xrOrigin.CameraFloorOffsetObject = floorOffset;
            xrOrigin.Camera = cam;

            // The game puts its floor at y = 0 and everything else relative to it, so the tracking origin has to
            // BE the floor. Left unspecified, the headset's height is not tied to the real floor and the placeholder
            // floor can sit half a metre below the head, slicing through menus.
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            UObject.DontDestroyOnLoad(xrOriginGo);
        }

        private static void BuildMinimalCamera()
        {
            if (UObject.FindObjectsByType<Camera>(FindObjectsInactive.Include).Length > 0)
                return;

            var camGo = new GameObject("Fallback Main Camera");
            try
            {
                camGo.tag = "MainCamera";
            }
            catch (Exception e)
            {
                TelemetryLogger.Warn("Bootstrap", $"Fallback could not set MainCamera tag: {e.Message}");
                Debug.LogWarning($"{LogPrefix}: Fallback could not set MainCamera tag ({e.Message}).");
            }

            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.stereoTargetEye = StereoTargetEyeMask.Both;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.05f, 0.05f, 1f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 500f;

            UObject.DontDestroyOnLoad(camGo);
            TelemetryLogger.Info("Bootstrap", "Fallback camera created (magenta-tinted clear).");
            Debug.Log($"{LogPrefix}: Fallback camera created (magenta-tinted clear for diagnosis).");
            SubsystemHealth.ArCamera = SubsystemStatus.Ok;
        }
    }
}
