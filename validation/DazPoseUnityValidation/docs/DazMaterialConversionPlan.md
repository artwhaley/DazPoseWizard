# Direct FBX material conversion and dissolve plan

**Implementation update:** the body/graft converter and complete Lara scene
candidate are now implemented and verified. See [LaraCandidateIntegration.md](LaraCandidateIntegration.md)
for the current tools, generated assets, GPU dissolve results and remaining
artistic/live review. The first outfit and hair conversion are also implemented;
see [LaraFirstOutfitIntegration.md](LaraFirstOutfitIntegration.md). The proposal
and baseline sections below record the earlier design. General wardrobe capture
and automatic override merging remain later slices.

The user accepted the captured graft opening and seams, rejected the shading,
and requested a repeatable conversion pipeline that avoids a second bridge
geometry export for every outfit. This document proposes that pipeline from
the installed bridge code and local source evidence. Runtime implementation
and material appearance remain for later incremental proofs.

## Recommendation

Use **one direct FBX geometry export plus a material sidecar from the same DAZ
scene**, feeding a project-owned material converter. Reuse the installed
bridge's material mapping rules and shader families. Generate materials with
dissolve support from the outset. A full bridge export is an optional calibration
reference when a new source shader cannot be reconstructed satisfactorily;
it is not a required per-outfit step.

The proposed user workflow is one export command that writes geometry and
material metadata together. Saving/reading DUF supplies the first metadata
prototype now. Capturing live material state during export is needed before
calling the workflow reliable for arbitrary new outfits.

## Evidence already available

- `Assets/Daz3D/Scripts/Editor/DTUConverter.cs` has standalone material conversion
  routines for Iray Uber/PBRSkin, DAZ Studio Default and several hair shaders.
  We can study/reuse those without invoking its skeleton/animation importer.
- The existing bridge material copy tool maps exactly 16 named body slots. It
  cannot safely handle the new 20-slot body or duplicate Torso names.
- Default FBX import loses source settings/maps. Examples include the dress's
  authored bump/normal inputs and the graft Anus texture: saved DUF references
  OMGPSY2 for both Vagina and Anus, while the measured default Unity Anus material
  has no texture map.
- The saved first-outfit DUF supplies 44 effective material records, with 49
  unique texture paths, all found in F:/Daz3D. Local material inheritance and
  geometry ownership resolve without errors. This snapshot includes hidden
  Starlette, so it is not a substitute for current live export selection.
- Forty records explicitly identify Iray Uber. Four have legacy/default channel
  layouts without an explicit studio/material shader tag: Fluid, Vagina, Anus
  and the hidden Starlette Scalp. The anatomy conversion must handle that source
  layout deliberately rather than silently treating everything as HDRP/Lit.
- Body Torso and graft Torso reference the same Ephestra color, bump, normal,
  specular and translucency inputs in the saved scene. Those inputs provide a
  concrete consistency check across the seam. Old Lara bridge materials are
  conversion/reference assets; assigning their old textures would change the
  user's current Ephestra appearance.

## Pipeline contract

1. **Capture.** Export direct FBX and source material metadata together. Record
   source hashes, exporter/tool versions, selected figures and texture hashes.
   Include native source shader identification, channel IDs/names/values, maps,
   gamma, tiling/offsets, UV references and figure/geometry/surface identities.
   Do not infer missing render settings from the impoverished FBX material.
2. **Normalize.** Merge source defaults and overrides into stable material
   records. Resolve all texture references. Give surfaces explicit roles
   (skin, graft skin, wet/transparent, hair, cloth/specular, metal). Bridge name
   heuristics can suggest roles; explicit overrides resolve cases like a plastic
   hairband or legacy anatomy material. Preserve unsupported rendering inputs
   in a report rather than dropping them without notice.
3. **Convert.** Adapt normalized records to the installed bridge's material
   routines where they can be isolated. Wrap or port narrow rules where the
   vendor API would overwrite files or alter global import settings. Pin/version
   that behavior. Keep source/accepted reference materials intact. Preserve
   native color, SSS, roughness/specular, dual-lobe, coat, normal/bump and alpha
   settings through the available target families; report approximations.
4. **Create runtime assets.** Use permanent project-owned dissolve shader
   variants and generate namespaced material/texture assets. Deduplicate textures
   by content and import role: a color, mask, bump and tangent-space normal do
   not all have the same import settings. Respect UV tiles and source gamma;
   do not pack or flatten channels without an explicit conversion recipe.
5. **Bind.** Produce an explicit renderer/slot-to-source-surface map, including
   merged graft provenance. A key includes asset identity, geometry and surface,
   not just the display name. Keep stable generated asset IDs on repeat runs.
   Flag ambiguous slot matches. Store artistic overrides separately from
   regenerated source-derived values so reimport preserves accepted tuning.
6. **Validate and review.** A dry run reports source shader families, missing
   maps, unsupported features, ambiguous bindings and dissolve readiness before
   assignment. A comparison preview uses fixed, known lighting. Human review
   accepts appearance; rendered dissolve tests verify complete disappearance,
   return and shadow/depth coverage.

