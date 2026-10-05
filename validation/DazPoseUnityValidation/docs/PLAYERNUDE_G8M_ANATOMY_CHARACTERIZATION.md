# Playernude G8M / Dicktator characterization

Investigation of local `main`, 2026-10-04, HEAD `d469ed61848f4a6dc3ffc2606b744562fb060a24`, including the existing uncommitted working-tree state. No Unity import, scene/material changes, runtime implementation, mesh surgery or execution packet was performed. Existing edits were preserved.

## Findings and confidence

**Proven from DSON/FBX/source:** Caine is a Genesis 8 Male with Meipex Dicktator G8M v3 attached. The FBX has a welded body/anatomy mesh, eyelashes, and a separate **unskinned** Dicktator Shell. It contains no erection/length/girth blendshapes. The native asset has a root and seven weighted shaft bones. `Flacid` and `Cock Length` are bone ERC controllers; `Shaft Inflate` is a substantial direct geometry morph. All 26 saved-scene texture sources resolve. Four thigh clusters unexpectedly contribute substantial anatomy weights; 615 anatomy-region vertices have raw total weights above 1.001, reaching 5.

**Strong inference:** the shaft chain is structurally adequate for a simple centerline and small contact offsets. Existing project SSS dissolve can represent the body and skin-like graft surfaces; shell alpha handling needs a deliberate decision. Wet is appropriate to eye wet surfaces, not automatically to genital skin. The current shell cannot follow shaft deformation without additional treatment. Raw weight normalization will likely dilute anatomical bone influence at affected vertices.

**Needs next export to verify:** DAZ export enumeration of the selected anatomy controls; whether bone-only ERC controls become useful exported channels at all; direct geometry morph survival on the merged body and shell; repeatability of saved bind transforms and unexpected thigh weights. The CSV uses the actual project schema, but is a characterization selection, **not a verified production guarantee of ERC transfer**.

**Needs human visual inspection:** deformation quality, pelvis/base anchoring in motion, seam and MemberOnly root appearance, material match and shell transparency. Numeric movement is not visual acceptance.

## 1. Sources and tools

Actual files found:

- DUF: `C:/Users/artwh/OneDrive/Documents/DAZ 3D/Studio/My Library/Scenes/playernude.duf`; SHA256 `93689f3218a5049e588df73059d7b1c4556a2340dc7907367eb33b672a8ab82a`.
- FBX: `validation/DazPoseUnityValidation/Assets/TestCharacter/playernude.fbx`, 168,792,608 bytes; SHA256 `be9405fff80359a76546976327d32131873d62585478c044e9ed0db4dfbff4d3`.
- Export directory: `Assets/TestCharacter/playernude.images` (nine images).
- DAZ log: `C:/Users/artwh/AppData/Roaming/DAZ 3D/Studio4/log.txt`, lines 20256–20265 records TIFF generation and this export. The running-session startup records DAZ Studio 4.24.0.4 and `dzunitybridge.dll`. The file's exporter settings are not embedded as a complete options manifest.
- Content root actually resolving the source assets: `F:/Daz3D`. Other searched roots were the user's My Library and Public My DAZ 3D Library.

Reused [inspect-fbx.py](../../../scripts/inspect-fbx.py) and [inspect-daz-materials.py](../../../scripts/inspect-daz-materials.py). FBX evidence uses Blender 4.5's standalone binary reader, **not Blender scene conversion**. Added [characterize-playernude.py](../../../scripts/characterize-playernude.py) for DSON modifier statistics, raw topology/skin/UV evidence and material mapping. It writes only investigation outputs under `TestOutput/playernude-characterization`.

Run from repository root with `C:/Program Files/Blender Foundation/Blender 4.5/4.5/python/bin/python.exe`; invoke the existing inspectors first (FBX with `--blender-scripts C:/Program Files/Blender Foundation/Blender 4.5/4.5/scripts/addons_core`, DUF with `--content-root F:/Daz3D`), writing `fbx.json` and `materials.json` into that output folder, then run the new script. Exact source paths, SHA256s, full bone paths, cluster matrices, formulas, channel values and texture sharing are retained in the linked inventories. Raw FBX unit scale is 1 centimeter; these are control points, not Unity's UV/material-split vertex counts. No Unity importer count is claimed.

## 2. Existing Lara pipeline and morph workflow

Read [QuickStart](LaraWardrobeImportQuickStart.md) and **Current handoff** in [Status](LaraWardrobeImportStatus.md). Current source takes precedence over older design prose. The canonical base import is `Assets/TestCharacter/lara.fbx`; `LaraCandidateInstaller.cs` reads it, retains its canonical bone and morph identities, and adds donor anatomy bones. Current candidate body is project-owned `Assets/TestData/LaraCandidate`, rather than assigning a new raw FBX as a production replacement. [LaraCandidateIntegration](LaraCandidateIntegration.md) documents 124 candidate channels, 170 canonical weighted bones, three local anatomy bones and 20 material slots. The shared wardrobe pipeline now has additional stages beyond that historical candidate report.

Exact material implementation: `Assets/DazPose/Editor/LaraCandidateBuilder.cs`, `ConvertMaterials`: consumes normalized native surface records, constructs `DTUMaterial`, invokes installed Bridge conversion functions, maps the resulting family onto project-owned dissolve graphs, and binds `results[surface.slot]`. Source and RuntimeMaterials are separate; current wardrobe importer preserves artistic overrides. `scripts/prepare-lara-candidate.py` and `prepare-lara-wardrobe.py` supply source figure/surface identities. The older Bridge reference is `Assets/Daz3D/larabridge/Genesis8Female`; older 16-slot name matching is insufficient for merged graft slots. No blind copying of Lara textures is proposed.

The repository **does have a DAZ library browser**: `src/DazPose.App/Services/LibraryIndexService.cs` indexes DUF metadata in SQLite. It is not a deep DSF geometry/ERC browser. Always-export morph editing exists in `AlwaysExportMorphsWindow`, `AlwaysExportMorphEditWindow`, and `MainWindowViewModel.GenerateDazMorphExportRules`. `RequiredMorphManifestService.GenerateExportRules`, lines 159–188, reads `.dazposewizard/required-morphs.json`, combines pinned and content-required names, quotes each as `"name","Export"`, and ends with `"Anything","Bake"`. There is **no header**. Existing working examples are `scripts/LaraGraftProbe-MorphExportRules.csv` and `scripts/LaraWardrobe-MorphExportRules.csv`.

The new CSV uses that exact schema and narrow-preset convention. It was serialized from the verified candidate names, not generated by changing the global manifest or launching the GUI. The existing GUI generator would mix in unrelated current content-required/pinned Lara controls, so it was deliberately not used for this character-specific preset. No new export schema was invented.

## 3. DSON asset graph

Primary physical sources:

```text
F:/Daz3D/data/DAZ 3D/Genesis 8/Male/Genesis8Male.dsf
  figure #Genesis8Male; geometry #geometry; skin #SkinBinding
  16,384 vertices / 16,196 polygons
F:/Daz3D/data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf
F:/Daz3D/data/Daz 3D/Genesis 8/Male Eyelashes/Genesis8MaleEyelashes.dsf
F:/Daz3D/data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/Dicktator_Genitalia_G8M.dsf
  figure #Dicktator_Genitalia_G8M; geometry #Futa GenitaliaSmall
  2,205 vertices / 2,177 polygons; native skin vertex_count 2,205
F:/Daz3D/data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/UV Sets/Meipex/Base/default.dsf
F:/Daz3D/data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Shell/Dicktator Shell.dsf
  zero-vertex native shell template; saved scene supplies generated shell geometry
```

DUF body label is **Caine**, with `CaineCTRL` saved at 1. Body identity is not plain neutral G8M. Its controller reference is `data/DAZ 3D/Genesis 8/Male/Morphs/TMHL/Caine/CaineCTRL.dsf#CTRLCaine`. DUF anatomy is parented and conformed to `#Genesis8Male`. Shell is parented to body and declared `studio/node/shell`; body surface visibility on that shell is false. It duplicates the anatomy region, not the full visible body. Saved shell UV overrides are absent; geometry carries the effective coordinates.

[dson-assets.json](../../../TestOutput/playernude-characterization/dson-assets.json) follows the directly referenced physical DSON assets (and records non-DSON URLs), including geometry, node/bone definitions, saved morph assets, skin summaries and UV definitions. All referenced paths resolve. [anatomy-native.json](../../../TestOutput/playernude-characterization/anatomy-native.json) retains the complete native node definitions, graft mapping and skin-joint metadata. [anatomy-morphs.json](../../../TestOutput/playernude-characterization/anatomy-morphs.json) inspects **252 anatomy modifiers** under the actual graft Morphs directory, including direct deltas measured from their files and complete formulas. This is discovery evidence; they are not all selected for export. [duf-structure.json](../../../TestOutput/playernude-characterization/duf-structure.json) retains saved scene state and generated shell geometry.

Important baked source anatomy values: `Shaft Shorten=0.1166667`, `Shaft Root Fold=0.1111111`, `Shaft Inflate=0.4074074`, `Shaft Inflate 1=0.4027778`, `Scale Down=0.6`, `Cock Length=0.03703701`, `Urethra Shape 1=-0.1`, `CorpusSpongiorum Inflate=-0.4814815`, `Scrotum Stretch=0.06481481`. Preserve this authored configuration during characterization. Neutral asset defaults are not the saved character state.

## 4. Baseline FBX mesh, skeleton and blendshapes

| Mesh / renderer candidate path | Raw points | Polygons | Declared slots / used slots | Skin clusters | Shapes |
| --- | ---: | ---: | ---: | ---: | ---: |
| `Genesis8Male/Genesis8Male.Shape` | 18,484 | 18,287 | 23 / 23 | 185 | 1 |
| `Genesis8Male/Genesis8MaleEyelashes/Genesis8MaleEyelashes.Shape` | 464 | 352 | 2 / 2 | 25 | 1 |
| `Genesis8Male/Dicktator Shell/Dicktator Shell.Shape` | 2,205 | 2,177 | 23 / 7 | **0** | **0** |

