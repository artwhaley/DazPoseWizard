# Independent reviewer brief — please challenge this architecture, not the product requirement

## Desired reviewer output

Give a red-team review for **correctness, Unity Editor technical feasibility, architectural seams, safety and ease of use**. Include (a) blocking errors, (b) important but nonblocking improvements, (c) concrete code/Unity evidence, and (d) proposed amendments scoped to the native Edit Mode design. Don't provide an unrelated product brainstorm or return to an independent preview renderer because it is familiar.

## One-paragraph problem statement

DazPoseWizard is a Unity HDRP interactive performance app driven by `.dazstory` choreography, source-owned bindings, DAZ-imported Lara poses, scene positions/seats and a global tag-filtered Director card pool. The last Scene Director Studio built a separate `PreviewRenderUtility`-based copy of the world, and the author finds it essentially unusable. We are replacing **only the authoring visual projection** with an Edit Mode performer/camera staging lease in the **actual saved Unity scene**. Standard **Scene view** handles edit saved anchors; standard **Game view** shows the real saved `PlayerView` camera framing the cue. Author steps linearly through script, sees settled cue endpoints, drags marks, previews composition and saves normal authored data—all without Play. The existing canonical interpreter/Director and source binding system are reused intact.

## Design commitments not open for reinterpretation

1. **One edit-time directing workflow**. Play is not needed to adjust every stand/sit/camera cue; Play may validate final effect timing later.
2. **Real Game view camera** from saved `PlayerView` hierarchy. No fake camera object/RenderTexture/private viewport and no merely-aligning SceneView camera as a substitute.
3. **Cumulative, real scene staging** of Lara in her actual prepared pose/seat, camera/end-state, visible via Unity Scene/Game. No cloned duplicate environment.
4. **Source state vs authored transforms**: stepping is temporary; real semantic marks and asset assignments are persistent Unity edits; normal Ctrl+S must be safe.
5. **One global tagged selectable card pool**; existing scenes match newly tagged cards automatically; no scene library declaration.
6. Preserve canonical `StoryMachine`, deterministic RehearsalSession, source-first bindings, FirstContact scene/creative assets and valid runtime behavior.

## Review targets / top engineering threats

**R1 — Is live Edit Mode Game-view HDRP rendering reliable with this exact saved Camera?** Check Unity 6000.5.9f1, actual `PlayerView` root/`viewRig`/`headPose`/MainCamera chain, enabled status, render graph, Game resolution and camera routing. Proposed test must use native Unity and actual camera, not a unit-test mock.

**R2 — Can previewing saved-scene actor/camera objects avoid contaminating serialized scene and prefab overrides?** Audit AnimationMode + dedicated driver behavior, direct non-animated Transform/material/visibility writes, sceneSaving/sceneSaved ordering, Ctrl+S/Save As/Prefab/Undo/domain reload. This is the largest data-integrity risk. Demand a deterministic restoration/suspend algorithm and a native fixture test.

**R3 — Does real Lara pose sampling preserve root placement and match generated DAZ `PerformerPose` assets?** Check animation clip binding paths and retargeting, Animator/PlayableGraph interactions, existing active animation owners, blendshape/property sampling on real saved Lara and `AnimationMode` restore semantics.

**R4 — Is the settled Lounger endpoint actually equivalent to runtime seating?** Trace `PerformerSeat`, `PerformerSeating`, `PerformerSeatingLayer` and baked pelvis offset transforms. Don't accept root=SeatAnchor; test actual sitting body/furniture contact and ApproachAnchor independence.

**R5 — Does camera endpoint math match PlayerView?** `PlayerView.ResetPerformanceView` sets player root and viewRig rotation separately. `DesiredRotation` uses actual camera/head position and up fallback; tracking persists across later author edits. Recommend minimal reusable pure helper, no runtime behavior change.

