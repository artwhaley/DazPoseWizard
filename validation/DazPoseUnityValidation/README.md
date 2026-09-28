# DAZ Pose Unity validation harness

This small Unity 6 project checks the canonical `.dazpose.json` against the actual neutral Genesis 8 Female skeleton imported from DAZ's `lara.fbx`. It does not use Humanoid retargeting, Blender, or BVH. The `.dazpose.json` keeps DAZ centimeters and the converter's evaluated rest and pose transforms; the Unity adapter derives one DAZ-to-Unity 3×3 world basis from corresponding neutral skeleton positions and converts translation deltas from centimeters to meters. A reflected basis is allowed only when the imported bone positions establish it in the fit.

## Local inputs

The character FBX, its texture sidecar, and converted pose are local assets and are intentionally not committed. In a checkout that has the repo's local DAZ fixtures, run these commands from the repository root before opening Unity:

```powershell
New-Item -ItemType Directory -Force .\validation\DazPoseUnityValidation\Assets\TestCharacter, .\validation\DazPoseUnityValidation\Assets\TestData | Out-Null
Copy-Item .\G8F-Base\lara.fbx .\validation\DazPoseUnityValidation\Assets\TestCharacter\lara.fbx
Copy-Item .\G8F-Base\lara.images .\validation\DazPoseUnityValidation\Assets\TestCharacter\lara.images -Recurse -Force
Copy-Item '.\output\Cherish Genesis 8 Female 16.dazpose.json' .\validation\DazPoseUnityValidation\Assets\TestData\ -Force
```

`G8F-Base\Genesis8Female.dsf` is byte-identical to `fixtures\private\Genesis8Female.dsf` in the current local checkout. Convert the pose with the standalone app first if the `.dazpose.json` is absent or old; regenerate it after changes to the canonical schema.

## Open and inspect

1. Open `validation\DazPoseUnityValidation` in Unity `6000.5.9f1`.
2. Wait for the FBX and texture import to finish. The project AssetPostprocessor sets `lara.fbx` to **Generic** and turns **Optimize Game Objects** off. It leaves the imported skeleton and FBX axis/unit settings otherwise untouched.
3. The committed `Assets\Scenes\PoseValidation.unity` already contains a Lara instance, neutral rest snapshot, camera, light, and floor. Choose **Tools > DAZ Pose > Open Validation Scene** to load it and select Lara. If you need to recreate it from the local FBX, choose **Tools > DAZ Pose > Setup Validation Scene**.
4. Confirm **Lara** is selected in the Hierarchy.
5. Choose **Tools > DAZ Pose > Inspect Selected Character**. Confirm the Console reports all 13 expected skeleton names, including `hip`, `pelvis`, `lThighBend`, `lShin`, `lFoot`, `rThighBend`, `rShin`, `rFoot`, and `head`. This FBX has duplicate hip/spine/head paths under the eyelashes mesh; the tool will warn and disambiguate them by the exact DAZ parent hierarchy.
6. The detailed import report is written to `TestOutput\lara-unity-skeleton.json`. Missing expected names are errors; duplicate names are warnings because the resolver disambiguates by exact DAZ parent ID.
7. Optionally choose **Tools > DAZ Pose > Inspect Pose Bone Mapping** and select the Cherish `.dazpose.json` to save an exact name/ID map before applying.

## Apply and reset the pose

1. Keep **Lara** selected and choose **Tools > DAZ Pose > Apply Pose to Selected Character**.
2. In the file picker, select `Assets\TestData\Cherish Genesis 8 Female 16.dazpose.json`.
3. The Console reports the pose/channel/bone counts, exact-name or exact-ID resolution totals, and neutral-rest landmark-fit error. The detailed matrix and per-bone results are written to `TestOutput\pose-application-report.json`.
4. If all active targets resolve and the neutral rest fit is within 20 mm RMS, Lara updates in the scene to the authored static Cherish pose. Inspect the overall body articulation first, then limbs and fingers. This first check only proves the visible application path is working; it does not establish fidelity against DAZ's posed reference export.
5. Use **Tools > DAZ Pose > Restore Captured Rest Pose** or Unity **Edit > Undo Apply DAZ Pose** to return the character to its imported neutral pose. The captured neutral transforms live on the Lara scene object and persist with the scene.

The calibration fits DAZ rest-bone positions (centimeters) for the pelvis/spine/head, shoulders/arms, and hips/legs to the corresponding actual Unity rest-bone positions (meters). It then converts DAZ world rotation deltas by matrix conjugation and position deltas with that same measured 3×3 basis. The report retains residuals for all 170 bones, including the face and fingers; active targets above 20 mm are called out in Console and the JSON report. The current local run finds 8.2 mm RMS over the 23 calibration landmarks, while the two ears and two big-toe end bones differ by 20–26 mm individually. This is an observed FBX/rest-center discrepancy; no per-bone offsets are added. If pose targets or calibration anchors are missing/ambiguous, or calibration-landmark RMS exceeds 20 mm, application stops and leaves a diagnostic report.

The **Tools > DAZ Pose > Run Adapter Self Tests** command runs nine checks for calibration math, centimeter conversion, reflected-basis conversion, neutral rotation identity, normalized quaternion output, `lThigh` → `lThighBend` exact-name resolution, and explicit missing-bone diagnostics.

## Evidence for a corrective pass

If the result is wrong, restore neutral and send back:

- `TestOutput\lara-unity-skeleton.json`
- `TestOutput\pose-application-report.json`
- Unity Console errors/warnings from the operation
- a screenshot showing Lara and the incorrect body regions

Do not change random FBX axis/import settings. The reports include each transform path, local/world transforms and matrices, exact mapping status, root scale, the derived world basis, and per-bone rest-fit residuals so the mismatch can be traced to source evaluation, bone resolution, coordinate conversion, root/unit scaling, or the FBX rest skeleton.
