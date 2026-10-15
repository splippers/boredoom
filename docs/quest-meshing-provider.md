# Quest mesh provider — what is missing, and the exact change

**Status: not applied.** This is the change Jon needs to make; it touches `Packages/manifest.json`,
which agents must not edit. Until it is applied, the device scan cannot see the room and falls back
(see "What happens without it" below).

## What is missing

`SpatialMeshTracker` reads environment geometry from an `XRMeshSubsystem`
(`UnityEngine.XR`, surfaced through `SubsystemManager`). That is the whole mechanism: it asks the XR
runtime for meshes, and meshes what comes back.

Unity's OpenXR plugin does **not** provide a mesh subsystem. Meshing is an OpenXR *vendor* extension
(`XR_FB_triangle_mesh`, `XR_MSFT_spatial_graph_bridge`), not part of core OpenXR, so the subsystem has
to come from a provider plugin. The current manifest has:

```
com.unity.inputsystem              1.7.0
com.unity.xr.core-utils            2.5.1
com.unity.xr.management            4.4.0
com.unity.xr.openxr                1.11.0
com.unity.xr.interaction.toolkit   2.5.4
com.unity.ugui                     1.0.0
com.ivanmurzak.unity.mcp           0.72.1
com.unity.modules.imageconversion  1.0.0
com.unity.modules.unitywebrequest  1.0.0
```

None of those is a mesh provider. (`com.unity.xr.openxr` ships a `Samples~/MeshingFeature` sample, but
it is a demo that serves a teapot mesh from a native plugin — it is an example of *how* to write a
provider, not one.) So `SpatialMeshTracker.MeshCount` stays 0 on device.

## The change

Add the Meta OpenXR provider, which implements the Quest AR Foundation subsystems — including the
mesh subsystem backed by `XR_FB_triangle_mesh`:

```diff
--- a/Packages/manifest.json
+++ b/Packages/manifest.json
@@
     "com.unity.xr.core-utils": "2.5.1",
+    "com.unity.xr.arfoundation": "6.2.0",
     "com.unity.xr.management": "4.4.0",
+    "com.unity.xr.meta-openxr": "2.2.0",
     "com.unity.xr.openxr": "1.11.0",
     "com.unity.xr.interaction.toolkit": "2.5.4",
```

**Confirm both version numbers in Package Manager before applying** — they track the editor version
(6000.4.5f1) and this note should not be trusted for them. Adding `com.unity.xr.meta-openxr` in the
Package Manager UI will pull `com.unity.xr.arfoundation` as a dependency; adding it explicitly just
makes the pin visible.

## Steps after the manifest change

1. Open **Project Settings → XR Plug-in Management → OpenXR** (Android tab).
2. Under **Feature Groups**, confirm **Meta Quest Support** is enabled (the existing
   `Assets/Editor/BoreDoomXrManagementBootstrap.cs` already enables it).
3. Enable the Meta OpenXR feature group's **Meshing** / **Scene** feature. This is what registers the
   mesh subsystem.
4. If meshing still does not appear at runtime, the provider likely wants an AR Foundation session to
   be live: add an `ARSession` and `ARInputManager` to the XR rig. On a plain OpenXR setup the loader
   starts the session itself, which is why this may not be needed — but it is the first thing to try.
5. Enable **passthrough** in the same place (needed for the pseudo-photogrammetry capture path).

No code change is needed for meshing. `SpatialMeshTracker` and `MeshRoomExtractor` already work
against any `XRMeshSubsystem`; they simply start receiving meshes once a provider exists.

## What happens without it

The scan completes and yields one room rather than failing:

- if the guardian boundary is available (plain OpenXR — no new package), the room is the player's
  real play space (`PlaySpaceBoundary`);
- otherwise it is an assumed 3×3 m room centred on the origin.

Both paths log which one was taken. Everything downstream (dollhouse, coverage, HUD) works
identically — the game is playable, it just cannot see the walls.

## Alternative: Meta XR All-in-One SDK / MRUK

`docs/quest3-build.md` already recommends importing the Meta XR SDK. The full SDK (with
`Meta.XR.MRUtilityKit`) is the richer long-term path: it gives the scene *model* (walls, floor,
furniture as labelled volumes) and room anchors, rather than raw triangles that have to be segmented.
That is the intended replacement for the `MeshRoomExtractor` heuristic. It does not need the
AR Foundation route above. The choice between the two is Jon's; it is a package decision, not a code
one, because `IScanSource` is the only seam either way.
