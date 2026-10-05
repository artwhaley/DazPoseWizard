# Lara wardrobe import status

Updated 2026-10-04. Procedure: [LaraWardrobeImportPlaybook.md](LaraWardrobeImportPlaybook.md).

## Current handoff

User reviewed the second outfit repairs and explicitly approved opacity and
walking: "opacity fixed, walk fixed." No further Daz export is needed for the
current prototype. The shared recipe importer and operating manual are now built
and **outfit three is imported, published and functionally approved by the user**.
Both existing outfits passed the shared asset,
render/dissolve and 181-frame walking checks. Reimport tests retained GUIDs and
edited opacity; the second retained footwear height/fit. Material baseline merge,
source hashes, independent coverage and donor exclusion also passed. Read
[LaraWardrobeImportQuickStart.md](LaraWardrobeImportQuickStart.md) first and
[LaraWardrobePipelinePlan.md](LaraWardrobePipelinePlan.md) for the implementation
sequence. Production equip/unequip, sitting/terrain IK and sheer genital coverage
remain unfinished; the walking approval does not imply those are implemented.
The user has now requested the next runtime/setup specification and defined its
three-layer dressing rules. Read
[the execution entry point](wardrobe-execution/START.md) for that work; it contains
ordered implementation stages, a fixed contract and resumable progress tracking.
[WardrobeRuntimeAndSetupSpec.md](WardrobeRuntimeAndSetupSpec.md) remains the product
reference. Runtime switching and the new execution runners are not yet implemented.
FinalIK remains outside this milestone. Independent-agent import acceptance is
still unpassed and is tracked separately from the approved visual result.

Current commands: `scripts/import-lara-outfit.ps1`; recipes in `configs/wardrobe`.
Generated data is `Assets/TestData/Wardrobe/<id>/Outfit.asset`, not another
outfit-specific runtime builder. Reports/captures are published to
`TestOutput/wardrobe-import/<id>`. First: 3 pieces / 17 surfaces / 2 local bones /
124 character channels; second: 17 pieces / 61 surfaces / 5 shells / 126 channels.
Full dissolve/restore differences: first 10/0 pixels, second 14/0, limit 20.
Walking: no corrected bounds/frustum misses; second pump shape error below
0.0005 mm, support error 0.188 mm and floor penetration 1.948 mm over 94 supported
samples. Existing main-project materials/baselines were staged and republished
through the wrapper; a new review revision preserved its preceding review scene.
Original accepted candidate/first/second scenes remain separate and preserved.

Third input request: one saved DUF and one direct FBX with the approved Lara/graft
reference, visible outfit and relevant followers/morphs; retain supplied shoe pose.
No routine bridge export or new clean reference.

## Third import: shared footwear pose calibration

Created `configs/wardrobe/third-outfit.json` for `laramaid.duf` and
`Assets/TestCharacter/laramaid.fbx`, targeting
`Assets/TestData/Wardrobe/third-outfit`. DUF preflight passed: 43 materials,
50 textures, no missing textures. The remade FBX now has a topology-matching
`Genesis8Female` mesh with 18,080 points / 17,856 polygons and the four graft
surface groups (`Torso`, `Fluid`, `Vagina`, `Anus`) on that mesh. This confirms
the graft is included as merged surface geometry. The four exact outfit owners
also extract successfully: `shoe_12520` (12,520 points / 6 surfaces),
`Stockings_5340` (5,340 / 4), `dress_7510` (7,510 / 6), and `Apron_6708`
(6,708 / 7). Render inspection shows rigid high-heeled pumps. The recipe now
uses `rigid-pair` with body-morph transfer disabled for shoes; toe weights alone
do not establish deforming footwear construction.

The initial stop was a 2.327 mm lower-leg difference above the helper's arbitrary
0.4 m foot cutoff. The 46 points exceeding 1.5 mm have shin/thigh influences,
not foot weights. All 226 measurable upper-region deltas match the accepted
second outfit's displacement scaled by 0.75/0.35, with maximum residual
0.000237 mm. The shared helper now verifies/transfers this empirical paired
tiptoe correction using `configs/wardrobe/paired-tiptoe-calibration.json`.
It retains the 1.5 mm guard for unexplained changes, verifies reference hash,
limits equal left/right controls to 0–1, and rejects per-point residual above
0.002 mm. The calibrated upper support includes 272 points (including tiny
displacements). Both existing exported references also pass classification.
Five synthetic guard tests pass, including unrelated body-change rejection.
The actual internal Daz driver remains unidentified; the primary foot-down JCM
does not explain the rejected points. Do not claim a general pose evaluator.

