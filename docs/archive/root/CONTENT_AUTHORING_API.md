# Content authoring API

For narrative authoring, `.dazstory` is the current source of First Contact setup, explicit Intro and sequence beats. Tagged selectable cards are published separately under `Assets/Production/Story/Cards/` and discovered globally by the Director. See the [current authoring guide](../../story-runtime/DAZSTORY_AUTHORING.md) and [scene/content ownership](../../cleanup-task1/SCENE_AND_CONTENT_OWNERSHIP.md). Current saved scenes are `Assets/Production/Scenes/MainMenu.unity` and `Assets/Production/Scenes/FirstContact.unity`; Clothing Setup is `Assets/Scenes/ClothingSetup.unity`, and the saved Showcase is under `Assets/Examples/Scenes`. `AuthoringWelcome` and `AuthoringCompanion` were retired. Historical implementation notes below are not current workflow instructions.


Current story authoring is `.dazstory`, introduced by the revised D5.5–D8 stack.
Start with [the readable source guide](../../story-runtime/DAZSTORY_AUTHORING.md) and
[the full-session handoff](../docs/story-runtime/D6_D8_HANDOFF.md). The full authored
FirstContact source and its content bindings are under Assets/Production/Story/Programs.
The JSON section below is historical compatibility documentation; it is not the
preferred human authoring surface. Existing typed C# performer/application APIs remain
valid infrastructure. Six imported pose wrappers are assigned. D6-D8 implementation includes normal cards, the Director, seated presentation and phase-owned event races; full playback evidence is tracked in the handoff.

Start with [Author your first scene](CONTENT_AUTHORING_WALKTHROUGH.md) for exact Unity setup, Inspector assignments, a complete starter script and expansion steps.

Write a scene as a sequence of actions against `PerformanceRun`. The saved scene supplies Lara, the player view, a shared motion clock and semantic targets. The application owns settings and Intiface. Menus only launch, stop and configure.

**Implementation, October 8, 2026:** the host, scoped API, application services, production editor commands, saved UI shell and trial sequences are implemented. Acceptance results are recorded separately in [the handoff](../docs/production-consolidation/HANDOFF.md). Source availability does not imply user visual approval.

## Create and launch a scene

1. Choose **DazPose → Scene → New Performance Scene...** to copy the clean saved production Void environment, or **Initialize Game Session** in an existing saved scene.
2. The initializer saves one host, bindings, Lara, player view, shared clock, controls and an Authoring Marks root. New Performance Scene also saves the prepared male player body and its measured contact target. Existing assignments and tuning take precedence. It reports **Ready to Play** or **Needs author assignment**, with exact missing references.
3. Choose **New Sequence Script...**, attach the script and assign it to the host. Assign its serialized content and Transform marks. Save.
4. Press Play. The saved host runs the saved sequence; no testing window or helper is required.
5. **Add Current Scene to Scene List** adds it to the main menu. **Open Main Menu** opens the game entry scene. **Build Windows Player...** builds that menu and enabled catalog scenes.

A new session starts with `WelcomePerformance` and Lara's existing InitialPose. If several unbound actors, cameras or clocks exist, assign the intended ones; the initializer does not choose arbitrarily. Each scene and actor has a distinct saved ID for tuning.

The preserved Showcase example source is [AuthoringShowcase.cs](../../../validation/DazPoseUnityValidation/Assets/Production/Content/AuthoringShowcase.cs); its saved scene is `Assets/Examples/Scenes/AuthoringShowcase.unity`. The old Companion scene is retired. The authoring sequences are regression/example source, not public scene entries.

## A short sequence

