# Pseudo-photogrammetry: the cleaned floor shows what the camera saw

## What it does

A cleaned floor texel stops being a flat green tint and becomes the photograph taken at
that spot. The reveal follows the coverage brush, so the floor comes back to you as you
work rather than at the end of the room — the room is not revealed at 95%, only
celebrated.

One texture, one mapping. `CoverageFloorVisualizer` already owned a texture over the
play area's world XZ; the photographs are written into the *same* texel grid, which is
why a photograph lines up with the coverage map and why a miniature can sample the same
texture with no copy.

## How a photograph gets onto the floor

`PhotoBakeDriver` takes a frame from an `ISurfaceFrameSource`, then for each texel inside
the camera's footprint works out where that world point lands in the image and copies
the colour there. The render and the bake both go through `PinholeCamera` — the forward
and inverse of one projection, in one place — so a viewport point round-trips through
the floor plane to within 8e-8.

A revealed texel with no photograph of its own takes the blurred average of the
photographed texels around it (`PhotogrammetrySettings.FillRadiusTexels`), not flat tint.
A reveal edge made of hard-edged unphotographed texels reads as a bug; a soft one reads
as depth of field.

Textures are read one texel at a time with `Texture2D.GetPixel`. `GetPixelBilinear` uses
its own coordinate convention and blurs across edges, and `GetPixels32` copies the whole
image on every capture.

## Where the camera comes from

`ISurfaceFrameSource` is the only seam. Whatever is behind it must hand back a readable
`Texture2D`, a pose, a vertical field of view, an aspect and the height of the plane to
project onto.

* **Editor** — `SyntheticFrameSource`. A downward eye above the tool that paints a
  deterministic *world-space* floor pattern. It is a property of world position rather
  than of the image, which is what makes "the floor shows what the camera saw" a claim
  that can be checked: a test asks for the surface colour at a texel's world centre and
  demands the baked texel matches. It is not a render of the room, it has no lighting,
  and it is a placeholder.
* **Device** — not written yet, and the one piece that cannot be exercised in the editor.
  The intended first attempt is the Horizon OS camera via `WebCamTexture` plus the
  runtime camera permissions, falling back to the Meta SDK, rather than assuming the
  SDK up front. **TODO:** the pose and the intrinsics. A `SurfaceFrame` needs the real
  lens parameters, not a guessed 65 degrees, and the pose must be the actual capture
  pose at the moment the frame was taken — a head pose sampled a frame later will smear
  the projection. Until that lands, `CreateFrameSource` returns null off the editor and
  the layer simply does not run.

## The switch

`PhotogrammetrySettings.Enabled`, on by default, read by the driver every frame. Off
mid-session drops every photograph and settles the revealed texels back to the flat tint
exactly, *without* losing which texels were revealed: the floor returns to how it would
have looked if the layer had never run, and turning it back on re-photographs from the
current pose.

## What was checked

* Viewport → floor → viewport: worst error 8.4e-8 over a grid.
* The synthetic render equals the surface function at each pixel's own ray: 0 mismatches.
* Against a smooth world-space gradient, which removes hard-edge resampling noise from
  the measurement: 234 texels baked under a yawed pose, 0 wrong, worst channel sum 2 of
  765. The counter-checks reject — a v-mirrored read would be wrong on 215 texels, an
  x/z swap on 223.
* Live in `HooverMode`, before a chore: 1 capture, 1680 texels photographed, 0 revealed —
  nothing displays until a chore is running. After Standard Coverage and the scan: 25
  revealed texels, all 25 photographed, all 25 showing the photograph, 0 wrong.
* The switch, as above.

Not yet: the dollhouse miniature still shows the tint material. Sharing this texture
needs a per-room UV scale and offset so a miniature room samples its own sub-rectangle
of the play area rather than the whole thing.
