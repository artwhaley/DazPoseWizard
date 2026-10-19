# G4 — actual Scene Director Studio

Status: WAITING ON ART. This is the integrated authoring Studio, not G2/G3 fixed shots. G1–G3 are accepted from Art’s reports. No executor Unity operation. No screenshots required.

## Open exactly this

- Unity 6000.5.9f1 project: `C:/Users/artwh/OneDrive/Documents/DazPoseWizard/.dazposewizard/native-directing/G1Project`
- Saved scene: `Assets/__NativeDirectingFixtures/G1_FirstContact.unity` (the same scene you used at G3).
- Stop the G3 preview first. Preserve/save any chair/mark edits you intend to keep. Let code finish importing/compiling. Keep Unity’s ordinary Scene and Game tabs visible. Stay in Edit Mode.
- Open **DazPose > Story > Native Directing Diagnostics > G4 Full Regression Export**. Click **1. Begin G4 — capture baseline, open Studio**. This requires a saved, clean disposable scene and no other preview owner.
- In **Scene Director Studio**, click **Start directing**. It finds Game Session and FirstContact automatically; **Find scene/source** retries. The separate G4 panel records evidence. Neither panel chooses a nicer camera angle.

## 1. Actual source cues and camera

In Studio’s **Cues** tab, type into the search field and click the actual source row. **After cue** is the normal setting; a click replays the same canonical seed/scenario through that exact owner/entry/PC/visit. Clear search to see the surrounding source. **Previous/Next** step canonical instructions. The displayed next instruction is before its execution; the selected-result message identifies the selected after/before result.

Check these in order:

1. Search `DissolveIn(lara_start)` and click the Intro row. Then `SexyVamp` and click **Intro’s** `await Lara.PersistentPose(SexyVamp)` row. Lara must be at stepforward in that pose. The source already executed Camera.Track(Lara), so the real Game camera tracks her actual head-relative focus.
2. Search `GlowyWireframe`, click Intro’s MagicShader row, then select Intro’s `MagicShader(Regular)` row. Inspect both looks. Select the following Say row: the shot and pose must persist.
3. Search `DissolveTo(Lounger)` and choose Intro’s row **without seated:**. This is standing and resets to configured InitialPose. Then search `SitAt(Lounger)` for Warmup’s default CrossLegs endpoint; choose `PersistentSitPose(Lounging)` for the distinct hip-corrected pose.
4. Search `DissolveTo(Lounger, seated: Lounging)` for Main’s later seated endpoint. Canonical replay includes the preceding Director visits. Search `DissolveTo(MainPosition1)` for the later standing return. The G3 focus-selection label is not part of these camera rules.
5. Search `PlayerBody.DissolveIn` / `PlayerBody.DissolveOut` for male-body visibility (later unresolved response may require the Scenario tab below). Search `PlayOneShotGesture`: awaited completion shows the base pose; non-awaited launch is explicitly labelled and does not invent a static terminal gesture pose. Dynamic grip is disclosed as intent, not a solved contact animation.

To edit: **Bindings** tab > Locations filter > choose `stepforward`, `Lounger` or `camera_start` > **Select placement**, **Select SeatAnchor**, **Select ApproachAnchor**, or **Select chair**. Use Unity W/E/R handles or Inspector. The shot must follow during dragging. ApproachAnchor alone must not shift settled Lara. **Select Lara camera focus (head-local offset)** selects the actual Track(Lara) target; its local edit is authored. Mark dragging must keep the current cue/card history; it does not replay the Director.

For the authored camera rotation alone, select setup’s `Camera.StartAt(camera_start)` row: Track has not executed yet. For tracking, select Intro’s `Camera.Track(Lara)` row. The difference comes from script order. There is no product camera-mode override.

## 2. Save and Undo

Before moving any marks in this G4 recording, stage a pose and GlowyWireframe, press normal Ctrl+S, then click G4’s **Force SaveScene — disposable scene only**. The forced clean save must preserve semantic scene data, restore temporary actor/material/camera state during serialization, and resume the same preview shot afterward.

Then move stand/camera/seat marks, including a chair prefab override. Ctrl+S; Undo/Redo; verify the live shot. Stop and reopen the same scene. Only authored mark/focus/override edits should remain, not Lara’s preview pose/materials/body visibility/camera placement. Start again, move a mark **without saving**, then click G4’s **Stop Studio — preserve unsaved authored mark**: the mark must remain moved and the scene must remain dirty.

## 3. Director and response paths

Studio **Scenario / Director** exposes seed, initial clothing, scenario load/save, detached state, pending responses, candidate reasons and per-encounter Force buttons. Click an actual Warmup Director row with **Before cue**, or pause/Next until that call is ready. Force an eligible card for that encounter; Next enters its actual source commands. Inspect a later encounter, force once, replay/Back and confirm choices reproduce. Card return and dialogue must retain the scene composition. Re-anchor and confirm seed/history/selection counts remain unchanged in the report.

For global discovery: G4 **Add isolated scratch global card** stops Studio and publishes a disposable Warmup card in this isolated project only. Restart Studio; pause at a Warmup Director call and Force `NativeG4Scratch` from candidates. Its actual PersistentSitPose must stage in the saved scene. Then G4 **Remove scratch and check isolated index baseline**. It must report the isolated index hash restored. No scene membership or original source is edited.

For climax responses, **New scenario** resets choices. Run or stage toward the wait; choose **Respond: UI.Pressed:ICame** for one path. In another new scenario choose **Respond: MotionDriver.Ended**, then **Simulate ICame** at the subsequent UI.WaitFor. Missing responses pause with choices; no hardware or real prompt is involved. Continue to Resolution. Do not treat an unreachable branch as successfully staged.

## 4. Lifecycle and negative checks (same disposable project)

Stop/close Studio; swap source; reopen it; close/reopen scene; manually trigger script compilation/domain reload. Preview properties must restore. G4 recording/journal survives reload. Animation Window preview must cause Studio to refuse acquisition rather than steal the animation owner. Unity Play is not required for G4; any separate Play/timing/grip trial belongs to Art.

For missing-link checks, Stop Studio first. Select **Game Session** in Hierarchy and its **StorySceneBindings** Inspector: temporarily clear **Lounger’s Seat**; attempt its seating cue and verify an explicit failure. Undo that deliberate edit before restarting. Similarly test an unassigned camera placement and a pose binding in the disposable project, then Undo. Errors must stop transport at the offending source/visit, not disappear behind a later movement/Director. Do not save broken bindings. The managed suite independently covers invalid visual followed by valid movement and Director with zero later effects.

Inspect wardrobe/heels/blendshapes and material refs after save/Stop/reopen; authored settings must survive. This is native acceptance, not established by source tests.

## 5. Return results

In G4 tick only the checks you actually observed. Stop Studio, then **Export G4 report**. The report folder is shown with **Show report folder**:

`<G1Project>/Temp/NativeDirectingDiagnostics/G4_<timestamp>/report.txt`

`report.json` contains snapshots/differences, exact source visits, scenario, director counts, re-anchor observations and saves. `visits.jsonl` preserves cue evidence incrementally. No screenshots required. Reply PASS / FAIL / BLOCKED; on failure give the source cue, action and Console message. A partial export remains useful; unobserved cases remain NOT RUN. A failed G4 blocks completion until repaired.
