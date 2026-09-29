# DAZ Pose Unity pipeline

This Unity 6 editor tooling imports canonical DAZ poses produced by DazPoseWizard and exposes them as production-ready PerformerPose assets. The validation scene is for development checks; it is not part of normal importing.

## Importing a new production pose

1. Open DazPoseWizard and choose the Unity destination folder.
2. Drag the pose onto that folder to convert and enqueue it.
3. Let Unity import it automatically.
4. Use the generated PerformerPose asset in performer or directing content.

Each import writes a canonical file under the configured import root, normally Assets/DazPoseImports. Unity writes the matching AnimationClip and PerformerPose under the configured final root, normally Assets/Animations/DazPoses, preserving the destination subfolders. The PerformerPose asset is the content reference; its Clip field points to the exact neighboring .anim file.

DazPoseWizard reports Converted only when the .anim and PerformerPose .asset both exist, their Unity meta files are available, and the wrapper references that clip. Unity also loads both assets and checks the typed PerformerPose reference before writing a Converted status. If either output is missing or mismatched, the app leaves the job awaiting Unity and Unity reconciles it again.

Canonical files can be enqueued while Unity is closed. On the next Unity startup the importer scans the configured canonical root and processes pending or incomplete jobs. Reports and per-job status are stored outside Assets under .dazposewizard. Failures appear in the Unity Console and on the corresponding DazPoseWizard card, with details in the job report. Use Tools > DAZ Pose > Reconcile Pending Pose Imports to retry or recover imports explicitly.

Normal importing does not require opening PoseValidation.unity, selecting Lara, generating JSON in Unity, or running a validation menu command.

## Configure the Unity project once

In Unity, open Tools > DAZ Pose > Pipeline Settings and assign the neutral Genesis 8 Female reference model. Its importer must use Generic rig import, Optimize Game Objects disabled, and Import BlendShapes enabled. The reference-model GUID is saved in ProjectSettings/DazPoseWizardSettings.json.

DazPoseWizard creates DazPoseWizard.project.json at the Unity project root after project configuration. It records the canonical import root and final asset root. Keep those roots separate and inside Assets. The application uses the project you select in its settings.

The importer instantiates the configured reference model in an isolated preview scene and never edits the active scene. Pose resolution and clip parity checks use that temporary instance.

## Runtime pose API

The performer offers both immediate and awaitable requests:

- SuccubusPerformer.Pose(PerformerPose pose)
- SuccubusPerformer.Pose(PerformerPose pose, PoseTransition transition)
- Awaitable<PoseCompletion> SuccubusPerformer.PoseAsync(PerformerPose pose)
- Awaitable<PoseCompletion> SuccubusPerformer.PoseAsync(PerformerPose pose, PoseTransition transition)

Pose returns while the transition continues. PoseAsync suspends its calling async sequence until the request settles, another request supersedes it, or the performer is disabled. PoseCompletion is Settled, Superseded, or PerformerDisabled. Both methods use the same latest-desired-state-wins runtime, including duration, curve, windup, overshoot, and continuity-preserving interruption.

## Experimental breathing

SuccubusPerformer adds a persistent breathing overlay after the captured base-pose output. The validation scene starts with a 10 BPM cycle, morph strength 0.25, relative Breathe/BreatheBelly strengths 1.0/0.7, and bone strength 0.3. Those are conservative starting values for visual tuning, not final animation constants. The curve holds an exhale at phase 0, rises smoothly to inhale by phase 0.32, holds briefly, then returns more slowly to rest by phase 1. The phase pauses while the master Breathing Enabled switch is off.

The Performer Pose Smoke Test overlay exposes the master switch, BPM, phase/value, morph and bone switches, and their strengths during Play Mode. Run its acceptance checks with F5 to verify the P0.4 pose behavior with breathing bypassed, then exercise morph-only, bone-only, combined, retarget-isolation, phase-continuity, and awaitable cases. The default torso channels target abdomenLower, abdomenUpper, chestLower, and chestUpper; each inhale rotation/position delta remains editable on SuccubusPerformer.

## Persistent procedural gaze

The performer graph is base-pose capture → breathing → gaze → Animator. `LookAt(Transform)` tracks a live Transform; `LookAt(Vector3)` holds a fixed world point; `ClearGaze()` smoothly reveals the authored head and eye pose. The validation rig resolves `head`, `lEye`, and `rEye` uniquely. At runtime it derives facial forward from the eye midpoint relative to the head, projects performer up onto the facial plane, then transforms that orthogonal basis into each bone's local space. No Unity XYZ axis is assumed.

The default gaze settings are head weight/response/limits 0.7, 4, ±40° yaw, ±25° pitch; eye weight/response/limits 1.0, 12, ±32° yaw, ±22° pitch; release response 4 and acquisition tolerance 2°. Eyes therefore respond faster than the head. Targets are smoothed independently for head and eyes, with left/right eye aim solved independently for near-target convergence. Gaze is added downstream of the captured base pose, so it cannot become the source of a later pose retarget. The effective head target, effective eye target, and preferred head bias are represented before smoothing and solving as the insertion seam reserved for future attention-life behavior.

The validation scene has a root-level empty `Gaze Target` at eye height in front of Lara. Select it in the Hierarchy and move it in the Scene view during Play Mode. The smoke overlay has buttons for this target, the Main Camera, and Clear Gaze, plus gaze/head/eye weights, responses, limits, acquisition tolerance, and live target/status readouts. Run F5 acceptance checks for bone calibration, bypass, eye-only/head-only/combined modes, moving and fixed targets, release, clamping, async supersession/loss, and gaze/pose/breathing independence.

## Continuing production workflows

- Tools > DAZ Pose > Validate Always-Export Morphs checks configured runtime morphs after refreshing the reference model.
- DazPoseWizard can generate the DAZ Morph Export Rules CSV from required morphs and approved pins. Import that CSV in DAZ Studio before exporting an updated reference FBX.
- After replacing the reference model, use Tools > DAZ Pose > Pipeline Settings to confirm its importer configuration and refresh waiting imports.
- Tools > DAZ Pose > Copy Bridge Materials to Selected Lara copies the bridge example's material assignments to the selected Lara scene object as undoable overrides. It does not alter FBX importer settings.

## Development menu

The normal Tools > DAZ Pose menu keeps production operations at its top level:

- Pipeline Settings
- Reconcile Pending Pose Imports
- Validate Always-Export Morphs
- Copy Bridge Materials to Selected Lara
- Development

Development contains validation-scene setup/opening, the performer acceptance harness setup, and diagnostics for the selected character, pose bone mapping, and clip bindings. Historical direct-apply, manual clip-generation, preview, old Playables demo, and one-off Stack3 setup commands have been removed.

PoseValidation.unity remains a development harness with the current SuccubusPerformer, PerformerPoseSmokeHarness, and PerformerPoseAcceptanceHarness. The acceptance harness checks transition continuity, latest-wins behavior, awaitable settlement, snap, supersession, same-target waiters, disable completion, and reentrant retargeting. It is not needed to import production content.

## Local validation inputs

The Lara FBX, texture sidecar, and DAZ fixtures are local and intentionally not committed. To restore the local validation FBX from the repository root, copy G8F-Base/lara.fbx and G8F-Base/lara.images into validation/DazPoseUnityValidation/Assets/TestCharacter. Assign that FBX as the pipeline reference model when validating locally.
