# Lara wardrobe import playbook

For the short operating procedure, start with
[LaraWardrobeImportQuickStart.md](LaraWardrobeImportQuickStart.md). This document
is the detailed reference; read relevant sections when the short procedure calls
for them. Read
[LaraWardrobeImportStatus.md](LaraWardrobeImportStatus.md) for the active import,
accepted choices and unfinished work. Follow the user's current instructions.
Do technical preparation autonomously; return to the user for Daz operations or
artistic decisions. Update the status after each import and this playbook when
a new procedure is verified. Do not repeat the original investigations.

## 1. Inputs and preservation

Workspace: `C:/Users/artwh/OneDrive/Documents/DazPoseWizard`.
Unity project: `validation/DazPoseUnityValidation`; Unity `6000.5.9f1`.
The signed-in user's editor must remain usable. Read root `AGENTS.md` for GUI
launch rules. Use `.dazposewizard/p0c-native-generation` for isolated Unity
batch diagnostics; never batch-open the main project while its editor is open.

The intended short workflow is **save one DUF and export one direct FBX**.
DUF supplies materials, texture locations, shell relationships and pose/morph
state; FBX supplies evaluated geometry, skinning and exported morphs. A bridge
export is a calibration fallback for a demonstrably unsupported shader, not
a routine second geometry export. Keep the approved Lara shape. Export visible
figures/followers together, retaining relevant morphs; exclude hidden old hair.
For heels retain the item's supplied foot pose and record its source controls.

Current second-import export request: `larasecondoutfit.fbx` includes Lara with
the graft plus all visible outfit/shell/shoe geometry, at the supplied foot pose.
Also keep a **one-time** `laracleanreference.fbx`: same approved Lara shape,
neutral pose, graft actually removed, useful morph-export settings retained;
clothes are unnecessary. An older graft-free `lara.fbx` exists, but the matching
clean reference enables comparison of smooth-body swapping and graft flattening
for sheer underwear. It is not an extra per-outfit export requirement. No
smooth-body swap or flattening method has been accepted/implemented yet.

Read [LaraAssetStorage.md](LaraAssetStorage.md) before archiving exports or
changing Git/LFS policy. Sources and export texture folders are local ignored
assets; preserve importer metadata and rebuild dependencies.

Preserve the original FirstPerformanceVoid scene and user edits. Work in an
outfit-specific candidate folder/scene. Back up a target before replacing it;
do not overwrite a scene that the user changed during generation. Keep owned
material edits and stable GUIDs. Licensed assets/texture copies remain ignored
under `Assets/TestData`.

## 2. Source preflight and extraction

Use Blender's bundled Python (its FBX parser needs no running Blender):

```powershell
$taskPython = 'C:\Program Files\Blender Foundation\Blender 4.5\4.5\python\bin\python.exe'
$taskDuf = 'C:\Users\artwh\OneDrive\Documents\DAZ 3D\Studio\My Library\Scenes\larasecondoutfit.duf'
& $taskPython scripts\prepare-lara-wardrobe.py --scene-only --duf $taskDuf --out validation\DazPoseUnityValidation\Assets\TestData\LaraCandidate\SecondOutfit\source-preflight.json
& $taskPython scripts\prepare-lara-wardrobe.py --duf $taskDuf --fbx Assets\TestCharacter\larasecondoutfit.fbx --out validation\DazPoseUnityValidation\Assets\TestData\LaraCandidate\SecondOutfit\wardrobe-manifest.json
```

For the verified second FBX, append these explicit ownership maps to the full
extraction command. Its five exported shell names omit the saved instance suffix:

```powershell
--map 'Umbra Bra Top Gloss Shell=Umbra Bra Top Gloss Shell-1'
--map 'Umbra Stocking L Gloss Shell=Umbra Stocking L Gloss Shell-1'
--map 'Umbra Stocking R Gloss Shell=Umbra Stocking R Gloss Shell-1'
--map 'Umbra Sleeve L Gloss Shell=Umbra Sleeve L Gloss Shell-1'
--map 'Umbra Sleeve R Gloiss Shell=Umbra Sleeve R Gloiss Shell-1'
```