## Dissolve is a required part of every family

The existing project owns `uDTU HDRP SSS Dissolve.shadergraph` and
`Wet Dissolve.shadergraph`, plus the common `PerformerDissolveField.hlsl`.
The SSS graph retains all 67 source properties and adds eight dissolve properties;
Wet retains all nine source properties and adds the same eight. They are useful
starting templates rather than grounds to rebuild the appearance shaders.

| Target family | Available source | Dissolve work |
| --- | --- | --- |
| Skin/SSS, including suitable graft surfaces | uDTU HDRP SSS | Existing owned variant; verify source mappings and diffusion profile |
| Eye wet layer and appropriate fluid/glass surfaces | Wet or suitable transparent family | Existing Wet variant for compatible cases; deliberate refraction/opacity recipe |
| Hair with strand cutout | uDTU HDRP Hair | Add owned variant preserving source opacity, double-sided shading and cutoff |
| Cloth/plastic/specular/metal | uDTU HDRP Specular / Metallic | Add owned variants preserving each workflow and cutout/transparency settings |

A transparent cloth or fluid must not be forced into Wet merely to get dissolve.
Select a family that can represent its source appearance and implement the same
dissolve contract there. With dissolve disabled, base alpha/cutout/refraction
and surface emission retain their normal behavior. With dissolve complete, the
surface, highlights and relevant depth/shadow passes must disappear.

The eight current controls are `_DissolveEnabled`, `_DissolveProgress`,
`_DissolveBoundsMin`, `_DissolveBoundsSize`, `_DissolveFieldParams`,
`_DissolveEdgeWidth`, `_DissolveEdgeColor` and `_DissolveEdgeEmission`.
The converter checks their presence and known shader-family compatibility;
property presence alone does not prove that a shader actually clips correctly.

The current rig/profile enforce one renderer, exactly 16 materials and two
hard-coded shader families. It uses that renderer's local bounds/object position
for the field. A converted 20-slot body therefore cannot simply be substituted
and declared integrated. A later bounded change must describe participating
renderers/materials and validate every participating shader. Share one performer
coordinate frame, field, progress and stable bounds across body, graft, clothing
and hair so their dissolves line up. Use property blocks on permanent materials;
avoid material swapping or allocations per frame.

Adding new geometry also changes particle surface sampling. The existing particle
binding assets must be revalidated/rebaked when the body mesh changes. Garment
particle emission policy is a separate later decision, not a reason to delay
shader compatibility in the material converter.

## Proposed tool and incremental implementation

The eventual tool is a DAZ export command paired with a Unity **Convert DAZ
Appearance** editor window. The window accepts FBX plus sidecar, offers a dry run,
reports planned surface roles/mappings and unsupported inputs, generates owned
material assets, and assigns them to an isolated preview before an accepted
character/outfit is published. A cached recipe handles subsequent imports.

The first implemented component is `scripts/inspect-daz-materials.py`. It reads
gzip or plain DUF, merges local material-library channels with scene overrides,
retains source identities and shader/channel/UV metadata, resolves texture paths,
and emits a read-only audit sidecar. It reports external/unresolved inheritance
rather than guessing. It does not yet create DTU-compatible values, convert
colors, create materials, author shaders or assign scene renderers.

Next implementation slice: **convert only the 20 body/graft slots in the accepted
opening preview**, using current source appearance and project-owned shader
variants. Validate legacy anatomy inputs, normal/bump treatment and the matching
torso settings. Add a simple dissolve control to that material preview and review
normal appearance plus dissolve at 0, halfway and 1 under known lighting. This
proves the source adapter and mandatory shader behavior without building the
wardrobe system or rewriting performer/VFX integration in the same slice.

Then extend to the chosen Emiko hair and two garments, one family at a time.
After appearance and dissolve are accepted, broaden performer bindings beyond
the old 16-slot fixture while retaining its existing accepted behavior.

No new bridge export is necessary to start the body/graft conversion proof.
If a source layout or visual mismatch remains ambiguous, request **one reference
bridge export of the current body/graft appearance** to calibrate and encode
the missing rule. It should eliminate that uncertainty for later assets, not
become an additional geometry export for every outfit. Truly new unsupported
source shaders may still need a new recipe/reference once.

## Current verification and limits

The material extraction ran successfully on the real saved DUF: 44 records,
49 texture paths, no missing textures or unresolved local owners/inheritance.
Graph-property inventory verified the SSS/Wet base-property preservation and
the missing dissolve contract in the vendor Hair/Specular/Metallic graphs.
The installed bridge's conversion and current dissolve binding code were read.
No new materials or shaders were assigned, and no performer runtime was changed.

Local evidence is under `TestOutput/appearance-evidence/`:
`firstoutfit.material-sidecar.audit.json`,
`material-pipeline.shader-inventory.json` and
`material-pipeline.converter-input-audit.json`. Those licensed scene-derived
records remain local. This is a conversion plan and tested extraction prototype;
converted shading and broader dissolve integration are not implemented or accepted.
