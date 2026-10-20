# G4 retry — cue authoring interface

Status: **WAITING ON ART**. Art rejected the first integrated Studio because its interface did not support the requested workflow. This correction is source-checked, not native-accepted. G1–G3 remain historical user acceptance. No screenshots required. Executor never runs Unity.

## First, try one actual authoring pass

Use Unity **6000.5.9f1**, the existing isolated project:

`C:/Users/artwh/OneDrive/Documents/DazPoseWizard/.dazposewizard/native-directing/G1Project`

Open its saved scene **Assets/__NativeDirectingFixtures/G1_FirstContact.unity**. Preserve existing fixture edits. Let Unity finish importing the synchronized code. Keep **Scene** and **Game** visible, in Edit Mode. Stop any G2/G3 diagnostic preview.

1. Open **DazPose > Story > Scene Director Studio**. The window should show **Script cues** on the left and the selected cue's inspector on the right. It finds the saved scene/script automatically; **Scene / script > Find open scene** reconnects if needed. There is no Start directing step.
2. In the left search box, type **SexyVamp**, then click **await Lara.PersistentPose(SexyVamp)** under **Intro**. Leave **After this cue** selected. Lara should appear at `stepforward`, in the assigned pose, with the script's tracking camera. Clear search to see the entire cue list.
3. On the right, the **SexyVamp** block is the pose slot. Drag a generated **PerformerPose** asset from Unity's Project window onto the large assignment field. The same cue should refresh automatically. This edits the named source-owned binding; other uses of SexyVamp in that same source share it. Use Undo if this was only a trial.
4. In the same right panel, find **stepforward — Performer position · carried into this cue**. Click **Select stepforward**, then drag its Move arrows in the Scene view. Lara should follow while retaining the pose. **Frame** locates the mark in Scene view. Press **E** to rotate a selected mark. Undo/Redo should restage the same cue.
5. Still in that panel, find **camera_start — Camera position · carried into this cue**. Select it and move it to reframe the shot. Because this cue follows Camera.Track(Lara), camera aim follows Lara's actual head target. The contextual **Camera aim** block selects that target's local offset. To inspect camera_start's own authored rotation, click **Camera.StartAt(camera_start)** in setup instead; tracking has not yet executed there.
6. Use **Next cue** / **Previous cue**, or click a different row on the left. No Bindings tab is involved. A following dialogue cue should retain the shot and expose its dialogue audio plus the pose/marks already in effect. Try the Warmup **SitAt(Lounger)** and **PersistentSitPose(Lounging)** cues: their inspector exposes **Lounger**, the seat, seat position and approach position. Drag the pose into the Lounging field and select/move the seat position from that panel.

For a missing placement, its block has **Create <name> mark**, which creates an ordinary scene Transform at the Scene view pivot and binds/selects it. Alternatively drag an existing Hierarchy Transform into **Scene mark**. A bound seat exposes separate **Seat position** and **Approach position** object fields, select/frame buttons and create buttons if an anchor is missing. Assign an existing chair's PerformerSeat in **Seat / chair**; seat profile creation/baking remains the content workflow.

**Stop preview** restores preview-owned actor/camera/material properties while retaining authored binding/mark edits. Normal **Ctrl+S** saves authored changes with temporary preview properties restored during serialization, then resumes the shot. Pose binding assets are saved on assignment and remain Undoable. This full interaction still needs Art's native check.

If this interface still prevents the single-pass workflow, report the cue and action; G4 stays failed. No screenshots needed.

## Context and path choices

The left list includes setup, sequence and linked card source cues. Compiler-only return/jump bookkeeping is hidden. Lines wrap and align left; drag the vertical divider to resize the sidebar. Search matches both section name and cue text.

**Before this cue / After this cue** selects the desired endpoint. Navigation uses the canonical story, including its actual branches and Director rules. Unreachable alternatives are reported; no invented pose/camera is substituted.

On a **Before** branch cue, the inspector exposes condition choices. On a **Before** Director call, it exposes eligible card choices. Selecting a candidate records that occurrence's preview choice; use **After this cue** to execute it. When navigation waits for a response, response buttons appear in the inspector. **Stop preview** also cancels a waiting traversal.

**Preview path options** contains **Loop occurrence** and **Random seed** for alternate paths. **Advanced playback / diagnostics** retains scenario load/save, initial clothing, reset, run/pause, state and trace for full regression; these are optional controls outside the everyday cue/binding workflow. Random cards that are not on the chosen path require a corresponding eligible card choice. The Studio never bypasses eligibility or changes production RNG rules.

## Remaining G4 regression, after the authoring workflow is usable

Open **DazPose > Story > Native Directing Diagnostics > G4 Full Regression Export**. Stop Studio preview, save intended authored changes, then click **1. Begin G4 — capture baseline, open Studio** in the clean disposable scene. This begins evidence recording; select cues in the actual Studio as above.

- Check Intro's **GlowyWireframe**, following **Regular**, and dialogue persistence. Check standing **DissolveTo(Lounger)** resets InitialPose, Warmup default **SitAt(Lounger)**, its separately hip-corrected **PersistentSitPose(Lounging)**, Main's later **DissolveTo(Lounger, seated: Lounging)** after Director visits, then **DissolveTo(MainPosition1)**. Confirm actual PlayerBody visibility, gesture disclosures and grip intent without hardware output.
- Before changing marks in the recording, stage a pose/material, press Ctrl+S, then click **Force SaveScene — disposable scene only** in G4. Preview must resume. Move/save/Undo stand, camera and seat marks, including chair prefab overrides. Stop/reopen: only authored changes should persist. Make an unsaved mark edit and use **Stop Studio — preserve unsaved authored mark**: the edit and dirty state must survive.
- On a Warmup Director call choose **Before this cue**, choose an eligible card in the inspector, then **After this cue**. Revisit/replay and confirm the same occurrence choices. Re-anchoring must not redraw cards or change the seed/history.
- For global discovery, G4 **Add isolated scratch global card** stops preview and creates a scratch card only in this isolated project. Preview a Warmup Director call before execution, choose `NativeG4Scratch`, then execute it. Finish with **Remove scratch and check isolated index baseline**. No original content/index changes.
- For climax paths use advanced **New scenario** as needed. Navigate toward the wait; choose `UI.Pressed:ICame`, or in another scenario `MotionDriver.Ended` then the subsequent `ICame` response. Continue to Resolution. Unreachable paths remain explicit failures, not accepted previews.
- Stop/close Studio, switch source/scene, reopen, and manually trigger compilation/reload. Baselines must restore. Animation Window ownership must be respected. Temporarily clear a seat/camera/pose binding in this disposable project, verify an explicit error, then Undo. Missing native input validation occurs at the offending command; failures discovered only by actual native sampling remain endpoint failures and block acceptance.
- Inspect wardrobe, heels, blendshapes and materials after Stop/save/reopen. Retained preview materials must be detached from scene serialization and destroyed on final Stop. No Play is required for this gate.

Tick only the checks actually observed in G4, Stop Studio, then **Export G4 report**. **Show report folder** opens:

`<G1Project>/Temp/NativeDirectingDiagnostics/G4_<timestamp>/report.txt`

`report.json` / `visits.jsonl` retain source/visit, scenario, re-anchor and save evidence. Return PASS / FAIL / BLOCKED and the failing cue/action/Console message if applicable. N11 remains blocked until actual G4 acceptance.
