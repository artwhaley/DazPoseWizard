# Product and project context — for a fresh reviewing agent

## What the application is

**DazPoseWizard** is a Unity/HDRP interactive, performance-driven scene application. A scripted succubus performer, **Lara**, appears in a deliberately authored void-like environment. The player sees her through a scene-bound **player view/camera rig**. Lara can assume imported DAZ poses, move between named scene positions, sit in authored furniture, speak with linked voice/lip sync, use shader/dissolve effects, manipulate wardrobe/physical contact, and interact with optional external toy motion/output. Scenes include interactive dialogs and a **Director** that chooses tagged performance cards according to player interests, equipment availability, current physical state, weight/cooldown and phase progress. It is intended to support a growing catalog of scenes and hundreds of reusable cards for years, with low-cost content authoring.

There are two distinct interfaces:
- **Player-facing UI**: the Main Menu, settings, searchable Interests/Equipment, in-session prompts and controls. Phases 1–3 introduced this and it is **not** the subject of the current replacement.
- **Creator-facing Scene Director Studio**: Unity Editor tooling to bind content and **stage/rehearse an entire performance cue by cue**, without having to press Play to inspect each shot. That is the requested correction.

## Repository and main components (verified on pinned commit)

- Unity project: `validation/DazPoseUnityValidation/Assets/` (Unity 6000.5.9f1, HDRP).
- Saved production entry scenes: `Assets/Production/Scenes/MainMenu.unity`, `Assets/Production/Scenes/FirstContact.unity`.
- Saved art-only environment template: `Assets/Production/Environments/VoidEnvironment.unity`; untouched artistic source `Assets/Scenes/FirstPerformanceVoid.unity`.
- Source choreography: `Assets/Production/Story/Programs/FirstContact.dazstory` and adjacent `FirstContact.bindings.asset`.
- Global selectable content: `Assets/Production/Story/Cards/*.dazstory`; index at `Assets/Resources/DazPose/GlobalStoryCards.asset` built by `DazPose/Editor/Application/GlobalStoryCardIndexBuilder.cs`.
- Source/compiler: `DazPose/Runtime/Story/Language/` (`DazStoryCompiler`, `StoryMachine`, `StoryDirector`, `StoryRehearsal`).
- Unity runtime adapter: `DazPose/Runtime/Story/Runtime/PerformanceStoryHost.cs`, `StoryProgramSequence.cs`, `StorySceneBindings.cs`; content reference owner `StoryContentBindings.cs`.
- Edit-time text, binding and rehearsals: `DazPose/Editor/StoryStudio/SceneDirectorStudioWindow*.cs`, `RehearsalSession.cs`, `RehearsalStoryHost.cs`, `SourceBindingReconciler.cs`, `SceneMarkAuthoring.cs`.
- **Broken architectural branch to retire:** `DazPose/Editor/StoryStudio/ScenePreviewSession.cs` contains `PreviewRenderUtility`, a private copied hierarchy, duplicated materials/lights/cameras, custom camera/orbiting, pose/seat and camera approximation. The Studio window shows that internal viewport. A corrective sprint improved that system without fixing its premise.
- Real player rig: `Assets/Player/Runtime/PlayerController.cs` and `PlayerView.cs` (note: *outside* `Assets/DazPose`). `PerformanceSceneBindings.Player` binds it. `PlayerView` has a root position, separate `viewRig` rotation, `headPose`, and `MainCamera`. Real camera coordinates cannot safely be inferred from an unrelated editor camera.
- Real Lara: `SuccubusPerformer`, generated `PerformerPose` assets; `StorySceneBindings.lara` and `laraFocus`. Standing marks and `StoryLocationBinding` resolve through `StorySceneBindings.locations`.
- Real sitting: `PerformerSeat` with separate `ApproachAnchor`, `SeatAnchor`, `PerformerSeatingProfile` and baked motion contact offsets. Avoid approximating seating as actor root = seat anchor.
- Example language commands: `Lara.StartHiddenAt(lara_start)`, `Camera.StartAt(camera_start)`, `Camera.Track(Lara)`, `Lara.LocomoteTo(stepforward)`, `Lara.PersistentPose(SexyVamp)`, **Warmup** `Lara.SitAt(Lounger)` → `Lara.PersistentSitPose(Lounging)`, and **later Main** `Lara.DissolveTo(Lounger, seated: Lounging)`, `Lara.Say(...)`, `Director.DrawAndExecute(tags: [Warmup])`. **Intro** `DissolveTo(Lounger)` is standing.

## Important prior decisions to preserve

1. **One global selectable card pool**: cards with tags are published globally. Old scenes automatically gain new eligible cards matching their runtime queries. A scene NEVER declares membership in card libraries. Untagged authored beats may be explicitly invoked.
2. The **canonical story interpreter** determines control flow, conditions, branches, card candidates, history, progress and deterministic selection. Rehearsal uses its hooks with a detached scenario and virtual external effects; don't fork these semantics.
3. Source text and generated typed bindings are separate from scene-spatial bindings. Source analysis discovers required pose/audio/effects/motion/location symbols without manually pre-filling them; reconciler preserves old assignments/source GUID ownership.
4. Art and tuning are authored data, not disposable test defaults. Don't replace the saved Void, lighting, camera, Lara import, released wardrobe, prepared male/contact, or exact pose bindings.
5. **Absolute executor ban on launching/operating Unity**, including Editor GUI, Unity Hub, CLI/batch EditMode tests, Play, build, computer-use Unity and hardware. Native Edit Mode checks are **Art-operated only** after executor pauses with exact menu/click instructions (G1–G4). Reports distinguish managed/source checks, Art-native checks and subjective visual acceptance. No native gate can be skipped by reporting PARTIAL and coding on blindly.

## What already landed and why this task exists

Phase 1 eliminated retired diagnostic scenes, cleaned production ownership, and established global card discovery. Phase 2 built shared scalable player UI. Phase 3 created source-first Scene Director Studio, rehearsal scenarios, Editor navigation and a **duplicated off-scene preview renderer**. The follow-up sprint corrected scroll/focus, source ownership, per-invocation overrides, stale-source invalidation, fake camera tracking and flow navigation, but still rendered a separately constructed scene. In actual author use the preview is confusing/unusable because it **isn't Unity's real edit scene and Game view**.

The target is NOT a conventional animation timeline or a second gameplay implementation. It is an **edit-time, stateful virtual rehearsal whose visual output stages the real saved scene and real Game-view camera at each cue's evaluated end state**, while the author manipulates the real semantic targets with native Unity gizmos. The author does this exactly once: later Play is optional integration/timing validation.
