# DAZ Pose Unity validation harness

This small Unity 6 project checks canonical `.dazpose.json` files against the actual Genesis 8 Female character imported from DAZ's `lara.fbx`. It does not use Humanoid retargeting, Blender, or BVH. The canonical file keeps DAZ centimeters and evaluated rest and pose transforms; format v2 also retains static figure controls without Unity renderer paths. Unity separates the figure `SkeletonRoot` from the common character `BindingRoot`, then resolves each control by exact imported name or the DAZ figure-root prefix Unity adds during FBX import.

## Local inputs

The character FBX, its texture sidecar, and converted pose are local assets and are intentionally not committed. In a checkout that has the repo's local DAZ fixtures, run these commands from the repository root before opening Unity:

```powershell
New-Item -ItemType Directory -Force .\validation\DazPoseUnityValidation\Assets\TestCharacter, .\validation\DazPoseUnityValidation\Assets\TestData | Out-Null
Copy-Item .\G8F-Base\lara.fbx .\validation\DazPoseUnityValidation\Assets\TestCharacter\lara.fbx
Copy-Item .\G8F-Base\lara.images .\validation\DazPoseUnityValidation\Assets\TestCharacter\lara.images -Recurse -Force
Copy-Item '.\output\Cherish Genesis 8 Female 16.dazpose.json' .\validation\DazPoseUnityValidation\Assets\TestData\ -Force
```

`G8F-Base\Genesis8Female.dsf` is byte-identical to `fixtures\private\Genesis8Female.dsf` in the current local checkout. Convert the pose with the standalone app first if the `.dazpose.json` is absent or old; regenerate it after changes to the canonical schema.

## Browser drag-to-Unity imports

The editor scripts in this harness also exercise the browser bridge. In Unity, choose **Tools > DAZ Pose > Pipeline Settings** and assign `Assets/TestCharacter/lara.fbx` to **G8F Reference Model**. Its importer must be **Generic**, have **Optimize Game Objects** disabled, and have **Import BlendShapes** enabled. The selected model GUID is stored in `ProjectSettings/DazPoseWizardSettings.json`.

In DazPoseWizard, open **File > Settings…** and set the DAZ content root and `Genesis8Female.dsf`, set this harness as the Unity project root, and choose separate Assets-relative roots such as `Assets/Animations/DazPoses` and `Assets/DazPoseImports`. Save, then drag pose or shape preset cards onto a folder in **UNITY POSE ASSETS**. The app writes canonical JSON into the mirrored import hierarchy; source `.duf` files and preview images stay in the DAZ library. Active figure controls are added to `.dazposewizard/required-morphs.json` at the Unity project root.

In DazPoseWizard, open **Tools > Manage Always-Export Morphs…** to edit the project's categorized pins. Generate `.dazposewizard/DazPoseWizard-MorphExportRules.csv` with **Tools > Generate DAZ Morph Export Rules**. The CSV unions enabled pins with morphs required by converted content, writes each exact name once, and ends with one `Anything,Bake` fallback. Removing or disabling a pin does not drop a morph still required by converted content. Import the generated CSV into DAZ Studio's FBX Morph Export Rules UI, export the reduced `G8F-Base/lara.fbx`, then replace `Assets/TestCharacter/lara.fbx` while preserving its `.meta`. Reimport the FBX; the project postprocessor enables blendshapes and retries waiting browser imports. The DAZ export stays a manual step, and always-export pins do not create `.anim` clips.

Unity detects imported canonical files after the AssetDatabase callback settles, then processes them one at a time from an isolated preview scene. **Tools > DAZ Pose > Process Pending Browser Imports** retries new, changed, failed, or missing-output jobs, including jobs that were waiting for a refreshed reference. Final clips use the canonical file's basename in the matching output folder. Reports, manifest, and per-job status stay outside `Assets` under `.dazposewizard`. The processor calls the same `TryResolvePose` adapter and `DazPoseAnimationClipGenerator` used by the manual converter, including Transform/blendshape parity checks and in-place GUID-preserving regeneration.

