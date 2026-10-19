# G2 — standing, camera and save check

**WAITING ON ART. No screenshots required. G1 is accepted from your reported PASS.**

## Open exactly this

Keep using the **isolated project**:
`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\native-directing\G1Project`

Open this scene by double-clicking it in Unity's Project panel:
**Assets > __NativeDirectingFixtures > G1_FirstContact**.
Its full path is:
`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\native-directing\G1Project\Assets\__NativeDirectingFixtures\G1_FirstContact.unity`

Let current code import/compile. Keep Scene and Game tabs visible. Stay in Edit Mode. No StorySceneBindings selection or Studio setup is needed. The diagnostic finds **Game Session** itself. If G1 Camera Proof is still shifted, click its **Restore Camera Rig** first. If the scene has an unsaved asterisk, save the disposable scene **before** starting G2.

## Five steps

1. Menu **DazPose > Story > Native Directing Diagnostics > G2 Standing and Save Proof**. Click **1. Start standing proof — SexyVamp**, then confirm **Start standing proof**. Real Lara should appear at stepforward in the source-bound SexyVamp pose, through the real player camera. Try the **Standing endpoint** dropdown: DissolveIn(lara_start), locomotion idle, SexyVamp, standing Lounger dissolve. Toggle Camera.Track(Lara) off/on to compare StartAt and tracking. Return to SexyVamp with tracking on.
2. **Before editing anything**, press normal **Ctrl+S**, then click **3. Force SaveScene — disposable scene only**. This forces a real serialization even if clean Ctrl+S does nothing. The shot should return after saving; no error should appear. The diagnostic checks byte identity against the clean baseline and actor/camera snapshots during save.
3. Click **2a. Select stepforward**. Unity selects the actual Hierarchy object named stepforward. In **Scene**, press **W** and drag a Move handle; **E** rotates it. Watch Lara and Game update. Click **2b. Select camera_start**, then move it the same way; watch the real shot update. Press **Ctrl+S**. Use **Ctrl+Z** then **Ctrl+Y** on a mark edit; confirm the mark and staging follow. A deliberate Lounger placement move may also be used to check new chair prefab overrides survive.
4. Move a mark **again without saving**, then click **4. Stop**. Lara/camera should restore; your latest mark edit and scene asterisk must remain. For the reopen check, save that deliberate mark edit after Stop, then reopen **G1_FirstContact** by double-clicking it in Project. Your saved marks should persist; temporary Lara/camera/pose/material values should not. Keep the G2 window open throughout so its baseline/report survives. No Play needed.
5. Tick the four **Art:** observation checkboxes matching what you saw. Click **5. Export report (after Stop)**. Reply **PASS**, **FAIL** or **BLOCKED**, with the report text or Console error if available. No screenshots needed.

Report folder:
`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\native-directing\G1Project\Temp\NativeDirectingDiagnostics\G2_<timestamp>\report.txt`
The G2 panel and Console display the exact generated directory. report.json includes actor/bone/renderer/prefab diffs and save observations; scene text copies support save mismatch investigation. An unexpected byte change, actor/prefab change, dirty/Undo change or Console exception is FAIL/BLOCKED for repair, not approval to continue.

**Why this pause:** packet N04 and MANUAL_GATE_PROTOCOL require your G2 PASS before N05 seating work. The executor has only run source/managed checks and copied code; it has not launched or operated Unity. This is a small source-bound technical fixture; full cue navigation is scheduled after G3.