Body and eyelashes are SkinnedMeshRenderer candidates. Shell is a static mesh candidate; no skin deformer or blendshape exists in the source FBX. No independent structural anatomy mesh exists. Distinguish the merged anatomy's slots 16–22 from the static overlay's matching seven used slots. Zero-polygon shell slots 0–15 are still declared bindings; a future Unity slot count must be measured because the importer may omit unused slots.

FBX is binary version **7400**, Y-up, centimeter scale. It has 259 model nodes. Entire hierarchy, parentage and transform properties are in [fbx.json](../../../TestOutput/playernude-characterization/fbx.json). Body maximum raw influences is 10, eyelashes 4; both have zero unweighted control points. Two bind poses exist: body and eyelashes. Cluster `Transform`, `TransformLink` and association matrices are retained. A single animation stack/layer exists but **no animation curves or curve nodes**. This is not an animated characterization capture.

Exact shapes:

- Body: `Genesis8Male__eCTRLEyesClosedL`, one frame at full weight 100.
- Eyelashes: `Genesis8MaleEyelashes__eCTRLEyesClosedL`, one frame at full weight 100.
- Shell: none.

No required control is exported. Their absence says nothing about native availability. Frame displacement statistics are retained in `fbx.json`; no broad facial/body library is present.

All three meshes have one raw UV layer named `Model_UV`, mapped ByPolygonVertex: body 20,335 UV entries, eyelashes 490, shell 2,389. Native default UV definitions contain body 18,160, lashes 490 and graft 2,255 UV entries; merged/export UV entries are not required to equal native counts. FBX texture references point to this actual `.images` directory; it also embeds 166,262,634 bytes of image data. Embedded imagery does not provide the missing DAZ PBR channels.

## 5. Small control selection, mechanisms and semantics

Selected CSV: [scripts/playernude_runtime_morphs.csv](../../../scripts/playernude_runtime_morphs.csv). Four Export rules, nine preceding narrow Bake guards and terminal Bake, in the proven project two-column/no-header format. The guards retain unrelated Flacid presets, Scrotum properties and Inflate 1–4 as baked configuration if DAZ matches rule substrings, avoiding accidental family-wide selection. Confirm the exporter matched-property list; this artifact does not prove whether it matches internal names, channel names or display labels for these particular controllers. Selected controls target the native 2,205-point Dicktator anatomy; geometry would appear on the merged body after successful export.

| Runtime semantic | Source internal id / label | Native range / default | Saved value | Mechanism | Suggested initial runtime interpretation |
| --- | --- | --- | --- | --- | --- |
| Erection state | `Flacid` / `Flacid Preset Base` | 0–1 / 0 | not overridden | Pure bone ERC, seven rotation outputs | `Erection=e` maps to `Flacid=1-e`; initially inspect e=0, .5, 1. This controls authored pose, not physical firmness. |
| Length | `Cock Length` / same | -1–1 / 0 | .03703701 | Pure bone ERC, six Z-scale outputs | Start with ±.05 source-dial variation around saved value. This is a conservative test interval, not measured ±5% physical length. |
| Girth | `Shaft Inflate` / same | -1–1 / 0 | .4074074 | Direct morph, no ERC formulas | Start at saved value ±.10; calibrate radius from evaluated geometry. Do not call the dial centimeters or diameter. |
| Root corrective dependency | `pJCM_Shaft1_up` / same | 0–1.7 / 0, hidden | automatic | Direct morph driven by shaft1 X rotation | Preserve for upward erection-angle or contact offsets; `clamp(-.04 * source shaft1 X degrees, 0, 1.7)` in the source coordinate convention. |

Selected source files are under the graft `Morphs` directory:

```text
Meipex/Base/Flacid.dsf
Clare3Dx/Bonus/Cock Length.dsf
Meipex/Base/Shaft Inflate.dsf
Meipex/Base/pJCM_Shaft1_up.dsf
```

The last path is verified by the inventory's `source` field; do not replace it with an assumed product name. `Flacid` drives shaft1/2/3/4 X rotations by **59/15/11/7 degrees**, scrotum +30, left testicle -14.5, right testicle -15. No direct geometry delta or downstream shape property is authored in that controller. Positive shaft1 X makes the upward corrective clamp to zero; the corrective is retained for the planned upward/straightening offsets, not because every flaccid evaluation requires it. No automatic change in runtime spring stiffness is encoded.

`Cock Length` drives shaft1 and shaft6 Z-scale contributions by .5 times its value, and shaft2/3/4/5 by 1 times value. Unity will not execute these DSON formulas merely because a property name appears in a CSV. The exact exported representation must be inspected. Bone scaling order/inheritance and DAZ scale weights matter; neither a naive blended pose nor mesh-only substitute is proven equivalent.

`Shaft Inflate` changes 1,343 points, max **1.203930 cm**, mean moved **.585797 cm**, RMS over all 2,205 points **.528385 cm** at source value 1. Axis RMS over stored entries is X=.399206, Y=.546809, Z=.004755 cm: predominantly radial to a +Z shaft. This establishes useful nonzero girth change. It also affects head/adjacent shape; constant radius is an interaction approximation, not a literal cylindrical mesh.

`pJCM_Shaft1_up` changes 27 points, max .317931 cm, mean .175953 cm, RMS over all .021502 cm. It is a local joint corrective, not a general soft-body morph. No need to export the entire body/face library or unrelated thigh correctives for these narrow controls.

### Alternatives rejected or deferred

`Erection Preset 01` is **not a single erection geometry morph**. It drives `Shaft Twist=.2325581`, `Curve 2=.1007752`, `Curve 1=.2868217`, and shaft5 X=-.308105 degrees. Those three properties then drive multiple shaft rotations. Preset 02 adds Scale All, side-to-side curvature and another bend curve; it couples size and pose. Their full downstream formulas are in the morph inventory. Prefer the simpler inverse Flacid semantic; choose a preset later only if its visual pose is desired.

`Up-Down` (label/channel name `Shaft Up-Down`) is an optional pure bone angle control, X=-80 times dial on shaft1. It is omitted from the CSV because the selected flaccid-state control already covers the required pose transition and angle can be driven on discovered bones. Upward rotation activates the selected corrective. There is no proven scalar physiological firmness morph.

`Cock Thickness` contains neither deltas nor local formulas, **but is not a no-op**: formulas authored in `Cock Tall` and `Cock Wide` each read it, then drive seven shaft Y and X scales respectively. It is a viable bone-driven girth alternative; use the measured `Shaft Inflate` for the first geometry export probe. The inventory traces the reverse dependency, avoiding the mistaken conclusion that an empty controller does nothing.

`Shaft Shorten` has 1,212 nonzero deltas, max 16.398476 cm, average 13.252477 cm, RMS 10.628119 cm, primarily Z. It also has **48 joint center/end/orientation formulas**. It already shapes this fixture, but exporting its mesh delta alone would not preserve moving rig configuration. Do not add it as a second runtime length control yet. `Scale Down` similarly couples 1,631 deltas and joint edits and is saved at its maximum .6. `Shorten` targets an older `Dicktator_Genitalia_2205` identity; avoid it for this asset. `Small Dick Length` has zero delta entries and zero formulas in its asset; no downstream readers were found in the scanned anatomy modifier set. It is not selected as useful length evidence.

## 6. Bone chain, anchoring and procedural offsets

Actual FBX hierarchy (under a **donor skeleton branch**, not the main G8M pelvis):

```text
Genesis8Male
├── hip → pelvis                       [main G8M bones]
├── Dicktator_Genitalia_G8M
│   └── hip → pelvis                    [donor hip/pelvis]
│       ├── shaftRoot
│       │   ├── shaft1 → shaft2 → shaft3 → shaft4 → shaft5 → shaft6 → shaft7
│       │   └── scrotum → lTesticle, rTesticle
│       ├── rectum1 → rectum2 → colon
│       └── legsCrease
├── Genesis8MaleEyelashes → hip → ...    [another follower hierarchy]
└── Dicktator Shell → Dicktator Shell.Shape
```

All anatomy-local bones above are real LimbNodes and linked to body skin clusters; they are not invisible deformer-only records. Native IDs differ from names: `root→shaftRoot`, `Shaft 1→shaft1`, `Scortum→scrotum`, `Rectum→rectum1`, etc. Bind paths, not ambiguous `hip`/`pelvis` names, are needed. The appended table lists all anatomy-region influencing clusters, including main G8M pelvis/thighs, with region weights and source ownership.

Seven segments are sufficient for the proposed centerline. Use shaft1 through shaft7 bind centers plus an evaluated tip endpoint; shaftRoot is an anchoring/base-deformation joint whose native endpoint points toward the perineum, **not the first shaft segment**. Do not blindly build a line from every bone endpoint. The exported centers progress roughly along +Z, with very small Y changes; native shaft orientation is near identity, shaft1 has a small authored tilt, rotation order ZYX. FBX retains pre/post rotations on the first segments. Calibrate Unity axes after coordinate conversion, not from display labels.

Analytic bind-space 5-degree X rotations with normalized subtree weights produce nonzero movement: shaft1 max 1.484526 cm and shaft4 max .750438 cm. This is a raw weighted geometric sensitivity calculation, **not a DAZ evaluator, rendered bend, or Unity validation**. It assumes a rigid subtree offset around recorded bind centers and normalizes raw weights. A useful deformation skeleton exists; skin quality and shell following remain unresolved.

**Skin concern:** the main body thigh Bend/Twist clusters have substantial weights on anatomy glans/shaft vertices, not just epsilon noise. Four thigh clusters each have ~457 summed anatomy weights and up to weight 1. Their anatomy vertex counts at weight ≥.1 are 507–511. Raw total weight reaches 5 at affected vertices. Native graft thigh joint records do not supply matching node-weight arrays. Treat this as an export representation anomaly requiring technical verification, not proof the product is incorrectly rigged. Max-four Unity skin weights could retain thigh contributions and drop intended shaft influence. Full-weight versus four-weight imported numeric comparison belongs to the later authorized import investigation; do not claim it has passed here.

**Pelvis anchoring:** the donor pelvis is parented under the body figure root, not main pelvis. It is spatially aligned in the bind state, but there are no exported animation curves or constraints synchronizing the two pelvis branches. Driving only main G8M pelvis is not proven to carry the whole shaft rig. Lara's accepted donor remapping pattern is relevant evidence for eventual integration; no remapping is done here.

