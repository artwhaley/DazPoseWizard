# Artist-authored flaccid / erect endpoint comparison

Investigation only. Source DUF/FBX/image assets were read without modification. No Unity runtime assets, scenes, solver or execution packet were created. The artist-authored endpoints remain authoritative.

**Decision: Outcome C / Plan C.** These files establish exact vertex correspondence, but do not establish a clean bone-only runtime interpolation or bone plus a small localized corrective. One body mesh can mathematically store the endpoint vertex difference, and one shell mesh can do the same; the quality of intermediate appearances and contact deformation remains unproven. Recover a common bind pose with meaningful posed bone orientation data, or deliberately evaluate a broad endpoint shape representation, before choosing the runtime implementation.

## E1 — Sources and fingerprints

- `C:\Users\artwh\OneDrive\Documents\DAZ 3D\Studio\My Library\Scenes\playerflacid.duf`
  - SHA-256 `a76a8765bd14f16121805c04e0459398cc380b10ddafd30b790e55ab6c93b9ba`; 273,299 bytes; modified UTC 2026-10-05T00:04:19.061975+00:00.
- `C:\Users\artwh\OneDrive\Documents\DAZ 3D\Studio\My Library\Scenes\playererect.duf`
  - SHA-256 `21cf79e53dbad71d3252edf9f99772eadac020cd69c02cf9b2651510b2a394b4`; 273,090 bytes; modified UTC 2026-10-05T00:02:19.924972+00:00.
- `C:\Users\artwh\OneDrive\Documents\DazPoseWizard\validation\DazPoseUnityValidation\Assets\TestCharacter\playerflacid.fbx`
  - SHA-256 `7a8e9188ca04e205870f0ef7619cc0f10ab59af81dab2cff4ff3590ed6a0cda4`; 168,773,792 bytes; modified UTC 2026-10-05T00:06:13.549228+00:00.
- `C:\Users\artwh\OneDrive\Documents\DazPoseWizard\validation\DazPoseUnityValidation\Assets\TestCharacter\playererect.fbx`
  - SHA-256 `ba2bcca1dadb350a59e86e118a1b9b0195fef6033c00c472cc1523bf9c3481f1`; 168,773,744 bytes; modified UTC 2026-10-05T00:06:49.193070+00:00.

Both corresponding image directories contain nine images. Complete image paths, sizes and SHA-256 fingerprints are in `TestOutput/player-endpoints/sources.json`. Embedded texture contents match exactly; endpoint directory names differ.

## E2 — Structural comparability

| Mesh | Control points | Polygons | Slots | Skin clusters |
|---|---:|---:|---:|---:|
| Genesis8Male | 18,484 | 18,287 | 23 | 185 |
| Genesis8MaleEyelashes | 464 | 352 | 2 | 25 |
| Dicktator Shell | 2,205 | 2,177 | 23 | 0 |

All three pairs have exactly equal polygon index streams, material slot order, per-polygon material indices and UV arrays/indices. Skeleton paths/types, FBX version (7400), axis and unit settings match. Vertex correspondence is proven, so the topology stop condition did not trigger. Body bind/link matrices differ between endpoint exports; eyelashes bind matrices are equal. Raw evidence retains the full matrices.

## E3 — Focused DUF state comparison

| Saved anatomy control | Flaccid | Erect |
|---|---:|---:|
| Flacid | 0.4814815 | absent |
| Flacid Preset 01 | 0.2777778 | absent |
| Flacid Preset 02 | 0.1481481 | absent |
| Scale All Segments | -0.0138889 | absent |
| Erection Preset 01 | absent | 1 |

“Absent” means no saved scene modifier override; it is not an observed exported morph channel. Direct CockLength (0.03703701), ShaftShorten (0.1166667), ShaftInflate (0.4074074), ShaftInflate1 (0.25), ScaleDown (0.6), and other saved anatomy settings match. Preset dependencies can still alter geometry/dimensions. The non-preview DUF diff contains only these five anatomy overrides; animation key data does not differ. Preview positions also differ. Body controller values match: FBX ControllerScales metadata merely reorders the same key/value entries. No unrelated body bone matrix change was found.

## E4 — Bone transforms and bind interpretation

