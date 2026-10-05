# Lara first outfit integration

The existing `larafirstoutfit.fbx` and saved DUF now supply Emiko hair, the
Peekaboo dress and Charlene's Closet panties on the approved Lara candidate.
No additional DAZ or bridge export was needed. Appearance awaits user review.

Open **Tools > DAZ Pose > Development > Open Lara First Outfit**. This opens
`Assets/TestData/LaraCandidate/FirstOutfit/FirstPerformanceVoidLaraFirstOutfit.unity`
and selects the performer. The `Lara First Outfit` component toggles hair,
dress and panties; the existing `Lara Anatomy Controls` component retains the
opening, nipple and breast controls. The approved nude candidate and the user's
original FirstPerformanceVoid scene are preserved.

## Geometry and materials

- One canonical body and skeleton remain. Attachment meshes bind to those
  bones; hair adds only `Emiko_leftpiggy` and `Emiko_rightpiggy` under the existing
  head. Their exported pivots remain available for later bone/physics experiments.
- The donor geometry and bind pose are evaluated before rebinding. Rest-pose
  error is below 0.0004 mm for all three attachments.
- Nonempty exported attachment morphs synchronize by semantic name. The dress
  receives a nearest-surface barycentric transfer of the approved native Lara
  breast displacement because this export lacks that garment channel. This is
  a mesh fitting preview; it does not implement DAZ joint ERC or cloth simulation.
- DUF ownership resolves all attachment surfaces and maps. The existing bridge
  converter supplies material parameters to owned Specular/Metallic/Hair dissolve
  graphs. The accepted body skin response is unchanged. Runtime material edits
  survive regeneration.
- All attachments share body-local dissolve coordinates, dissolve properties and
  body `forceRenderingOff`. The existing particle cloud continues to sample the
  body; garment/hair particles are not added in this slice.

## Graft coverage

One Lara body is sufficient for this opaque underwear. Its copied mesh stores
the coverage mask in UV3, and the owned shaders reject covered fragments in
color, depth and shadow rendering. The front panel's actual projected geometry
selects the outer graft region; covered internal Vagina/Fluid surfaces are also
masked. Exposed rear torso and anus are preserved. Hiding the whole graft would
leave a hole because it replaced original body polygons.

The mask is enabled only while panties are shown. Removing panties restores
the desired anatomy morph state immediately; sliders are never reset. Coverage
is disabled on attachment renderers even when the donor uses UV3 for textures.
Only the body owns this authored mask.

Presentation/culling update: review Game and Scene cameras use HDRP TAA, and
hair has geometric specular AA. Attachments refresh a shared body world-bounds
envelope after animation and update while offscreen. The original mesh-space
attachment bounds were invalid relative to their hip root, explaining camera-
dependent disappearance. Body UV3.y now carries independent dress coverage,
with a 10 mm guard at garment openings; UV3.x remains underwear coverage. Both
uniforms are disabled on attachment renderers.

Future garments need their own coverage region, reviewed at openings and edges.
Transparent fabric, displacing skirts and garments that expose the crotch need
a different coverage policy or a flattened anatomy morph. This first mask is
specific to these opaque panties, rather than a universal clothing collision
solution. Extreme poses and transferred garment morph fit remain artistic
review checkpoints.

## Rebuild and verification

Run `scripts/prepare-lara-wardrobe.py` using Blender's bundled Python to resolve
the saved source and write `Assets/TestData/LaraCandidate/wardrobe-manifest.json`.
`LaraFirstOutfitBuilder.Build` verifies FBX/DUF hashes, generates owned assets,
and saves the separate outfit scene. The Open menu preserves an already generated
scene; it does not rebuild over scene edits.

`LaraFirstOutfitValidation.BuildAndRun` checks front/rear/side views, head pose,
breast extremes, nipples, breathing, covered/uncovered opening and full dissolve
restoration using evaluated geometry and HDRP rendering.
`LaraCandidateLiveDiagnostics.RunFirstOutfit` checks actual Play mode GPU skinning,
morph/visibility/dissolve synchronization and clothing coverage toggles.
Reports and images are under `TestOutput/appearance-evidence/first-outfit*`.
Generated licensed geometry and textures remain local and Git-ignored.

Final checks on 2026-10-04: all 14 Play mode phases completed, including clothing
removal/restoration. Full dissolve differed from the empty scene by 6 pixels
in Play mode (5 in evaluated HDRP captures); restoration differed by 12 pixels
in Play mode (0 in evaluated captures), within the 20-pixel comparison limit.
All 389 serialized references in the generated outfit assets resolve in the
main project's Assets or installed Unity packages.