Future conceptual order: capture/evaluate DAZ-authored baseline geometry and pose; apply base configuration; then compose bounded procedural local quaternion offsets; restore that base each update rather than accumulate offsets. Root anchoring, bone scales and moving pivots must remain consistent. `Flacid`, curves and presets already drive shaft rotations; overwriting rotations would destroy their authored state. Root corrective should be evaluated in the appropriate source angular frame after the relevant base/contact angle. This is planning semantics only.

## 7. Compliance and squish candidates

The appended candidate table gives actual internal IDs, labels, paths, native ranges/defaults, geometry metrics and ERC counts. Classification is a hypothesis grounded in delta direction/extent or formulas; it does not promise contact quality.

- **Procedural contact candidates:** `pJCM_Shaft1_up` for upward base bending; `Glans Flatten` for tip axial flattening (595 points, max 1.839849 cm, strongly Z-dominant); `Glans Height`/`Glans Width` for local shape compression; `Shaft Inflate` or localized Inflate 1–4 at small relative values for radial change. These are broad authored deltas, not localized hand pressure. Negative values must remain within source ranges and be visually reviewed.
- **Configuration candidates:** `Shaft_Straight` (490 points, max .514704 cm) is geometry shape straightening with no bone formulas; `Shaft Conical`, glans/corona/urethra shapes, root folds and foreskin shapes configure appearance. Bone centerline and mesh surface will not necessarily stay coincident when these are changed.
- **Bend controls:** Curve 1/2/3, Side-Side curves and Shaft Twist are bone ERC, not compressive mesh deltas. Useful reference distributions for offsets, not spring solvers. Curve 3 drives all seven shaft X rotations; the existing chain can support small offsets without selecting every controller for export.
- **Probably irrelevant to first hand contact:** testicle squeeze, scrotum folds/inflation/stretch, rectum/anus expansion, silicone/identity presets and Androginy conversion branches. Keep their evidence but omit them from the required CSV. No general pressure/softness simulation is defined by a discovered scalar morph.

## 8. Graft topology and presentation

Native graft replaces **86** body polygons and adds **2,177**:

`16,196 - 86 + 2,177 = 18,287`, exactly matching the FBX body. All 2,177 anatomy polygons occupy the seven graft slots; the remaining host polygons total 16,110. This proves replacement-style merged topology rather than two complete overlapping visible structural meshes. The shell intentionally duplicates anatomy overlay geometry; it is a separate rendering layer.

Native graft declares 37 vertex pairs (36 perimeter plus an additional pair). Exported body has **36 shared host/anatomy edges and 36 seam vertices**, each counted with one polygon on each material side. There are zero nonmanifold edges; 314 total body boundary edges include other open surfaces, so do not interpret that total as 314 graft seam defects. Anatomy-region-only topology has **58 boundary edges**, including the 36 boundaries exposed where host geometry would be hidden. Shell also has 58 boundary edges. No closed MemberOnly root cap is evidenced.

FullBody: merged structural body/graft remains; shell's appearance/deformation must be preserved. GhostBody: still must distinguish host versus anatomy **material regions**, retain skeleton and synchronize shell; no mode is implemented. MemberOnly: disabling `Genesis8Male.Shape` hides both structural body and member. Leaving Shell enabled retains only an open, unskinned overlay. An eventual material-region visibility/splitting choice must retain anatomy and the appropriate base patch. Current slots include Torso_Front/Middle/Back and rectum, not only shaft/glans. Hiding everything except shaft/glans would expose additional cuts. Human inspection should judge root termination, retained perineum/scrotum and any need for a cap; no mesh surgery is proposed yet.

## 9. Complete materials, channels and texture sources

There are **48 saved-scene effective material records**: body 16, eyelashes 2, native graft 7, shell 23. Every one is `studio/material/uber_iray` (Iray Uber). Exact renderer/slot/source ownership is [renderer-material-map.json](../../../TestOutput/playernude-characterization/renderer-material-map.json) and the appended table. Slot order is taken from actual FBX connections; it is not guessed from DUF material order. Shell's first 16 slots have no polygons in this export.

[materials.json](../../../TestOutput/playernude-characterization/materials.json) merges material-library defaults with scene overrides without inheritance or ownership issues. [material-channels.csv](../../../TestOutput/playernude-characterization/material-channels.csv) records **every channel for every material**: native ID/label/type/value, texture path, resolved file, gamma/image settings, shader and UV reference. This companion is the complete channel inventory; selected readings below do not replace it. [textures.json](../../../TestOutput/playernude-characterization/textures.json) records resolution, JPEG format, SHA256, source path, every sharing use and export filename/stem matches.

Body and native graft skin surfaces share `TMHL/Caine` textures, including anatomy Shaft/Glans using Caine-Torso. Body/graft Torso skin sample: diffuse `[.9764706,.9764706,.9764706]`, Translucency Weight=.5, Glossy Layered Weight=.0940171 with SPB map, Glossy Reflectivity=.5598291, Glossy Roughness=.7393162, Dual Lobe Specular Weight=.1794872, Reflectivity=.5, Bump Strength=.4 with SP map, Normal Map strength=1 with N map, Top Coat Weight=.12 with SPB, SSS Amount=2, Direction=.5, metallic=0, emission black, cutout=1, displacement strength=0. **SP and SPB filenames alone are insufficient to assign roles**; source channels prove their usage.

The seven used shell surfaces share Meipex Texture Set 1 diffuse and Translucency Color, dual-lobe reflectivity SM, bump BM and cutout TM. Representative shell Glans/Shaft: Translucency Weight=.85, Dual Lobe Specular Weight=.65, reflectivity=.5 mapped, Bump Strength=5, Glossy Roughness=0, Glossy Layered Weight=0, SSS Amount=2, Direction=0, metallic=0, refraction=0, emission black. Normal map NM00 is used on Torso_Back and Rectum only; other shell surfaces have scalar normal strength but no normal texture. This is skin with a cutout overlay, not evidence for assigning the Wet shader to all anatomy.

Source families are **21 Caine JPEGs** (mostly 4096², Eyes-01 and EyesN 2048²) and **five Meipex JPEGs** (three Texture Set 1 maps 2048²; TM/NM00 4096²). All are listed with exact paths/channel sharing in the appendix. No source texture is missing on disk. Direct export contains six Caine diffuse JPEGs and three generated TIFFs: Caine Lashes, Caine Ref, Dicktator_DK_S1_DifM02a. Thus Meipex diffuse and Caine lashes/ref are present under converted extensions; do not mark them missing merely because the JPEG basename is absent. The useful normal, bump, coat/specular, limb displacement, dual-lobe SM and cutout TM maps largely remain only in the DAZ library. TIFF pixel identity/gamma against the source JPEG is not claimed.

Native base UV is Base Male; eyelashes use Basic Male; anatomy uses graft default. Shell's `uvSet=null` means no explicit material override, **not missing FBX UV coordinates**. Existing saved/exported UVs must be retained. Source channel inventory includes displacement strengths/limits, refraction/IOR, top-coat, transmission/scattering, glossiness/roughness, tiling and other DAZ controls even when their effect is disabled by a weight. Conversion should report unsupported inputs rather than silently drop them.

## 10. Lara and installed Bridge comparison

Lara uses Ephestra skin on G8F plus SH_G8FG anatomy; this fixture uses Caine G8M and Dicktator skin/shell. Lara's legacy Fluid/Vagina/Anus source records require a different legacy/default handling path; playernude's entire saved material set is explicitly Iray Uber. Lara's body/graft Torso consistency checks apply by principle, but the male anatomy includes an extra material shell and seven region slots. Copying Lara's skin response scalars automatically is an artistic assumption; reuse shader architecture while preserving Caine values for the first reference.

Installed Unity Bridge evidence is `Assets/Daz3D/Scripts/Editor/DTUConverter.cs` and `Assets/Daz3D/Shaders/uDTU`. `IsDTUMaterialSkin` (around line 217) treats Iray Uber/PBRSkin actor materials as skin except wet/hair/sclera and name exclusions for eyes, teeth/nails/mouth. `IsDTUMaterialWet` (around 174) matches cornea/eyemoisture/reflection/tear. `ConvertToUnityIrayUber` (around 396) consumes Base Mixing, Diffuse Color, opacity, bump/normal, gloss/specular/roughness, translucency, dual-lobe, top-coat, refraction and SSS inputs. Around 970–1110 it maps metallic, translucency and dual-lobe into `_Metallic`, `_MetallicMap`, `_TranslucencyColor`, `_TranslucencyColorMap`, `_TranslucencyWeight`, `_DualLobeSpecularWeight`, `_DualLobeSpecularReflectivity(Map)`, lobe roughness maps and ratio; glossiness workflows use 1-minus conversion. Normal/bump and opacity imports use appropriate texture import roles. Weighted diffuse overlays are partly approximated and texture tiling support has a TODO; this is not exact Iray rendering.

Expected families under explicit actor/skin roles: body and graft skin → `uDTU HDRP.SSS`; eyes wet → Wet; teeth/nails/iris/mouth → specular/appropriate non-skin family; eyelashes → cutout family. Shell must be explicitly classified as skin overlay because Bridge name/asset heuristics may miss its shell asset type. Existing project graphs include **SSS, Wet, Specular, Specular Transparent, Metallic, Metallic Transparent and Hair dissolve** variants. There is **no project-owned SSS Transparent dissolve graph** in this checkout. SSS with alpha testing may be adequate for a cutout shell; if smooth alpha blending with its SSS/dual-lobe response is required, an additional transparent SSS variant or deliberate compositing recipe would be needed. The existing transparent Specular family is an alternative with a different skin response, not an automatically equivalent replacement. No wholly new appearance family is proven necessary, but direct reuse for this layered shell is not established. Wet can be reused for compatible eye wet layers, but is not the general anatomical-equivalent shader.

The native DAZ Bridge DLL is currently loaded according to the DAZ log; installed Unity converter source is the concrete mapping evidence used here. No Bridge geometry export was invoked, and no switch away from direct FBX is recommended.

## 11. Dissolve, visibility and particles