```csharp
using DazPose.Motion;
using DazPose.Performer;
using UnityEngine;

public sealed class ArrivalSequence : PerformanceSequence
{
    [SerializeField] Transform entrance, destination, gazeTarget;
    [SerializeField] PerformerPose arrivalPose;
    [SerializeField] PerformerExpression expression;
    [SerializeField] AudioClip greeting;
    [SerializeField] FunscriptMotionProgram motion;

    public override async Awaitable ExecuteAsync(PerformanceRun run)
    {
        var actor = run.Performer;

        // Entrance is a content decision, including when replaying.
        if (actor.IsSeated) await actor.StandUpAsync();
        if (actor.VisibilityState == PerformerVisibilityState.Hidden)
            await actor.DissolveInAtAsync(entrance, 1.5f, arrivalPose);
        else
            await actor.DissolveToAsync(entrance, 1.5f, arrivalPose);

        actor.LookAt(gazeTarget);
        actor.Expression(expression, .6f);
        await actor.SayAsync(greeting);
        await actor.DissolveToAsync(destination, 1.5f, arrivalPose);

        await actor.GripAsync(run.ContactTarget);
        run.StartPlayback(motion, loop: true);
        actor.FollowGripMotion();
        run.Toys.Follow();
        run.Toys.Vibrate(.25f);
        await run.Delay(8f);

        run.Toys.Stop();
        run.StopPlayback();
        await actor.ReleaseGripAsync();
        await actor.DissolveOutAsync(1.5f);
    }
}
```

Assign all fields and a partner contact binding for that example. For dialogue alone, omit contact and motion. A host binds its primary Lara explicitly; scripts do not discover a cast or look up skeleton paths.

## Timing, overlap and cancellation

Await finite commands when the next beat needs their endpoint. Most successful typed results can be ignored. Persistent commands return immediately and remain active until changed, cleared or cancelled.

To overlap compatible work, keep each Awaitable and consume it once:

```csharp
var cameraMove = run.Player.MoveToAsync(cameraMark, 3f);
var wave = run.Performer.GestureAsync(greetingGesture);
await run.Performer.SayAsync(line);
await wave;
await cameraMove;
```

Use `await run.Delay(seconds)` for scaled game time. It remains cancellable at time scale zero. Speech and wardrobe have their own queues; full-body actions still have ownership rules. Sequence incompatible body moves deliberately.

A non-toy failure identifies actor, verb and outcome and stops the run. Toy errors never become a script failure.

**Stop** cancels the current session's local work, clock, followers, queued/ramped toy output and Settings test output, then attempts hardware Stop without waiting for a receipt. It does not reset tuning, pose or committed wardrobe. **Start** invokes the script again from current state. **Restart Scene** reloads the saved scene and applies current cached tuning. A stopped C# invocation cannot resume midway.

The host owns cleanup. `State` is Idle, Running, Stopped, Completed or Failed; `Failure` describes a non-toy failure. Natural completion stays Completed after cleanup. Retired facades and late cleanup cannot revive work or cancel a replacement session.

## Actor verbs

All calls below are on `run.Performer`.

| Task | API |
| --- | --- |
| Pose | `await PoseAsync(pose)` uses Lara's configured smooth default. Override deliberately with `PoseAsync(pose, PoseTransition.Smooth(1.2f))`; `Snap` is explicit. |
| Expression | `Expression(expression)`, `Expression(expression, intensity)`, or `Expression(expression, intensity, blendSeconds)`. `ExpressionAsync(...)` waits for the blend. `ClearExpression()` explicitly clears it. |
| Speech | `await SayAsync(audioClip)`. Gaze, expression and compatible gestures can overlap speech and lip sync. |
| Gaze | `LookAt(transformOrPosition)`, `await LookAtAsync(transformOrPosition)`, `ClearGaze()`. Supply semantic head/eye marks. |
| Walk / turn | `await WalkToAsync(transformOrPosition)`; `await TurnToAsync(transformOrPosition)`. Transform walks also accept `LocomotionArrivalBehavior.Hold` or `RestorePose`, and the existing strict-ownership option. |
| Sit / stand | `await SitAtAsync(seat)`, optionally with `PerformerSeatedStyle`; `await StandUpAsync()`. Use a prepared seat and its authored anchors. |
| Action | `await PerformAsync(action)` for a prepared full-body action. |
| Gesture | `await GestureAsync(gesture)` for a compatible overlay. |
| Appear | `await DissolveInAtAsync(mark, seconds, optionalPose)`; `DissolveInAsync(seconds)` reveals at current placement. |
| Disappear | `await DissolveOutAsync(seconds)`. |
| Relocate with dissolve | `await DissolveToAsync(markOrPosition, seconds, optionalPose)`. A Transform supplies placement and facing. |
| Teleport | `await TeleportToAsync(markOrPosition)`, optionally with a pose. |
| Spell | `await CastAsync(spell)` or `CastAsync(spell, target)`. |
| Aura | `Aura(aura)` or `Aura(aura, target)`; `ClearAura()`. |

