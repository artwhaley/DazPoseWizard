# DazPoseWizard — Native Scene & Game View Directing Correction

**Status:** revised, executor-ready packet; implementation and native acceptance are still pending. The executor must never run Unity; the author performs all native checks at explicit pause gates.

**Repository:** `https://github.com/artwhaley/DazPoseWizard` · `main` verified at `4656eb56fe1cfa2201104e960b4d7e8dc41cb766` (2026-10-19 05:15:59 UTC). **Unity project:** `validation/DazPoseUnityValidation`, Unity `6000.5.9f1`, HDRP. Before executing, compare `HEAD` with the pinned revision and inspect any differences. Never silently revert or overwrite newer work.

## What the user is asking for

Build **one Edit Mode directing workflow**. The Unity **Scene view** is where the author moves real staging and camera anchor objects with standard transform gizmos; the Unity **Game view** is the continuously updated view from the saved scene's **real player camera**, showing Lara at the current script cue. The Studio window is *control chrome*: source navigation, bindings, step/back/run-to-cue, scenario choices, Director candidates, and status. The author should click through the whole story, pose Lara, adjust standing/sitting/camera targets, and see the shot update **without pressing Play**. Play is later timing/integration verification, **not a second required staging workflow**.

A representative **Art-operated** acceptance path, all in Edit Mode:
1. Open the existing `Assets/Production/Scenes/FirstContact.unity`, then Studio and the standard Scene and Game tabs.
2. Select the `SexyVamp` cue. Lara is visibly staged in the real scene at her preceding mark, in her pose, and the Game view shows the active player-camera shot.
3. Select `stepforward` in the Hierarchy or via Studio. Drag it with Unity's normal Move/Rotate gizmo; Lara and the Game view respond continuously.
4. In **Warmup**, stage `Lara.SitAt(Lounger)` and then `Lara.PersistentSitPose(Lounging)`; these are FirstContact's **first seating** instructions. Inspect the real CrossLegs/default SitAt rest and then the separately hip-corrected Lounging posture. In **Main**, also test the later `Lara.DissolveTo(Lounger, seated: Lounging)` after a Director-loop visit. Drag the seat anchor; the Game view reflects the adjusted settled seat.
5. Select `Camera.StartAt(camera_start)` and `Camera.Track(Lara)`. The **saved `PlayerView` rig** takes its evaluated endpoint, not a Studio camera. Drag the authored camera mark; Game view updates immediately. Persistent tracking aims at `laraFocus` **after** sampled Lara pose is applied.
6. Save the scene normally: anchor edits persist, **rehearsal-only** actor pose, transform, visibility, shader and camera state do not. Stop rehearsal: baseline Lara/camera state is restored; saved marks stay edited. Undo/redo an anchor edit restages correctly.

## Read in this order

1. `PRODUCT_CONTEXT.md` — what this app is and why these decisions matter.
2. `CODEBASE_EVIDENCE.md` and `ARCHITECTURAL_DECISION.md` — the pushed code seams, authoritative model, ownership, endpoint rules and save safety.
3. `CUE_BEHAVIOR_MATRIX.md` — detailed authoring behavior for every supported command family.
4. `TICKET_ORDER.md` → `tickets/N00` through `tickets/N11` — **execute in order**, with validation at each gate.
5. `MANUAL_GATE_PROTOCOL.md`, `AGENTS_MD_HANDOFF.md`, `REVIEW_RESOLUTIONS.md` — mandatory handoffs and reconciliation of all three reviews.
6. `ACCEPTANCE_MATRIX.md`, `UNITY_API_AND_RISK_NOTES.md`, `REVIEWER_BRIEF.md` and `REVIEWER_PROMPT.md` — proof, current API checks and reviewer questions.
7. `EXECUTOR_PROMPT.md` — paste this into the implementation agent.

## Non-negotiables

- **DO NOT** reproduce a scene, render it using `PreviewRenderUtility`, add custom orbit controls, or implement another camera view.
- **DO** operate on the open, actual saved Unity scene, visible through its normal Scene/Game tabs. No Play required for cue-end framing.
- **DO** use the existing canonical `StoryMachine`/`StoryDirector` and global tag-discovered selectable cards. No scene-specific card libraries; new tagged cards can appear in old stories automatically.
- **DO NOT** regenerate, retune or rewrite authored First Contact, frozen Void, Lara rig/meshes, prepared male body, wardrobe or content, except as a separately approved *deliberate author edit*.
- **THE EXECUTOR MUST NEVER LAUNCH OR OPERATE UNITY.** No Editor GUI, CLI/batch-mode, `-executeMethod`, EditMode tests initiated via Unity, Play, build, standalone player or computer-use Unity, even on a copy. Art alone opens Unity, compiles/imports in Unity, clicks temporary diagnostic menus and performs native checks. See `MANUAL_GATE_PROTOCOL.md`.
- **HARD PAUSE:** when a ticket depends on Unity, finish source work and the diagnostic/handoff, report `WAITING ON ART`, give an exact short click checklist and await Art's reported result. No implicit permission from a PARTIAL status to skip a gate or continue blind. A failed native gate stops dependent work.
- **Protect shared assets:** a copied scene still references shared binding assets/global indexes, which may mutate on import/reconciliation. Test in a separate isolated project copy or request an explicitly approved migration/baseline; copying just the scene is insufficient.
- **Authority:** acceptance of this packet authorizes implementation and logical **local commits**, but never `git push`. Do not interpret obsolete `AGENTS.md` packet references as permission to ignore its Unity prohibition. Update `AGENTS.md` top active-packet reference explicitly at N00 with no relaxation of the ban.
- This is a replacement of the **visual authoring adapter**. Preserve the successful source-first compiler, managed binding reconciler, scenario/Director logic, and transactional UI.

## Definition of done

The author can stage the linear sequence of cues in the *real* Scene/Game views, make and Undo native authored mark edits, inspect global-card choices, save safely, and resume with no second composition pass. No custom Studio viewport remains. Native Unity import, Game-view rendering, seat/pose/mark behavior and save/Undo safety must actually be verified; managed tests alone are not sufficient. A missing native gate is **PARTIAL**, not PASS.
