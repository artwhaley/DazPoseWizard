# Lara first outfit: characterization and proposed next proofs

Inspected 2026-10-04, against main at `d469ed6`. This is evidence and a proposed
sequence, not acceptance of appearance or authorization for a wardrobe runtime.

## Inputs and source-state difference

- Saved scene: `C:/Users/artwh/OneDrive/Documents/DAZ 3D/Studio/My Library/Scenes/larafirstoutfit.duf`.
- New direct export: `Assets/TestCharacter/larafirstoutfit.fbx`, 178,338,848 bytes.
- Canonical reference: `Assets/TestCharacter/lara.fbx`, configured GUID
  `16025d97afd36114ca02420ca90171e8`, Generic animation import.
- Scene SHA-256: `69c815876d5fdb4f1a6ec5d9bcf782a43f79df86e0df8a1ac9e1c8558375ed63`.
- New FBX SHA-256: `992bcc227f7f61a29c44774a42b57d86eb3d9c1072118ee3522ea7e0a1d0b628`.
- Reference FBX SHA-256: `fccdeb0960ba70e8c1a3c4b2fe38a43a14f4349c626bc067ce39306729cd95bd`.

The saved scene predates the addition of Charlene's Closet panties. Its source
records do identify the visible Emiko hair, hidden Starlette hair, Peekaboo dress,
and fitted SH_G8FG graft. The later FBX contains the panties as well. Save the
current DAZ scene again before using the DUF as the complete source of truth.

## Geometry, skinning and morph inventory

Raw control-point counts differ from imported vertex counts because Unity splits
vertices at material/UV/normal boundaries. All exported and imported meshes have
skin weights on every vertex/control point. Each new mesh has 122 one-frame
blendshape channels, all defaulting to weight zero; this does not imply 122 useful
morphs on each attachment.

| Mesh | Raw points / polygons | Unity vertices | Material slots | Weighted bones | Nonempty raw shapes |
| --- | ---: | ---: | ---: | ---: | ---: |
| Canonical old Lara | 16,556 / 16,368 | 19,331 | 16 | 170 | See reference report |
| New Lara plus graft | 18,080 / 17,856 | 21,335 | 20 | 173 | 122 (includes common pelvic offset) |
| Emiko hair | 59,875 / 45,866 | 61,532 | 14 | 16 | 10 |
| Charlene's Closet panties | 1,981 / 1,860 | 2,195 | 2 | 7 | 3 |
| Peekaboo dress | 16,572 / 16,288 | 16,854 | 1 | 20 | 43 |
| Hidden Starlette hair | 70,569 / 58,848 | 77,306 | 7 | 14 | 85 |

The installed export preferences have `IncludeVisibleOnly=false`,
`MergeFollowers=false`, and `StaticFollowers=false`. Starlette has raw FBX
Visibility=0 but imports with an enabled, active renderer. A clean test export
should use **visible figures only** to avoid the extra hair and its material-name
collision. Ordinary attachments retain donor skeletons in this export.

The panties' seven weighted bones have matching canonical hierarchy suffixes and
bind matrices (within numerical roundoff). The dress's 20 weighted bones match
canonical names/paths; the largest bind-matrix element difference is
`0.0000246689`, at the two pectorals. Raw cluster matrices corroborate this.

Emiko has 14 canonical weighted bones and two attachment-local bones,
`leftpiggy` and `rightpiggy`, both beneath its donor `head`. Nine canonical hair
bind matrices differ from Lara; the largest element difference is `0.0185132`
in Unity coordinates. Name matching alone would therefore be an insufficient
binding proof. Retain the two local bones, anchor them to the canonical head, and
validate any rest/bind conversion before declaring the hair compatible.

Default Unity imports cap weights at four influences per vertex. Raw maxima are
10 for the combined body, 6 for Emiko, 6 for panties, and 7 for the dress. An
isolated custom-import probe with a 32-influence ceiling retained up to 10, 6,
5, and 6 respectively. Although the probe requested a `1e-7` weight floor, Unity
persisted `0.001`; raw weights beneath that effective floor can be discarded.
The diagnostic quality setting is Unlimited, and the imported renderers use
Auto skin quality. This proves a more complete imported representation is
possible; motion/deformation still needs validation. The user's live import
settings were not changed.

## Graft: welded geometry exists; toggle and selected morphs remain unproven

