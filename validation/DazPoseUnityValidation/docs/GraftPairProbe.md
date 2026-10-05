# Captured graft opening: export pair and Unity preview

Measured 2026-10-04. This continues the anatomy investigation before clothing,
hair rebinding or runtime architecture work. The development preview uses cloned
body geometry and the source donor hierarchy; it does not replace the performer.

User checkpoint: the opening and seams were accepted. Shading was rejected.
Material reconstruction and dissolve compatibility are the next slice; this
acceptance does not establish posed skeleton fidelity or anatomy suppression.

## What the pair establishes

`Assets/TestCharacter/l.aranudeclosed.fbx` (118,352,416 bytes) and
`Assets/TestCharacter/l.aranudeopen.fbx` (118,352,176 bytes) contain identical raw
topology, bone hierarchy and skin-cluster evidence. Both use the targeted saved
rules: Vagina.Open1 Export, eCTRLEyesClosedL Export, Anything Bake.

Neither contains a Vagina.Open1 channel. Each contains only one left blink
channel on each of the three meshes. Explicitly exercising the graft control
therefore did not make its native channel survive this direct FBX attempt.

The opening is preserved in the base geometry:

| Mesh | Changed raw points | Maximum movement |
| --- | ---: | ---: |
| Combined body/graft | 270, all graft-only | 16.8192 mm |
| Panties | 206 | 6.83523 mm |
| Emiko hair | 0 | 0 |

No host-body or seam-shared point changes in the opening comparison. The body
has its original maximum height coordinate of 186.86508 cm again. The targeted
rules preserve the chosen baked identity rather than exporting all Lara controls.

All 2,303 native graft points map consistently to the merged body through the
ordered 2,218 graft polygons. The 191 native Open1 delta points are all among
the 270 changed points, but the captured delta is not numerically identical to
the native DSF shape: there are 79 additional points and a maximum vector
difference of 5.02645 mm. This proof uses the actual captured endpoints rather
than assuming that source deltas reproduce the evaluated fitted DAZ geometry.
The control is named CapturedOpening pending confirmation of the exact slider
changes. The pair alone cannot establish an intermediate DAZ control curve.

## Blink contamination remains

The closed export's blink moves 1,626 raw graft-only points by up to 4.31209 mm,
in addition to its real eye movement. The open export is contaminated too;
the two graft target shapes are not identical. This does not justify asserting
that the exporter always resets the graft to one particular neutral shape.

The preview preserves the original exported blink and supplies a candidate
corrected blink that removes only graft-only vertex, normal and tangent deltas.
Host and face movement remain unchanged. This is a diagnostic correction for
this blink, not a blanket filter for valid anatomy/body controls.

## Recovered opening proof

Unity splits the closed body into 21,133 vertices and the open body into 21,169.
Subtracting the imported arrays by index would be invalid. The preview transfers
the raw matched-point opening and normal deltas to the closed Unity vertex
layout, matching position and material membership. Ambiguous transfers fail.

Validation passed for all 21,133 imported closed vertices:

- Maximum raw/imported point match error: `1.335e-7` m.
- Recovered endpoint versus independently imported open positions: `6.008e-8` m.
- Opening changes 348 imported vertices and leaves host geometry unchanged.
- Candidate corrected blink removes motion from 1,825 imported graft vertices.
- Stored opening and corrected blink frames pass readback checks; corrected
  graft blink deltas are zero and host-face blink deltas are preserved.

Normal deltas come from the captured source FBX normals. Tangent changes for
the opening are not reconstructed. Source material conversion, interpolation,
shading, seam appearance and pose behavior still require visual assessment.
The proof uses the current diagnostic Standard/four-influence imports and a
static donor rig; it does not establish runtime canonical-skeleton pose fidelity.

## Human visual checkpoint

In the validation editor, choose:
**Tools → DAZ Pose → Development → Open Graft Pair Preview**.

Unity first offers to save any currently modified scene. The command then builds
and opens the separate generated `Assets/TestData/GraftPairProbe/GraftPairPreview.unity`
scene, hides clothing/hair, focuses the pelvis in Scene view and opens
the **DAZ Graft Probe** panel. It does not need Play mode.

1. Move **Opening** through 0, 50 and 100. Inspect the opening, seam and shading.
2. Set **Left blink** to 100 while changing Opening. The default corrected blink
   should leave the graft alone.
3. Toggle **Use exported blink** to compare the uncorrected source behavior.

The user's acceptance of the geometry/appearance is still required. No further
DAZ export is needed for this particular captured-opening visual test. A later
graft-off body reference is still needed for closed-body anatomy suppression;
closed vagina here means the graft remains fitted, not anatomy removed.

## Files and scope

The editor utility is `Assets/DazPose/Editor/DazGraftPairProbe.cs`.
The local endpoint manifest and generated assets live under the ignored
`Assets/TestData/GraftPairProbe/` directory. Licensed FBXs and texture sidecars
remain excluded from Git. Evidence under `TestOutput/appearance-evidence/` includes
the raw/imported reports, `graft-pair.audit.json`,
`graft-recovery.validation.json` and Unity logs.

Source SHA256:

- Closed: `c0a836ba17c50060d1b2b82a30e535d2da0dfb490bd0bd0eb459b8d5169c1aa6`
- Open: `bef1a84980dc49cd08dd5abd41580d43f2f7284be42983327d3e5af20e26038c`

No live scene was switched during automated validation. The existing main editor
was confirmed visible/responding in the signed-in user's session, with unsaved
FirstPerformanceVoid edits. The preview remains for the user to open through the
menu and evaluate. No rebinder, wardrobe system, anatomy toggle or milestone
commit was added.