These are command arguments, not separate PowerShell commands. Preserve the
source spelling `Gloiss`. Extraction produced 17 visible parts and 61 surface
slots, including all five shells and the shoes; hidden Emiko was excluded.

Use a new output folder for each outfit. The extractor verifies resolved maps,
discovers visible followers, and records raw points, nonempty position morphs,
surface ownership, hashes, shell/pose records, and a conservative opacity flag.
Use `--include` to select exact FBX geometry names or `--map FBX-name=DUF-node-id`
only after proving ownership. Ambiguous owners, unresolved textures and
unsupported multi-frame morphs must be investigated rather than guessed.

`--scene-only` does **not** produce importable geometry. Sheer, opacity-mapped
or refractive surfaces are marked for review; `opaque-candidate` is not artistic
approval to hide the entire underlying body. Check shell visibility/offsets and
surface assignments. Saved transform channels can be driven by modifier ERC,
so absence of a direct foot rotation does not mean the foot is unposed.

When a recipe includes `shoes`, heel-reference preparation requires a real,
topology-matching `Genesis8Female` body mesh in the direct FBX. A hidden body can
leave a named but empty FBX geometry node; outfit followers may still extract,
but footwear calibration must stop rather than fabricate or omit the captured
foot shape. If this occurs, ask for the approved body/graft to be included in a
fresh direct export while retaining the supplied shoe pose and follower morphs.
Do not solve it with a bridge or clean-body export.

If the body topology is present but heel-reference preparation rejects a body
difference above the feet, retain the measured source controls. The shared
`poseCalibration` recipe field now recognizes the approved paired tiptoe
lower-leg pattern, checks every predicted point within 0.002 mm, and transfers
matching actual geometry alongside the feet. The 1.5 mm guard remains for
unexplained upper-body changes. Calibration is reference-hash locked and limited
to equal left/right tiptoe values from 0 to 1. A mismatching pattern, asymmetric
pose, or different reference still stops for shared investigation. Do not lower
the supplied pose, raise the guard, or create an outfit-specific builder.

Evidence: maid tiptoes 0.75 versus accepted second outfit 0.35. All 226 measurable
points above the old 0.4 m cutoff follow the same scaled displacement pattern,
with maximum residual 0.000237 mm. The primary `pJCMFootDwn_75` delta support
does not explain the 46 previously rejected points; do not misidentify it as
the proven driver. The small checked-in calibration preserves the empirical
pattern and its source hashes. Synthetic guard tests cover an unexpected leg
pattern, unrelated body change, absent calibration and unequal controls.

## 3. Conversion rules already proved

Current production entry point: `scripts/import-lara-outfit.ps1` with a recipe
under `configs/wardrobe`. See the short manual for exact commands. The first and
second builders below are historical fixtures; `WardrobeImporter` now shares
their algorithms and generates `WardrobeOutfitDefinition` equipment assets.

Shared implementation: `LaraCandidateBuilder` and `LaraFirstOutfitBuilder`.
**The first-outfit builder remains a three-piece historical proof.** Do not invoke
it on a new manifest or extend it per outfit. Configure the shared recipe importer.

- Retain one canonical Lara mesh/rig and the accepted facial/anatomy channels.
  Rebind attachments to actual canonical bone transforms. Evaluate the donor's
  skin/bind pose before rebinding; name matching alone does not prove a match.
  Keep genuine attachment-local bones under the correct canonical parent,
  with distinct names. Never leave follower Genesis rigs or duplicate bodies.
- Convert FBX points with `(-x,y,z) * .01`. Check rest-pose errors; the first
  import was below 0.0004 mm. Preserve all skin influences already imported.
- Derive blendshape normal deltas from **deformed positions**, using
  `SmoothPointNormals` grouped by raw point across UV/material splits. Raw FBX
  shape normals caused the apparent grey under-body and must not be reused.
  Keep semantic channel mapping stable. For missing garment breast movement,
  the first proof transferred the accepted native body displacement by nearest
  triangle barycentric binding; fit needs review and this excludes joint ERC.