The first export's empty body issue was corrected by the user. The current FBX
was checked against the previous staged input and has a new hash. The shared
import has now built four pieces / 23 surfaces / 126 body channels with measured
shoe lift 0.115710 m. Asset, render/dissolve and rigid-pump walking checks pass:
181 frames, no corrected bounds/frustum misses, maximum rigid shape error
0.000535 mm, support error 0.147 mm, floor penetration 1.440 mm, 94 supported
samples. Dissolve remaining/restored pixels are 0/0. Reimport preservation passed
for GUIDs, artist opacity, footwear height and fit. Final refreshed asset/render/
walking checks passed, including agreement between the actual captured Unity
pose and all classified source deltas. Published review:
`Assets/TestData/Wardrobe/third-outfit/Review-20261004-165635.unity`; evidence:
`TestOutput/wardrobe-import/third-outfit`. Open through **Tools > DAZ Pose >
Wardrobe > Imported Outfits**, then select third-outfit's review. Prior
main-project scenes remain preserved and the importer did not switch the active
editor scene. The user subsequently confirmed the third outfit worked. No extra
Daz export is currently needed for that approved result.

User has now confirmed the third outfit worked. The functional result is
approved, but the clean-context Luna demonstration required parent takeover and
therefore failed that separate gate. The manual/tooling have been hardened from
this case: Inspect verifies real FBX body geometry and explicit shoe policy;
supported footwear calibration is automatic; rigid shoes skip body-shape
transfer mechanically. Ten synthetic guard/default tests pass. The revised
second and maid paths both passed full asset/render/dissolve/walking regressions
in the isolated project, preserving the user's active scene. No new production
scene was published for these regressions. Do not claim an independent-agent pass until a fresh-context agent
completes the revised path without shared-code intervention. Next production
specification is [WardrobeRuntimeAndSetupSpec.md](WardrobeRuntimeAndSetupSpec.md):
Base/1/2 mesh layers, relative remove/add commands and state queries, shoes owned
by their outfit layer, retained hair, and one shared setup/runtime controller.
Per-outfit review scenes remain test fixtures.

## Accepted baseline

User approved the graft opening/seams, nipple control and bidirectional native
Lara breast adjustment. Facial morphs coexist with those controls. The apparent
grey body breakthrough was repaired by deriving morph normals from positions.
Skin gloss values were copied from the accepted original scene.

First outfit: Emiko hair + Peekaboo dress + Charlene's Closet panties on one rig.
User judged it pretty good; requested less aliasing, stronger coverage for tiny
dress poke-throughs, and correction of intermittent disappearing attachments.

## Latest repairs

- Corrected attachment culling envelopes and skin updates. Walking regression:
  181 frames / 543 attachment samples; legacy bounds missed 543, corrected
  bounds missed 0, false culling across two camera frusta 0. All 95 clip target
  paths resolved on `Lara — Validated Performer`.
- Added independent dress coverage in body UV3.y; underwear remains UV3.x.
  1,803 dress-covered vertices, with a 10 mm border at openings. Removing either
  garment disables only its mask. Both uniforms remain off on attachments.
- Game/Scene review TAA and geometric hair specular AA configured. The user's
  scene camera updates on Open or editor assembly reload, without overwriting
  user scene edits. The original validation scene remains untouched.
- Evaluated HDRP regression and all 14 Play mode phases passed after these
  repairs; Play dissolve/restore differences 6/12 pixels within the 20-pixel
  limit. Updated owned body coverage and evidence copied to the main project.

Main review scene:
`Assets/TestData/LaraCandidate/FirstOutfit/FirstPerformanceVoidLaraFirstOutfit.unity`.
Open through **Tools > DAZ Pose > Development > Open Lara First Outfit**.
Evidence: `TestOutput/appearance-evidence/wardrobe-motion`, `first-outfit`,
and `first-outfit-live`. User review of the latest repair is still pending.

## Second import: active source inspection