Current contract is the eight properties `_DissolveEnabled`, `_DissolveProgress`, `_DissolveBoundsMin`, `_DissolveBoundsSize`, `_DissolveFieldParams`, `_DissolveEdgeWidth`, `_DissolveEdgeColor`, `_DissolveEdgeEmission`, written via MaterialPropertyBlock by `PerformerDissolveRig.cs`. `PerformerDissolveField.hlsl` derives local-position noise/vertical field, clips with **negative alpha** (alpha zero can survive threshold zero), and adds edge emission. It also consumes coverage controls/UV2-component masks for wardrobe coverage. Those coverage features are not a ready-made MemberOnly policy.

`PerformerDissolveProfile` supports explicit converted slot shaders; historical assertions of exactly 16 slots/two families are obsolete when that list is populated. Current rig still validates its target renderer against the profile's permanent materials and drives a target renderer; merely having matching shader properties does not make three male renderers integrated. Future setup needs matching material profiles and consistent performer-space field/bounds on every participating renderer, including shell/lashes. Keep normal appearance when disabled and verify full disappearance in color, highlights, depth and shadows with restore. Existing SSS can be reused with male textures; shell requires a verified cutout/compositing recipe or a new transparent SSS dissolve variant, plus its deformation solution. Existing Wet can be reused for eye wet surfaces.

Visibility OUT reaches shader dissolve completion at clock/.62; IN reenables the renderer at .17 while still clipped and reveals over .66. It uses `forceRenderingOff`; skeleton continuity must be independent of renderer visibility. `PerformerParticleBody` samples a live skinned target using a topology-specific `PerformerSurfaceBindingAsset` (32,768 required entries). Lara's bindings cannot be assigned to this 18,484-point source mesh. A future male surface/binding/profile must be generated after topology is accepted, and shell/lash particle policy decided. No particle or visibility work was done.

## 12. Next direct-FBX characterization export recipe

This is a measured probe recipe, not an implementation packet. Preserve known Lara direct-export conventions unless a deliberate difference is stated.

1. Open the exact playernude DUF. Keep Caine, fitted Dicktator G8M and the visible Dicktator Shell together; include eyelashes for appearance comparison. Select/export the visible character subtree and ensure body geometry is included. Do not export the shell alone as anatomy.
2. Load `scripts/playernude_runtime_morphs.csv` in DAZ FBX morph rules; enable morph export. Inspect the actual matched/export-listed properties **before exporting**, because fitted-graft enumeration failed in previous Lara probes. Preserve all unrelated saved identity/shape values as Bake; no broad Caine/Meipex/Anything Export rule.
3. Retain merged fitted graft behavior: current file proves a welded body/anatomy and correct replacement polygon total. Keep the overlay shell separate; merging it into body would lose the layered material representation. Do not enable figure merging indiscriminately. Retain the explicit anatomy surface groups and donor bones.
4. Match current binary 7400/version option and centimeter/file-scale behavior. Keep current base-resolution topology for this control test: native host minus hidden graft polygons plus graft exactly explains its polygon count. Subdivision baking would change correspondence and add no proof of missing controls; defer high-resolution/HD exports until the base channel mechanism is established.
5. Retain visible-only/hidden-poly graft treatment giving `16196 - 86 + 2177` polygons. Do not export hidden underlying host polygons or hidden shell body groups; this would introduce duplicate geometry. No special geograft checkbox name is asserted without its UI/settings evidence. Validate the resulting topology numerically.
6. Collect textures if convenient for baseline diffuse rendering; embedding is optional and already makes this fixture 168 MB. **Save a same-state DUF regardless**, because direct FBX does not preserve all material channels/maps. Source PBR texture resolution remains required even if collect/embed is enabled.
7. Geometry/morph baseline can remain without animation. However, CSV cannot guarantee bone-only ERC transfer. For a decisive bone-control probe, make a **separate characterization animation export** with animation enabled and known sample frames: saved state; Flacid=1; Flacid=.5; restored Flacid=0; Cock Length at saved±.05; restored; modest shaft1/shaft4 X/Y bends individually; restored. Change one property at a time and record frame/dial values. Retain existing baked Scale Down/Shaft Shorten settings throughout. This deliberate departure from Lara's static export is justified by the discovered pure bone controllers. Do not overwrite the baseline fixture.
8. Inspect next binary output for nonzero `Shaft Inflate` and `pJCM_Shaft1_up` frames, useful controller representation, anatomy versus shell shapes/skin, node animation curves, shaft bind transforms and the 615 overweight vertices. A channel name alone is not a pass. If bone-only controllers remain absent, preserve authored bone animation or plan a narrow source-formula mapping later; do not disguise a no-op blendshape as a working erection control.

CSV schema is proven; useful next-export behavior remains unverified. Do not label this a production export until channel movement and controller/bone behavior are measured. No generated CSV can by itself solve unskinned shell following, pelvis synchronization or raw skin anomalies.

## 13. Acceptance gates and unresolved issues

Technical next-export gates (agent-inspectable): selected geometry channels nonzero and correctly owned; bone/ERC mechanism transfer; raw/normalized influence totals and max-four versus full-weight implications; shell skin/morph following; donor/main pelvis synchronization; correct replacement topology and material slot mapping. Do not ask the user to manually read these technical facts.

Human visual gates once a later preview is authorized:

- **Erection:** compare e=0/.5/1 and a small upward-angle change; natural pose, no root pinching, corrective quality and stable scrotum. Firmness is a future compliance parameter, not assumed from the controller.
- **Length/girth:** compare the modest ranges against restored saved configuration; inspect head/shaft/base and skin detail, no shell sliding or scaling double application.
- **Bending:** test shaft1 and mid-chain ±3–5° independently, then release; smooth centerline/surface fit and useful spring-back appearance. Numeric movement cannot decide acceptable creasing.
- **Seam/MemberOnly:** close views of welded root under neutral/bent poses; compare full body and host-hidden material regions. Judge exposed base perimeter and necessary retained geometry.
- **Materials:** Caine body/native graft plus Meipex shell at fixed lighting; texture/UV match, layer opacity, bump strength, wet eyes, dual-lobe highlights and full dissolve/restore.

Most important open issues: (1) bone-only ERC export versus future formula/animation representation; (2) static shell following; (3) anomalous thigh weights and Unity influence limits; (4) donor pelvis synchronization; (5) visual root/material/control quality. Existing chain is promising, **not yet validated for runtime contact**. Stop here for joint review before an execution packet or runtime implementation.

<!-- GENERATED EVIDENCE TABLES -->

## Appendix A. Deterministic FBX renderer/material mapping

Iray Uber for every row. Paths and slot counts are raw FBX; empty shell slots remain declared. UV references are material overrides from DSON; shell UVs exist in FBX despite no override.

### `Genesis8Male/Genesis8Male.Shape`

| Slot | FBX material / DAZ surface | Source figure | DAZ material ID | Polygons | UV set |
| --- | --- | --- | --- | --- | --- |
| 0 | Face | Genesis8Male | Face-2 | 1588 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 1 | Lips | Genesis8Male | Lips-2 | 170 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 2 | Teeth | Genesis8Male | Teeth-2 | 1080 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 3 | Torso | Genesis8Male | Torso-2 | 2832 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 4 | Ears | Genesis8Male | Ears-2 | 434 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 5 | Legs | Genesis8Male | Legs-2 | 3364 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 6 | EyeSocket | Genesis8Male | EyeSocket-2 | 112 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 7 | Mouth | Genesis8Male | Mouth-2 | 978 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 8 | Arms | Genesis8Male | Arms-2 | 3886 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 9 | Pupils | Genesis8Male | Pupils-2 | 64 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 10 | EyeMoisture | Genesis8Male | EyeMoisture-3 | 216 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 11 | Fingernails | Genesis8Male | Fingernails-2 | 560 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 12 | Cornea | Genesis8Male | Cornea-2 | 64 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 13 | Irises | Genesis8Male | Irises-2 | 96 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 14 | Sclera | Genesis8Male | Sclera-2 | 248 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 15 | Toenails | Genesis8Male | Toenails-2 | 418 | /data/DAZ 3D/Genesis 8/Male/UV Sets/DAZ 3D/Base/Base Male.dsf#Base Male |
| 16 | Glans | Dicktator_Genitalia_G8M | Glans-1 | 400 | /data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/UV Sets/Meipex/Base/default.dsf#default |
| 17 | Shaft | Dicktator_Genitalia_G8M | Shaft-1 | 738 | /data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/UV Sets/Meipex/Base/default.dsf#default |
| 18 | Testicles | Dicktator_Genitalia_G8M | Testicles-1 | 344 | /data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/UV Sets/Meipex/Base/default.dsf#default |
| 19 | Torso_Front | Dicktator_Genitalia_G8M | Torso_Front-1 | 54 | /data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/UV Sets/Meipex/Base/default.dsf#default |
| 20 | Torso_Middle | Dicktator_Genitalia_G8M | Torso_Middle-1 | 91 | /data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/UV Sets/Meipex/Base/default.dsf#default |
| 21 | Torso_Back | Dicktator_Genitalia_G8M | Torso_Back-1 | 88 | /data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/UV Sets/Meipex/Base/default.dsf#default |
| 22 | Rectum | Dicktator_Genitalia_G8M | Rectum-1 | 462 | /data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/UV Sets/Meipex/Base/default.dsf#default |

### `Genesis8Male/Genesis8MaleEyelashes/Genesis8MaleEyelashes.Shape`

| Slot | FBX material / DAZ surface | Source figure | DAZ material ID | Polygons | UV set |
| --- | --- | --- | --- | --- | --- |
| 0 | EyeMoisture | Genesis8MaleEyelashes | EyeMoisture-4 | 100 | /data/DAZ 3D/Genesis 8/Male Eyelashes/UV Sets/DAZ 3D/Base/Basic Male.dsf#Basic Male |
| 1 | Eyelashes | Genesis8MaleEyelashes | Eyelashes-1 | 252 | /data/DAZ 3D/Genesis 8/Male Eyelashes/UV Sets/DAZ 3D/Base/Basic Male.dsf#Basic Male |

### `Genesis8Male/Dicktator Shell/Dicktator Shell.Shape`