**R6 — Does the visual projection accidentally become a second story engine?** It should accumulate *already executed* effects only, not parse statements/evaluate predicates/select cards/replay logical code on every mark drag. Is reference retention correct so moving a mark instantly re-resolves current projection without RNG/history changes?

**R7 — Is the authoring UI stripped down enough?** Studio should be obvious control chrome around native Scene/Hierarchy/Inspector/Game. Test a first-time author selecting source, cue, mark and saving with no custom viewport or hidden prerequisite.

**R8 — How are invalid/unsupported cues shown?** No bogus successful preview when solver/dynamics unavailable; distinguish correct end-state from approximate timed effects and never ask author to stage all framing a second time in Play.

**R9 — Lifecycle and multi-scene hazards:** Multiple open scenes, disabled host, changed source/bindings, Undo event storms, async UI queue, domain reload, scene close and partial setup. Does the preview lease fail closed and leave the original editable scene intact?

**R10 — Packet completeness:** Every implementation decision assigned to a ticket? Any backdoor allowing executor to sidestep native evidence by claiming managed suite success? Any unapproved content changes?

**R11 — Manual native gate discipline:** This executor is **absolutely prohibited from running Unity** (GUI/Hub/CLI/batch/automated Unity tests/Play/build/computer-use). All Unity compile/import and G1–G4 diagnostic clicks are **Art-operated**, after an explicit executor pause. Check that no ticket implies the executor can self-run Unity, advance past unrun gate, or auto-open Editor. Source code for user-invoked menu checks is permitted.

**R12 — Previously missed regressions:** Compare command-specific standing rotation/reset, Warmup first sit vs Main seated dissolve, pose-sampled hip-to-seat correction, awaited/non-awaited gesture after states, `MagicShader` cloned sharedMaterial references, `[ExecuteAlways]` wardrobe writes, chair prefab override authoring, laraFocus head-descendant feedback, save dirty-state correctness, scene-copy shared-asset leak, and fail-fast rehearsal queue. `REVIEW_RESOLUTIONS.md` gives required dispositions.

## What to return

Use this structure:

```text
Verdict: ACCEPT / ACCEPT WITH MANDATORY CORRECTIONS / REJECT
Blocking finding: [ID, severity, exact file/method/API, test or reproducible failure, minimal fix]
Important finding: [same]
No-change confirmations: [architecture requirements that should remain]
Proposed patch to packet: [section + exact amended wording]
Native tests required to establish disputed claims: [named tests]
```

### Reviewers should inspect these first

- `validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/ScenePreviewSession.cs` (retiring it)
- `.../SceneDirectorStudioWindow.cs`, `.Rehearsal.cs`, `.Revisions.cs`, `SceneMarkAuthoring.cs`
- `.../RehearsalSession.cs`, `RehearsalStoryHost.cs`, `SourceBindingReconciler.cs`
- `Assets/Player/Runtime/PlayerView.cs` and `PlayerController.cs`
- `Assets/DazPose/Runtime/Performer/{PerformerSeat.cs,PerformerSeating.cs,PerformerSeatingLayer.cs,PerformerSeatingProfile.cs}`
- `Assets/DazPose/Runtime/Story/Runtime/{StorySceneBindings.cs,PerformanceStoryHost.cs}`
- Saved `Assets/Production/Scenes/FirstContact.unity` and `.dazstory` + adjacent `.bindings.asset`

Paths above are relative to `validation/DazPoseUnityValidation/` unless prefixed `validation/`; confirm exact file paths against HEAD.

## Review constraints

The repository is pinned at `4656eb56fe1cfa2201104e960b4d7e8dc41cb766` for this packet; check newer HEAD before coding. Native rendering/save safety remain **unverified** and require **Art's G1–G4 reports**. **Reviewers and executors must not launch or control Unity**; ask Art if additional Editor experiments are needed. Flag API-version assumptions and whether menu-only diagnostics can establish them safely on an isolated project.
