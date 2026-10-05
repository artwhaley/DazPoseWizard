# E4: compile every reachable layer state

Read the completed E2/E3 reports before changing fitting. Add shared editor
`WardrobeFitCompiler.cs`; it produces at most four distinct visible-mask states.
An asset is releasable only when all its reachable states are validated.

## Coverage

Refactor existing coverage generation into per-piece canonical-body masks, then
union masks of visible pieces into each compiled body state. The existing UV3
coverage channels do not represent three clothing layers: do not treat them as
layer indices. Preserve all unrelated mesh channels and blendshape frames.
Hidden clothing must not leave a body hole; visible clothing retains the accepted
coverage expansion. Compare naked body coverage against the canonical baseline.
Material transparency alone does not decide anatomical coverage.

## Footwear and retained garments

Footwear/contact support is active only while its assigned shoe layer is visible.
Bent-foot pose remains active while any visible piece declares
`requiresBentFootPose` (including hosiery). When its outfit has a footwear profile,
that profile's foot shrink and standing lift remain active too. Removing shoes while those
pieces remain is a supported layer state: keep the foot deformation, turn off
shoe-floor contact, and accept that the result may look awkward or float. Do not
reject import/setup or mark this state Needs Fit merely because stockings remain
without shoes. Barefoot restoration happens when no visible piece requires the
bent-foot pose. Content authors are responsible for removing hosiery and shoes
together when that is the intended look. A future import may carry paired stocking
variants; that is outside this implementation.

Shoe calibration is optional for compilation and setup. If a pose-dependent piece
has no footwear profile, retain the baked bent-foot body deformation, use zero
shoe lift/shrink, and disable shoe-floor contact. This can look awkward by design;
the lack of a shoe profile is not a fit/import blocker.

Compile toe corrections, foot shrink, instance pelvis/visual lift and walk policy
for each visible state. Reuse accepted contact measurements and heel policy; do not
rescale shoes or introduce a second height owner. Retain current WalkTo requests
when changing footwear. Blend standing lift with
`SuccubusPerformer.SeatingOwnershipWeight` as the existing reviewed behavior does;
do not claim general sitting IK from that behavior.

`WardrobeGeometry.TransferBodyShapes` only transfers torso-region body shapes.
Do not require a barefoot stocking conversion for the retained-hosiery state.
Any optional future fit transfer must use validated canonical/source mapping,
preserve garment offsets/material layout, recalculate normals and rebake particle
bindings. This optional future work must not gate today's shoe/hosiery pairing.

## Fixtures and gate

Keep shipped defaults Base. Create temporary technical fixtures with:

1. Shoes in Base: footwear persists until nude.
2. Maid stockings in Base, dress/apron in 1, paired shoes in 2: removing shoes
   leaves hosiery on the bent-foot pose, with shoe-floor contact disabled. Removing
   hosiery then restores the bare foot.
3. Sparse layer masks and dependencies, including an empty top layer.

These are test assignments, not approved artistic configuration. Exercise every
reachable state through live Remove/Add and direct concealed initialization.
Validate morph preservation, body coverage, feet/stocking shape, rigid shoe
geometry, walking support, culling and effect mesh identities. Use the thresholds
and evidence requirements in VALIDATION. An unsupported retained-garment fixture
is an incomplete E4 gate; independent later UI work may proceed, but release and
the overall assignment cannot be declared complete.
