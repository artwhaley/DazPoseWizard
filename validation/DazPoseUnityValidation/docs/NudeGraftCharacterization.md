# Laranude direct-FBX graft investigation

Measured 2026-10-04. This records the attempted export and the next controlled
probe. No runtime character, scene, rebinding or anatomy policy was changed.
Visual acceptance remains pending.

## Result

The fitted graft survives as welded body geometry, with its extra bones and
material groups. Native graft morph export has **not** succeeded in this file:
there are no `Vagina.*` shape channels. Every facial channel contains the same
unwanted graft-only deformation. The source FBX already contains this defect;
Unity preserves it.

The broad `Lara` export rule also changes the body's neutral geometry and exports
identity/corrective controls. The resulting asset is not ready to replace Lara.

## Source and imported measurements

Source: `Assets/TestCharacter/laranude.fbx`, 137,110,496 bytes,
last written 2026-10-04 00:56:23 local time. SHA256:
`6aceceebe426d52838b40b50ffaf230e7f51f881df18752cac7c70d559d546aa`.
The DAZ log records two exports to this filename; this report measures the later one.

| Mesh | Raw points / polygons | Unity vertices | Weighted bones | Shape channels |
| --- | ---: | ---: | ---: | ---: |
| Combined Genesis body and graft | 18,080 / 17,856 | 21,601 | 173 | 118 |
| Emiko hair | 59,875 / 45,866 | 61,801 | 16 | 118 |
| Charlenes Closet panties | 1,981 / 1,860 | 2,203 | 7 | 118 |

The dress and Starlette are absent from this FBX. Hair and panties remain present;
the filename alone does not establish a body-only export.

Unity 6000.5.9f1 imported the asset as Generic, optimization off, blendshapes on,
file scale enabled. The diagnostic import uses Standard skin weights, limiting
each vertex to four influences; raw body/hair/panty maxima are 10/6/6. Both raw
and imported geometry have no vertices without weights. No full-weight import
repeat was needed to establish the morph defect.

All 354 raw/imported shape-frame names and counts match. Maximum displacements
agree after centimeter-to-meter conversion to within `1.04e-7` meters. Unity
completed successfully with exit code 0. These checks confirm faithful transfer
of this file's data, not correct anatomy behavior.

## Graft geometry, rigging and materials

The native asset is
`F:/Daz3D/data/SledgeHammer/SH_G8FG/SH_G8FG/SH_G8FemGen_2303.dsf`.
Its graft replaces 730 host polygons and adds 2,218 polygons:
`16,368 - 730 + 2,218 = 17,856`, matching this export.

There are 100 edges and 100 vertices shared by host and graft material regions.
Each seam edge has two incident polygons; no nonmanifold seam edge was found.
This is a topology check, not a visual seam or pose acceptance.

The body retains 170 canonical weighted bones plus three donor-local bones:

- `Genesis8Female/SH_G8FemGen_2303/hip/pelvis/Vagina`
- `Genesis8Female/SH_G8FemGen_2303/hip/pelvis/Vagina/Fluid`
- `Genesis8Female/SH_G8FemGen_2303/hip/pelvis/Anus`

Their raw positive-weight entry counts are 417, 994 and 243 respectively;
these are not disjoint vertex populations or rigidly assigned vertex counts.
The extra bones remain under the donor graft hierarchy. They have not been
reparented onto the runtime skeleton.

The combined body has 20 material slots. Its four graft slots import in the
diagnostic project as HDRP/Lit:

| Slot | Name | Imported maps | Render queue |
| --- | --- | --- | ---: |
| 16 | Torso | EphestraTorso_1002 base color | 2225 |
| 17 | Fluid | RipplesRefl normal | 3000 |
| 18 | Vagina | OMGPSY2 base color | 2225 |
| 19 | Anus | No texture map | 2225 |

This inventory does not validate the source shader conversion or appearance.
Hiding these slots cannot restore the removed host polygons. A closed-body
reference is still needed for future anatomy suppression.

## Morph findings

The saved signed-in DAZ exporter options contain `Vagina -> Export` and
`Lara -> Export`, followed by facial rules and `Anything -> Bake`. They contain
no separate `Nipples` rule. These are the options read after export, rather than
an embedded per-file settings manifest.

