# Phase 3 — Scene Director Studio product and interaction spec

## Launch / ownership

Menu `DazPose > Story > Scene Director Studio`, generic dockable `EditorWindow`. Accept `.dazstory` source selection or current scene runner as inputs; if no active scene, select/create a production scene (saved scene from art-only Void template). Display (left) outline/source flow, (middle) preview scene camera, (right) command-specific bindings/scene handles, (bottom) transport, state and errors. Four panes resizable, selection/source line synchronization and direct open-in-editor. No cloned story text editor. Source file remains editable in normal IDE; on Unity reimport, window refreshes diff without losing bound choices.

## Script to binding lifecycle

`New Scene from Story...`: author chooses a `.dazstory`; `Analyze` discovers all requirements without any preexisting `.bindings.asset` and can create a typed companion. Source-local alias names, kind and source locations visible. Empty entries (unbound) are ordinary not compiler errors; truly invalid grammar/types remain errors. `Reconcile` preserves all valid assignments and posture metadata, surfaces unused bindings for explicit archival/cleanup. Typed ObjectFields for generated `PerformerPose` wrappers, `PerformerGesture`, `AudioClip`, `PerformerMagicShaderProfile`, `FunscriptMotionProgram`; scene locations and live roles on `StorySceneBindings` with direct selection/authoring. Dragging raw `.anim` into `PerformerPose` field produces useful "use wrapper" diagnostic and does not silently accept.

Source owner identity = `.dazstory` GUID, item identity = `{symbol, type}` or current literal dialogue SHA-256 ID. On source rename/move preserve GUID and binding. New occurrence of existing symbol uses existing assignment; same symbol requested as incompatible types is a compile error, not two conflicting slots. Dialogue text changes invalidate audio mapping as existing policy, but old clip/record remains visible in unused group. No YAML editing, no copying generated ID strings. Bound source shouldn't be mutated when the window simply opens. Explicit `Create Binding Asset`/automatic guarded safe sync manages consistent assets with idempotence.

## Story flow / UI

Outline: `setup`, `card`, `sequence`, `phase`, command lines, nested branches, repeat loops, `Director.DrawAndExecute`; click highlights exact authored source location and readable original line. Show current source line, command arguments, status and expected post-state. Allow filters: Unbound, Warnings, Locations, Assets. Search symbols and dialogue.

Transport: `Reset`, `Back`, `Step`, `Preview Step` with duration scrub, `Run To Selection`, `Run To Next Decision`, `Continue` (fast simulated), `Stop Rehearsal`, `Seed` input, `Path Overrides` panel, `Save Rehearsal Scenario`/`Load Scenario`. One selected rehearsal path at a time. All visual-only stepping should be bounded/no infinite loop; loops with waits are rendered as events/transitions, not require real wall-clock completion. `Run To Here` stops at unresolved input/branch/event/card and asks for explicit decision or uses seeded settings.

## Contextual preview details

- `Lara.PersistentPose(SexyVamp)`: pose at after-previous-command position, scrub actual imported clip and blend metadata; keep object at correct facing. Assign wrapper by drag/drop. Shows expected World.Posture metadata.
- `Lara.LocomoteTo(stepforward)`: start/end markers and facing arrows in world, path/turn and arrival preview using actual movement clips or a clearly labelled static trajectory overlay if speed/animation is unsupported in EditMode; never animate an unrelated generic mannequin.
- `Lara.DissolveTo(Lounger)` and `seated: Lounging`: cloned Lara resolves exact scene Lounger placement+seat and pose, not a fake standing-at-chair intermediate. Show target seat anchors and collider/wall intersections. If seat missing, unresolved red slot rather than substitute `SitAt`.
- `Lara.Say("...")`: verbatim transcript + assigned AudioClip waveform/time/audio playback at author request, safe facial sampling if capable; no user profile writes.
- `Lara.MagicShader(GlowyWireframe)`: sample performer-owned material profile on cloned renderers, restore cleanup; don't rewrite shared material assets.
- `Camera.Track(Lara)` / `Camera.StartAt(camera_start)`: switch Orbit vs Player camera, show actual camera frustum and facing; focus mark visible. Scene view camera editor never changes game camera prefab until Commit.
- `PlayerBody.StartHiddenAt(player_start)` / `PlayerBody.DissolveIn()`, `Lara.Grip(Penis)`: display exact prepared male body/contact targets and staged hand position; no toy output/contact IK solver side effects on active production rig. Preview contact result only when safely supported; otherwise display explicit anchor geometry and diagnostics.
- `MotionDriver.PlayFunscript`: render position-vs-time curve and sample toy-neutral motion in preview; do not output Bluetooth signals.
- `Popup.Show`, `UI.ShowAction`, `WaitAny`: display mock readable UI state/actions and allow editor to inject simulated response/event. No real settings or player profile changes.

