# G1 — Art-operated camera feasibility check

Status: **WAITING ON ART**. N00 is complete; N01 source is ready. The packet's MANUAL_GATE_PROTOCOL requires a hard pause here. N02 and all later tickets remain unstarted until Art confirms G1 PASS. The executor has not launched or operated Unity.

## Open this independent project

`C:/Users/artwh/OneDrive/Documents/DazPoseWizard/.dazposewizard/native-directing/G1Project`

Use Unity **6000.5.9f1**. This is a physical project with its own Assets, Packages, ProjectSettings and package cache. Its disposable scene and shared content/index are independent of the authoritative validation project. Current code and metas are synchronized; `.native-directing-fixture.json` records the source commit and code fingerprint. The panel verifies the fingerprint before staging. Let Unity's manual import/compilation finish. Initial import may take time because this independent project has no imported artifact cache.

## Five manual steps

1. Open `Assets/__NativeDirectingFixtures/G1_FirstContact.unity`. Use a single loaded scene; open the standard **Scene** and **Game** tabs. Keep Game visible. Record a baseline Game screenshot. Do not press Play.
2. Optional context check: **DazPose > Story > Scene Director Studio**, click **Find scene/source**, then **Start directing**. Inspect the actual saved Lara, body, Player root, ViewRig, HeadPose and camera references. This N01 screen connects the context; full cue staging waits for later gates. Select the real player camera to inspect its configured hierarchy/HDRP settings.
3. Open **DazPose > Story > Native Directing Diagnostics > G1 Camera Proof**. Click **Validate fixture and bindings (read only)**, then **Shift Camera Rig** and accept its explicit isolated-fixture confirmation. The real root moves sideways 0.75 m and its ViewRig yaws 18 degrees; no extra camera/renderer is created. **Do not save, edit the rig, or press Play while shifted.** Observe Game and capture the shifted screenshot. Tick **Art: shifted Game frame is visibly different** only if observed.
4. Click **Restore Camera Rig**. Observe/capture the restored Game frame and tick **Art: restored Game frame matches the baseline** only if true. The panel must retain the shifted state until this explicit action. Closing it, changing scenes, compiling, Undo/Redo or a user Play transition also triggers cleanup. Any save attempt filters the asset-save path or restores before direct scene serialization and invalidates the proof; this is not the N02 save-safety gate.
5. Enter screenshot paths/notes if useful and click **Export Report**. Its displayed folder and Console contain the exact output path. Return `report.txt`, the two Game screenshots, and **PASS / FAIL / BLOCKED** with any Console errors. If compilation prevents the menu from appearing, return the first Console error instead.

Report directory: `<isolated-project>/Temp/NativeDirectingDiagnostics/G1_<UTC timestamp>/report.txt` and `report.json`. **Read-only Status and Export Report** under the same menu exports current wiring/status without shifting anything. Native reports stay in the fixture's ignored Temp directory.

## PASS / failure and cleanup

PASS requires Art-observed distinct Game images from the real saved camera in Edit Mode, a matching restored Game frame, restored serialized root/rig/actor/renderer/prefab state, preserved dirty status and unchanged saved scene bytes. The report records exact before/shifted/current object JSON and restored differences. Checking boxes does not replace the two actual visual observations. No pose, seating, material or general save-safety acceptance is inferred at G1.

On FAIL/BLOCKED, click Restore if available, close the panel, and send report/Console evidence. Do not repair the authoritative original scene or import new art to make the check pass. The executor will fix source, synchronize it without overwriting fixture content, and request G1 again. If a disposable scene was accidentally edited, report that first; recovery uses the captured independent baseline and preserves original project data.

To independently inspect wiring, select `Player` and its PlayerView, then `ViewRig/HeadPose/MainCamera`. The real camera's projection/FOV/culling/HDRP settings are unmodified by G1. A missing/inactive/competing Game camera produces a diagnostic rather than an automatic repair.
