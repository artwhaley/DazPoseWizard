# G4 — responsive visual authoring

Status: **WAITING ON ART after source delivery**. G4 remains unaccepted. This replaces the old runtime-rehearsal navigation checks. Art operates Unity; no screenshots required.

Open the existing isolated project in Unity 6000.5.9f1:
`C:/Users/artwh/OneDrive/Documents/DazPoseWizard/.dazposewizard/native-directing/G1Project`

Open **Assets/__NativeDirectingFixtures/G1_FirstContact.unity**. Preserve your existing edits. Let code import; keep Scene and Game visible in Edit Mode. Stop any G2/G3 proof preview. Open **DazPose > Story > Scene Director Studio**.

## First check — the previous blocking problem

1. Search **SexyVamp**, select **await Lara.PersistentPose(SexyVamp)** under **card Intro**, and use **After this cue**. Lara should be at stepforward in the assigned pose, camera tracking her head. Clear search and select the next dialogue row: same shot, no pose rebuild.
2. Search **Sassy** and select **await Lara.PersistentPose(Sassy)** under **sequence FirstContact** near the end of the root script. Jump back to Intro, then back to Sassy. These must select promptly, without Finding cue, questions, waits, loops, or random choices. Sassy should be at lara_start after its explicit standing dissolve. The right inspector should follow every selected row immediately.
3. Search **Lounging** and select **await Lara.PersistentSitPose(Lounging)** under **sequence FirstContact** (the first Warmup occurrence). Test the preceding **await Lara.SitAt(Lounger)** too. Then select **await Lara.DissolveTo(Lounger, seated: Lounging)** in Main. The two seating rules should remain correct.
4. At SexyVamp, drop another valid **PerformerPose** into its pose slot. The selected cue, placement and camera must remain; the pose changes. Undo/Redo. Use **Select stepforward**, W/E, move/rotate and release: handles should remain smooth with **Live drag off**. Try Live drag on if desired. Repeat pose replacement and seat position movement at the known Lounging cue.
5. Select setup **Camera.StartAt(camera_start)** to test the mark's own rotation. Later Track(Lara) cues intentionally aim at Lara. At either cue select camera_start in the inspector and move it. Finish with Ctrl+S, check preview resumes, then Stop preview: authored edits persist and temporary actor/camera/pose state restores. Reopen the saved scene to check serialization.

Report **PASS**, or **FAIL** with the cue/action that failed. No screenshots needed. Do not accept minute-long or multi-second normal navigation; native timing is not established by managed tests.

## Ambiguity and full save/Undo regression

All source cues, including global cards and conditional bodies, remain editable. Skipped blocks are labelled. An unresolved field shows its saved baseline plus a warning; an explicit later assignment resolves it. No variable/branch/RNG controls remain.

Full checklist: `newtickets/DazPoseWizard_Visual_Authoring_Execution_Packet/CHECKS_AND_ACCEPTANCE.md`. Open **DazPose > Story > Native Directing Diagnostics > G4 Full Regression Export** for event counters, forced clean save and baseline comparison. Begin G4 before preview when recording. Cue observations include index builds/statements, lookup/projection/request timings, stale request count, pose samples and spatial updates. Dialogue with identical state should add zero samples; mark editing should add only spatial updates.

The panel's explicit **Add isolated scratch global card** action is optional to exercise ambiguity. It creates NativeG4Scratch only in this independent project. Select that card directly in Studio: SitAt establishes the seat, the wait after its conditional displays unknown pose/placement, and its following unconditional SitAt/pose resolve them. Finish with **Remove scratch and check isolated index baseline**. Do not modify production source to manufacture this test.

Exercise forced/normal Save, Undo/Redo, unsaved mark Stop, source/scene/window close, manual compile/reload, independent missing bindings, sparse pose behavior, wardrobe/heels/materials and prefab overrides before final G4 acceptance. Previous handoff is preserved as G4_REHEARSAL_HANDOFF_HISTORICAL.md; its scenario/Director instructions are superseded.