Full anatomy local/global matrices, effective local translation deltas, raw T/R/S, RotationOrder, PreRotation/PostRotation and properties are in `comparison.json` → `boneDifferences`; `bindPoses` retains pose matrices. Serialized FBX matrices are transposed into column-vector convention. Effective rotation uses PreRotation × ordered local Euler rotation × inverse PostRotation; pivots and scales are checked by the script.

Every endpoint local Lcl Rotation is zero and scale is (1,1,1); pre/post rotations cancel in the evaluated transform. All effective relative rotations are identity (largest shaft difference **0°**), and scale ratios are (1,1,1). Quaternion difference is therefore identity (0,0,0,1), independent of quaternion sign convention. Appearance is baked into vertices and joint positions: zero exported rotation differences do not imply zero DAZ posing differences.

| Bone | Erect minus flaccid effective local translation, cm |
|---|---|
| shaft1 | 0.00085145, 0.00565338, -0.06079006 |
| shaft2 | -0.00314466, 2.35276031, 1.92156315 |
| shaft3 | -0.01943121, 1.80973053, 1.40766907 |
| shaft4 | -0.04191628, 2.11508942, 1.60762024 |
| shaft5 | -0.07345523, 2.47601318, 1.89175797 |
| shaft6 | -0.11718738, 3.16476440, 2.07872963 |
| shaft7 | -0.15528610, 3.71556091, 2.05484581 |
| lTesticle | 0.02704477, -0.05920410, 0.53767586 |
| rTesticle | -0.03854668, -0.02632904, 0.46767330 |

shaftRoot, scrotum, donor hip/pelvis, rectum chain and other anatomy transforms remain equal. Testicle translations differ as listed; geometry in their region also changes. Global evaluation independently agrees with cluster TransformLink to within 4.77e-7 matrix units; each cluster Transform × TransformLink is identity. Thus each DAZ cluster Transform here is the inverse link transform, rather than a generic shared mesh transform.

## E5 — What Erect and Flacid exported as

Both FBXs have **zero Shape/BlendShapeChannel geometry channels**. Neither Erect nor Flacid is an exported morph, despite the supplied Export rules. The static endpoint geometry and bind/joint positions carry their evaluated appearance. There is no exported animation curve carrying the endpoint motion. Export rule labels alone do not create Unity deformation data.

## E6–E8 — Vertex change and real bone-only reconstruction

Control-point measurements are in the common FBX centimeter coordinate space; errors below are converted to millimeters. Changed means displacement >0.0001 mm. Region membership includes shared boundary control points, so region counts do not sum to a disjoint partition.

| Mesh | Changed points | Maximum delta mm | RMS over all points mm |
|---|---:|---:|---:|
| Genesis8Male | 1,670 | 227.373 | 42.800 |
| Genesis8MaleEyelashes | 0 | 0.000 | 0.000 |
| Dicktator Shell | 1,670 | 227.719 | 123.998 |

Experiment: use flaccid exported mesh as source; for each influence apply `G_erect × inverse(G_flaccid) × p_flaccid`, weight by the file’s raw cluster weights, then normalize the full influence sum per vertex. Compare against the actual erect control point at the same proven index. All moving bones are included. Reverse direction and same-state reconstructions are also executed. This is a measured reconstruction, not a comparison of transform labels.

| Region | Changed endpoint points | Maximum endpoint delta mm | Maximum reconstruction residual mm | Residual RMS mm |
|---|---:|---:|---:|---:|
| Torso | 25 | 0.769 | 0.769 | 0.030 |
| Glans | 428 | 227.373 | 189.249 | 173.731 |
| Shaft | 766 | 218.133 | 180.060 | 61.671 |
| Testicles | 369 | 30.124 | 30.380 | 11.539 |
| Torso_Front | 68 | 25.081 | 24.671 | 14.689 |
| Torso_Middle | 129 | 17.108 | 16.935 | 6.091 |
| Torso_Back | 26 | 0.102 | 0.102 | 0.019 |
| Rectum | 0 | 0.000 | 0.000 | 0.000 |

Full normalized reconstruction: maximum **189.249 mm**, RMS over all 18,484 points **28.532 mm**, mean over the 1,670 changed points **64.710 mm**. Reverse reconstruction produces the same error. Same-state maximum error is 8.53e-13 mm; this validates numerical reconstruction consistency, while the independent link-matrix check validates the transform evaluator.

