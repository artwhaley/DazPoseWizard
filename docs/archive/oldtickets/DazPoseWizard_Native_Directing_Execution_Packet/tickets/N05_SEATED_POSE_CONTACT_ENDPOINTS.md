# N05 — Real seated/contact end frames and authorable seat anchors

**Depends on:** N04. **Deliverable:** validated seat endpoint resolver and correct Lounger Game-view composition.

## Execute

1. Read actual `PerformerSeat.TryCaptureFrames` (Editor assembly already friend via `InternalsVisibleTo("DazPose.Editor")`), `PerformerSeating.AnchorPersistentPose()`, `PerformerSeatingLayer`, baked motions/profiles and real hip Transform. **Do not refactor internal visibility unnecessarily.** Separate two actual runtime stable endpoint families; no generic baked-pelvis assumption.
2. `Lara.SitAt(location)` ends in the default seat style (current Lounger CrossLegs), whose **baked transition/rest** must be reproduced and Art-verified. `PelvisOffsetAt(1)` may preplace root/drive sit-down but **is not** the universal pose-specific final alignment. For `Lara.PersistentSitPose(pose)` and `Lara.DissolveTo(location, seated: pose)`: sample bound pose onto real skeleton, set root yaw to captured seat yaw, evaluate live hip world position, then apply `root.position += SeatAnchor.position - hip.position` (consistent transform-space/scaled-rig math) and verify contact within tolerance. Repeat sample/re-solve from baseline to avoid drift. Preserve runtime initial sit entry semantics separately.
3. Separate `ApproachAnchor` (approach/motion preparation, initial alignment diagnostics) from `SeatAnchor` (settled seated contact). Don't move settled pelvis in response to *only* an approach anchor shift. Verify profile readiness, facing mismatch, pelvis error; display exact errors and stage nothing misleading if unavailable.
4. Native Scene/Hierarchy selection of SeatAnchor/ApproachAnchor and seat focus. Moving SeatAnchor live re-solves hip and camera; **author-created overrides** on Validated Lounge Chair prefab instance are permitted/preserved, **stage-created overrides** prohibited. Never rewrite baked data. Camera `laraFocus` updates after pose sampling. Test scaled actor/parent, rotated chair, unchanged repeated restage and tracking.
5. Standing return is cue-dependent and uses **standing DissolveTo's initialPose reset**. FirstContact **Intro** `DissolveTo(Lounger)` is standing; first seated cues in **Warmup** are `SitAt(Lounger)` → `PersistentSitPose(Lounging)`, whereas **Main** later has `DissolveTo(Lounger, seated: Lounging)` after Director. Native screenshots by **Art** for all three states are necessary. **G3 uses fixed source-bound diagnostic projections** for these endpoint families before N06 transport exists; source/visit locations are verified, full replay follows N06/G4.

## Gate

- Art verifies **both** first Warmup default CrossLegs and later persistent Lounging, plus separate Main seated dissolve: real pelvis contact/facing in standard Scene/Game. Drag SeatAnchor and see live corrected pose/framing.
- Moving ApproachAnchor doesn't erroneously translate the stable seated pelvis; seated profile offsets match accepted runtime geometry within declared tolerance.
- Missing/wrong `PerformerSeat`, posture metadata or baked data rejects with actionable source diagnosis; no floating avatar pass.
- Save/Undo the seat mark and close Studio: legitimate new chair prefab override persists/undoes, preview pose/camera/material disappear, dirty author state preserved.
- **HARD PAUSE G3:** deliver `G3 Seating Proof` and exactly documented steps; **wait for Art PASS before N06**. Any hip/furniture misalignment or save leak = FAIL; executor does not launch Unity to investigate.
