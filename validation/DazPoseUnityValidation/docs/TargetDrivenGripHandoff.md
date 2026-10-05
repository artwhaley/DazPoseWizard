# Target-driven reach / walk / IK / grip — executor handoff

## Assignment

Finish the user's original TARGET-DRIVEN REACH / WALK / IK / GRIP ticket stack, not merely a foundation. The runtime pipeline is now implemented and automated end-to-end tests pass on actual Lara. **Do not mark the full stack accepted yet:** visual finger/skin acceptance, diagnostic completeness, robustness review, and bounded commits remain.

The original packet in the user conversation is authoritative for scope; this file explains actual current implementation and remaining gaps. Keep one writer/integrator. No subagents or model-selection tools were available to this executor.

## Safety / repository baseline

- Workspace: `C:/Users/artwh/OneDrive/Documents/DazPoseWizard`.
- Unity project: `validation/DazPoseUnityValidation`.
- Baseline and current HEAD: `ae7a6d7a8d6972994c1daa7f6b6c642964dd825d`.
- Branch main, ahead of origin/main by two commits. Prior HandGrip foundation: `72c2c8a`.
- **No commits, staging, push, reset, restore, clean, stash, or remote reconciliation performed.** All this executor's work is local/uncommitted.
- Git requires a command-local exception: `git -c safe.directory=C:/Users/artwh/OneDrive/Documents/DazPoseWizard ...`. Do not change global Git config.
- Many changes were already present: wardrobe, anatomy, dissolve, magic, scene modifications, scripts. Local work wins. Never broadly stage.
- Particularly shared/pre-modified files: SuccubusPerformer.cs; HandGripAcceptanceSetup.cs; HandGripAcceptanceHarness.cs; HandGripAcceptance.unity; HandGripImplementationNotes.md. Commit only your owned hunks. SuccubusPerformer has extensive existing wardrobe changes and mixed line endings; do not stage it wholesale.
- This executor did not edit partner/G8M, wardrobe/import implementations/assets, or FirstPerformanceVoid.unity. It did make narrow grip API/graph additions in the already wardrobe-modified SuccubusPerformer.
- Original interactive Unity remains running on the main project (PID 41136 at handoff; inspect anew). **The user explicitly chose a NEW ISOLATED test project, not taking over the open editor.** Do not open scenes/enter Play Mode in their main editor.
- Another agent used `.dazposewizard/p0c-native-generation`; do not touch that project. Our isolated project is `.dazposewizard/handgrip-validation/project`.
- No isolated Unity run remains active at handoff. A local read-only Python HTTP server was started on port 8879, background PID 63576; inspect before stopping/reusing. It serves `.dazposewizard/handgrip-validation` for render review.

## What is implemented

### Dependency / graph

Main manifest and lock now include Animation Rigging **1.4.1**. Unity registry metadata confirmed it is released and requires Unity 6000.0. Actual Unity is **6000.5.9f1**. Main editor generated the lock after manifest change; other package versions were not deliberately changed.

Final constructed order:

Persistent Pose → existing Locomotion body mixer → Seating → Perform/Action → Motion → Gesture → Bone Breathing → Arm IK → Finger Grip → Gaze (head/eyes) → Expression → Blink.

`PerformerArmIKLayer` creates an AnimationScriptPlayable wrapper that binds and invokes the released package's **TwoBoneIKConstraintJob** inside the performer's existing graph. No RigBuilder, competing graph, new Animator, Humanoid IK, direct LateUpdate skeleton write, or Final IK. Two small external target/hint transforms are runtime-owned objects, not finger probe objects. Weight properties are custom stream properties set in the wrapper. Stream marker `Grip.IK.Evaluated` lets the finger job reject contact evaluation without the upstream IK job.

`PerformerExpressionLayer` now rejects non-head-descendant transform channels on a grip-enabled Animator. Existing expression regression tests pass. This is a narrow downstream ownership safeguard; no generic whole-graph write-set diagnostic yet.

### Actual right-arm paths

Common prefix:
`Genesis8Female/hip/abdomenLower/abdomenUpper/chestLower/chestUpper/rCollar/`

- Root: prefix + `rShldrBend`
- Mid: prefix + `rShldrBend/rShldrTwist/rForearmBend`
- Tip: prefix + `rShldrBend/rShldrTwist/rForearmBend/rForearmTwist/rHand`

Package solver works with these intermediate twist transforms. Actual lengths are measured at binding, not hardcoded.

### Profile and digit correction