| Slot | FBX material / DAZ surface | Source figure | DAZ material ID | Polygons | UV set |
| --- | --- | --- | --- | --- | --- |
| 0 | Face | Dicktator Shell | Face-3 | 0 | no explicit override; geometry UV |
| 1 | Lips | Dicktator Shell | Lips-3 | 0 | no explicit override; geometry UV |
| 2 | Teeth | Dicktator Shell | Teeth-3 | 0 | no explicit override; geometry UV |
| 3 | Torso | Dicktator Shell | Torso-3 | 0 | no explicit override; geometry UV |
| 4 | Ears | Dicktator Shell | Ears-3 | 0 | no explicit override; geometry UV |
| 5 | Legs | Dicktator Shell | Legs-3 | 0 | no explicit override; geometry UV |
| 6 | EyeSocket | Dicktator Shell | EyeSocket-3 | 0 | no explicit override; geometry UV |
| 7 | Mouth | Dicktator Shell | Mouth-3 | 0 | no explicit override; geometry UV |
| 8 | Arms | Dicktator Shell | Arms-3 | 0 | no explicit override; geometry UV |
| 9 | Pupils | Dicktator Shell | Pupils-3 | 0 | no explicit override; geometry UV |
| 10 | EyeMoisture | Dicktator Shell | EyeMoisture-5 | 0 | no explicit override; geometry UV |
| 11 | Fingernails | Dicktator Shell | Fingernails-3 | 0 | no explicit override; geometry UV |
| 12 | Cornea | Dicktator Shell | Cornea-3 | 0 | no explicit override; geometry UV |
| 13 | Irises | Dicktator Shell | Irises-3 | 0 | no explicit override; geometry UV |
| 14 | Sclera | Dicktator Shell | Sclera-3 | 0 | no explicit override; geometry UV |
| 15 | Toenails | Dicktator Shell | Toenails-3 | 0 | no explicit override; geometry UV |
| 16 | Dicktator_Genitalia_G8M_Glans | Dicktator Shell | Dicktator_Genitalia_G8M_Glans-1 | 400 | no explicit override; geometry UV |
| 17 | Dicktator_Genitalia_G8M_Shaft | Dicktator Shell | Dicktator_Genitalia_G8M_Shaft-1 | 738 | no explicit override; geometry UV |
| 18 | Dicktator_Genitalia_G8M_Testicles | Dicktator Shell | Dicktator_Genitalia_G8M_Testicles-1 | 344 | no explicit override; geometry UV |
| 19 | Dicktator_Genitalia_G8M_Torso_Front | Dicktator Shell | Dicktator_Genitalia_G8M_Torso_Front-1 | 54 | no explicit override; geometry UV |
| 20 | Dicktator_Genitalia_G8M_Torso_Middle | Dicktator Shell | Dicktator_Genitalia_G8M_Torso_Middle-1 | 91 | no explicit override; geometry UV |
| 21 | Dicktator_Genitalia_G8M_Torso_Back | Dicktator Shell | Dicktator_Genitalia_G8M_Torso_Back-1 | 88 | no explicit override; geometry UV |
| 22 | Dicktator_Genitalia_G8M_Rectum | Dicktator Shell | Dicktator_Genitalia_G8M_Rectum-1 | 462 | no explicit override; geometry UV |

## Appendix B. Anatomy-region bone influences

Counts overlap because vertices can use multiple bones and surfaces. Counts below include any positive exported weight on slots 16–22. Whole-mesh positive counts and 3D influence bounds are retained in fbx-extra.json. Bind centers come from cluster TransformLink (cm). Region max and ≥.1 counts expose substantial inherited thigh influence.

| Bone | Exact parent path | Ownership / purpose | Export bind center XYZ cm | Whole-mesh positive points | Anatomy max weight | Anatomy points ≥.1 | Positive points per anatomy surface |
| --- | --- | --- | --- | --- | --- | --- | --- |
| lThighTwist | Genesis8Male/hip/pelvis/lThighBend | base G8M attachment-region influence | 10.83351, 76.27148, 0.28365 | 1480 | 1.000000 | 507 | Torso_Middle:10, Torso_Back:8, Torso_Front:1, Glans:428, Shaft:254 |
| rThighTwist | Genesis8Male/hip/pelvis/rThighBend | base G8M attachment-region influence | -10.83051, 76.26679, 0.28933 | 1479 | 1.000000 | 507 | Torso_Middle:10, Torso_Back:7, Torso_Front:1, Glans:428, Shaft:254 |
| lThighBend | Genesis8Male/hip/pelvis | base G8M attachment-region influence | 7.97852, 97.87061, 0.82092 | 1180 | 1.000000 | 511 | Torso_Middle:9, Torso_Back:8, Torso_Front:1, Glans:428, Shaft:254 |
| rThighBend | Genesis8Male/hip/pelvis | base G8M attachment-region influence | -7.97897, 97.87616, 0.82634 | 1179 | 1.000000 | 511 | Torso_Middle:9, Torso_Back:7, Torso_Front:1, Glans:428, Shaft:254 |
| pelvis | Genesis8Male/hip | base G8M attachment-region influence | 0.00000, 109.70168, 1.81291 | 1008 | 1.000000 | 37 | Torso_Back:16, Torso_Front:5, Torso_Middle:20 |
| shaft7 | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/shaftRoot/shaft1/shaft2/shaft3/shaft4/shaft5/shaft6 | shaft centerline/shape | -0.00055, 92.28179, 28.57028 | 648 | 1.000000 | 504 | Glans:428, Shaft:254 |
| shaft6 | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/shaftRoot/shaft1/shaft2/shaft3/shaft4/shaft5 | shaft centerline/shape | -0.00076, 92.28317, 25.71581 | 627 | 1.000000 | 454 | Glans:113, Shaft:548 |
| shaft5 | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/shaftRoot/shaft1/shaft2/shaft3/shaft4 | shaft centerline/shape | -0.00043, 92.28692, 22.94445 | 322 | 0.858106 | 109 | Shaft:322 |
| shaft4 | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/shaftRoot/shaft1/shaft2/shaft3 | shaft centerline/shape | 0.00038, 92.28639, 20.54345 | 320 | 0.827451 | 129 | Shaft:320 |
| shaft3 | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/shaftRoot/shaft1/shaft2 | shaft centerline/shape | 0.00030, 92.26086, 18.24642 | 322 | 0.834119 | 134 | Shaft:322, Testicles:9, Torso_Front:9, Torso_Middle:2 |
| shaft2 | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/shaftRoot/shaft1 | shaft centerline/shape | -0.00071, 92.23196, 16.01393 | 332 | 0.851301 | 132 | Shaft:260, Testicles:58, Torso_Front:32, Torso_Middle:18 |
| lTesticle | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/shaftRoot/scrotum | scrotal region | 1.50408, 89.53796, 11.26240 | 242 | 0.999786 | 162 | Testicles:231, Torso_Middle:27 |
| rTesticle | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/shaftRoot/scrotum | scrotal region | -1.42088, 89.57742, 11.27316 | 205 | 0.999939 | 136 | Testicles:196, Torso_Middle:20 |
| colon | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/rectum1/rectum2 | perineal/rectal region | -0.00004, 92.11727, -1.37577 | 270 | 1.000000 | 151 | Rectum:270 |
| shaft1 | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/shaftRoot | shaft centerline/shape | -0.00943, 92.10443, 12.04518 | 365 | 0.987854 | 174 | Shaft:186, Testicles:110, Torso_Middle:63, Torso_Front:63 |
| scrotum | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/shaftRoot | scrotal region | 0.09896, 90.77687, 11.05211 | 474 | 0.940002 | 211 | Shaft:45, Testicles:367, Torso_Middle:106, Torso_Front:22, Torso_Back:4 |
| rectum2 | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/rectum1 | perineal/rectal region | 0.00270, 91.29950, -2.73939 | 515 | 0.999786 | 372 | Torso_Back:80, Rectum:456 |
| shaftRoot | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis | shaft centerline/shape | -0.00070, 94.30353, 5.06292 | 403 | 0.997635 | 120 | Testicles:231, Shaft:48, Torso_Middle:109, Torso_Front:63, Torso_Back:23 |
| rectum1 | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis | perineal/rectal region | 0.00545, 90.48172, -4.10300 | 338 | 0.999191 | 186 | Torso_Middle:25, Torso_Back:93, Rectum:248 |
| legsCrease | Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis | perineal/rectal region | 0.00000, 89.38209, 2.52855 | 217 | 0.998779 | 55 | Testicles:85, Torso_Middle:81, Torso_Front:4, Torso_Back:72, Rectum:17 |

### Native anatomical joint directions

Native reference before saved Scale Down/Shaft Shorten/Caine evaluation; do not use these as the saved FBX bind centers. All rows are graft-owned.