The source is
`F:/Daz3D/data/SledgeHammer/SH_G8FG/SH_G8FG/SH_G8FemGen_2303.dsf`.
Its geometry has 2,303 vertices, 2,218 polygons, 204 graft vertex pairs and a list
of 730 hidden host-body polygons. The exported polygon count matches exactly:

`16,368 - 730 + 2,218 = 17,856`.

The fitted graft is baked into Lara's body renderer despite ordinary follower
merging being disabled. The four appended material slots are Torso, Fluid,
Vagina and Anus. Three additional weighted bones remain under the graft donor
hierarchy: Vagina, its child Fluid, and Anus. Raw body/graft material adjacency
has 100 shared seam edges and 100 shared seam vertices; every such edge has two
incident polygons. This is a topology measurement, not visual seam acceptance.

Hiding graft submeshes alone would expose the region where the original body
polygons were removed. We need a closed-surface fallback when anatomy is
suppressed. Possible export/import representations should be tested on the same
canonical skeleton; none has been implemented and no runtime mesh surgery is
proposed.

The source has native graft shapes such as `Vagina.Open1`, `Vagina.Shape`,
`Anus.Open1` and `Squirt1`. None appears in this FBX. The saved export rules do not
request those controls, so this export does **not** establish that direct FBX
cannot preserve them. Test one native graft control explicitly before drawing
that conclusion.

There is also a morph defect to isolate: 23 unrelated body channels, including
both Grace breast controls, have byte-identical sparse delta data affecting
1,626 points in the pelvis (Y about 84.7–107.7 cm), with a 4.31 mm maximum.
The old body's Grace controls have zero displacement. The common pelvic change
is consistent with a graft assembly/rest-shape offset; its cause remains an
inference until a graft-off/on controlled comparison is made. These slots must
not be accepted as breast controls or automatically driven in Lara's runtime.

## Actual morph evidence and a human candidate

The outfit already contains meaningful matched source-name deformations:

| Shape | Body max | Dress max | Panties max |
| --- | ---: | ---: | ---: |
| EX_Breathe | 12.00 mm | 11.90 mm | 0.218 mm |
| EX_BreatheBelly | 23.84 mm | 12.79 mm | 0.944 mm |
| Sakura 8 Glute Size | 6.71 mm | 7.19 mm | 7.36 mm |
| PBMNipples | 11.60 mm* | 11.39 mm | Empty |

*The body measurement includes the shared pelvic offset. The dress's nipple
deformation is localized to its breast region. Matching names and nonempty
shapes establish export data, not correct garment fit or an accepted body range.
The failed Grace breast shapes are empty on both garments.

A stronger **source candidate**, not yet a Unity blendshape, is
`PBM Lara Breasts` (label **Lara Breasts**, category `/People/Stylized`):

- Source: `F:/Daz3D/data/DAZ 3D/Genesis 8/Female/Morphs/Thorneworks/Lara/PBM Lara Breasts.dsf`.
- Saved value: `0.5555556`; declared range: `0..1`; AutoFollow is enabled.
- 961 source vertices move; maximum 32.84 mm; RMS over those vertices 16.21 mm.
- The asset also has 12 ERC formulas affecting pectoral center/end points.
- It is baked into the current body rather than exported as a runtime shape.

Human checkpoint: compare 0%, about 55.6%, and 100% in DAZ; restore the chosen
baseline afterward. Decide whether this is a desirable runtime control or whether
body shaping should be deferred. Do not propagate it into wardrobe or create a
semantic contract before that judgment and the exported-delta validation.

## Materials: preserve the saved source appearance

The source body/graft use Ephestra textures, rather than the old canonical
Lara texture set. Emiko source materials specify color, cutout, bump, normal,
roughness, glossy, refraction and top-coat state. The dress has color, bump,
normal and glossy/top-coat state; the panties use leather trim and latex cloth.

Default imported new materials are HDRP/Lit. Hair contains opaque and transparent
slots; its transparent slots have alpha clipping enabled but cutoff zero.
Fluid is transparent. Both garments import opaque. These defaults are not
appearance acceptance or a performer material conversion.

Measured losses/collisions:

- Many Emiko color maps remain linked in the raw FBX but are missing from the
  corresponding imported Unity material slots. This can be addressed in an
  Editor-side normalization proof using measured source data.
- Two exported materials are named Scalp. Unity assigns Starlette's scalp
  texture to Emiko's scalp. Excluding hidden Starlette removes that particular
  collision; importer/material identity still needs asset and surface scope.