- Resolve materials by **node + surface**, never surface name alone. Use
  `ConvertMaterials(..., destination, applySkinResponse:false)` for followers.
  It reuses the installed DTU converter and owned dissolve shader families.
  Keep source alpha/refraction, UVs, normal/bump maps and gloss parameters.
  Preserve the accepted body skin response; never apply it to cloth or hair.
  **Do not assign the converted array in FBX material-table order.** Unity
  reorders submeshes by polygon use. `MatchImportedSurfaces` matches each
  imported renderer's surface names to that node's converted material assets.
  Import two proved this mattered for 10 of 17 pieces: stocking band and fishnet
  materials were reversed, and the bra/shell/garter surfaces were also reordered.
  Validate this mapping against the imported donor before any appearance review.
  Metallic/Specular opacity surfaces use the explicitly **Transparent Dissolve**
  graphs generated by `build-wardrobe-transparent-variants.py`, not an opaque
  graph with transparent material flags. Refresh HDRP material keywords/state
  after replacing a graph. Preserve saved opacity and runtime artistic edits.
  Grayscale opacity is retained in RGB and alpha; the current Metallic graph
  samples R. Missing JPEG alpha was not the source of this outfit's opacity bug.
- Every material must support the eight `_Dissolve*` contract properties.
  All attachments use body-local coordinates and follow body dissolve and
  stable visibility. Regenerate copied particle bindings/profile when the body
  mesh changes; verify `PerformerDissolveRig.IsReady`. Current particles sample
  the body only. Negative masked alpha rejects dissolved depth/shadow/specular
  fragments even when the clip threshold is zero.

## 4. Coverage, culling and presentation

First outfit uses body UV3.x for opaque underwear coverage and UV3.y for dress
coverage. Enable each independently when that garment is visible. Disable both
coverage uniforms on **all attachment renderers**: their existing UV3 may be
texture data. Masking an entire graft leaves a hole because it replaced body
polygons. Preserve exposed rear skin and garment openings. Dress projection
keeps a 10 mm opening border; tune coverage with source geometry and review.
For sheer layers keep the body visible and solve garment fit/layering; do not
apply an opaque mask through transparent fabric.

Skinned bounds use root-bone space. Assigning `mesh.bounds` to an attachment
rooted at the hip misplaced the hair box by about a metre. The proof uses the
body envelope with padding, refreshes a shared world envelope every LateUpdate,
enables `updateWhenOffscreen`, and disables dynamic occlusion on attachments.
Do not confuse intentional dissolve/teleport hiding with culling. There is no
bone-copy loop; source bones are shared. Larger/new attachments must pass the
motion envelope check before delivery.

Review cameras use high-quality HDRP TAA with modest sharpening, full resolution
and no dynamic resolution. Hair also uses geometric specular AA. The Open menu
configures Scene-view TAA/animated materials too. Diagnostic pixel regressions
deliberately use AA off; they are not the presentation quality setting.

## 5. Checks and handoff

For the first proof the isolated batch entry points are:

- `LaraFirstOutfitValidation.UpdateAndRun`: update owned body coverage without
  rebuilding user scene edits; render front/back/side, morph extremes, covered
  and uncovered anatomy, head pose, dissolve and restoration.
- `LaraCandidateLiveDiagnostics.RunFirstOutfit`: actual Play mode morph,
  visibility and coverage toggles plus dissolve/restore. Omit `-quit`; it exits.
- `LaraWardrobeMotionDiagnostics.Run`: resolved walk bindings, 181 animated
  frames, actual baked geometry inside renderer bounds and two camera frusta;
  captures TAA presentation. Omit `-quit`.

Use fresh logs and inspect results/images under `TestOutput/appearance-evidence`.
Check shader compilation and serialized GUID closure in Assets **and** installed
packages. Copy only owned generated assets/metas back to the main project.
Preserve the user's scene and material changes. Extend these checks for the new
part inventory; their first-outfit names are not general import support.

For the second proof, run `scripts/prepare-lara-heel-reference.py` with Blender
Python after preparing the wardrobe manifest. It requires matching body topology
and records source hashes plus above-foot differences. This FBX has identity foot
rotations: Daz baked the stance into vertices and toe joint centers. Retain the
captured foot geometry and toe centers, rebuild rest bind poses, then measure
standing support height. Do not copy Daz Euler angles or assume a flat foot from
identity exported rotations. The second proof needs a 0.04959 m standing offset.