| Native ID → node name | Native parent ID | Center XYZ cm | End XYZ cm | Orientation XYZ degrees | Rotation order |
| --- | --- | --- | --- | --- | --- |
| hip → hip | #Dicktator_Genitalia_G8M | 0.00000, 103.59000, 1.92000 | 0.00000, 86.72000, 0.69000 | 0.00000, 0.00000, 0.00000 | YZX |
| pelvis → pelvis | #hip | 0.00000, 105.53000, 1.81000 | 0.00000, 85.94000, 2.43000 | 0.00000, 0.00000, 0.00000 | YZX |
| root → shaftRoot | #pelvis | -0.00002, 91.59230, 3.88371 | 0.00000, 85.56656, 2.56685 | 0.00000, 0.00000, 0.00000 | YZX |
| Shaft 1 → shaft1 | #root | -0.00025, 89.61116, 10.74309 | 0.00007, 89.48688, 16.55043 | 0.77294, 0.00000, 0.00000 | ZYX |
| Shaft 2 → shaft2 | #Shaft 1 | 0.00000, 89.65733, 16.66635 | 0.00018, 89.65501, 20.83475 | 0.00000, 0.00000, 0.00000 | ZYX |
| Shaft 3 → shaft3 | #Shaft 2 | -0.00004, 89.67529, 20.81800 | 0.00003, 89.71519, 25.25365 | 0.00000, 0.00000, 0.00000 | ZYX |
| Shaft 4 → shaft4 | #Shaft 3 | 0.00003, 89.71519, 25.25365 | -0.00004, 89.71484, 29.59807 | 0.00000, 0.00000, 0.00000 | ZYX |
| Shaft5 → shaft5 | #Shaft 4 | -0.00004, 89.71484, 29.59807 | -0.00012, 89.71477, 33.94252 | 0.00000, 0.00000, 0.00000 | ZYX |
| Shaft 6 → shaft6 | #Shaft5 | -0.00012, 89.71477, 33.94252 | 0.00003, 89.71467, 37.94505 | 0.00000, 0.00000, 0.00000 | ZYX |
| Shaft 7 → shaft7 | #Shaft 6 | 0.00003, 89.71467, 37.94505 | 0.00000, 89.71314, 41.26508 | 0.00000, 0.00000, 0.00000 | ZYX |
| Scortum → scrotum | #root | -0.00029, 88.09441, 9.85525 | 0.00000, 80.43754, 10.17941 | 0.00000, 0.00000, 0.00000 | YZX |
| Left Testicle → lTesticle | #Scortum | 1.95073, 86.70118, 9.96072 | 1.99201, 80.46210, 10.24930 | 0.00000, 0.00000, 0.00000 | YZX |
| Right Testicle → rTesticle | #Scortum | -1.93285, 86.75327, 9.96087 | -1.91124, 80.59781, 10.24495 | 0.00000, 0.00000, 0.00000 | YZX |
| lThighBend → lThighBend | #pelvis | 7.90921, 96.41562, 0.39138 | 9.31103, 75.58411, 0.07406 | 0.64882, 4.95047, 1.92386 | YZX |
| lThighTwist → lThighTwist | #lThighBend | 8.60795, 75.47764, 0.14419 | 9.30690, 53.80843, -0.26619 | 0.64882, 4.95047, 1.92386 | YZX |
| rThighBend → rThighBend | #pelvis | -7.90921, 96.41562, 0.39138 | -9.31103, 75.58411, 0.07406 | 0.64882, -4.95047, -1.92386 | YZX |
| rThighTwist → rThighTwist | #rThighBend | -8.60795, 75.47764, 0.14419 | -9.30690, 53.80843, -0.26619 | 0.64882, -4.95047, -1.92386 | YZX |
| Rectum → rectum1 | #pelvis | 0.00524, 86.99935, -3.94616 | 0.00260, 87.78526, -2.63474 | 48.13058, 0.00000, 0.00000 | YZX |
| Rectum 2 → rectum2 | #Rectum | 0.00260, 87.78526, -2.63474 | -0.00004, 88.57117, -1.32332 | 48.13057, 0.00000, 0.00000 | YZX |
| Colon → colon | #Rectum 2 | -0.00004, 88.57117, -1.32332 | 0.00000, 92.15230, -3.94599 | -40.88448, 0.00000, 0.00000 | YXZ |
| Legs Crease → legsCrease | #pelvis | 0.00000, 85.94000, 2.43000 | 0.00000, 105.53980, 2.43000 | 0.00000, 0.00000, 0.00000 | YZX |

## Appendix C. Selected and contact/shape candidate morphs

All paths below are relative to `F:/Daz3D/data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/Morphs/`. Deltas are measured at value 1, in source cm; mean is over moved points, RMS is over the entire target geometry. `ERC` means no direct deltas; mixed records also contain joint/property formulas. This table inventories candidates, not export selections. Complete operations and dependencies remain in anatomy-morphs.json.

