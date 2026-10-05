# Lara body/graft candidate

The local candidate combines the accepted closed/open graft capture with the
first-outfit facial and nipple channels and materials reconstructed from the
saved DUF. It runs on the existing FirstPerformanceVoid performer. This is the
body/graft review checkpoint; appearance and live performance remain for the
user to judge in Unity.

## Open and review

In Unity, choose **Tools > DAZ Pose > Development > Open First Performance Void
Lara Candidate**. The editor offers to save the currently modified scene, then
opens a copy of FirstPerformanceVoid with the replacement body and all existing
scene controls. Select the performer root to use **Lara Anatomy Controls** in
the Inspector during Play Mode: Captured Opening, Nipples and Lara Breasts.
The existing visibility IN controls reveal Lara if the scene starts hidden.
Use the existing speech, expression, pose, teleport and dissolve controls for
the live integration review.

For direct Edit Mode sliders, select the body renderer and choose **Tools > DAZ
Pose > Development > Lara Candidate Controls**. It includes opening, left blink,
speech AA, nipples, breast adjustment and dissolve. In Play Mode, the existing
scene controls retain ownership of dissolve effects.

After appearance is accepted, **Replace Lara In First Performance Void** applies
the same conversion to the working scene, first saving a local scene backup.
Both commands preserve the existing performer and animation hierarchy.

## Implemented behavior

- The generated readable mesh has 21,133 vertices and 124 channels. Its first
  101 channels preserve the original Lara's exact names and Unity indices.
- The first-outfit and closed exports have identical base positions/topology
  and matching base normals. Raw-point correspondence transfers evaluated
  facial/nipple channels onto the accepted closed body. Coincident points must
  agree across every transferred channel before the mapping is accepted.
- The repeated graft-only export bias is subtracted. Facial channels have zero
  graft position and normal movement. The previous raw FBX normal adaptation
  caused grey reflective patches during deformation, resembling a second body.
  The builder now derives area-weighted normal changes from each target mesh,
  sharing those changes across UV/material splits at each welded source point.
  Imported rest normals and all approved position deltas remain unchanged.
- CapturedOpening reproduces the evaluated closed/open positions. Its normals
  are derived from that geometry. Its exact DAZ dial combination
  is still unconfirmed; it is labeled as a captured opening.
- PBMNipples is available as a runtime control. Other exported nipple channels
  remain in the mesh but are not exposed as additional artistic controls.
- LaraBreastsAdjustment maps the authored breast DSF through surviving host
  polygon correspondence. The control adjusts relative to the saved DUF's baked
  value, currently 0.5555556. Normal changes are derived from the adjusted mesh.
  **This is a mesh preview; DAZ joint-center/end-point ERC is not implemented.**
- All 170 canonical weighted bones are reused. Only Vagina, Fluid and Anus
  transforms are added under canonical pelvis/Vagina parents, with rest
  transforms derived from the donor bind matrices. No follower Genesis rig is
  added to the runtime scene.
- Twenty material slots bind explicitly by source figure and surface identity.
  The installed bridge's standalone material routines consume native DUF values
  and resolved texture files; no bridge geometry import is needed. Body/graft
  Torso color, normal and height texture references are identical. The legacy
  Anus texture is recovered from the DUF.
- Owned SSS, Wet and Specular dissolve shaders cover all participating body
  materials. A Metallic variant is also derived for later material recipes.
  The profile supports explicit converted slot shaders while retaining its
  original 16-slot contract when that list is empty.
- The new body receives a separate 32,768-entry particle binding asset and a
  copied dissolve profile. SALSA and the particle/dissolve configuration checks
  pass against the existing scene components.

## Local regeneration

Run `scripts/prepare-lara-candidate.py` with Blender's bundled Python from the
repository root, passing `--duf` with the saved scene path. The default content
root is F:/Daz3D. `scripts/build-dissolve-variants.py` derives owned Specular and
Metallic graphs without changing vendor assets. Unity's
`DazPose.UnityValidation.LaraCandidateInstaller.BuildAll` builds the combined
mesh, source/runtime materials and complete scene copy.

Generated meshes, manifests, textures, materials, scene copies and particle
bindings stay in ignored `Assets/TestData/LaraCandidate`. Runtime materials
retain stable asset identities. Source materials regenerate separately;
existing runtime materials preserve their current tuning in full. Automatic
merging of later DUF changes with artistic overrides is not implemented yet.
New skin materials copy the accepted FirstPerformanceVoid roughness/specular/
top-coat scalars. The explicit **Copy Accepted Lara Skin Response** command
also applies those settings to existing candidate materials without changing
their textures, colors, shader, or dissolve properties.
Hair/clothing bindings and a general wardrobe conversion window are later
slices; unsupported shader families fail rather than receiving a guessed shader.

The user approved opening, nipples, and breast adjustment after reviewing the
runtime anatomy controls. No further anatomy export is currently needed.
In Play Mode, adjust these through **Lara Anatomy Controls**; it owns their
renderer weights each frame. Eighteen of the twenty-one exported nipple
channels have no position changes on this Lara. The useful general Daz control
is **Nipples** (`PBMNipples`); its evaluated export includes Lara's corrective.

## Verification

The latest checks ran in a separate Unity project with the current source and
assets. All 21,133 vertices matched raw points, with maximum positional error
1.335e-7 meters. Original morph indices, zero facial graft movement and matching
torso texture references passed. Canonical bind matrices differ by at most
2.467e-5 in their largest component; all 170 canonical bones are reused.

The actual HDRP GPU camera check rendered visible, half dissolved, fully
dissolved and restored states. Full dissolve matched an adjacent disabled-body
background within two pixels at a three-byte color tolerance; restoration
matched the initial visible image exactly. Halfway coverage changed from
77,113 to 43,624 differing pixels. The adjacent background control avoids an
HDRP initialization difference in the first capture. These checks validate the
rendered native surface; live particle choreography and artistic appearance
still need review in the scene.

The repaired mesh was also captured across actual Play Mode frames: nipples,
breathing at 100/300/600, both breast endpoints, facial shapes, combined anatomy,
full dissolve and restoration. Position-only and recomputed-normal comparisons
isolated the earlier grey patches to the old normal recipe. Rebuilt normal
frames eliminated them at ordinary weights. At 600% the approved breathing
geometry itself folds strongly; this extrapolation remains a stress case.
With unrelated animated smoke disabled for repeatability, restored combined
appearance differed by 12 pixels at a three-byte tolerance. See `live/restore.json`
and `live/phase-0.png` through `phase-10.png`. The live batch entry is
`LaraCandidateLiveDiagnostics.Run` without `-quit`; it exits after capture.

Reports and image captures are under local
`TestOutput/appearance-evidence/`: `lara-candidate.validation.json`,
`lara-scene-integration.validation.json`, and `render/validation.json`.
`LaraCandidateRenderValidation.BuildAndRun` is the graphics-enabled batch entry.
Its neutral comparison uses temporary HDRP lighting/exposure and does not save
those settings into the validation scene.
