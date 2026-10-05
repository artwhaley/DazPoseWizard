# Lara import: operator manual

Read root AGENTS.md and **Current handoff** in [status](LaraWardrobeImportStatus.md).
This is the operating procedure for the recipe importer. The
[detailed playbook](LaraWardrobeImportPlaybook.md) is an exception reference.
Do not reread historical investigations or write another outfit-specific builder.

The maid's visuals were approved, but its first clean-context Luna run required
shared-tool repairs. It did **not** pass the independent-agent acceptance gate.
The next clean-context trial must use this revised procedure without importer
code changes or another agent taking over. Record that outcome separately from
technical checks and artistic approval; do not call a rescued run a pipeline pass.

## Inputs

User saves one DUF and exports one direct FBX under `Assets/TestCharacter`, with
the approved Lara body/graft and visible followers. A `Genesis8Female` node name
alone does not prove inclusion: `Inspect` verifies nonzero vertices and polygons.
Retain the supplied shoe pose
and relevant follower morphs. The body is a geometry/pose reference; approved
facial/anatomy channels come from the canonical character. No routine bridge
export or additional clean-body export. Agent handles technical work; return to
the user for necessary Daz actions or artistic decisions with a preview.

Use `scripts/LaraWardrobe-MorphExportRules.csv` for wardrobe exports. It exports
the proven nipple control (`PBMNipples`), breathing/belly breathing, the existing
left blink and opening probe; everything else is baked. Keep Lara Breasts at the
approved saved value. The importer supplies the approved mesh-only
`LaraBreastsAdjustment` to clothing by body-surface transfer, rather than treating
a raw Daz breast/ joint-ERC channel as equivalent. It also supplies missing
nipple/breathing follower shapes, so a Daz garment exporting empty channels is
supported but its fit must be reviewed. Original `LaraGraftProbe` CSV is the
minimal probe, not the recommended wardrobe preset. Do not zero breast shape
or nipple controls merely to satisfy the CSV; retain the approved source state.

## Recipe: LLM judgement

Copy `configs/wardrobe/template.json` to a new recipe. Set exact source paths,
unique id and `Assets/TestData/Wardrobe/<id>` destination. Preserve canonical
reference fields. Run Preflight below; read `source-summary.json`, not the huge
manifest. It lists DUF node IDs, labels, visibility and surfaces.

For a missing FBX owner, compare `geometry-inventory.json` with the DUF summary:
use asset URL, label, hierarchy, surface list and visibility to establish ownership.
Put proven `from`/`to` pairs in `ownership`. Never guess from similar names alone.
The second recipe illustrates shell instance suffixes and a source spelling typo.

`policies` entries use exact FBX node names:
- `role`: hair, dress, underwear, footwear or another descriptive equipment role.
- `coverage`: `none` (default), `opaque-front` (body UV3.x), or `opaque-envelope`
  (UV3.y). These are the approved Lara projection algorithms. One owner per
  channel. Start new garments at none; review opaque panels/openings before
  assigning a mask. Sheer fabric needs visible skin; do not mask the whole graft.
- `allowLocalBones`: true by default; disable when unexpected bones need review.
- `transferBodyMorphs`: true by default, except hair. Rigid footwear mechanically
  skips transferred body shapes even when this flag is omitted or true.

Source morphs matching canonical semantic names bind mechanically. For a different
name representing the same control, use `shapeNames` (`from` exported channel,
`to` canonical channel) after proving equivalence. Unmatched local morphs remain
available on the attachment and appear in the report; they are not body-driven.

If shoes are present, set `shoes.node`, `articulation` (`rigid-pair` for pumps,
`skinned` for deforming footwear), optional `capSurface` for a closed toe roof,
and `defaultFootFit` (0.05 is the proven pump default). Decide articulation from
construction, not the filename or the presence of toe skin weights. Pumps can
have toe weights and still need rigid articulation. Current calibration expects one combined shoe
pair with both foot bindings and the supported baked-pose export convention.
Skinned shoes need visual deformation review; the rigid invariance check applies
only to rigid pairs; floor penetration and supported contact checks run for both.
Footwear automatically uses the shared calibration when `poseCalibration` is
omitted; the template makes that default explicit. Its small, shared
`paired-tiptoe-calibration.json` captures the approved lower-leg deformation per
unit of the saved Daz tiptoe control. The helper verifies reference hash, equal
left/right controls in 0–1, and every calibrated point within 0.002 mm before
transferring those points as part of `CapturedHeelFootPose`. Unexplained changes
above the feet still face the 1.5 mm guard. Unequal controls or another pose
pattern need investigation and a shared calibration extension; do not raise the
guard or invent a garment-specific fix. The exact internal Daz driver is not
identified; this is an empirically verified geometry calibration, not a general
Daz pose evaluator. Read the compact `poseCalibration` result in heel-reference
evidence; the agent need not read the calibration's point payload during normal
imports. Split shoes/boot segmentation require a shared capability
extension, not another bespoke outfit builder.

## Commands: mechanical execution

From the repository root, in the signed-in user's shell (Codex uses escalated
exec for Unity licensing and process checks):

```powershell
.\scripts\import-lara-outfit.ps1 -Recipe configs\wardrobe\third-outfit.json -Action Preflight
.\scripts\import-lara-outfit.ps1 -Recipe configs\wardrobe\third-outfit.json -Action Inspect
.\scripts\import-lara-outfit.ps1 -Recipe configs\wardrobe\third-outfit.json -Action All
```