| Internal ID (bold = selected) | Display label | Source DSF | Min..max; default | Mechanism | Formula count | Moved points | Max / mean / RMS cm | Proposed classification |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Cock Length** | Cock Length | Clare3Dx/Bonus/Cock Length.dsf | -1..1; 0 | ERC/controller | 6 | — | — | permanent configuration |
| Cock Tall | Cock Tall | Clare3Dx/Bonus/Cock Tall.dsf | -1..1; 0 | ERC/controller | 8 | — | — | permanent configuration |
| Cock Thickness | Cock Thickness | Clare3Dx/Bonus/Cock Thickness.dsf | -0.5..1; 0 | ERC/controller | 0 | — | — | permanent configuration |
| Cock Wide | Cock Wide | Clare3Dx/Bonus/Cock Wide.dsf | -1..1; 0 | ERC/controller | 8 | — | — | permanent configuration |
| ScrotumBack | Scrotum Back | Creative_Awakening/Bonus/ScrotumBack.dsf | -1..1; 0 | direct morph | 0 | 428 | 3.406687/1.711355/0.936910 | probably irrelevant to first shaft contact |
| ScrotumLeftForward | Scrotum Left Forward | Creative_Awakening/Bonus/ScrotumLeftForward.dsf | -1..1; 0 | direct morph | 0 | 276 | 2.546483/0.943866/0.450213 | probably irrelevant to first shaft contact |
| ScrotumNarrow | Scrotum Narrow | Creative_Awakening/Bonus/ScrotumNarrow.dsf | -1..1; 0 | direct morph | 0 | 332 | 0.690433/0.304671/0.141274 | probably irrelevant to first shaft contact |
| ScrotumRightForward | Scrotum Right Forward | Creative_Awakening/Bonus/ScrotumRightForward.dsf | -1..1; 0 | direct morph | 0 | 284 | 2.478085/0.891398/0.414546 | probably irrelevant to first shaft contact |
| ScrotumTiny | Scrotum Tiny | Creative_Awakening/Bonus/ScrotumTiny.dsf | -1..1; 0 | direct morph | 0 | 429 | 3.883321/2.054914/1.091823 | probably irrelevant to first shaft contact |
| Small Dick Length | Small Dick Length | Meipex/Androginy/Small Dick Length.dsf | -0.2..1.5; 0 | direct morph | 0 | 0 | 0.000000/0.000000/0.000000 | no useful geometry evidence |
| Corona Forward 1 | Corona Forward 1 | Meipex/Base/Corona Forward 1.dsf | -0.2..1.2; 0 | direct morph | 0 | 101 | 0.368309/0.169662/0.045970 | permanent configuration |
| Corona Forward 2 | Corona Forward 2 | Meipex/Base/Corona Forward 2.dsf | -0.2..1; 0 | direct morph | 0 | 457 | 0.735842/0.133724/0.095720 | permanent configuration |
| Corona Forward 3 | Corona Forward 3 | Meipex/Base/Corona Forward 3.dsf | -0.5..1; 0 | direct morph | 0 | 59 | 0.321189/0.093648/0.018817 | permanent configuration |
| Corona Inflate 1 | Corona Inflate 1 | Meipex/Base/Corona Inflate 1.dsf | -0.5..1; 0 | direct morph | 0 | 272 | 0.374791/0.066696/0.038186 | permanent configuration |
| Corona Inflate 2 | Corona Inflate 2 | Meipex/Base/Corona Inflate 2.dsf | -0.5..1.5; 0 | direct morph | 0 | 77 | 0.303010/0.076781/0.022274 | permanent configuration |
| Corpus Spongiorum Inflate | Corpus Spongiorum Inflate | Meipex/Base/Corpus Spongiorum Inflate.dsf | -1..1; 0 | direct morph | 0 | 546 | 0.830501/0.235055/0.159216 | permanent configuration |
| CorpusSpongiorum Inflate | Corpus Spongiorum Inflate | Meipex/Base/CorpusSpongiorum Inflate.dsf | -0.5..1; 0 | direct morph | 0 | 546 | 0.830498/0.235053/0.159215 | permanent configuration |
| Crown Straight 1 | Corona Straight 1 | Meipex/Base/Crown Straight 1.dsf | 0..1; 0 | direct morph | 0 | 61 | 0.224635/0.070690/0.015727 | permanent configuration |
| Corona Straight 2 | Corona Straight 2 | Meipex/Base/Crown Straight 2.dsf | -1..1; 0 | direct morph | 0 | 92 | 0.367032/0.090932/0.027403 | permanent configuration |
| Erection Preset 01 | Erection Preset 01 | Meipex/Base/Erection Preset 01.dsf | 0..1; 0 | ERC/controller | 4 | — | — | permanent configuration |
| Erection Preset 02 | Erection Preset 02 | Meipex/Base/Erection Preset 02.dsf | 0..1; 0 | ERC/controller | 6 | — | — | permanent configuration |
| Flacid Preset 01 | Flacid Preset 01 | Meipex/Base/Flacid Preset 01.dsf | 0..1; 0 | ERC/controller | 17 | — | — | permanent configuration |
| Flacid Preset 02 | Flacid Preset 02 | Meipex/Base/Flacid Preset 02.dsf | 0..1; 0 | ERC/controller | 19 | — | — | permanent configuration |
| Flacid Preset 03 | Flacid Preset 03 | Meipex/Base/Flacid Preset 03.dsf | 0..1; 0 | ERC/controller | 21 | — | — | permanent configuration |
| Flacid Preset 04 | Flacid Preset 04 | Meipex/Base/Flacid Preset 04.dsf | 0..1; 0 | ERC/controller | 31 | — | — | permanent configuration |
| **Flacid** | Flacid Preset Base | Meipex/Base/Flacid.dsf | 0..1; 0 | ERC/controller | 7 | — | — | permanent configuration |
| Frenulum Crease | Frenulum Crease | Meipex/Base/Frenulum Crease.dsf | 0..1.2; 0 | direct morph | 0 | 73 | 0.200821/0.050114/0.012217 | permanent configuration |
| Glans Bottom Down | Glans Bottom Down | Meipex/Base/Glans Bottom Down.dsf | -0.2..1.2; 0 | direct morph | 0 | 214 | 0.359579/0.098249/0.044813 | permanent configuration |
| Glans Cleavage | Glans Cleavage | Meipex/Base/Glans Cleavage.dsf | 0..2; 0 | direct morph | 0 | 127 | 0.147580/0.025516/0.009433 | permanent configuration |
| Glans Conical | Glans Conical | Meipex/Base/Glans Conical.dsf | -1..1; 0 | direct morph | 0 | 366 | 0.512004/0.138755/0.075286 | permanent configuration |
| Glans Fantasy Bumps 1 | Glans Fantasy Bumps 1 | Meipex/Base/Glans Fantasy Bumps 1.dsf | -1..1; 0 | direct morph | 0 | 181 | 0.376821/0.076877/0.036902 | permanent configuration |
| Glans Fantasy Bumps 2 | Glans Fantasy Bumps 2 | Meipex/Base/Glans Fantasy Bumps 2.dsf | -1..1; 0 | direct morph | 0 | 186 | 0.676778/0.142606/0.064074 | permanent configuration |
| Glans Flatten | Glans Flatten | Meipex/Base/Glans Flatten.dsf | -0.5..1; 0 | direct morph | 0 | 595 | 1.839849/0.735602/0.521307 | procedural candidate; broad/local shape or root corrective |
| Glans Height | Glans Height | Meipex/Base/Glans Height.dsf | -1..1; 0 | direct morph | 0 | 666 | 0.591356/0.219334/0.143220 | procedural candidate; broad/local shape or root corrective |
| Glans Inflate 1 | Glans Inflate 1 | Meipex/Base/Glans Inflate 1.dsf | 0..1.5; 0 | direct morph | 0 | 104 | 0.290075/0.077330/0.024347 | permanent configuration |
| Glans Inflate 2 | Glans Inflate 2 | Meipex/Base/Glans Inflate 2.dsf | -0.5..1.5; 0 | direct morph | 0 | 159 | 0.142881/0.026347/0.012697 | permanent configuration |
| Glans Inflate 3 | Glans Inflate 3 | Meipex/Base/Glans Inflate 3.dsf | 0..1.5; 0 | direct morph | 0 | 397 | 0.444923/0.078542/0.057188 | permanent configuration |
| Glans Inflate 4 | Glans Inflate 4 | Meipex/Base/Glans Inflate 4.dsf | -0.2..1.2; 0 | direct morph | 0 | 296 | 0.307343/0.066454/0.037857 | permanent configuration |
| Glans Length | Glans Length | Meipex/Base/Glans Length.dsf | -1..1; 0 | direct morph | 0 | 491 | 0.983105/0.604341/0.331874 | permanent configuration |
| Glans Preset 01 | Glans Preset 01 | Meipex/Base/Glans Preset 01.dsf | 0..1; 0 | ERC/controller | 18 | — | — | permanent configuration |
| Glans Preset 02 | Glans Preset 02 | Meipex/Base/Glans Preset 02.dsf | 0..1; 0 | ERC/controller | 7 | — | — | permanent configuration |
| Glans Preset 03 | Glans Preset 03 | Meipex/Base/Glans Preset 03.dsf | 0..1; 0 | ERC/controller | 9 | — | — | permanent configuration |
| Glans Preset 04 | Glans Preset 04 | Meipex/Base/Glans Preset 04.dsf | 0..1; 0 | ERC/controller | 6 | — | — | permanent configuration |
| Glans Preset 05 | Glans Preset 05 | Meipex/Base/Glans Preset 05.dsf | 0..1; 0 | ERC/controller | 11 | — | — | permanent configuration |
| Glans Preset 06 | Glans Preset 06 | Meipex/Base/Glans Preset 06.dsf | 0..1; 0 | ERC/controller | 9 | — | — | permanent configuration |
| Glans Preset 07 | Glans Preset 07 | Meipex/Base/Glans Preset 07.dsf | 0..1; 0 | ERC/controller | 11 | — | — | permanent configuration |
| Glans Preset 08 | Glans Preset 08 | Meipex/Base/Glans Preset 08.dsf | 0..1; 0 | ERC/controller | 21 | — | — | permanent configuration |
| Glans Preset 09 | Glans Preset 09 | Meipex/Base/Glans Preset 09.dsf | 0..1; 0 | ERC/controller | 14 | — | — | permanent configuration |
| Glans Preset 10 | Glans Preset 10 | Meipex/Base/Glans Preset 10.dsf | 0..1; 0 | ERC/controller | 11 | — | — | permanent configuration |
| Glans Scale | Glans Scale | Meipex/Base/Glans Scale.dsf | -0.5..1; 0 | direct morph | 0 | 614 | 1.279161/0.809224/0.468639 | permanent configuration |
| Glans Silicone Inflate | Glans Silicone Inflate | Meipex/Base/Glans Silicone Inflate.dsf | -0.2..1.5; 0 | direct morph | 0 | 808 | 1.218026/0.137958/0.154597 | probably irrelevant to first shaft contact |
| Glans Up 1 | Glans Up 1 | Meipex/Base/Glans Up 1.dsf | -0.5..1; 0 | direct morph | 0 | 679 | 0.619957/0.511413/0.301724 | permanent configuration |
| Glans Up 2 | Glans Up 2 | Meipex/Base/Glans Up 2.dsf | 0..1; 0 | direct morph | 0 | 762 | 2.859129/1.538400/1.052441 | permanent configuration |
| Glans Width | Glans Width | Meipex/Base/Glans Width.dsf | -1..1; 0 | direct morph | 0 | 773 | 0.557753/0.171405/0.134218 | procedural candidate; broad/local shape or root corrective |
| Left Testicle Squeeze | Left Testicle Squeeze | Meipex/Base/Left Testicle Squeeze.dsf | 0..2; 0 | direct morph | 0 | 108 | 0.889196/0.276820/0.084321 | probably irrelevant to first shaft contact |
| **pJCM_Shaft1_up** | pJCM_Shaft1_up | Meipex/Base/pJCM_Shaft1_up.dsf | 0..1.7; 0 | direct morph + ERC | 1 | 27 | 0.317931/0.175953/0.021502 | procedural candidate; broad/local shape or root corrective |
| Right Testicle Squeeze | Right Testicle Squeeze | Meipex/Base/Right Testicle Squeeze.dsf | 0..2; 0 | direct morph | 0 | 112 | 0.924820/0.272914/0.085281 | probably irrelevant to first shaft contact |
| Scale Down | Scale Down | Meipex/Base/Scale Down.dsf | -0.2..0.6; 0 | direct morph + ERC | 60 | 1631 | 17.813321/10.501726/10.612750 | permanent configuration |
| Scrotum Bulge Fix2 | Scrotum Bulge Fix | Meipex/Base/Scrotum Bulge Fix.dsf | 0..2; 0 | direct morph | 0 | 56 | 0.601539/0.196577/0.042549 | probably irrelevant to first shaft contact |
| Scrotum Cleavage | Scrotum Cleavage | Meipex/Base/Scrotum Cleavage.dsf | -1..1.5; 0 | direct morph | 0 | 115 | 0.465142/0.128901/0.042197 | probably irrelevant to first shaft contact |
| Scrotum Flacid Push3 | Scrotum Flacid Push | Meipex/Base/Scrotum Flacid Push.dsf | 0..2; 0 | direct morph | 0 | 94 | 1.640759/0.460099/0.139023 | probably irrelevant to first shaft contact |
| Scrotum Fold 1 | Scrotum Fold 1 | Meipex/Base/Scrotum Fold 1.dsf | 0..1; 0 | direct morph | 0 | 22 | 1.044299/0.300949/0.045444 | probably irrelevant to first shaft contact |
| Scrotum Fold 2 | Scrotum Fold 2 | Meipex/Base/Scrotum Fold 2.dsf | 0..1; 0 | direct morph | 0 | 111 | 0.463386/0.160366/0.045521 | probably irrelevant to first shaft contact |
| Scrotum Fold 3 | Scrotum Fold 3 | Meipex/Base/Scrotum Fold 3.dsf | 0..1; 0 | direct morph | 0 | 36 | 0.243989/0.133814/0.019119 | probably irrelevant to first shaft contact |
| Scrotum Inflate | Scrotum Inflate | Meipex/Base/Scrotum Inflate.dsf | 0..1.5; 0 | direct morph | 0 | 428 | 2.226890/0.613265/0.395994 | probably irrelevant to first shaft contact |
| Scrotum Silicone Inflate | Scrotum Silicone Inflate | Meipex/Base/Scrotum Silicone Inflate.dsf | 0..1.5; 0 | direct morph + ERC | 18 | 436 | 5.673145/1.881789/1.116728 | probably irrelevant to first shaft contact |
| Scrotum Stretch | Scrotum Stretch | Meipex/Base/Scrotum Stretch.dsf | 0..1; 0 | direct morph | 0 | 430 | 16.052029/10.251771/5.440896 | probably irrelevant to first shaft contact |
| Scrotum_Flacid | Scrotum Flacid | Meipex/Base/Scrotum_Flacid.dsf | 0..1; 0 | direct morph | 0 | 391 | 1.442435/0.175987/0.122384 | probably irrelevant to first shaft contact |
| ScrotumBack_Fold | Perineal Fold | Meipex/Base/ScrotumBack_Fold.dsf | 0..1; 0 | direct morph | 0 | 21 | 0.898016/0.184723/0.032069 | probably irrelevant to first shaft contact |
| Curve 1 | Shaft Bend Curve 1 | Meipex/Base/Shaft Bend Curve 1.dsf | -1..1; 0 | ERC/controller | 6 | — | — | bone bend reference; not squish |
| Curve 2 | Shaft Bend Curve 2 | Meipex/Base/Shaft Bend Curve 2.dsf | -1..1; 0 | ERC/controller | 4 | — | — | bone bend reference; not squish |
| Curve 3 | Shaft Bend Curve 3 | Meipex/Base/Shaft Bend Curve 3.dsf | -1..1; 0 | ERC/controller | 7 | — | — | bone bend reference; not squish |
| Shaft Conical | Shaft Conical | Meipex/Base/Shaft Conical.dsf | -1..1; 0 | direct morph + ERC | 5 | 0 | 0.000000/0.000000/0.000000 | permanent configuration |
| Shaft Inflate 1 | Shaft Inflate 1 | Meipex/Base/Shaft Inflate 1.dsf | -0.5..1; 0 | direct morph | 0 | 1345 | 1.203900/0.576587/0.522919 | procedural candidate; broad/local shape or root corrective |
| Shaft Inflate 2 | Shaft Inflate 2 | Meipex/Base/Shaft Inflate 2.dsf | -0.5..1.5; 0 | direct morph | 0 | 726 | 0.959524/0.293034/0.224258 | procedural candidate; broad/local shape or root corrective |
| Shaft Inflate 3 | Shaft Inflate 3 | Meipex/Base/Shaft Inflate 3.dsf | 0..1.5; 0 | direct morph | 0 | 613 | 1.545088/0.355051/0.285221 | procedural candidate; broad/local shape or root corrective |
| Shaft Inflate 4 | Shaft Inflate 4 | Meipex/Base/Shaft Inflate 4.dsf | 0..1.5; 0 | direct morph | 0 | 551 | 0.675422/0.202510/0.135238 | procedural candidate; broad/local shape or root corrective |
| **Shaft Inflate** | Shaft Inflate | Meipex/Base/Shaft Inflate.dsf | -1..1; 0 | direct morph | 0 | 1343 | 1.203930/0.585797/0.528385 | procedural candidate; broad/local shape or root corrective |
| Shaft Preset 01 | Shaft Preset 01 | Meipex/Base/Shaft Preset 01.dsf | 0..1; 0 | ERC/controller | 6 | — | — | permanent configuration |
| Shaft Preset 02 | Shaft Preset 02 | Meipex/Base/Shaft Preset 02.dsf | 0..1; 0 | ERC/controller | 8 | — | — | permanent configuration |
| Shaft Preset 03 | Shaft Preset 03 | Meipex/Base/Shaft Preset 03.dsf | 0..1; 0 | ERC/controller | 17 | — | — | permanent configuration |
| Shaft Preset 04 | Shaft Preset 04 | Meipex/Base/Shaft Preset 04.dsf | 0..1; 0 | ERC/controller | 12 | — | — | permanent configuration |
| Shaft Preset 05 | Shaft Preset 05 | Meipex/Base/Shaft Preset 05.dsf | 0..1; 0 | ERC/controller | 8 | — | — | permanent configuration |
| Shaft Preset 06 | Shaft Preset 06 | Meipex/Base/Shaft Preset 06.dsf | 0..1; 0 | ERC/controller | 10 | — | — | permanent configuration |
| Shaft Preset 07 | Shaft Preset 07 | Meipex/Base/Shaft Preset 07.dsf | 0..1; 0 | ERC/controller | 13 | — | — | permanent configuration |
| Shaft Preset 08 | Shaft Preset 08 | Meipex/Base/Shaft Preset 08.dsf | 0..1; 0 | ERC/controller | 9 | — | — | permanent configuration |
| Shaft Preset 09 | Shaft Preset 09 | Meipex/Base/Shaft Preset 09.dsf | 0..1; 0 | ERC/controller | 15 | — | — | permanent configuration |
| Shaft Root Fold | Shaft Root Fold | Meipex/Base/Shaft Root Fold.dsf | 0..1.5; 0 | direct morph | 0 | 47 | 0.443763/0.163242/0.032462 | permanent configuration |
| Shaft Shorten | Shaft Shorten | Meipex/Base/Shaft Shorten.dsf | -0.1..0.8; 0 | direct morph + ERC | 48 | 1212 | 16.398476/13.252477/10.628119 | permanent configuration |
| Shaft Side-Side Curve 1 | Shaft Side-Side Curve 1 | Meipex/Base/Shaft Side-Side Curve 1.dsf | -1..1; 0 | ERC/controller | 7 | — | — | bone bend reference; not squish |
| Shaft Side-Side Curve 2 | Shaft Side-Side Curve 2 | Meipex/Base/Shaft Side-Side Curve 2.dsf | -1..1; 0 | ERC/controller | 6 | — | — | bone bend reference; not squish |
| Shaft Side-Side | Shaft Side-Side | Meipex/Base/Shaft Side-Side.dsf | -1..1; 0 | ERC/controller | 1 | — | — | permanent configuration |
| Shaft Silicone Inflate | Shaft Silicone Inflate | Meipex/Base/Shaft Silicone Inflate.dsf | 0..1.5; 0 | direct morph | 0 | 841 | 1.399966/0.519743/0.403217 | probably irrelevant to first shaft contact |
| Shaft Twist | Shaft Twist | Meipex/Base/Shaft Twist.dsf | -1..1; 0 | ERC/controller | 6 | — | — | bone bend reference; not squish |
| Up-Down | Shaft Up-Down | Meipex/Base/Shaft Up-Down.dsf | -1..1; 0 | ERC/controller | 1 | — | — | bone bend reference; not squish |
| Shaft_Straight | Shaft Straight | Meipex/Base/Shaft_Straight.dsf | -1..1; 0 | direct morph | 0 | 490 | 0.514704/0.134943/0.092438 | permanent configuration |

