# N05 — seating source delivery, awaiting G3

## Runtime oracles

The adapter directly calls existing internal PerformerSeat.TryCaptureFrames using the existing DazPose.Editor friend assembly. It validates the actual scene chair/profile, default style, separate anchors and unique real `hip` transform. It does not change visibility declarations, bake a clip, retune a profile or introduce a second interpreter.

| Source endpoint | Runtime methods | Native settled state |
|---|---|---|
| Warmup `Lara.SitAt(Lounger)` | PerformerSeating.BeginSitStart, AdvanceRootMotion, StartLoop; PerformerSeatingLayer.BeginMotion | Keep the SitStart landing root: SeatAnchor minus captured seat yaw times scaled SitStart.PelvisOffsetAt(1). For current CrossLegs default, sample the actual rebased CrossLegsLoop at time zero with final blends settled. Basic style resolver uses SitStart's terminal frame instead. |
| Warmup `Lara.PersistentSitPose(Lounging)` | PerformerSeating.SetPersistentPose, AnchorPersistentPose; PerformerSeatingLayer.SetPersistentPose | Validate Sitting metadata and real bound pose/clip. Sample at zero, set seat yaw, read actual world hip and translate root by SeatAnchor minus sampled hip. |
| Main `Lara.DissolveTo(Lounger, seated: Lounging)` | SuccubusPerformer.RelocatePresentationAsync; PerformerSeating.ArriveHiddenAt, AnchorPersistentPose | Same final custom-pose hip correction, following its own hidden-arrival/preplacement rule. Distinct source endpoint, not an assertion that the preceding Director loop was replayed. |
| Standing source endpoint | RelocatePresentationAsync standing branch | Existing initialPose reset and location yaw; G3 exposes MainPosition1 as a separate fixed endpoint, without claiming chronological transport. |

The CrossLegs loop was already reordered to its selected source entry by PerformerSeatingBaker.FrameIndices. Do not apply SourceEntryPhase a second time. CrossLegsBodyRootOffset was already baked into body-root curves; do not add it again.

### Two contact measurements

The existing unit-scale profile has a **0.0306574 m hip-to-seat gap** at CrossLegsLoop frame zero compared with SitStart's landing contact. That is existing baked geometry, not an invitation to force the default state through the persistent-pose solver. N05_SOURCE_ORACLE.json records the actual curve values/hashes and confirms all **95 clip transform paths** in each baked motion exist in the saved Lara prefab.

