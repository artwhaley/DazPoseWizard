# Partner character handoff for hand/contact integration

## Entry points

- Prepared character prefab: `Assets/PartnerProof/PartnerCharacter.prefab`.
- Male acceptance scene: `Assets/Scenes/PartnerRigAcceptance.unity`.
- Preparation: `Tools > DAZ Pose > Partner Proof > Prepare Skinned Character and Materials`.
- Source recipe: `scripts/prepare-partner-character.py`, run with Blender 4.5's bundled Python from the repository root.
- Numeric/material audit: `TestOutput/PartnerCharacter/Preparation.json` in the Unity project.

The prefab is for placing the same accepted male in the hand integration scene.
It carries the body, lashes, textured shell, original skeleton, generated
erection channels and acceptance controller. Its camera reference is cleared.
Use the existing smoke panel when exposing its controls; the controller has no
standalone GUI window. Do not add another overlapping control panel.

## Shell deformation

The canonical source is `playererect.fbx`; the second authored endpoint is
`playerflacid.fbx`. The shell source is unskinned, but its 2,205 raw control
points have a verified bijection to the 2,205 anatomy-region points in the
merged body. All 2,177 polygons match through that mapping in all seven
material regions at both endpoints. Positions differ by the small authored
shell offset; matching by nearest vertex alone is ambiguous around some folds.

The Unity recipe maps imported vertices back to those raw control points,
handles Unity's UV-seam duplicates, and copies the **imported** body influences
exactly. Duplicate body vertices must have identical influences. The shell
uses the body's bone order and renderer-compatible bind poses. It retains all
available influences; no independent raw-FBX weight normalization is applied.
Both shell endpoint positions are solved through the transferred skinning so
adding weights preserves the authored erection transition.

`PartnerAnatomyTestController.ShellFollowsBones` exposes the result. Shaft1,
shaft4 and distributed bend are enabled with the shell visible at
`Erection01 == 1`. Intermediate-state bone bending remains outside the
accepted proof because the skeleton keeps the canonical erect pivots.

Skinning transfers bone motion. It does **not** automatically transfer a future
body squeeze morph or local mesh deformation. Use the verified correspondence
to apply corresponding shell deltas, preserving its offset. The reusable
mapping lives in `Assets/TestData/PartnerCharacter/manifest.json`.

## Materials

The recipe reads the **current `playererect.duf`** through the shared DSON
material inspector and checks its hash. It resolves the host, graft,
eyelash and shell ownership into 32 used Unity material slots and all 26
referenced texture sources. UVs and slot topology are retained.

Conversion uses the installed DAZ-to-Unity `DTU.ConvertToUnityIrayUber` pipeline
and its HDRP SSS/specular/metallic, eye and lash shader families underlying
Lara's materials. Partner dissolve behavior is outside this preparation.
Source conversions
and runtime materials remain separate under
`Assets/TestData/PartnerCharacter/{SourceMaterials,RuntimeMaterials}`.
Texture imports distinguish color, linear data, normal and opacity roles.
The shell retains its authored diffuse, dual-lobe reflectivity, bump,
cutout-opacity and surface-specific normal assignments; it is not globally
assigned the Wet shader. Eyes and lashes retain their bridge shader families.

Skin surfaces copy Lara's accepted response from
`Assets/DazPose/Effects/Dissolve/LaraRuntimeMaterials`: roughness, the two
specular lobe roughness values, glossy weight, dual-lobe weight/ratio and
top-coat weight/roughness. Partner colors and texture assignments remain its
own. This includes the correction that reduced Lara's overly slick bridge
appearance. The shell uses `Assets/PartnerProof/PartnerShell.shadergraph`, an
alpha-blended variant of the bridge HDRP specular graph. Its authored opacity
map controls continuous coverage, including the soft root fade. Alpha clipping,
transparent depth writing and preserved specular lighting are disabled so
highlights also fade with coverage. Shell roughness is .8, second-lobe roughness
.75, glossy and dual-lobe weights .2, and top-coat weight zero.
`PartnerCharacterPreparation.RefreshShellMaterials` reapplies this correction
without rebuilding the scene or skinning. Seven live materials were updated;
the shader compiled successfully. Final appearance remains subject to visual review.

## Hand integration contract and remaining scope

- `Erection01` clamps to 0..1 and drives both authored endpoint shapes. Erection
  still includes the artist-authored length/girth changes; do not normalize
  those dimensions silently.
- `StructuralBody` and `ShellRenderer` expose the two evaluated renderers for
  surface queries and future squeeze work.
- `GetContactFrame(position, out centerRoot, out tangentRoot, out radius)`
  returns values in the partner root's coordinate frame. Convert with the
  partner root's `TransformPoint`/`TransformDirection`. Keep character scale at
  one unless integration explicitly handles scaling the radius.
- `Position01` moves the existing contact preview; it does not animate a hand.
- The centerline/radius are the prior endpoint-derived contact approximation.
  They do not yet sample the currently bent/squeezed mesh. Start integration
  with zero procedural bend and add evaluated-surface contact deliberately.
- The pelvis slider is an acceptance diagnostic that moves the main pelvis
  and consequently the body/legs. Leave it at zero for hand integration.
  The acceptance controller is not a production pose-animation owner.
- `HandGripAcceptance.unity` is owned by the finger work. This preparation
  does not insert the male there or reposition its grip rig; the hand agent
  should use the prepared prefab in the intended integration setup.

## Verification status

Unity 6000.5.9f1 preparation passed and its outputs were copied to the live
project. The isolated run exited successfully; the live editor was not driven.

- All 2,389 imported shell vertices have copied weights across 185 bone entries
  (maximum six influences on this anatomy overlay). The imported influence
  arrays match the corresponding body vertices exactly.
- Maximum rendered shell errors: erect 0.00006665 mm; flaccid 0.00006145 mm.
  Five interpolation samples also pass the 0.01 mm tolerance.
- Shaft1 and shaft4 at both -5 and +5 degrees move the visible shell. The
  smallest maximum surface movement among those cases is 7.578 mm. Maximum
  shell/body gap across the cases is 0.207668 mm.
- All 32 material slots are populated. Supported core diffuse/normal/bump/
  cutout/specular maps match their DUF source file hashes and import roles.
  All 26 referenced source textures resolve. Shader families are bridge HDRP
  SSS, Specular, Wet (eyes) and Hair (lashes).
- The saved scene and prefab reference all 32 converted runtime materials.
  The saved prefab retains its body/shell controller and all bone references,
  and its scene camera reference is null.

Refresh Unity and reopen `PartnerRigAcceptance.unity` to load the new serialized
bone/material references into an already-open scene. Final material appearance
remains subject to user review. The acceptance controller remains a test harness;
the hand agent owns integrating its contact/pose behavior into the hand system.

Licensed FBX/DUF files, texture copies and the local manifest remain local
assets; a clean machine needs those sources and the recipe before rebuilding.
No commit is part of this preparation request.