## Appendix D. Exact texture files and map sharing

All source files below are JPEG. An export stem match with `.tif` is a converted file, not byte-identical source evidence. A dash means no same-stem image in playernude.images. Source inventory retains every individual surface use; this table groups channels and figures.

| Exact source path | Resolution | Used by figures | Exact mapped channel IDs | Export image filenames |
| --- | --- | --- | --- | --- |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-Face.jpg | 4096 × 4096 | Genesis8Male | Translucency Color, diffuse | Caine-Face.jpg |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-FaceSPB.jpg | 4096 × 4096 | Genesis8Male | Glossy Layered Weight, Top Coat Weight | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-FaceSP.jpg | 4096 × 4096 | Genesis8Male | Bump Strength, Dual Lobe Specular Weight, Glossy Layered Weight, Top Coat Bump | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-FaceN.jpg | 4096 × 4096 | Genesis8Male | Normal Map | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-Teeth.jpg | 4096 × 4096 | Genesis8Male | Translucency Color, diffuse | Caine-Teeth.jpg |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-TeethB.jpg | 4096 × 4096 | Genesis8Male | Bump Strength | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-Torso.jpg | 4096 × 4096 | Dicktator_Genitalia_G8M, Genesis8Male | Translucency Color, diffuse | Caine-Torso.jpg |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-TorsoSPB.jpg | 4096 × 4096 | Dicktator_Genitalia_G8M, Genesis8Male | Glossy Layered Weight, Top Coat Weight | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-TorsoSP.jpg | 4096 × 4096 | Dicktator_Genitalia_G8M, Genesis8Male | Bump Strength, Top Coat Bump | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-TorsoN.jpg | 4096 × 4096 | Dicktator_Genitalia_G8M, Genesis8Male | Normal Map | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-Legs.jpg | 4096 × 4096 | Genesis8Male | Translucency Color, diffuse | Caine-Legs.jpg |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-LegsN.jpg | 4096 × 4096 | Genesis8Male | Normal Map | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-LegsDis.jpg | 4096 × 4096 | Genesis8Male | Displacement Strength | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-Arms.jpg | 4096 × 4096 | Genesis8Male | Translucency Color, diffuse | Caine-Arms.jpg |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-ArmsN.jpg | 4096 × 4096 | Genesis8Male | Normal Map | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-ArmsDis.jpg | 4096 × 4096 | Genesis8Male | Displacement Strength | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine-Eyes-01.jpg | 2048 × 2048 | Genesis8Male | Translucency Color, diffuse | Caine-Eyes-01.jpg |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine EyesSP.jpg | 4096 × 4096 | Genesis8Male | Bump Strength, Glossy Roughness | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine EyesN.jpg | 2048 × 2048 | Genesis8Male | Normal Map | — |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine Ref.jpg | 4096 × 4096 | Genesis8Male | Cutout Opacity, diffuse | Caine Ref.tif |
| F:/Daz3D/Runtime/Textures/TMHL/Caine/Caine Lashes.jpg | 4096 × 4096 | Genesis8MaleEyelashes | Cutout Opacity | Caine Lashes.tif |
| F:/Daz3D/Runtime/Textures/Meipex/M_Dicktalicious/Genitalia_v3/Dicktator/Texture Set 1/Dicktator_DK_S1_DifM02a.jpg | 2048 × 2048 | Dicktator Shell | Translucency Color, diffuse | Dicktator_DK_S1_DifM02a.tif |
| F:/Daz3D/Runtime/Textures/Meipex/M_Dicktalicious/Genitalia_v3/Dicktator/Texture Set 1/Dicktator_DK_S1_SM.jpg | 2048 × 2048 | Dicktator Shell | Dual Lobe Specular Reflectivity | — |
| F:/Daz3D/Runtime/Textures/Meipex/M_Dicktalicious/Genitalia_v3/Dicktator/Texture Set 1/Dicktator_DK_S1_BM.jpg | 2048 × 2048 | Dicktator Shell | Bump Strength | — |
| F:/Daz3D/Runtime/Textures/Meipex/M_Dicktalicious/Genitalia_v3/Dicktator/Dicktator_DK_TM.jpg | 4096 × 4096 | Dicktator Shell | Cutout Opacity | — |
| F:/Daz3D/Runtime/Textures/Meipex/M_Dicktalicious/Genitalia_v3/Dicktator/Displacement/Dicktator_DK_NM00.jpg | 4096 × 4096 | Dicktator Shell | Normal Map | — |

## Appendix E. Reproduction inventory and checks

| Artifact under TestOutput/playernude-characterization | Purpose |
| --- | --- |
| fbx.json | Existing binary inspector: hierarchy, slots, blendshape frames/deltas, all cluster bind matrices, materials and texture links |
| fbx-extra.json | UV sets, used slots, topology seams/boundaries, per-region influences, analytic rotation sensitivity, embedded bytes and animation object counts |
| duf-structure.json | Saved scene nodes/property values/generated shell geometry |
| dson-assets.json | Resolved directly referenced physical assets with hashes, bone/geometry/UV and modifier summaries |
| anatomy-native.json | Native graft nodes/topology/graft mapping/skin metadata |
| anatomy-morphs.json | 252 modifiers: channel metadata, target, source hash/path, all ERC operations and measured direct deltas |
| renderer-material-map.json | Deterministic FBX renderer/slot → DSON figure/surface/material/shader/UV mapping |
| materials.json | Complete saved effective channel values/maps, no ownership/inheritance issues |
| material-channels.csv | Readable complete channel inventory for all 48 materials |
| textures.json | 26 physical source files with dimensions/hashes/sharing/export counterparts |

Technical checks: inspector completed with no malformed shape indices/deltas; all direct DUF references resolved; 48 material records, 26 unique source textures, zero missing textures or ownership/inheritance issues; renderer mapping resolved every declared slot to exactly one source record; selected CSV IDs are unique native modifiers; terminal Bake schema checked. No rendered or Unity acceptance is claimed.