The automatic pipeline does not require a selected character or open validation scene. It does not parse DAZ source files or modify scene objects. If the reference model is missing or a pose cannot resolve against G8F, that job receives a **Failed** status with the error in the Unity Console and browser card; the rest of the queue continues.

### Stage 5 morph workflow

The browser indexes both `preset_pose` and `preset_shape`. Converting a shape preset preserves its active DAZ figure control in canonical v2 and adds the decoded name once to the project Required Morph Manifest. Manage approved project pins in **Tools > Manage Always-Export Morphs…**; categories are Breathing, Blink, Body Customization, Lip Sync, and Manual. Generate the DAZ Morph Export Rules CSV after editing pins. The project-level manifest is the source for both content-required names and always-export pins; names are exact and no rule is typed by hand.

After the user imports the CSV into DAZ Studio and exports the reduced `G8F-Base/lara.fbx`, copy it over the existing Unity reference at `Assets/TestCharacter/lara.fbx` without changing its `.meta`. The project postprocessor requires **Generic**, Optimize Game Objects off, and Import BlendShapes on. Unity preserves the GUID-based reference setting and retries pending/failed canonical imports after the FBX refresh; the explicit retry command is **Tools > DAZ Pose > Process Pending Browser Imports**.

Use **Tools > DAZ Pose > Validate Always-Export Morphs** to check enabled pins against the configured reference model after refresh. Its compact Console result lists Present, Missing, and Ambiguous totals and missing names; the complete report is `TestOutput/always-export-morph-validation.json`. Multiple renderers may each carry a valid shape. Body categories require the match on the identified figure body renderer, so a clothing- or hair-only match remains Missing.

The shared resolved pose separates `SkeletonRoot` (`Genesis8Female`) from `BindingRoot` (the selected character root). Skeletal transform curves and sibling renderer blendshape curves are relative to `BindingRoot`. Each active DAZ control first matches its exact imported blendshape name, then checks the exact `<figure-root-name>__<control-name>` name Unity imports from a DAZ FBX. Matches on multiple renderers are emitted as multiple curves. Missing controls and ambiguous duplicate shapes on one renderer fail clearly. Each renderer uses its mesh's highest positive frame weight as the full DAZ value, so the same DAZ value maps against the imported frame data instead of assuming every mesh uses a 100 frame.

Choose **Tools > DAZ Pose > Inspect Selected Character** to write `TestOutput/lara-unity-skeleton.json`. The report includes renderer paths, mesh names, exact blendshape names and indices, frame counts, and frame weights. **Tools > DAZ Pose > Run Stage 5 Morph Self Tests** covers shape-only clips, mixed skeletal/morph clips, two-renderer resolution, untouched unrelated renderers, missing-morph diagnostics, and direct/clip parity.

## Open and inspect

1. Open `validation\DazPoseUnityValidation` in Unity `6000.5.9f1`.
2. Wait for the FBX and texture import to finish. The project AssetPostprocessor sets `lara.fbx` to **Generic**, turns **Optimize Game Objects** off, and enables **Import BlendShapes**. It leaves the imported skeleton and FBX axis/unit settings otherwise untouched.
3. The committed `Assets\Scenes\PoseValidation.unity` already contains a Lara instance, neutral rest snapshot, camera, light, and floor. Choose **Tools > DAZ Pose > Open Validation Scene** to load it and select Lara. If you need to recreate it from the local FBX, choose **Tools > DAZ Pose > Setup Validation Scene**.
4. Confirm **Lara** is selected in the Hierarchy.
5. Choose **Tools > DAZ Pose > Inspect Selected Character**. Confirm the Console reports all 13 expected skeleton names, including `hip`, `pelvis`, `lThighBend`, `lShin`, `lFoot`, `rThighBend`, `rShin`, `rFoot`, and `head`. This FBX has duplicate hip/spine/head paths under the eyelashes mesh; the tool will warn and disambiguate them by the exact DAZ parent hierarchy.
6. The detailed import report is written to `TestOutput\lara-unity-skeleton.json`. Missing expected names are errors; duplicate names are warnings because the resolver disambiguates by exact DAZ parent ID.
7. Optionally choose **Tools > DAZ Pose > Inspect Pose Bone Mapping** and select the Cherish `.dazpose.json` to save an exact name/ID map before applying.

