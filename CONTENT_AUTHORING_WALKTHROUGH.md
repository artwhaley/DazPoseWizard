# Author your first scene

This walkthrough uses the implemented production path in `validation/DazPoseUnityValidation`. Paths beginning with `Assets/` below are Unity Project-window paths. The API reference is [CONTENT_AUTHORING_API.md](CONTENT_AUTHORING_API.md).

Player navigation, Settings persistence, Stop/Start, Restart and scene switching were accepted by the user on October 11, 2026. This exercise measures how comfortably a new scene can be authored and how the resulting performance feels.

## 1. Write five beats

Start with a short scene that has a beginning and ending:

| Beat | What the audience sees | What you need |
| --- | --- | --- |
| Arrival | Lara appears at an entrance, facing the audience | Entrance mark, arrival pose |
| Greeting | She looks at the viewer and speaks | Viewer gaze target, audio clip |
| Invitation | She changes pose smoothly and holds it | Second pose, hold duration |
| Approach | She dissolves to a closer position | Destination mark |
| Departure | She pauses, then dissolves away | Hold duration |

Choose actual assets and positions before expanding the script. Add one new beat at a time after this version plays. The script defines the order; marks and asset fields supply the content.

## 2. Create the saved scene

1. Open the Unity project `validation/DazPoseUnityValidation`.
2. Choose **DazPose → Scene → New Performance Scene...**. Save as `Assets/Production/Scenes/MyFirstScene.unity`.
3. This copies the authored `FirstPerformanceVoid` environment and initializes Lara, camera/view, shared motion clock, `Game Session`, controls and `Authoring Marks`. Keep the copied floor, smoke, lights, camera optics and volume settings. The original remains the environment source.
4. Select **Game Session**. Inspect **Performance Host** and **Performance Scene Bindings**. Performer, Player and Playback should already reference the intended scene objects. The initial Sequence is `WelcomePerformance`.
5. Press Play once to see the initialized scene; leave Play Mode before authoring saved positions or replacing its sequence.

For an existing saved scene, use **DazPose → Scene → Initialize Game Session** instead. If it reports **Needs author assignment**, assign the named reference on the selected session. Reinitialization preserves existing assignments and tuning. **Ready to Play** concerns session setup; it does not check fields on your new script.

## 3. Place semantic marks

Under **Authoring Marks**, create empty GameObjects named **Entrance**, **Destination** and **ViewerGaze** using Unity's **Create Empty** command.

- Entrance and Destination represent Lara's root placement. Put them at the desired floor position. Their blue Z axis supplies her forward direction for dissolve placement.
- ViewerGaze is an eye-height point near the viewing position. The actor's gaze uses its position, not its rotation. This static mark is suitable for a fixed initial view. For a moving player, assign the PlayerController's configured head Transform instead.
- Position marks in the Scene view and judge the result in the Game view. Do not move or retune the environment to compensate for a bad mark.

Marks are saved Transform references. Give them story names such as Doorway, Bedside or CloseView; scripts should not search the hierarchy for objects.

## 4. Create and assign the sequence

1. Choose **DazPose → Scene → New Sequence Script...**.
2. Save it as **`Assets/Production/Content/MyFirstSceneSequence.cs`**. This folder belongs to the existing `DazPose.Content` assembly, which references Runtime. Keep script filename and class name identical.
3. Replace the generated sample with the code below. Save the file and let Unity compile.
4. Select **Game Session**, use **Add Component** to add `MyFirstSceneSequence`.
5. Drag that component into **Performance Host → Sequence**, replacing `WelcomePerformance`. Remove the unused WelcomePerformance component after replacing the reference. Keep the bindings and host.
6. Assign every field on the new sequence. Save the scene.