- The FBX dress material links only its diffuse map. Its normal and bump maps
  exist in the saved DUF but do not survive this direct export. Recover them from
  source metadata rather than asking the user to remap every surface manually.
- Most source skin, cloth and hair shading properties are absent from default
  imported material state. Retain source material metadata; preserve appearance
  before introducing dissolve support. Existing body dissolve shaders may be
  reusable families, but their old material instances are not the new source look.

## Proposed incremental sequence

1. Save the current scene to capture the complete chosen outfit. Resolve the
   Lara Breasts visual question only if runtime body shaping is wanted.
2. Prepare one small, reproducible direct-FBX comparison: visible chosen assets,
   one explicitly selected native graft control, targeted nipple rules, and
   a graft-off reference at otherwise identical body state. This should isolate
   the pelvic-offset defect and native graft morph survival. Confirm settings
   together before the next manual DAZ export.
3. Present the resulting skeleton/bind evidence, then prove a single garment
   renderer on the canonical skeleton in an isolated validation scene. Begin
   with the panties (simplest binding) and then the fitted dress. Preserve donor
   geometry and local bones; remove duplicate canonical donor bones only after
   deterministic rest/pose checks pass.
4. Prove source-faithful materials for that garment and Emiko. Stop for visual
   acceptance of fit, clipping, hair alpha and the source appearance. Treat the
   pigtail bones as retained transform chains; add no physics during this proof.
5. Resolve closed-surface anatomy suppression and restore behavior, then inspect
   the graft seam, poses and one morph with the user. Keep persistent desired
   anatomy state distinct from outfit-imposed effective state.
6. Generalize renderer visibility/dissolve only after those attachment proofs.
   Current dissolve/stable visibility assume one body renderer, a 16-slot
   material profile and body-local dissolve bounds; teleport already captures
   child Renderers broadly. Shared performer coordinates/bounds need a later
   visual proof. `ChangeOutfit()` remains a later accepted slice.

## Tools and validation records

`scripts/inspect-fbx.py` uses Blender's installed standalone binary FBX reader.
It reports hierarchy, skin clusters/matrices, weight statistics, material/texture
connections, morph frames, delta fingerprints, moved-point bounds and displacement.
Raw FBX coordinates here are centimeters (`UnitScaleFactor=1`).

`Assets/DazPose/Editor/DazFbxEvidenceInspector.cs` reads imported assets without
instantiating or changing scenes, rigs, meshes or materials. Its menu command is
**Tools > DAZ Pose > Development > Inspect Selected FBX Evidence**. It reports
import/quality settings, bone paths, bind poses, submeshes, weights, all shape
frame displacement statistics/default weights and shader/material properties.
Positions are imported mesh-local Unity units; do not mix raw and imported counts.

Full local reports are under `TestOutput/appearance-evidence/`: raw and Unity
inventories for both FBXs, the full-weight probe, unpacked DSON and Unity logs.
Unity runs used the existing ignored diagnostic project at
`.dazposewizard/p0c-native-generation`, Unity 6000.5.9f1. The canonical FBX and its
current importer metadata were copied there; new FBX source hashes match between
raw and imported reports. Imported baseline material observations are specific
to that diagnostic copy, not live scene material overrides.

Compilation/import inspection passed. Cross-reader checks covered both source
hashes, every mesh and weighted-bone count, and all 711 shape-frame names/counts
and maximum displacements. The largest raw-centimeter to imported-Unity
displacement discrepancy was less than `9.54e-8` meters. Visual appearance, animation fit,
rebinding, runtime anatomy toggles, graft-specific morph export and dissolve
integration remain unaccepted. No runtime architecture or live scene was edited.
The new licensed texture sidecar is excluded from Git. No milestone was committed.

Reference behavior: DAZ's [geo-grafting documentation](https://docs.daz3d.com/public/software/dazstudio/4/referenceguide/terms/geo-grafting/start)
describes fitted host-polygon removal, welding and retention of extra bones.
Its [FBX export scripting sample](https://docs.daz3d.com/_export/raw/public/software/dazstudio/4/referenceguide/scripting/api_reference/samples/file_io/export_fbx_silent/start)
documents substring morph-rule matching; the broad `Nipples` rule explains the
many unrelated corrective candidates. These references inform the comparison,
while the actual asset measurements above remain the basis for decisions.