## Apply and reset the pose

1. Keep **Lara** selected and choose **Tools > DAZ Pose > Apply Pose to Selected Character**.
2. In the file picker, select `Assets\TestData\Cherish Genesis 8 Female 16.dazpose.json`.
3. The Console reports pose/channel/bone counts, active figure controls, exact-name or exact-ID resolution totals, and neutral-rest landmark-fit error. The detailed matrix and per-bone results are written to `TestOutput\pose-application-report.json`.
4. If all active targets resolve and the neutral rest fit is within 20 mm RMS, Lara updates in the scene to the authored static Cherish pose. Inspect the overall body articulation first, then limbs and fingers. This first check only proves the visible application path is working; it does not establish fidelity against DAZ's posed reference export.
5. Use **Tools > DAZ Pose > Restore Captured Rest Pose** or Unity **Edit > Undo Apply DAZ Pose** to return the character to its imported neutral pose. The captured neutral transforms live on the Lara scene object and persist with the scene.

The calibration fits DAZ rest-bone positions (centimeters) for the pelvis/spine/head, shoulders/arms, and hips/legs to the corresponding actual Unity rest-bone positions (meters). It then converts DAZ world rotation deltas by matrix conjugation and position deltas with that same measured 3×3 basis. The report retains residuals for all 170 bones, including the face and fingers; active targets above 20 mm are called out in Console and the JSON report. The current local run finds 8.2 mm RMS over the 23 calibration landmarks, while the two ears and two big-toe end bones differ by 20–26 mm individually. This is an observed FBX/rest-center discrepancy; no per-bone offsets are added. If pose targets or calibration anchors are missing/ambiguous, or calibration-landmark RMS exceeds 20 mm, application stops and leaves a diagnostic report.

The **Tools > DAZ Pose > Run Adapter Self Tests** command runs nine checks for calibration math, centimeter conversion, reflected-basis conversion, neutral rotation identity, normalized quaternion output, `lThigh` → `lThighBend` exact-name resolution, and explicit missing-bone diagnostics.

## Phase 1 review record

The user visually reviewed multiple G8F direct-pose applications and confirmed they worked correctly on 2026-09-27. That is the accepted Phase 1 baseline. The clip generator reuses the same resolved local transforms captured from the existing direct-apply conversion; it does not add a second DAZ-to-Unity conversion path.

## Phase 2 review record

The user confirmed the Phase 2 native clip, editor preview, and Play Mode animation smoke test all worked. This is the accepted Phase 2 baseline for the runtime blending experiment.

## Generate a native Unity AnimationClip

1. Open `Assets/Scenes/PoseValidation.unity` and select the **Lara** root in the Hierarchy.
2. Choose **Tools > DAZ Pose > Generate AnimationClip from Pose**.
3. Select the canonical `*.dazpose.json` file. Unity resolves the figure on a hidden temporary copy restored to the captured import rest pose, leaving the visible Lara unchanged.
4. If the same clip already exists, confirm **Regenerate**. Unity replaces it deterministically only after this explicit prompt.
5. Unity writes a non-Legacy, one-second clip to `Assets/Generated/DazPoses/G8F/<pose name>.anim`, plus a neighboring `<pose name>.report.json` with source provenance, skeleton and binding roots, stable bone and renderer paths, exact blendshape names/frame data, curve counts, and parity errors.
6. Inspect the Console summary. Rotation data uses complete `m_LocalRotation.x/y/z/w` quaternion curves. Local position curves are emitted only for resolved positions changed by this pose. Shape-only clips may contain no Transform curves. Renderer and bone paths are relative to the common character `BindingRoot`.