Read-only story state: `CurrentWardrobe`, `VisibilityState`, `IsSpeaking`, `IsGripping`, `IsSeated`. Use it to choose a beat, not to poll command completion.

## Wardrobe and hair

Current `.dazstory` commands, named slots and Studio workflow: [Wardrobe in First Contact](../docs/wardrobe-directing/FIRST_CONTACT_WARDROBE.md). OpeningOutfit and OpeningHair are already assigned in First Contact.


| Task | API on the scoped actor |
| --- | --- |
| Outfit | `await OutfitAsync(preset)` or `OutfitAsync(stablePresetId)`. |
| Clear outfit selection | `await OutfitAsync("unclothed")`. |
| Hair | `await SetHairAsync(stableHairId)`; `"none"` clears hair. |
| Undress / dress a layer | `await TryRemoveLayerAsync()`; `await TryAddLayerAsync()`. |

Each accepts an optional `WardrobeTransition`. Default is a cut; use `WardrobeTransition.Dissolve(outSeconds, inSeconds)` when wanted. Pose and wardrobe have different defaults.

Layers are Base, Layer 1 and Layer 2. Removal takes the highest worn layer; addition restores the lowest missing layer. Removing every layer retains the selected outfit for dressing again. Already equipped, fully dressed and nothing left to remove are successful no-ops.

Use IDs from the released inventory. Current outfit IDs include `first-outfit`, `second-outfit` and `third-outfit`; the existing hair's full ID is **`first-outfit/EmikoHair_59875`**. A mesh name alone is not a released hair ID.

`CurrentWardrobe` exposes outfit, effective hair and layer state, including `CanRemoveLayer` / `CanAddLayer`. Use Clothing Setup to prepare and publish content. Scripts select released content; they do not modify meshes, masks, layer assignments or immutable releases. Scene wardrobe recall is explicit, never a lifecycle reset.

## Contact and shared motion

Acquire the prepared partner with `await actor.GripAsync(run.ContactTarget)`. Acquisition holds the center by default; `GripAsync(target, initialPosition01)` chooses another held position. Motion starts only when explicitly requested.

- `actor.HoldGrip(position01)` holds contact, default .5.
- `run.StartPlayback(program, loop: true)` starts the bound funscript clock.
- `actor.FollowGripMotion()` and `run.Toys.Follow()` consume that same clock without rewinding it.
- `run.StopPlayback()` stops the clock.
- `actor.ReleaseGrip()` starts release; `await ReleaseGripAsync()` waits.

Import a project `.funscript` to obtain `FunscriptMotionProgram`. Read `run.IsPlaybackRunning` / `PlaybackPosition01` when the story needs state. The obsolete mutable `run.Playback` is not the authoring path.

Use Lara's prepared right-hand calibration and the production partner's supported rigid contact setup. No bone discovery, new calibration or geometry pipeline belongs in a script.

## Player camera

All calls below are on `run.Player`.

| Task | API |
| --- | --- |
| Position | `await MoveToAsync(markOrPosition)`, optionally with seconds or `ViewTransition`. |
| Aim | `await LookAtAsync(markOrPosition)`, optionally with seconds or `ViewTransition`. |
| Persistent tracking | `Track(target)`, `Track(target, acquireSeconds)` or `Track(target, ViewTrackingSettings)`; `StopTracking()`. |

Position and orientation are independent channels. A position mark does not implicitly start tracking. Bind the authored camera/view rig and preserve its optics and environment settings.

## Toy output

Calls on `run.Toys` return immediately:

`Vibrate(level, optionalChannel)`, `Oscillate(level, optionalChannel)`, `RampVibration(target, seconds, optionalChannel)`, `RampOscillation(...)`, `MoveTo(position01, seconds)`, `Follow()`, `Follow(driver)`, `StopFollowing()`, `Stop()`.

Levels/positions are normalized 0–1. Omitted channels address enabled matching routes; an explicit channel uses the Settings mapping.

Write toy commands unconditionally. Missing service, missing route, disconnection, rejected output and slow acknowledgements are all silent successful no-ops to the app. There is no scene/UI success or failure response. Use Delay for choreography; never await delivery or branch on availability. Troubleshoot devices in Intiface.