Profile: `Assets/DazPose/Generated/HandGrip/Profiles/LaraRightHandGrip.asset`.

- PalmAnchor local position: `(0.03035795, -0.040982924, 0.0016906671)` meters.
- PalmAnchor local quaternion: `(-0.01526141, 0.048985567, -0.41428897, 0.9086981)`.
- GripFrame calibration: identity.
- Palm clearance: 0.003 m.
- Contact clearance: 0.003 m; near-contact range: 0.006 m.
- Blend-in/out: 0.30 / 0.30 seconds.
- Reach fractions: minimum 0.20, preferred 0.80, maximum 0.95, with unequal-bone physical minimum also enforced.
- Elbow hint Lara-local preference `(1, -0.6, -0.5)`.
- Five digits: Thumb/Index/Middle/Ring/Little; bone prefixes rThumb/rIndex/rMid/rRing/rPinky.
- Three controlled joints and **four probes per digit**, twenty total.
- Contact: **32 first-collision bracket steps + 10 binary iterations**.

Old direction selection compared positive/negative fingertip distance to an IN-PLANE palm-center point. That cannot distinguish inward/outward reliably. Old Index/Little endpoints had opposite signs from Middle/Ring despite similar reference directions. The user's visual 'first and fourth' labels cannot be conclusively mapped to anatomical digits; don't invent that identification.

New calibration computes a shared palm contact half-space using finger/across-palm geometry and thumb side, computes per-joint flex axes geometrically, and uses quaternion endpoints. Wrapping flex degrees 85/95/65. Thumb gets explicit spread away from contact (45° proximal open-pose spread) and 35/65/55 opposition/flexion. These are measured-contact-valid defaults but STILL NEED SKIN VISUAL APPROVAL, especially thumb.

Old candidate FK skipped rCarpal1/2/3/4. New job binds each digit's real first parent and reads its post-IK world pose; controlled local offsets also come from the current stream. Only controlled finger local rotations are written. Profile validation now demands direct consecutive controlled-joint parents.

Calibration window retains raw quaternion previews and adds Scene View PalmAnchor position/rotation handles. Runtime raw sliders disable contact.

### Spatial targets / planner

`GripPalmTarget` is pure math:
- Palm = Center + Normal * (Radius + clearance).
- Rotation = world tangent twist × canonical LookRotation(Tangent, Normal) × calibration.
- Wrist = desired palm composed with inverse calibrated hand→palm pose.

`GripReachPlanner` caches 17 wrist samples, tests current stance first, then searches 13 distance × 7 lateral × 5 yaw candidates on target-normal side. Offsets scale with measured arm length; survivors score reach margin, preferred reach, walking distance and yaw. Straight GripContactRod additionally certifies continuous segment minimum distance, while maximum is bounded by endpoints. General nonlinear targets are sampled only. Active checks disable expensive staging search (`searchStaging=false`).

### Runtime / API

`PerformerHandGripController` now owns interaction state and backend layers; no separate PerformerGripInteraction file was necessary.

Public performer API:
- `Awaitable<GripCompletion> GripAsync(IGripTarget)`
- `void Grip(IGripTarget)`
- `void ReleaseGrip()`
- GripState/IsGripping/GripTarget/GripPosition01/GripRequiredLocomotion/GripPositionError/GripRotationError/GripReachFraction/GripFailureReason.

State: Idle/Planning/Walking/Acquiring/Active/Releasing/Failed.

Walks through existing internal facing overload via `SuccubusPerformer.WalkToGripAsync`, never writes actor root. MotionDriver is unchanged and never paused/sought by interaction. Acquisition uses its current sample. IK weight blends first; fingers acquire after ~0.7 arm weight. Complete span is rechecked during acquisition/active. Out-of-reach active target releases/replans/walks/reacquires with bounded replans. Generation checks prevent stale walk completions. GripAsync supersession is deterministic. Release while walking supersedes its locomotion with current stance through existing locomotion alignment. Failure while weighted now releases before Failed.

Motion layer gets explicit spatial suppression, smoothly restored independently of source publication. Legacy Motion remains for other interactions.

### Acceptance scene / controls

HandGripAcceptance scene only, not source scene. Existing runtime panel is retained. Rod span shortened from impossible/near-full-arm 0.90 m to 0.12 m. Default rod no longer follows an authored clip. Grip-on-enable disabled; harness reference connected.