| Residual threshold mm | Points exceeding threshold |
|---:|---:|
| 0.1 | 1639 |
| 0.5 | 1618 |
| 1 | 1610 |
| 2 | 1595 |

Normalizing only the largest four influences worsens maximum error to 227.373 mm and RMS to 34.474 mm. Residual is broad over shaft/glans and includes testicles and torso-front/middle; it is not a tiny root-only correction. Host Torso has 25 changed points, maximum 0.769 mm; this material region shares the graft boundary. Other host material regions and eyelashes are unchanged. This result rejects Outcome A and the “small localized corrective” interpretation of Outcome B for these measured exported transforms.

## E9 — Existing upward shaft corrective

Native `pJCM_Shaft1_up.dsf` drives its clamped [0,1.7] value from Shaft 1 rotation/x × -0.04. It contains 27 delta points, maximum unit-value delta 3.179 mm (5.405 mm at channel maximum, before downstream deformation). The measured residual extends across hundreds of glans/shaft points with maxima near 189 mm. This native corrective cannot explain the broad missing deformation by itself.

FBX effective rotations are baked/cancelled and cannot be used as the DAZ formula’s source angle. Native polygon stream differs from the shell stream, so a native-to-FBX index map has **not** been established; no unverified delta subtraction or claim of exact JCM equivalence is made. A new broad corrective or recovered pose data is needed; this evidence does not yet define a production corrective.

## E10–E11 — Centerline and dimension coupling

Samples use actual shaft1 through shaft7 global centers plus an evaluated glans support-plane tip along the distal shaft6→shaft7 direction. FBX bone attributes contain display Size only, not an authored tip endpoint. Thus the final point is an explicit reproducible geometric proxy. Full points, base/tip, segment directions, turns and overall direction are in `comparison.json`.

| t | Centerline length cm | Minimum segment cm | Maximum turn ° |
|---:|---:|---:|---:|
| 0 | 13.35451 | 1.43627 | 15.63428 |
| 0.25 | 10.96422 | 1.16089 | 8.53355 |
| 0.5 | 11.36128 | 1.25593 | 4.24376 |
| 0.75 | 14.49000 | 1.68713 | 5.91357 |
| 1 | 18.93206 | 2.23268 | 8.20320 |

Interpolated local translations give these centerline centers because measured local rotations are identity. All segments remain nonzero continuously: analytic minimum over t∈[0,1] is 1.13907 cm. There are no negative scales or sampled direction flips, but the centerline substantially shortens between endpoints. This is continuous position interpolation, not proof of acceptable surface interpolation or preserved arc length.

Endpoint centerline length grows **41.77%**. Mid-shaft nearest-centerline radius proxy grows from 2.00946 to 2.31221 cm (**15.07%**), diameter proxy 4.01893→4.62441 cm. Samples use arc 20–80%, 502 versus 440 control points, so this is an approximate surface measure with different sample selection, not an exact circular girth/volume claim. Cylinder volume proxy changes 169.41→317.98 cm³ and must not be treated as anatomical volume.

**Artist decision required before a runtime contract:** accept endpoint-dependent length/girth as part of erection, or preserve user-controlled dimensions and obtain approved size-normalized endpoints. Do not automatically rescale these accepted appearances. Equal direct length/girth sliders do not imply equal evaluated dimensions.

## E12 — Shell

Shell endpoint topology, UVs and material slots match exactly; 1,670 of 2,205 shell points change. It is static and unskinned in both exports. A paired shell vertex shape/interpolation (shell option A) is structurally possible. Its intermediate shape, body clearance and shading are unproven. Shell option B, transferring body/graft skinning, requires an explicitly proven shell→body vertex/surface mapping and offset policy; equal counts with native geometry do not establish that mapping. No invisible double shell should remain.

## E13 — Weights

Every cluster path, index, raw weight and mode matches exactly across the pair. Nevertheless 615 body control points sum above 1.001, maximum total 5. Anatomy vertices have substantial main-body thigh Bend/Twist influences: left/right Bend affect 664/663 points and left/right Twist 665/664. This is consistent source data, not evidence of sound runtime normalization or influence truncation. Full normalized and top-four experiments expose the practical difference.