User chose a **sheer/layered outfit with heels**, saved
`C:/Users/artwh/OneDrive/Documents/DAZ 3D/Studio/My Library/Scenes/larasecondoutfit.duf`.
Source preflight is generated at
`Assets/TestData/LaraCandidate/SecondOutfit/source-preflight.json`.

- 95 material records / 92 unique textures; none missing, no ownership or
  material-inheritance errors. This includes hidden Emiko and body/graft.
- Umbra bra/choker, corset/laces, underwear, left/right stockings, sleeves,
  arm straps, **five gloss geometry shells**, and `BD Shoes` (`shoe_6146`).
- Emiko is explicitly hidden; preserve that choice. Do not silently re-add it.
- Saved figure controllers: `pCTRLlFootTipToes` and `pCTRLrFootTipToes` = 0.35;
  left/right toe spread = -0.5. Foot bones have no direct rotation override;
  evaluate those controller formulas/donor pose rather than assuming flat feet.
- `Assets/TestCharacter/larasecondoutfit.fbx` arrived during inspection:
  19 meshes including the grafted body, hidden Emiko and all five gloss shells.
  Extraction succeeded for 17 visible outfit parts / 61 surface slots. Hidden
  Emiko was excluded using the saved DUF visibility. No missing textures.
  The five FBX shell names omit the DUF instance suffix `-1`; explicit verified
  ownership maps are recorded in the playbook. Extracted follower channels have
  no nonzero position deltas; preserve the approved body and transfer its shape
  changes rather than assuming these followers contain functional morphs.
  Raw evidence: `.dazposewizard/second-outfit-fbx.json`; prepared inventory:
  `Assets/TestData/LaraCandidate/SecondOutfit/wardrobe-manifest.json`.
  No additional outfit or bridge export requested.

User offered the FBX export and asked whether to include the body/keep a clean
body reference. Requested `larasecondoutfit.fbx` with grafted Lara + outfit +
shells + heels at the supplied pose, and a one-time `laracleanreference.fbx`
with matching approved shape, neutral pose, graft removed and the successful
morph settings. Compare a smooth-body swap versus a same-topology flattening
morph for sheer coverage; do not choose that artistic policy before review.

Clean reference has arrived: one graft-free Genesis8Female mesh, 16,556 raw
points / 16,368 polygons. It contains only the left-eye-close exported channel;
retain it and derive/validate the remaining approved morphs before any swap.
No further Daz export requested yet. Storage audit and archive candidates are
recorded in [LaraAssetStorage.md](LaraAssetStorage.md); no exports were moved.

Second-outfit scene is now assembled in the isolated project by
`LaraSecondOutfitBuilder`: 17 shared-rig parts, 61 material slots, 5 gloss shells.
FBX shells are static MeshRenderers; their parent garment weights are inherited
through the matching raw point IDs and checked for consistency across splits.
Rest skinning errors are below 0.00025 mm. Nearby breast/nipple/breathing body
displacements are transferred to garments and shells; fit remains a user review.
No blanket opaque/graft coverage mask is enabled for this sheer inventory.

`prepare-lara-heel-reference.py` captured 2,604 changed foot points, maximum
62.68 mm; topology matches the approved body. Above-foot geometry differs by at
most 1.086 mm and is not applied to the approved body. Unity FBX foot rotations
are identity; toe positions changed by roughly 51 mm vertically / 32 mm in depth.
The foot shape and exported toe centers are retained, body bind poses rebuilt,
and particle bindings/profile regenerated for the outfit's owned body copy.
The measured standing support height is 0.04959 m.

Review scene:
`Assets/TestData/LaraCandidate/SecondOutfit/FirstPerformanceVoidLaraSecondOutfit.unity`.
Open **Tools > DAZ Pose > Development > Open Lara Second Outfit**. This opens
**Lara Outfit Review** controls for individual parts, gloss shells, inspection
light, anatomy/breathing, foot/toe pitch, standing height and edit-mode dissolve.
The captured stance is a standing reference; heel-aware locomotion, ground
contact, turning and seating calibration are still provisional.

Rendered layer/morph/dissolve checks passed: complete dissolve difference 4
pixels / restoration 0 pixels (20-pixel limit unchanged). Play walking bounds
check passed over 181 frames / 3,077 part samples: corrected bounds misses 0,
two-frustum false negatives 0. The 35 cm wardrobe envelope margin accommodates
the swinging heels. The scene, owned assets, script metadata and evidence have
been copied to the main project without replacing an existing scene.