Use `LaraSecondOutfitBuilder.BuildAndRun` in the isolated Unity project for the
17-part scene/material/rest-skinning/render checks. It refuses to overwrite an
existing review scene. `LaraSecondOutfitValidation.Run` checks existing assets;
`LaraWardrobeMotionDiagnostics.RunSecondOutfit` runs Play-mode walking bounds
checks and exits itself (omit `-quit`). Evidence is in `second-outfit` and
`second-outfit-motion`. Preserve owned assets/metas when copying to the main
project. Open through **Tools > DAZ Pose > Development > Open Lara Second Outfit**
and use its **Lara Outfit Review** window for artistic decisions.

`LaraWardrobe` supports the arbitrary part inventory and shared dissolve/shape
state. Static gloss shells inherit parent skin weights through validated raw
point IDs; do not leave them as rigid meshes. The recipe importer handles the
inventory, coverage policies and persistent overrides; import three tests it on
new input. `LaraHeelReview` exposes captured standing shape,
foot/toe pitch and rig height. Ground contact through walking/turning/seating and
per-shoe locomotion calibration remain unfinished; do not call heels production
ready from the standing or bounds checks alone.

## 6. Footwear profiles and import-two repairs

`PerformerFootwearProfile` stores shoe support height, heel/toe contact points,
body foot fit, toe constraints, floor offset and swing blend distance. These are
asset data, not 12 separate scene implementations. Import calibrates a profile
once; artistic review adjusts it; runtime pose support consumes it after animation.
The production wardrobe manager still needs to assign/clear this profile when
shoes change and restore the neutral body foot shape/pivots for barefoot use.
The recipe importer captures matching-topology body foot references and stores
the resulting mesh plus toe transforms in the footwear asset. Current supported
calibration expects a combined pair and the baked-pose convention, with an upper
body difference guard. Runtime equip/unequip remains work after import three.

For these closed pumps the owned shoe mesh binds rigidly to the corresponding
foot, preventing barefoot toe/metatarsal animation from bending the sole/heel.
This is a **pump policy**, not permission to rigid-bind a tall boot's shaft.
`LaraHeelReview` retains the exported toe positions/rotations, applies the measured
standing lift, then performs a two-bone floor support correction. Floor support
fades during swing and seating ownership. Seated foot/hand targets, uneven terrain,
horizontal planted-foot locking and heel-specific animation remain future work.
FinalIK has not been imported. The contact/profile interface should survive a
later solver replacement.

`WardrobeFootFit` moves only body foot vertices inward along deformed smooth
normals, faded by foot skin weights, plus a measured closed-pump toe-cap roof
clearance correction (2 mm at default fit, 18 vertices in this source). The ratio
is relative to measured foot width;
5% is an artist fit control, **not literal shared-bone scaling**. Ankle-centred
scaling raised the forefoot and was replaced. Shared bone scaling would shrink
the shoes too. The owned anatomy/facial frames and their ordering are preserved.

Use `LaraSecondOutfitRepair.RepairAndRender` in the isolated batch project to
migrate owned material state, correct surface order, regenerate the foot fit and
rigid pump binding, and bind the profile. It preserves existing artist materials
and profile values. It saves only the isolated scene. In the main editor the
scene-open/assembly-reload/asset-import hook corrects renderer material arrays,
updates owned material shader/state without replacing artist values, and
associates the profile in memory; the user's unsaved scene is not overwritten.
Save the scene normally after review to retain those associations in builds.

Walking diagnostics now measure per-foot shoe shape invariance, planted heel/toe
floor error and lowest-vertex floor penetration, in addition to culling. A bounds
pass alone cannot validate footwear deformation. The review window exposes saved
bra/stocking opacity, foot fit, support height and a floor-support toggle.
`LaraSecondOutfitRepair.VerifyExistingAssets` checks all 61 imported node/surface
identities, transparent graph selection, shader errors and the active foot-fit
profile. Run it in isolated batch mode after migrations; it does not rebuild the
owned geometry. Final import-two render difference was 14/0 pixels and the
181-frame walking checks passed, including the measured 2 mm toe-cap correction.

Return a reviewable scene and a short report: what works, what is provisional,
which artistic decision is next, and the exact Daz action only if needed.
User review owns fit, transparency/coverage, colors, sheen and heel behavior.