There are zero `Vagina` channels anywhere in the binary FBX. The only channel
containing `Nipples` is `MCM Lara Nipples`, copied across all three meshes. Its
body shape changes only graft vertices; hair/panty copies are empty. It is not
evidence of a working nipple control.

Native `Vagina.Open1.dsf` exists and has 191 authored delta entries, maximum
source displacement 1.30024 cm at value 1, range 0–1.5. `Vagina.Shape.dsf` has
133 entries, maximum 0.59128 cm, range 0–1.2. Missing exported channels therefore
do not mean the graft lacks authored shapes. Whether the live graft properties
are loaded/enumerated by the exporter remains unresolved.

The common false delta moves 1,354 raw vertices, all exclusively in graft
material regions, by up to **3.90460 mm**. Its moved-point bounds are:
`[-19.08654, 84.05887, -12.05053]` to
`[19.08976, 106.75482, 11.35029]` cm.
All **95 of 95** facial `eCTRL` channels include these exact same 1,354 vectors.
Thirteen unrelated channels are entirely identical to this delta, including
W/M/K JawOnly, Lara nipple/navel/eye correctives, four shoulder correctives,
Mix-Lara-PoseyHead and Lara lashes.

This proves a repeated graft baseline component. It does not yet prove the
internal DAZ mechanism causing it. Subtracting a common component would be a
possible later correction only after a controlled reference validates it.

All canonical facial controls survive by name. The three missing canonical
channels are the two ineffective Grace breast candidates and Sakura glute size.
Twenty Lara identity/corrective channels were added, including Lara body, head
and breasts. All exported default morph weights are zero.

The neutral body's highest raw vertex is **179.94762 cm**, versus **186.86508 cm**
in the original Lara and first-outfit FBXs. Other bounds also change. The broad
Lara rule is a likely explanation because it exports identity controls previously
baked into the body; appearance should not be accepted from this neutral asset.

## Next manual DAZ probe

Use `scripts/LaraGraftProbe-MorphExportRules.csv`, which exports only
`Vagina.Open1` and one left blink control. Everything else is baked, preserving
the current chosen body state instead of requesting every Lara identity morph.
Import this CSV as the complete replacement rule list, not an addition to the
current broad rules.

1. Keep Lara and the fitted `SH_G8FemGen` visible. Temporarily hide clothing/hair
   to reduce the probe; keep the current body settings and pose fixed.
2. Select the graft and set `Vagina.Open1` to **0%**. Export
   `laragraftprobe_open0.fbx` beside the other models, with Morphs enabled,
   the probe CSV, Include Visible Only on, and the existing other FBX settings.
3. Set that same graft control to **100%** (value 1.0). Export
   `laragraftprobe_open100.fbx` with identical settings. Then restore the control
   to 0% and the desired clothing/hair visibility.

These two files test whether explicitly exercising the live native control makes
it export, and whether its effect is baked into the assembled geometry when its
channel is omitted. The blink tests whether the graft baseline defect persists
with identity morphs baked. If the native channel remains missing but the two
baked bases differ correctly, the result will support investigating an export
assembly workaround rather than repeating broad morph exports.

A graft-off closed-body comparison follows when needed to isolate restoration
and the baseline component. No artistic choice is required before this probe;
visual graft acceptance follows once one control can be exercised reliably.

## Evidence and scope

Full licensed-data reports stay local under `TestOutput/appearance-evidence/`:
`laranude.raw-fbx.json`, `laranude.unity-fbx.json`, `laranude.graft-audit.json`,
and `unity-nude-graft.log`. Imports ran in the existing ignored diagnostic project
`.dazposewizard/p0c-native-generation`. The new texture sidecar is excluded from Git.

The latest saved first-outfit DUF hash is
`c37fec189a828ef1803e4a0fd0c325f6d4edc8c5ebcc2d361f08f66e5b16e31e`.
That saved snapshot still lists hidden Starlette; the DAZ log records its removal
after that save. The inspected FBX excludes it, as requested. The DUF snapshot
does not establish the current unsaved live scene state.

DAZ's [official FBX scripting sample](https://docs.daz3d.com/_export/raw/public/software/dazstudio/4/referenceguide/scripting/api_reference/samples/file_io/export_fbx_silent/start)
documents substring rule matching and Export/Bake actions. The targeted rule list
uses those semantics. Conclusions about this export come from its measured data.