- Default: compare actual sampled hip with root.TransformPoint(the selected baked motion's pelvis offset at its sampled phase), with **0.01 m residual tolerance**. Display the actual hip-to-SeatAnchor gap separately. The author must still approve real furniture contact and facing.
- Persistent Lounging and seated dissolve: compare actual sampled hip directly to SeatAnchor, with **0.001 m tolerance**. The world-space correction remains valid for rotated/scaled actor parents; no inverse baked-pelvis shortcut is used.

A bad residual throws a source-aware error and tears down the preview; an old or substitute shot is not silently retained. Approach/seat yaw disagreement is displayed, with the seat's own warning threshold. ApproachAnchor affects approach diagnostics and readiness, not settled root position. Zero/nonfinite actor or seat world scale is rejected.

## Preview ownership

NativeSceneStage gains a seated projection overload and shares its existing visibility/material/camera presentation helper with standing. NativePreviewLease and the retained NativePoseSampler now accept sample seconds; existing G2 calls still default to zero. Every projection restores baseline before sampling; root curves are isolated, live hip correction happens after sampling, and tracking reads post-sample laraFocus. No authored anchor is written. Real player root/ViewRig and original camera projection/HDRP settings remain the camera authority.

The same editor-writer guard freezes competing wardrobe/heel writers, preserving their original serialized controls and cache ownership. No runtime session, async command or lifecycle reset is invoked. The body remains explicitly hidden as FirstContact setup requires, with its geometry/transforms untouched. All root/bone/renderer/body/camera values are restored through the existing ledger before serialization and on Stop/reload/lifecycle cleanup.

G3 has an explicit **temporary nonuniform actor-scale probe** (1.10 / 0.95 / 1.20), owned by that same localScale ledger. It starts from baseline each time, is restored before Save/Stop and never writes prefab assets. G3's saved snapshots must cover this extra property family. Native tests can also rotate/scale the real chair instance; those are deliberate author edits and remain intact. Parent/actor-space math is tested separately with rotated nonuniform matrices. Native appearance and exact Unity callback behavior are not established by those managed checks.

Missing clip rig paths are now diagnosed explicitly rather than comparing a null target with an unassigned optional location focus. Clip curves that actually target authored anchors remain refused. No source rig path or asset was changed to satisfy the check.

## G3 diagnostic

Menu: **DazPose > Story > Native Directing Diagnostics > G3 Seating Proof**. Same physical isolated project and disposable G1_FirstContact scene used for G1/G2; no new scene/assets required from Art.

The panel resolves Game Session and the actual compiled FirstContact bindings automatically. NativeSeatingSourceOracle checks/cites Warmup SitAt → PersistentSitPose and Main's seated dissolve after its Standing Director draw. This validates fixed diagnostic locations in already compiled source; full StoryMachine replay/visit parity remains N06/G4.

A/B/C buttons select the real endpoints. Native SeatAnchor, ApproachAnchor, chair, seat focus and camera mark selection buttons avoid hierarchy guessing. Tracking is clearly labelled as overriding camera-mark rotation. Its default is on for the seating proof, matching the story's accumulated Track(Lara). Seat focus is selectable, but that source cue still tracks Lara. Marks and chair/parent matrices are watched at a 25 Hz target cadence; no Director draws occur. An approach-only move automatically verifies that settled root/facing stayed fixed.

Normal Save and user-invoked forced SaveScene use the G2-proven suspension/resume path. G3 records actor/bone/renderer/body/camera/actor-prefab snapshots while suspended; legitimate chair prefab overrides are allowed. Clean scene data comparison permits only empty-string/line-ending normalization. A report PASS requires A/B/C observations and saved baseline evidence for each family, a saved actor-scale probe, approach independence, live/rotated-scaled-chair and Save/Undo/Stop/reopen author confirmations, clean dirty/Undo behavior and retention of unsaved author dirty state on Stop. Full differences and scene text are exported under fixture Temp/NativeDirectingDiagnostics/G3_timestamp.

Art explicitly declined screenshots earlier; G3 keeps that preference. A verbal result is accepted as author evidence; no fabricated screenshot or native run is claimed. N05 is source-ready only until G3 PASS. N06 has not begun.

## Evidence

N05_SOURCE_ORACLE.json: protected profile/motion/source hashes, measured contact offsets, rig-path audit and pre-sync Art-edited fixture scene hash.
N05_SOURCE_COMPILE.json: seven standalone Roslyn assembly compiles, no Unity launch.
N05_MANAGED_RESULTS.json and N05_*Tests.log: full managed regression outputs, including actual production math/oracle helpers. Test-only Unity math shims do not execute native sampling.
N05_ASSET_AUDIT.json: static meta/reference audit summary/hash; full report is ignored, historical p2-p3 audit preserved.
PROTECTED_CHECK.json: all original protected files checked independently.
G3_CODE_SYNC.json: exact code/meta fingerprint; fixture marker records synchronized local code revision. Scene/content/index data is never synchronized back or overwritten.

Final N05 source evidence: **244 managed tests PASS** (90 Story, 24 Player, 61 Rehearsal including 21 new seating/scale/oracle checks, 69 Application), zero failures/skips. All seven standalone source assembly compiles PASS. Static asset audit: zero issues, duplicate GUIDs or missing metas. All 7,211 protected original files unchanged. Native sampling, visual contact and Unity save/Undo acceptance are pending Art G3.
