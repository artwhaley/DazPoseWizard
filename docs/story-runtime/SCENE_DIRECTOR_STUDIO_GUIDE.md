# Scene Director Studio — FirstContact authoring

The Studio edits the script's asset bindings, poses and scene transforms in Edit Mode. Art accepted the practical authoring check on October 19. Detailed edge-case evidence remains limited; see [historical acceptance evidence](../archive/docs/visual-authoring/IMPLEMENTATION_REPORT.md).

## Open the real scene

1. Open Unity 6000.5.9f1 with project `C:/Users/artwh/OneDrive/Documents/DazPoseWizard/validation/DazPoseUnityValidation` (the main project, not the independent G1Project).
2. In Project, open **Assets/Scenes/FirstContact.unity**.
3. Keep the normal **Scene** and **Game** tabs visible. Remain in Edit Mode.
4. Open **DazPose > Story > Scene Director Studio**. It finds the scene and source automatically. If needed, expand **Scene / script** and click **Find open scene**.

The code is already in this project. Fixture scene/art changes are separate; see [historical production handoff](../archive/docs/visual-authoring/PRODUCTION_HANDOFF.md).

## Select and edit a cue

The left **Script cues** sidebar shows the source with wrapped, left-aligned text, grouped by declaration. Search filters it. Drag the divider to resize it.

Click a row. Its inspector appears on the right, and its resolved pose/placement/camera state appears in the real Scene/Game views. **Previous cue**, **Next cue** and **Before this cue / After this cue** select source endpoints. No Start directing step, story playback, seed or branch response is required.

For the first pass, search **SexyVamp** and select **await Lara.PersistentPose(SexyVamp)** under **card Intro**. Use **After this cue**:

- Drop a generated **PerformerPose** asset from Project into the **SexyVamp** slot. Raw .anim clips are not that slot's asset type. Keep appropriate World posture metadata. This edits a shared named binding: other uses in the same source use it too.
- Under the inherited performer position, click **Select stepforward**. In Scene view, **W** moves and **E** rotates the actual mark. **Frame** locates it. With **Live drag off**, preview updates on release; optionally enable Live drag for continuous placement updates.
- Under inherited camera position, select **camera_start** and move it to reframe. At this cue the script tracks Lara's actual head target. To tune the mark's own explicit rotation, select setup **Camera.StartAt(camera_start)**, before tracking is enabled. The contextual **tracking target offset** control edits Lara's actual authored focus offset.
- Dialogue cues retain their inherited shot and expose their audio assignment. Use Unity's audio preview controls deliberately; selecting a cue does not run timed speech.

All relevant assignments stay in this inspector. A missing scene mark provides a create button or accepts an existing Transform dragged from the same scene's Hierarchy. Pose/assets come from Project. Use **Create missing asset slots** only if that source companion/row is missing. Source-owned aliases remain separate between files.

## Seated cues

Search **Lounging**. Warmup's **Lara.SitAt(Lounger)** uses the seat's default rest and baked landing root. The following **Lara.PersistentSitPose(Lounging)** uses the assigned seated pose and sampled hip alignment. Main's **Lara.DissolveTo(Lounger, seated: Lounging)** is another explicit seated endpoint.

The inspector exposes **Seat / chair**, **Seat position** and **Approach position**, with assignment/Select/Frame/create controls. Move the settled seat position to move the seated pose. Approach is the travel approach, not a separate settled camera target. Changing a shared Transform affects every field bound to it; inspect the assignments when a handle has more than one use.

## Logic and warnings

Studio indexes visual assignments and skips choice/loop execution. A permanent visual write inside a choice violates the authoring rule and makes the affected downstream field unknown. A dynamic card can also make incoming visual state unknown. Explicit later assignments resolve the fields they establish.

Unknown fields show their saved scene baseline with a warning, independently of which cue was selected previously. Other known fields and binding controls remain available. Rows inside skipped blocks are labelled as local editing context. Cards are selectable directly; no random Director draw is needed. Mixed runtime base/seated animation layers may be unresolved and are disclosed.

## Save, Undo and Stop

**Ctrl+S** saves deliberate mark/binding edits while the preview owner restores temporary properties before serialization and resumes the selected shot afterward. Pose assignments use Unity Undo and save the owned companion. **Undo/Redo** should update the current cue. **Stop preview** restores preview-owned actor, camera, bones, visibility and material state while retaining deliberate authored edits.

For the first real editing session, move one mark, Save, Undo/Redo and Stop once before a long pass. Report any discrepancy with its exact cue/action; extended lifecycle/material/sparse-pose cases have not all been individually certified. Do not use blanket prefab override resets or clear dirty state to fix a discrepancy.

## Fine-tuning pass

Start with Intro, one cue at a time: arrival, stepforward/SexyVamp, dialogue framing, Lounger arrival and LeaningForward. Then tune Warmup's seating and continue through Main, Climax and Resolution. Preserve the fixed transcript unless intentionally changing it.

The editor shows settled visual authoring endpoints. Later, Art runs the actual scene in Play to inspect timed travel, transitions, dialogue/lip sync and dynamic contact/motion. Record any endpoint disagreement as a defect, alongside the exact cue. The executor never launches or operates Unity or hardware.