Preflight needs only the DUF. With an FBX available, run Inspect and read the
compact `source-contract.json` beside `source-summary.json`: actual geometry
counts, body inclusion, shoe node/policy, and calibration selection. Inspect does
not launch Unity. Resolve shoe construction before All: rigid sole/heel pumps
use `rigid-pair`; flexible footwear uses `skinned`. If construction cannot be
established from geometry/product evidence, a provisional recipe may run All in
the isolated project to obtain `render/heels.png` and walking captures. Inspect
them, correct the recipe and rerun All before Publish. This is normal recipe
iteration, not permission to change shared code or count a failed check as a pass.
Ask the user only when the intended artistic behavior remains ambiguous after
that evidence. Do not infer flexibility from toe weights.
All checks body geometry and pose before expensive follower conversion.
All stages run in
`.dazposewizard/p0c-native-generation`; the user's active Unity project stays open.
The isolated project must already be staged with canonical assets/packages.
The wrapper refuses concurrent ownership, stages code and selected FBXs, caches
preparation by hashes including texture content, then builds, checks assets,
renders morph/dissolve states and samples 181 walking frames.

Read `TestOutput/wardrobe-import/<id>/report.md` and `report.json` in that project.
Inspect `render/front.png`, back/side/heels and `motion/taa-walking.png`; numeric
passes alone do not approve fit. Failures have a stage log; investigate that stage
and relevant detailed-reference section. `Import` rebuilds without GPU checks;
`Validate` runs checks on the current generated host.

Handle known failures directly:
- `BODY_GEOMETRY_REQUIRED`: request one corrected direct FBX with visible/exported
  approved body/graft, retained pose and followers. No bridge/clean-body export.
- `SHOE_POLICY_REQUIRED`: correct the recipe's node/articulation from construction
  evidence, then rerun Inspect. This is a recipe decision, not a new exporter.
- Tiptoe residual/range/reference mismatch: retain evidence and report a genuinely
  unsupported pose. Do not lower the pose or increase tolerances.
- Changed artist assets at Publish: save edits and rerun All. An identical repeat
  publication is allowed; differing new artist bytes are protected.
For a new technical limitation, describe the exact missing shared capability.
Do not count that import as a completed clean-context demonstration. Routine
diagnostics/recipe repairs do not require user approval; user intervention is for
necessary Daz actions or artistic decisions.

Before treating a new import as repeatable:

```powershell
.\scripts\import-lara-outfit.ps1 -Recipe configs\wardrobe\third-outfit.json -Action ReimportTest
.\scripts\import-lara-outfit.ps1 -Recipe configs\wardrobe\third-outfit.json -Action Validate
.\scripts\import-lara-outfit.ps1 -Recipe configs\wardrobe\third-outfit.json -Action Publish
```

ReimportTest temporarily changes opacity/footwear settings, rebuilds, verifies
GUIDs/settings and restores the values. Publish requires a current passed report,
backs up owned assets and preserves existing main-project artist materials,
footwear settings and review scenes. Concurrent override edits cause publication
to stop; save those edits and rerun All. A new review revision preserves the
previous scene. It does not switch the user's active scene.
Review through **Tools > DAZ Pose > Wardrobe > Imported Outfits**. Save material
and footwear edits; the next import stages those authoritative overrides first.

## Produced assets and handoff

`Outfit.asset` is a WardrobeOutfitDefinition: piece meshes/materials, canonical
bone names, local bone hierarchy, roles, coverage channels and optional footwear.
`ReviewBody.asset` retains all approved character channels plus import-specific
coverage/foot shapes. `Footwear.asset` carries contact/height/fit and toe/body
reference data. Particle bindings/profile are regenerated for the review body.
`Review.unity` is a disposable test host, not a runtime outfit implementation.
Import generates it for isolated automated checks. Scenes are not outfit identity
or the storage for tuning: materials, meshes, recipe and footwear assets are.
The planned persistent clothing setup scene will browse all configured outfits
through the same runtime switcher as the game. Importing an outfit should add
assets to that catalog without adding an outfit-specific production scene.
Save artistic scene arrangements separately. See the pipeline plan for the
next shared switcher/setup-scene contract; it is not implemented yet.
The authoritative [runtime/setup specification](WardrobeRuntimeAndSetupSpec.md)
uses Base/1/2 mesh assignments, default Base, all populated layers shown on load,
and TryRemoveLayer/TryAddLayer commands. The artist assigns the layers in Setup;
import does not infer them. Shoes follow their assigned layer; hair persists.
Existing layer assignments must survive reimport when that runtime milestone is
implemented. Current import tools still produce the existing package format.

Runtime materials are artist assets; regenerated source materials are the baseline.
Reimport adopts changed source properties only where the runtime value still
matches its previous baseline, retaining edited opacity/roughness/maps. Footwear
height/fit are retained; measured ground, heel/toe contacts and reference geometry are regenerated.
Keep output GUIDs. Do not wholesale replace edited assets or vendor shaders.

Report passes, unresolved review items and exact Daz actions if any. Record user
approval in status. Full graft removal leaves a hole; the clean reference lacks
full morph compatibility. Flat-floor support is not sitting/terrain/hand IK.
