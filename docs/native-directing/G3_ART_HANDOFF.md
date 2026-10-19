# G3 — seating gate

**WAITING ON ART. N05 source is ready; N06 is held until G3 PASS. No screenshots required.**

## Exact project and scene

Use the same isolated project:
`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\native-directing\G1Project`

Open **Assets > __NativeDirectingFixtures > G1_FirstContact** in Unity's Project panel.
Full scene path:
`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\native-directing\G1Project\Assets\__NativeDirectingFixtures\G1_FirstContact.unity`

Let the new code compile. Keep Scene and Game visible; stay out of Play. Stop G2 if its proof is still running, and save any outstanding fixture mark edits before starting. No Game Session/StorySceneBindings selection is needed. Your saved G2 mark/camera edits are preserved.

## Check

1. Menu **DazPose > Story > Native Directing Diagnostics > G3 Seating Proof**. Header: **G3 v1 — real seating and save**. Click **1. Start G3 — default SitAt**, then **Start G3** in the confirmation. A shows actual default CrossLegs. Inspect the seated contact/facing, press normal **Ctrl+S**, then click **Force SaveScene — disposable scene only** before changing any marks. The shot must return automatically. Tick **Art: A default CrossLegs looks correct** if it does.
2. Click **B. Warmup PersistentSitPose(Lounging)**, inspect, and tick its Art box. Enable **Temporary Lara scale probe**: it temporarily scales her X/Y/Z by 1.10/0.95/1.20. The pose must stay hip-aligned; click **Force SaveScene** while the probe is on and confirm automatic resume. Then test **C. Main seated DissolveTo(Lounger, Lounging)**, tick its Art box if correct, and click **Force SaveScene** for C too. A/B/C must each have a saved baseline observation. You can switch between them repeatedly without pressing Start.
3. With B or C active, click **Select SeatAnchor**, hover Scene and use **W** to move it. Lara and the Game shot must follow live. Click **Select ApproachAnchor** and move that alone: the settled Lara must **stay put**. Click **Select chair — rotate/scale**; use **E** around the Y axis and **R** for a modest scale change. Lara's seating and tracked shot must re-solve. Use **Ctrl+Z / Ctrl+Y** on an anchor edit and inspect both views. No Save/Start is needed for any live update. If needed, press **F** over Scene to frame the selected chair/anchor.
4. Save a deliberate chair/anchor edit with normal **Ctrl+S** while preview is active. It must resume automatically. Then make another anchor edit **without saving** and click **Stop — restore preview, preserve authored chair edits**. Actor pose/camera/temporary actor scale must restore; your latest anchor edit and scene asterisk must remain. Save after Stop, then reopen G1_FirstContact: authored marks/chair overrides persist, rehearsal-only values do not. Keep the G3 panel open through reopening so its baseline/report survives.
5. Tick the remaining Art observation boxes that match your results, then **Export G3 report (after Stop)**. Reply **PASS / FAIL / BLOCKED** and mention any error. No screenshots required. The separate **Standing endpoint — MainPosition1 / initialPose** button is available to inspect standing return; it is a fixed source endpoint, not full story navigation.

## Reading the numbers

- A/default CrossLegs uses the existing baked runtime placement. Its hip-to-SeatAnchor distance is about **30.657 mm at the original unit scale**, so it is not expected to read zero. **Error vs runtime endpoint** should be within **10 mm**; real furniture contact still needs your visual approval.
- B/C Lounging align the actual sampled hip directly to SeatAnchor. Their residual should be within **1 mm**, including the scale probe.
- **Camera orientation** defaults to **Track Lara — overrides mark rotation**, matching the story. Choose **Use camera_start rotation** if you want camera-mark angles to control the shot. Selecting the chair's focus does not change the source cue's Lara tracking target.

Report location:
`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\native-directing\G1Project\Temp\NativeDirectingDiagnostics\G3_<timestamp>\report.txt`
The panel and Console show the exact generated folder. report.json includes contact measurements, actual scale, source lines, saved actor/prefab diffs and observations for all three states. A bad contact solve, save leak, missing binding or dropped preview is FAIL for repair; the executor has not operated Unity.
