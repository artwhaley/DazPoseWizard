# G2 — standing, camera and save check

**G2 v1 FAILED. v2 repair is ready for your retry. No screenshots required. G1 remains accepted.**

## Open exactly this

Keep using the **isolated project**:
`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\native-directing\G1Project`

Open this scene by double-clicking it in Unity's Project panel:
**Assets > __NativeDirectingFixtures > G1_FirstContact**.
Its full path is:
`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\native-directing\G1Project\Assets\__NativeDirectingFixtures\G1_FirstContact.unity`

Let current code import/compile. Keep Scene and Game tabs visible. Stay in Edit Mode. No StorySceneBindings selection or Studio setup is needed. The diagnostic finds **Game Session** itself. If G1 Camera Proof is still shifted, click its **Restore Camera Rig** first. If the scene has an unsaved asterisk, save the disposable scene **before** starting G2.

## Five steps

1. Menu **DazPose > Story > Native Directing Diagnostics > G2 Standing and Save Proof**. Click **1. Start standing proof — SexyVamp**, then confirm **Start standing proof**. The panel header must say **G2 v2**. Lara is sampled at stepforward in the source-bound SexyVamp pose; the camera now uses your saved camera_start orientation. Your previous camera edits are preserved, so it may currently point away from Lara. Try the **Standing endpoint** dropdown: DissolveIn(lara_start), locomotion idle, SexyVamp, standing Lounger dissolve. The **Camera orientation** dropdown starts on **Use camera_start rotation**. Select **Track Lara — overrides mark rotation** to aim at Lara, then return to **Use camera_start rotation** for the rotation test. Tracking intentionally supersedes mark orientation; it is now clearly labelled.
2. **Before editing anything**, press normal **Ctrl+S**, then click **3. Force SaveScene — disposable scene only**. This forces a real serialization even if clean Ctrl+S does nothing. The shot should return after saving; no error should appear. The diagnostic records raw bytes and checks scene data equivalence (only empty-string/line-ending normalization allowed), plus actor/camera snapshots during save. Do not click Start again to recover the shot; failure to resume is still FAIL.
3. Click **2a. Select stepforward**. Unity selects the actual Hierarchy object named stepforward. In **Scene**, press **W** and drag a Move handle; **E** rotates it. Watch Lara and Game update. Click **2b. Select camera_start**, then move it the same way; watch the real shot update. Keep **Use camera_start rotation** selected to test angular edits. No Save/Start is needed for either handle drag. Press **Ctrl+S**. Use **Ctrl+Z** then **Ctrl+Y** on a mark edit; confirm the mark and staging follow. A deliberate Lounger placement move may also be used to check new chair prefab overrides survive.
4. Move a mark **again without saving**, then click **4. Stop**. Lara/camera should restore; your latest mark edit and scene asterisk must remain. For the reopen check, save that deliberate mark edit after Stop, then reopen **G1_FirstContact** by double-clicking it in Project. Your saved marks should persist; temporary Lara/camera/pose/material values should not. Keep the G2 window open throughout so its baseline/report survives. No Play needed.
5. Tick the four **Art:** observation checkboxes matching what you saw. Click **5. Export report (after Stop)**. Reply **PASS**, **FAIL** or **BLOCKED**, with the report text or Console error if available. No screenshots needed.

Report folder:
`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\native-directing\G1Project\Temp\NativeDirectingDiagnostics\G2_<timestamp>\report.txt`
The G2 panel and Console display the exact generated directory. report.json includes actor/bone/renderer/prefab diffs and save observations; scene text copies support save mismatch investigation. A meaningful scene-data change, actor/prefab change, dirty/Undo change or Console exception is FAIL/BLOCKED for repair, not approval to continue.

**Why this pause:** packet N04 and MANUAL_GATE_PROTOCOL require your G2 PASS before N05 seating work. The executor has only run source/managed checks and copied code; it has not launched or operated Unity. This is a small source-bound technical fixture; full cue navigation is scheduled after G3.