## E14 — Donor pelvis and follow relationships

Main and donor pelvis global matrices are unchanged across endpoints. Donor relative translation is (0, 0.00530243, -0.00229537) cm with identity rotation, about 0.0578 mm offset. Static alignment passes.

Both FBXs contain matching parent-follow constraints, including donor pelvis following main pelvis at 100% weight with zero offsets; translation affect flags are disabled. This corrects the earlier characterization’s incomplete “no constraints” interpretation. Static exported constraints do not prove that Unity imports or executes them. A later moving-pelvis test must verify donor following and avoid double application through constraints plus hierarchy.

## E15 — Material/UV sanity

FBX material properties, DUF material data and embedded image contents match. Mesh UV arrays/indices, slot order and polygon material assignment match exactly. Appearance differences therefore do not originate in changed material/UV authoring. This is source comparison only; Unity shader correctness, shell cutout and seams were not visually validated.

## E16 — Runtime alternatives

- Plan A: measured exported bones only. **Rejected** by the 189.249 mm residual.
- Plan B: these bones plus one small localized corrective. **Rejected** by broad residual support. A broad endpoint delta could reproduce the endpoint, but is a different hypothesis requiring interpolation/contact validation.
- Plan C: **recommended; runtime implementation blocked pending representation proof.** Preserve both files. Recover a common bind mesh and actual oriented posed bone transforms, rerun reconstruction, then assess a localized residual. Alternatively explicitly choose broad paired endpoint shape data for body and shell and test all intermediate states. Neither path requires preserving vendor controller semantics.

Exact data needed: proven common body vertex topology/UV/material identity (available), common bind pose and weights with chosen influence policy (raw weights available; policy unresolved), effective oriented pose transforms or deliberate broad endpoint deltas (missing choice/proof), evaluated tip definition (geometric proxy available; authored tip missing), shell deformation/mapping policy (paired points available), donor follow ownership, and an approved erection-versus-size contract.

## E17 — Future evaluation order

Start from the approved common bind representation; evaluate erection pose/shape, then independent length/girth controls according to the approved dimension contract, then pose/size correctives, then contact deformation. Derive the centerline and evaluated tip from the final deformation used for contact, and update the shell with the same final state/offset policy. Contact iteration may feed back into centerline evaluation. Keep one owner for pelvis following and one owner for shell deformation. This is a proposed order, not an implemented contract.

## E18–E19 — Smallest future Unity proof and visual acceptance

After the missing representation decision, use one body SkinnedMeshRenderer, one shell renderer, the minimal required anatomy chain plus pelvis attachment, and an erection t slider with fixed approved length/girth. Retain endpoint meshes as comparison references only. Measure endpoint error against these files, and inspect t=0/.25/.5/.75/1 from front/side/top and silhouette views.

Acceptance checks: endpoint appearance preserved; intermediate shaft arc and tip trajectory coherent without shortening artifacts; root fold and glans stable; scrotum/testicles do not collapse; shell maintains clearance without seams/double surfaces; neutral and moving pelvis follow correctly; thigh poses do not pull anatomy unexpectedly; independent length/girth remain predictable; contact bending preserves continuity. Numeric endpoint proof and user visual acceptance must be recorded separately. No Unity proof or user visual approval of intermediates occurred in this pass.

## Reproduction and evidence

Run `scripts/compare-player-endpoints.py` using Blender 4.5 bundled Python/NumPy from the repository root. It reuses standalone `inspect-fbx.py` and `inspect-daz-materials.py`, reads source assets, fingerprints them, stops on topology mismatch, and writes only `TestOutput/player-endpoints`. Evidence includes `sources.json`, `comparison.json`, the two `*-fbx.json` inventories, and two `*-materials.json` resolved inventories. Full bone values and matrices are deliberately retained in JSON rather than rounded in this report.

Limitations: this evaluates exported control points and FBX bind transforms, not a Unity importer, DAZ live scene evaluation, subdivision, rendered silhouette, or interactive contact. The large residual is decisive for the measured exported-bone hypothesis; it does not invalidate the artist-approved endpoints.