User's Unity editor was confirmed visible/responding in their interactive
session, still on the dirty first-outfit scene. Do not switch it automatically
and discard edits. Open the second review through the menu after the user's
normal save/discard choice. Artistic fit, gloss, transparency and heel stance
review is next; no Daz export/action requested.

## Pipeline agreement and remaining work

### Import-two user feedback and active fixes

User approved the second standing shoe angle and general prototype. Reported
lost bra/stocking transparency, foot poke-through and collapsing shoes during
walking. Requested a body foot fit control and heel-height pelvis support.
User chose **preserve saved bra opacity and provide a slider**; do not impose a
50% override. With the surface mapping repaired the saved bra already renders
as lace; texture-wide averages do not establish opacity over the actually used UVs.

Root cause of the lost material appearance: Unity submesh order differed from
FBX material-table order in 10/17 pieces, including bra, garter, underwear,
stockings/shells and arm straps. Corrected by exact node-scoped imported surface
name matching. Added explicit transparent dissolve graphs and HDRP state refresh;
source and artist material values are retained. Grayscale opacity is imported
into both RGB/alpha, but this Metallic shader samples R; missing JPEG alpha was
not the cause. Rendered lace/fishnet appearance and the cup opacity control now
work. Updated full dissolve/restore passed at 11/0 changed pixels, limit 20.

Added `BD Shoes Footwear.asset` and reusable `PerformerFootwearProfile` data.
Height remains 0.04959 m. The pumps are rigid per foot; reference toe/metatarsal
pose is protected from barefoot animation; post-animation floor support solves
legs and reach-related pelvis adjustment. Initial 181-frame/3,077-part walking
test passed: shape error 0.000494 mm, planted contact error 0.188 mm, maximum floor
penetration 1.948 mm, 94 fully supported foot samples, no bounds/frustum misses.
This is prototype flat-floor support, not completed sitting/terrain/foot-lock IK.

Body-only foot fit now uses an inward surface offset (5% of foot width, faded by
foot skin influence) to avoid raising the forefoot. A pump-specific roof check
corrected 18 remaining toe vertices for 2 mm toe-cap clearance at default fit.
The close-up shows the visible skin spots removed. Final render passed at 14/0
dissolve/restore pixels. Final 181-frame walking rerun retained the metrics above
with corrected materials and no culling misses. All 61 node/surface associations
and the active profile/126-shape body passed the final asset check. Captures are
in the main `TestOutput/appearance-evidence/second-outfit*` folders.

The owned body, rigid pump mesh, particle bindings/profile and footwear profile
were backed up and transferred to the main project. Runtime material files were
not wholesale replaced: the editor migration updates their shader/state and
surface associations while retaining artist values. The saved scene SHA256 was
unchanged (`TestOutput/second-outfit-transfer.json`). User's second-outfit editor
was confirmed visible/responding with its dirty scene preserved. If it is in
Play or has not refreshed, stop Play and focus Unity to compile/import; the
scene-open/reload/asset-import hook binds repairs in edit mode. Save normally
after review to serialize the new material arrays and footwear association.

User has now reviewed the repairs and approved opacity and walking. Preserve
saved opacity and the existing review controls. No Daz export/action is needed.
Production locomotion/IK remains unfinished; FinalIK has not been imported.

The user accepts hands-on learning during import two. Import **three** should
test the short repeatable workflow. Use a hybrid: deterministic tooling for
extraction/conversion/binding/checks, and this concise playbook for fresh-context
agents such as Luna to run tools, handle exceptions and seek artistic review.

`prepare-lara-wardrobe.py` now accepts arbitrary DUF/FBX/output paths, discovers
visible followers, supports explicit ownership maps, tags transparency and
shells, and preserves source modifier/pose records. Existing first-outfit points,
morphs and material records were regression-compared with no changes.

Second prototype proved inventory and static-shell skinning. The subsequent
recipe importer now handles local bones/source morphs, declared coverage,
persistent material overrides and footwear references through a shared command.
The first/second bespoke builders remain historical fixtures, not the import
entry point. The third input tests the general workflow on new content; current
supported limits are documented in the operator manual and pipeline record.