Settings owns explicit Connect / Disconnect / Scan, endpoint, discovered feature routes, channels, ranges, inversion, Test and Stop All Outputs. Bluetooth pairing stays in Intiface. Settings tests are silent. Stop All Outputs cancels toys without stopping actor choreography.

## Tuning and persistence

```csharp
run.Performer.SetTuning(new PerformerTuning {
    DefaultPoseTransition = PoseTransition.Smooth(.9f),
    BreathsPerMinute = 16f,
    HeadGazeWeight = .7f
});
```

Only supplied nullable properties change. Tuning covers pose/expression defaults, breathing, gaze, attention and blinking; [PerformerTuning.cs](../../../validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Performer/Authoring/PerformerTuning.cs) lists every property. Stop, failure, completion and replay never restore old values.

The app captures live actor/grip/view/clock tuning, including supported Play Mode Inspector edits, before teardown. Current cache wins on navigation/reload. Missing snapshot fields retain serialized values. Content state, rig references and shared profiles are not tuning snapshots.

Behavior settings save automatically when changed during Play, including supported Inspector edits while paused, stopped or completed. They persist for that scene and actor across future launches; no Save Current Tuning menu command is required. In Edit Mode normal scene saving applies. The existing performer inspector's explicit Undoable instance-transfer operation remains separate; it never writes a prefab or shared profile automatically.

App files live under `Application.persistentDataPath/DazPose/`: `game-settings.json` for Intiface/audio and `scene-tuning.json` for automatically saved scene/actor behavior settings. Opening Settings only reads values.

## Prepared assets and acceptance

Use the existing Lara prefab, prepared poses, expressions, seats, action/gesture clips, effects and released wardrobe. Gesture/action clips must be standalone `.anim` assets; the production bake commands create those. Store each sequence's assets once on that sequence, not in a duplicate binding manifest.

Save the scene with its host and sequence. Press Play in that exact scene, then run it from Main Menu. Review appearance, speech, pose transitions, framing, wardrobe, motion and effects. A compile pass does not approve the composition or the still-pending second-outfit toe appearance.

The fresh Windows entry point is `validation/DazPoseUnityValidation/Builds/Authoring/DazPoseGame.exe`. Both example scenes completed saved-scene graphics Editor playback. The user accepted the remaining functional player checks on October 11, 2026. Artistic approval and the historical toe observation remain separate in [the acceptance record](../docs/production-consolidation/authoring-reports/B08_MANUAL_CHECKS.md).

## Legacy JSON story compatibility — archived D1–D5 reference

A saved `StoryProgramSequence` can now serve as the host's sequence. It reads a `dazpose.story` v1 JSON TextAsset, validates saved symbolic bindings before the first step and uses the same scoped `PerformanceRun` verbs. Existing C# sequences remain supported. Application-owned `Profile` stores interests, equipment and persistent story memory separately from settings/tuning; fresh story invocations own contract/session/world/local state.

This is a historical account of the D1–D5 compatibility delivery, not a current refresh procedure. The saved scene is now `Assets/Production/Scenes/FirstContact.unity`; `.dazstory` authoring and current scene operations are documented at the top of this guide. The moved ticket packet is archived under [oldtickets/D1-D5](../oldtickets/D1-D5/README_FIRST.md).

Player body vocabulary is `await run.Player.DissolveBodyInAsync(seconds)` and `DissolveBodyOutAsync(seconds)`, using its saved prepared-male body binding and the existing finite dissolve engine. The player body retains the original male meshes and measured contact-column setup; it is not a Lara copy. The D1–D5 checkpoint does not include the later body-reveal beat.

See [schema v1](../docs/story-runtime/SCHEMA_V1.md), [source handoff](../docs/story-runtime/D1_D5_HANDOFF.md) (historical; original ticket packet: [oldtickets/D1-D5](../oldtickets/D1-D5/README_FIRST.md)) and [manual checklist](../docs/story-runtime/MANUAL_TEST_CHECKLIST.md). The JSON supports eight nested step blocks; deeper content is rejected explicitly. Source/Editor checks passed; **user Play Mode acceptance is pending**.

Production scene composition and clear-English Console tracing: [Production scenes](../../story-runtime/PRODUCTION_SCENES.md).