```csharp
using DazPose.Performer;
using UnityEngine;

public sealed class MyFirstSceneSequence : PerformanceSequence
{
    [SerializeField] Transform entrance;
    [SerializeField] Transform destination;
    [SerializeField] Transform viewerGaze;
    [SerializeField] PerformerPose arrivalPose;
    [SerializeField] PerformerPose invitationPose;
    [SerializeField] AudioClip greeting;

    public override async Awaitable ExecuteAsync(PerformanceRun run)
    {
        var actor = run.Performer;

        // Make the opening work when Start is pressed after a stopped beat.
        if (actor.IsGripping) await actor.ReleaseGripAsync();
        if (actor.IsSeated) await actor.StandUpAsync();
        actor.ClearGaze();
        if (actor.VisibilityState == PerformerVisibilityState.Visible)
            await actor.DissolveOutAsync(.7f);

        await actor.DissolveInAtAsync(entrance, 1.5f, arrivalPose);
        actor.LookAt(viewerGaze);
        await actor.SayAsync(greeting);
        await actor.PoseAsync(invitationPose);
        await run.Delay(3f);
        await actor.DissolveToAsync(destination, 1.5f, arrivalPose);
        await run.Delay(3f);
        actor.ClearGaze();
        await actor.DissolveOutAsync(1.5f);
    }
}
```

The opening chooses visible story state explicitly. It changes no tuning. Start runs the script again; it does not continue a stopped C# invocation halfway through.

### Fields to assign

| Sequence field | Drag this reference into the Inspector |
| --- | --- |
| Entrance | Your Entrance Transform |
| Destination | Your Destination Transform |
| Viewer Gaze | Your ViewerGaze Transform |
| Arrival Pose | The pose asset referenced by Lara's SuccubusPerformer Initial Pose |
| Invitation Pose | A prepared `PerformerPose` asset; the delivered showcase uses `Assets/Generated/DazPoses/G8F/Vintage Glamour Genesis 8 Female 22.asset` |
| Greeting | An imported AudioClip; `Assets/Production/Content/Assets/Welcome.mp3` is an existing trial clip |

Use your own imported audio when ready. `SayAsync` drives the existing speech/lip-sync path. Pose commands use the configured smooth default. Change a duration only when the beat calls for it.

## 5. Play and shape the performance

Press Play in **MyFirstScene.unity**. The saved host automatically runs your assigned sequence. Watch entrance placement, facing, speech, pose transition, gaze and departure. Check the Console's local performance-ended message for Completed or a concrete non-toy failure.

Adjust the script's beat order/delays and saved marks in Edit Mode, then play again. Change one thing at a time so its effect is clear. Ordinary Play Mode scene edits are temporary; author placement and content assignments in Edit Mode.

Behavior settings such as breathing, gaze response, blink timing, pose blend defaults, grip strength, camera defaults and motion frequency save automatically when changed during Play, including supported Inspector edits. The saved values belong to this scene and actor and survive Stop/Start, completion, navigation and future launches. No extra save command is required. Edit scene marks and asset assignments in Edit Mode and save the scene normally.

## 6. Add richer beats

Add the serialized asset/Transform fields required by each beat, assign them in the Inspector, then put the command at the desired point in ExecuteAsync. The [API reference](CONTENT_AUTHORING_API.md) lists the full vocabulary and transition options.

| Beat | Commands and content |
| --- | --- |
| Expression | Add a `PerformerExpression` field. `actor.Expression(expression, .6f, .8f)` begins a blend; `ExpressionAsync` waits for it. Existing prepared expression: `Assets/Production/Content/Assets/Invitation.asset`. |
| Gesture during speech | Add a `PerformerGesture` field. Start `var wave = actor.GestureAsync(gesture);`, await speech, then `await wave;`. Consume each Awaitable once. Existing gesture: `Assets/Production/Content/Assets/Greeting.asset`. |
| Wardrobe | `await actor.OutfitAsync("first-outfit");`, `await actor.TryRemoveLayerAsync();`, `await actor.TryAddLayerAsync();`. Choose released content; do not edit its meshes or layers from the script. Wardrobe defaults to a cut. |
| Hair | `await actor.SetHairAsync("first-outfit/EmikoHair_59875");`. Use the complete released ID. |
| Camera movement | Add a CloseView Transform under Authoring Marks and an ActorFocus mark parented to Lara. Move with `await run.Player.MoveToAsync(closeView, 3f);`; aim separately with `await run.Player.LookAtAsync(actorFocus, 1f);`. Position and aim may overlap. Camera-mark rotation does not implicitly aim the view. |
| Walking | `await actor.WalkToAsync(destination);` or `await actor.TurnToAsync(viewerGaze);`. Keep incompatible body movements sequential. |
| Seating | Add a reference to a prepared `PerformerSeat`; `await actor.SitAtAsync(seat);`, then `await actor.StandUpAsync();`. Use authored seat anchors; a plain Transform is not a seat. |
| Action/effects | Add prepared action, spell or aura references and use `PerformAsync`, `CastAsync`, `Aura` and `ClearAura`. See `AuthoringCompanion.cs` for a saved example. |