## Typed locations

`StoryLocationBinding` is an authoring object with `id`, `placement Transform`, optional `PerformerSeat`, `focus Transform` and descriptive role/type where necessary. IDs come from script requirements, not manually typed object lists. Placement-specific editor actions: `Create Mark Here`, `Choose Existing Transform`, `Frame Mark`, `Frame Actor At Mark`, `Align Mark to Current Scene View`, `Capture Player Camera to Mark`, `Adjust facing`, `Preview Arrival`, `Commit Position/Rotation`. Creation/editing of real mark and host bindings happens **only** via Undo; no automatic creation on a mere preview visit. Generated mark starts unresolved until explicitly placed/saved; inspector displays world/local coordinates. `Lounger` standing vs seated behaviors resolve same semantic object.

## Preview stage

Use a dedicated Editor-only preview stage/renderer rather than changing active saved scene actors. Clone geometry/actors from saved scene into isolated preview owner; disable or do not clone gameplay session/bootstrap components, event handlers or toy services. Preview Lara is real Lara model and authored animation, *not a proxy*. Cache original-object-to-preview-object map, scene locations resolved by ID to clones/coordinate snapshots. No real live profile, persistent application/session object or physical toy connection.

Preview state has transient performer pose/visibility/placement, player body, camera, virtual mood/contract/world, simulated UI answers, card selection trace, deterministic RNG. No direct `SetDirty` on source prefab/scene; materials are preview instances restored/disposed. Ticks only modify preview-owned clones/temporary data. EditorWindow teardown clears animation mode/Playables/timelines and preview resources; test that closing twice is safe. Do not create new saved `.unity` fixture for each preview.

## Decisions

At `if`, show predicate, current inputs/value and branches; choose follow actual or force chosen path. Override flags are clearly tagged "FORCED REHEARSAL" and never persist into the authored program; coherent state override may be used when possible, otherwise trace forced branch decision distinctly. No accidental overwriting of actual user profile.

At `Director.DrawAndExecute`, show current query tags, all globally eligible/ineligible cards, requirements, physical state and rejection reason, weight and seeded selected card. Choose `Use seeded selection` or `Force eligible card`; force choice only if it passes hard requirements and stage resources; never make a physically illegal card legal by selecting it. Step *into* selected global card and return. No scene-registered library and no separate rehearsal Director; one canonical `StoryDirector` with debug selection seam.

At `WaitAny` / `UI.Pressed` / `MotionDriver.Ended`, offer synthetic events and race winner selection. Simulated timeline may advance `Finisher1` to natural end; user can force press before; state/event lease generation rules mirror runtime; no event leakage. No hardware.

## FirstContact acceptance

Open saved `FirstContact.unity` and actual `FirstContact.dazstory`, don't rebuild or reset. Click through `Intro` with full 19 dialogues visible as needed, `SexyVamp`, seated Lounger, `GlowyWireframe`; preview after MainPosition1 and Bedside without altering original transforms; select none/two/three clothing branches; inspect Warmup cards globally (including newly added test card); step into selected card; inspect standing and sitting Main, climax simulated early vs finish/loop, Resolution, final goodbye. A test run must make zero persistent scene/prefab/profile source changes absent explicit author Commit. Validation both source-driven and actual Editor staged preview.