Harness has grip/release, raw per-digit calibration, radius, tilt, near/far/impossible placement, five normalized position controls (using existing sine Seek inverse), wrist twist, probe/plan gizmos. Setup no longer requires MotionSet or derives rod endpoints from a clip. Unused legacy helper `SampleMotionGripCenters` remains and should be removed, alongside obsolete GripCenter diagnostics if safe.

## Files owned/edited by this executor

Under `validation/DazPoseUnityValidation/`:

New:
- Assets/DazPose/Runtime/Performer/HandGrip/GripPalmTarget.cs
- Assets/DazPose/Runtime/Performer/HandGrip/GripReachPlanner.cs
- Assets/DazPose/Runtime/Performer/HandGrip/PerformerArmIKLayer.cs
- Assets/DazPose/Runtime/Performer/HandGrip/HandGripSpatialProof.cs
- Assets/DazPose/Editor/HandGrip/HandGripValidationRunner.cs
- Assets/DazPose/Editor/HandGrip/HandGripLiveProof.cs
- Assets/DazPose/Editor/HandGrip/HandGripVisualProof.cs
- docs/TargetDrivenGripExecution.md
- docs/TargetDrivenGripHandoff.md (this file)

Changed:
- HandGripCalibrationWindow.cs, HandGripAcceptanceSetup.cs
- HandGripRigProfile.cs, HandGripDefinitions.cs, HandGripAnimationJob.cs, HandGripSolver.cs, PerformerHandGripLayer.cs, PerformerHandGripController.cs, HandGripAcceptanceHarness.cs, HandGripRuntimeSelfTests.cs
- Runtime/Performer/SuccubusPerformer.cs (NARROW owned hunks amid preexisting wardrobe work)
- Runtime/Performer/PerformerMotionLayer.cs
- Runtime/Performer/PerformerExpressionLayer.cs
- Generated/HandGrip/Profiles/LaraRightHandGrip.asset
- Assets/Scenes/HandGripAcceptance.unity (NARROW owned hunks amid existing edits)
- Packages/manifest.json and packages-lock.json
- docs/HandGripImplementationNotes.md (replaced old defective alignment instructions)

Unity generated .meta files for new sources in main project. Check all before committing.

## Verified evidence (not visual approval)

Latest geometry/spatial report:
`.dazposewizard/handgrip-validation/project/TestOutput/TargetDrivenGrip/tests.txt`
Timestamp 2026-10-05T03:19:45Z. Runner: BatchCalibration. Log: `regressions-repaired.log` in handgrip-validation root.

- **52** geometry, palm, reach and synthetic contact assertions pass.
- Existing expression runtime regression checks pass.
- Actual Lara Generic arm: **15** radius/position combinations (15/25/35 mm × 0/.25/.5/.75/1), position error prints 0.000 mm at three decimal places, orientation prints 0.000°. Do not call this mathematical zero.
- All accepted probes pass no-penetration classification with existing 10 μm tolerance. Contact status is Contact, no BasePenetration.
- Index curl monotonic radius check passes. Latest dynamic-offset curls differ slightly from earlier snapshots; read report for final values.
- Six tilt/twist combinations (0°/30° tilt × -20°/0°/+20° twist) pass spatial/contact tests.
- 1,000 graph Evaluate frames: **304.65 ms total**, **0 main-thread managed allocated bytes**. This is editor manual graph evaluation, not player profiler or a benchmark speedup claim.

Latest live report:
`.dazposewizard/handgrip-validation/project/TestOutput/TargetDrivenGrip/live.txt`
Timestamp 2026-10-05T03:29:01Z. Log: `orchestration-final.log`.

**16 recorded PASS gates**:
- Near no-walk; palm error 0.000120137 mm.
- Two seconds sine tracking.
- Release to Idle.
- Far walk→grip; palm error 0.0003874302 mm; reach margin 0.177249 m.
- Moved-target release/walk/reacquire (test now observes Releasing/Walking before accepting reacquisition).
- Impossible vertical explicit failure.
- Funscript seeks at 0/.25/.5/.75/1; largest recorded palm error ~0.001329 mm.
- Continuous Funscript contact/tracking.
- Stop holds physical position; resume continues.
- Superseded GripAsync resolves once, newer GripAsync acquires once.
- Locomotion supersession fails GripAsync once.

Unity compiles these latest sources in isolated project. An intermediate regression-runner compile failure (internal test type inaccessible to editor assembly) was repaired by invoking it from runtime proof; rerun passed. Do not cite the failed run as passing.