Persistent commands such as LookAt, Expression, Aura and Track remain active until changed, explicitly cleared or cancelled. Decide their visible ending as part of the beat. Await finite commands when the next beat needs completion; use `run.Delay` for pauses.

### Contact and shared motion

1. In Edit Mode choose **DazPose → Scene → Add Partner**. With one session and an empty contact binding, this assigns the prepared partner contact component to **Performance Scene Bindings → Contact Target**.
2. Place and orient the partner for your composition. Inspect the delivered AuthoringShowcase scene for a working relative placement; adding the prefab does not arrange the scene for you. Keep its prepared contact setup and Lara calibration.
3. Add `[SerializeField] DazPose.Motion.FunscriptMotionProgram motion;` to your sequence. Assign the imported `Assets/motiondrive.funscript`, or import and assign your own project `.funscript`.
4. Add this beat after Lara reaches the contact placement:

```csharp
await actor.GripAsync(run.ContactTarget); // Holds center first.
run.StartPlayback(motion, loop: true);
actor.FollowGripMotion();
run.Toys.Follow();
run.Toys.Vibrate(.25f);
await run.Delay(12f);
run.Toys.Stop();
run.StopPlayback();
await actor.ReleaseGripAsync();
```

Visual motion and toys consume the same clock. Write toy commands unconditionally; all toy errors are silent successful no-ops. Connection and routes are configured through application Settings, with Bluetooth pairing/troubleshooting in Intiface. No connection object belongs in the scene script.

## 7. Put the scene in the game

1. Save MyFirstScene with its host, assigned sequence, content and marks.
2. Choose **DazPose → Game → Add Current Scene to Scene List**.
3. Select `Assets/Production/Application/SceneCatalog.asset` to edit this entry's Title and Description. Keep its ID and Scene Path. Ensure Enabled is checked.
4. Choose **DazPose → Game → Open Main Menu**, then press Play and select your new scene.
5. Choose **DazPose → Game → Build Windows Player...** when ready for a standalone run. Rebuild after changing content or scripts; an older executable will not contain your edits.

The authored scene is delivered with its own saved sequence. Menus launch it; the host owns execution and cancellation. Add no choreography to UI button handlers or test windows.

## Authoring friction found while writing this walkthrough

These are current limitations, not implemented helper commands:

1. **Sequence creation requires manual attach and assignment.** New Sequence Script creates only the file. Recommended improvement: attach the compiled new sequence to the selected session and assign the host, with an explicit replacement choice if it already has authored content. Do not silently overwrite that content. This can remove steps 4-5 in section 4.
2. **Marks have no placement helper or preview.** The initializer creates an empty Authoring Marks root. Current path is Create Empty plus Scene/Game view positioning. Recommended next improvement, if this interrupts the first scene: create an actor-placement mark from the actor's current root and a view-position mark from the current camera. Preserve authored values; camera aim remains explicit.
3. **Partner creation binds contact but does not stage it.** The author must compose actor/partner placement, with no contact preview. First judge this in the actual scene; add a focused authoring helper only if placement proves difficult. Do not auto-calibrate or reset the rig.
4. **Prepared content discovery is scattered.** This guide gives existing asset paths and wardrobe IDs. If finding content slows the test, consolidate a small author-facing asset inventory with paths/types/preview notes. Creating new poses, seats and baked clips needs its own focused workflow; this walkthrough uses prepared assets.

The first two are small editor workflow changes. The latter two should be driven by the first real scene's experience. No runtime changes or new automated validation campaign were made while preparing these instructions.