The Console reports and adjacent JSON report include the exact clip path. Generated assets are local outputs and are ignored by Git in this validation harness.

## Preview and restore

1. Select **Lara** in the Hierarchy.
2. Choose **Tools > DAZ Pose > Preview AnimationClip on Selected Character**.
3. In the file picker, choose the generated `.anim` under `Assets/Generated/DazPoses/G8F/`.
4. Unity restores the captured import rest Transform and blendshape state and samples the clip at 0.5 seconds relative to the common character `BindingRoot`.
5. Choose **Tools > DAZ Pose > Stop Preview / Restore Pose**. Unity exits its Animation Mode and restores the pre-preview rest state.

For a direct-versus-clip visual comparison, first apply the same `.dazpose.json` with **Apply Pose to Selected Character** and inspect Lara. Then preview the corresponding `.anim`. Stop the preview when finished.

## Play Mode smoke test

1. Select **Lara** and choose **Tools > DAZ Pose > Run Play Mode AnimationClip Smoke Test**.
2. Select the generated `.anim` asset. Unity adds a validation-only Animator and Playables driver to the common character `BindingRoot` and enters Play Mode.
3. Confirm the Console prints `PASS DAZ Pose runtime smoke test` with low local transform errors. The static clip stays active while Play Mode is running.
4. Stop Play Mode. The temporary driver and an Animator added by the validation command are removed automatically.

The smoke test uses a `PlayableGraph` and `AnimationClipPlayable`; it does not use a Legacy Animation component or a production Animator Controller.

## Automated Phase 2 validation

From Unity batch mode, `DazPose.UnityValidation.DazPoseEditorCommands.RunBatchValidation` runs the adapter and Stage 5 morph self-tests, direct-apply regression, native clip generation, binding inspection, and parity checks at 0.0, 0.5, and 1.0 seconds for every `*.dazpose.json` currently present in `Assets/TestData`.

`DazPose.UnityValidation.DazPoseEditorCommands.RunBatchPlayableSmokeTest` runs that suite and then enters Play Mode to verify Unity's Animator/Playables path. The editor monitor exits batch mode only after the runtime driver passes, and returns a failure exit code on a parity or timeout error.

The repository currently contains one local pose fixture (`Cherish Genesis 8 Female 16.dazpose.json`). The Phase 2 batch runner automatically adds any second known-good G8F `.dazpose.json` copied into `Assets/TestData`.

## Phase 3 runtime pose blending

This experiment blends ordinary sparse G8F `.anim` pose clips through a two-input Playables mixer on the common character `BindingRoot`. Transform and blendshape curves use the same native AnimationClip mixer. It does not use BVH, retargeting, or an Animator Controller state machine. The Animator's root motion is disabled by the setup command; Lara remains the outer scene-placement object.

### Prepare three poses

1. Convert the three selected poses with DazPoseTool V0 using the same `Genesis8Female.dsf` figure. Keep each generated `.dazpose.json` output.
2. Copy the three JSON files into `validation\DazPoseUnityValidation\Assets\TestData` (the Unity project will import them).
3. In Unity, open `C:\Users\artwh\OneDrive\Documents\DazPoseWizard\validation\DazPoseUnityValidation` with Unity `6000.5.9f1`, then open `Assets\Scenes\PoseValidation.unity` with **Tools > DAZ Pose > Open Validation Scene**.
4. Select **Lara** in the Hierarchy. For each JSON file, choose **Tools > DAZ Pose > Generate AnimationClip from Pose** and select that file. Confirm **Regenerate** if Unity asks about an existing clip. The `.anim` files are written to `Assets\Generated\DazPoses\G8F`.

### Configure and start the demo