Live logs also contain an unrelated Unity Editor SearchDatabase startup ArgumentOutOfRangeException and editor warnings. Do not claim a completely warning/error-free editor log. No grip runtime failure was observed in the latest live gates.

No commits yet; therefore no new SHA or last-green-commit claim. Preserve test artifacts and local code as checkpoint.

## Reproduce validation

Unity executable:
`C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe`

The isolated project already contains ~7 GB copied assets plus initialized Library. Do not recopy entire project or use another agent's test project. Before each run, sync ONLY current task source files and exact needed main integration files into our isolated project; copy preserves source meta GUIDs. Check no Unity owns the isolated project before launching.

Example Bash:

```bash
cp validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Performer/HandGrip/*.cs .dazposewizard/handgrip-validation/project/Assets/DazPose/Runtime/Performer/HandGrip/
cp validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Performer/{SuccubusPerformer,PerformerMotionLayer,PerformerExpressionLayer}.cs .dazposewizard/handgrip-validation/project/Assets/DazPose/Runtime/Performer/
cp validation/DazPoseUnityValidation/Assets/DazPose/Editor/HandGrip/*.cs .dazposewizard/handgrip-validation/project/Assets/DazPose/Editor/HandGrip/
```

Run BACKGROUND, inspect log/report freshness, actual process exit and artifacts:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "$(pwd)/.dazposewizard/handgrip-validation/project" \
  -executeMethod DazPose.Editor.HandGrip.HandGripValidationRunner.BatchCalibration \
  -logFile "$(pwd)/.dazposewizard/handgrip-validation/next-spatial.log"
```

Live: replace executeMethod with `DazPose.Editor.HandGrip.HandGripLiveProof.Run`. Runner enters Play Mode, persists pending flag through domain reload, measures public API, exits automatically. Usually ~30–60 seconds after warm import. Batch entry points intentionally reject main project.

Visual: `DazPose.Editor.HandGrip.HandGripVisualProof.Run`, omit -nographics. Seven PNGs (0 open, 1 all closed, 2 thumb, 3 index, 4 middle, 5 ring, 6 little) at `project/TestOutput/TargetDrivenGrip/visual/`. The renderer was repaired to BakeMesh each candidate; earlier direct-skin renders reused stale skin poses and are invalid evidence. Latest log is `visual-baked.log`. Seven current PNG hashes differ, proving distinct renders, NOT inward correctness.

## Visual review state / tool failures

Initial screenshot inspection showed stale direct-skin rendering, so VisualProof now bakes meshes per pose. Repaired images exist and differ, but this executor could NOT inspect them after repair: browser screenshot calls returned `preview webview is not being composited` / `produced no frames` despite reopen/reload. **Raw finger visual acceptance remains unverified.** This is a real tool failure, not an IK blocker.

Gallery: `.dazposewizard/handgrip-validation/visual.html` (embedded PNGs); served at `http://127.0.0.1:8879/visual.html`. Browser tab ID previously `55aebc1c-2150-4f4f-8bd9-ec8f3953c733`; query fresh state. `preview_open` requires profileId `default`; empty profile failed. register_preview tool exposed required incompatible mode fields; multiple attempts were rejected, so don't assume registration succeeded.

Render appearance also needs cleanup: original scene materials produced magenta/black during initial synchronous render warmup. Repaired bake renders may still have lighting/material artifacts. Use simple isolated proof materials/lighting or Play Mode warmup if needed; don't modify Lara import/wardrobe assets to fix a diagnostic image.

## Remaining work — prioritize

1. **Read and audit current files before edits.** Some robustness choices are expedient and require cleanup; don't take this handoff as acceptance gospel.
2. Finish raw finger VISUAL acceptance in isolated scene. Inspect every anatomical digit toward palm, thumb spread/opposition, no flipping. Capture usable visual evidence. Current thumb reference is intentionally calibrated/spread, not exact original imported rest; explain to user.
3. Finish scene control/diagnostic contract: Start/End editing, Normal/roll, explicit sine/stop/restart/Funscript controls (some inherited panel), minimum per-digit clearance/contact count/desired curl, IK weight, actual wrist/hint/bend angle and reach fraction, target/palm axes, yellow near-limit colors. Existing controls/gizmos are functional but not all ticket fields.
4. Add stronger raw-job tests: actual animation-stream endpoint identity, repeated application, uncontrolled bones unchanged. Current raw tests largely cover quaternion math and synthetic parent FK, not complete stream ownership.
5. Strengthen span/planner tests: continuous rod min/collapse edge, invalid profile/target; distinguish general-target sampled guarantee. Five/17 samples cannot certify arbitrary nonlinear targets.
6. Strengthen orchestration: disable during acquisition/walking, release while walking, stale completion/reentrancy, active invalid target, moving body pose/shoulder, target destruction. Async callback reentrancy around Complete can replace state; audit carefully. Public controller Grip overloads for MonoBehaviour vs IGripTarget can be ambiguous for concrete rod callers; public performer API avoids it, but simplify controller compatibility if needed.
7. Acquisition checks currently read scene transforms/results from prior evaluation; spatial target itself is current-frame before graph, but Active transition diagnostics are previous-frame. Verify no lag claims under rapid source seek/moving target. Do not silently accept stale contact state.
8. `Fail` while weighted blends out, but unsafe target release uses last desired pose and may briefly remain extended while blending. Review desired safe failure behavior. Release while Walking currently requests current stance through existing locomotion; ensure external request ownership cannot be accidentally superseded.
9. Stronger performance: core math has no frame allocations on valid target path. Planner catches exceptions for invalid targets and allocates through exception paths. Whole existing performer Update allocates settings objects independently; do not claim whole-performer no allocations. Add profiler evidence if packet requires it.
10. Package stream marker verifies upstream IK presence, not a complete declarative graph write-set audit. Finish meaningful graph diagnostic (including expression/gaze/face-only checks) and test body breathing/pose while hand remains constrained.
11. Remove obsolete authored-alignment helper and obsolete GripCenter diagnostics/accessors only where safe; preserve serialized compatibility if needed.
12. Promote calibrated profile from isolated project only after reviewing any repeated ConfigureFromRig recaptures. Main profile was copied after contact-calibration passed; later dynamic-offset solver tests use same endpoint generation but read real local offsets. Compare main vs isolated explicitly.
13. Save evidence into task-owned review/report location; TestOutput and .dazposewizard are ignored/untracked and not a shipped documentation guarantee. Update completion report with honest remaining visual-quality issues.
14. Final rerun after final edits: geometry + spatial + live + relevant regression checks, compile. Tests passing before later repairs do not cover them.
15. Create requested bounded commits, **only owned hunks**, preserving preexisting shared-file work. No commit stack yet. If safe hunk staging is difficult due shared edits, leave ambiguous hunks uncommitted and explain rather than capture another agent's wardrobe changes. Review git diff and git log per executor instructions before committing. Don't push.

## Ticket deficiencies already called out

- Five samples don't certify arbitrary target motion; straight constant-frame rod can be certified continuously with endpoint max + segment minimum.
- Binary search isn't safe with nonmonotonic quaternion curls unless collision interval is bracketed. 32 finite brackets still approximate continuous geometry and can miss extremely narrow collision intervals; note/test bounds, don't overclaim.
- Reach distance does not certify orientation feasibility, obstacle clearance or walkability. T0 assumes open planar floor, no nav planner.
- Digit visual order from user observation isn't anatomical enumeration evidence.
- Probes validate analytic contact only, not complete skinned mesh surface.
- Final IK was not needed; no architectural IK blocker was encountered.

## Delivery status

Manual-verification preparation: reuse the existing isolated project with
`scripts/open-handgrip-manual.ps1`; see `TargetDrivenGripManualVerification.md`.
The harness now provides direct Sine/Stop/Restart controls, palm error, reach,
walk usage and anatomical digit status. No scene regeneration, recalibration,
automated Play takeover, or visual acceptance was performed in this preparation.

Follow-up manual feedback: user said the first run was "pretty good", requested
more wrist twist and a glass-like opposed thumb. Harness range is now ±90°;
palm twist orbits its contact anchor around the cylinder. Thumb calibration and
the saved Lara profile now use opposite-side extension and 100/65/55° wrapping
endpoints, independently stopped by its probes. Standalone palm-space checks
pass at 15/25/35/55 mm, but full Unity spatial/live reruns and visual acceptance
of this revised thumb remain pending. Sources/profile copied into the existing
interactive isolated project; reenter Play after compilation to rebuild bindings.
Earlier comments above about the 45° spread thumb and fixed palm point are
historical and superseded by this follow-up.

Implemented and quantitatively demonstrated target→palm→IK→post-IK contact and near/far/reacquire/normalized-source runtime. **Full ticket stack is not signed off** until visual/raw digit and remaining diagnostics/hardening gates above are addressed. Continue from this checkpoint; do not start over or replace local work with remote main.