1. Select **Lara**, then choose **Tools > DAZ Pose > Setup Runtime Blend Demo**. The command puts or reuses the Animator, `DazPoseBlendPlayer`, and `DazPoseBlendDemo` on Lara's common `BindingRoot`. It selects that root afterward.
2. In the Inspector, find **Daz Pose Blend Demo**. Assign the generated `.anim` assets to **Pose A**, **Pose B**, and optionally **Pose C** by dragging them from the Project window. Leave Pose C empty for a two-pose test. Set **Blend Ease**; the default is SmoothStep.
3. Save the scene with **Ctrl+S** while still in Edit Mode.
4. Click Unity's **Play** button. The character snaps to Pose A as its known starting pose; there is no neutral-to-A blend.
5. In the Game view's **G8F Runtime Pose Blend Test** panel, click **2 - Pose B**, then **1 - Pose A** to test both directions. If using Pose C, click **3 - Pose C** as well. Keyboard keys **1**, **2**, and **3** select the corresponding slots when the Game view has focus.

The Game view panel contains a blend-duration text field and Windup/Overshoot sliders. The sliders range from 0% to 30% in one-percent steps and start at 0%. Each pose button or 1/2/3 key snapshots the duration, ease, windup, and overshoot as one command. The input controls lock while a transition runs; pose requests can still be queued. A queued request carries its own snapshot, and the latest request for a pose already in the queue replaces that command's options. Asking for the active target does not restart the transition, and asking for the already steady pose does nothing.

### Compare transition parameters

Set values in the Game view panel, then click the opposite pose button (1 or 2). Let each transition finish before starting the next comparison. Suggested trials:

- `0.15`, `0.35`, `0.70`, and `1.20` seconds with both sliders at 0% for the existing baseline.
- `0.70` seconds with 10% windup and 0% overshoot.
- `0.70` seconds with 0% windup and 10% overshoot.
- `0.70` seconds with 8% windup and 12% overshoot.
- Repeat the combined setting at `0.25` and `1.20` seconds.

The current transition's captured settings appear as **Active**; the next request's settings appear as **Pending**. These values stay fixed for each request. The Console logs source, target, duration, easing, windup, and overshoot once per transition.

### Endpoint and repeated-transition check

With Pose A and Pose B assigned and no blend in progress, click **F5 - Check endpoints and repeat A/B 20 times** in the Game view panel. This checks trajectory math, shaped A/B Transform and blendshape endpoints against direct clip samples, blendshape interpolation when the clips differ, captured active and queued options, Lara's outer world transform, mixer weight limits, 20 repeated transitions, and two graph disable/enable cycles. The Console should print `PASS DAZ Pose runtime blend validation`. It is a brief validation run and returns to Pose A.

### Compare sparse bindings after a visible snap

In Edit Mode, choose **Tools > DAZ Pose > Compare AnimationClip Bindings A vs B**. Select the exact two `.anim` clips tested. Unity reports the shared, only-in-A, and only-in-B binding counts and saves the full path/property comparison to `TestOutput\pose-binding-comparison.json`. Send that JSON if a transition snaps at its start or end; do not expand the clips to the full skeleton during this experiment.

### What to report back

- Which Pose A, Pose B, and (if used) Pose C clips you tested.
- Which duration, windup, and overshoot values looked best, and whether Linear or SmoothStep looked better.
- Any shoulder, wrist/finger, hip, knee/foot, or spine movement that looked wrong.
- Whether a snap happened at the beginning or end of a transition.
- `TestOutput\pose-binding-comparison.json` if a snap occurred, plus any Unity Console error.

## Evidence for a corrective pass

If the result is wrong, restore neutral and send back:

- `TestOutput\lara-unity-skeleton.json`
- `TestOutput\pose-application-report.json`
- `Assets\Generated\DazPoses\G8F\<pose name>.report.json`
- Unity Console errors/warnings from the operation
- a screenshot showing Lara and the incorrect body regions

Do not change random FBX axis/import settings. The reports include each transform path, local/world transforms and matrices, exact mapping status, root scale, the derived world basis, and per-bone rest-fit residuals so the mismatch can be traced to source evaluation, bone resolution, coordinate conversion, root/unit scaling, or the FBX rest skeleton.
