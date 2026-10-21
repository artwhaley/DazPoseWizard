# Authoring with .dazstory

Use the [Studio guide](SCENE_DIRECTOR_STUDIO_GUIDE.md) for current static visual authoring. First Contact is `Assets/Scenes/FirstContact.unity`; setup/Intro/sequence source and typed companions remain under `Assets/Production/Story/Programs/`. Cards publish globally from `Assets/Production/Story/Cards/`.

The [architecture plan](../plan/ARCHITECTURE_SPRINT_PLAN.md) and [STATUS](../plan/STATUS.md) govern current work. D6–D8 execution is historical; preserve the [fixed oracle](FIRST_CONTACT_D8_ORACLE.md). Six imported pose wrappers, engineering placeholder audio and production wardrobe integration are delivered. Practical Studio authoring was accepted; new wardrobe native use and extended cases remain pending Art’s checks.

Source-first import derives typed binding rows; use Studio fields/visual marks. Manual serialized GUID arrays are not the authoring workflow. Art operates Play/native tests; agents do not launch Unity or hardware.

## Global cards and explicit local beats

Publish tagged cards below `Assets/Production/Story/Cards/`; import automatically updates one application-owned runtime index. The compiled Inspector opens source or selects its adjacent typed companion. No GUID pasting or per-scene registration is required. Public tagged IDs are unique; untagged helpers remain source-local and never enter random draws. `play Intro` retains direct execution.

Tags classify; requirements establish legality. Equipment tags remain open. Missing scene resources reject the affected card; invalid owned assets/code invalidate publication with source diagnostics. Remove a declaration/source to remove its cards. The compiler is `Assets/DazPose/Runtime/Story/Language/DazStoryCompiler.cs`; typed binding navigation is in `Assets/DazPose/Editor/Application/StoryContentBindingsEditor.cs`. See [publication and ownership](../cleanup-task1/SCENE_AND_CONTENT_OWNERSHIP.md#publish-a-selectable-card).

## Write the scene

Edit `validation/DazPoseUnityValidation/Assets/Production/Story/Programs/FirstContact.dazstory`. The local file holds setup, Intro and sequence. The 17 original tagged cards now live in `Assets/Production/Story/Cards/FirstContactEngineering.dazstory`; their bodies and combined 19-dialogue oracle are preserved. Dialogue remains literal readable text. Commands and branching belong here; the Unity adapter does not contain a second choreography script. `Profile.SubmissionScore` is persistent profile state from 0–100; `SubmissionScore.Calculate()` recomputes it from enabled kink catalog entries. It is separate from card `Progress`, which contributes through the session pacing multiplier.

```text

setup FirstContact

{

    Lara.StartHiddenAt(lara_start)

    PlayerBody.StartHiddenAt(player_start)

    Camera.StartAt(High)

    Lara.StartIdle()

}

card Example

{

    Lara.PersistentPose(SexyVamp)

    await Lara.Say("Hello.")

    wait 500ms

}

sequence FirstContact

{

    play Example

    if World.Clothing.Count == 0

    {

        Session.Mood += 5

    }

}

```

`await` waits for that source thread. An unawaited `PersistentPose` or one-shot gesture is owned by the story, can overlap compatible speech, and is drained before normal completion. Starting a conflicting body command while its predecessor is still running produces a source-level error. Stop cancels owned work and prompts. No Unity Awaitable is left unconsumed.

Conditions support comparisons, `&&`, `||`, `!` and parentheses, with short-circuit evaluation. `while`, `repeat`, local `let` values, arithmetic, `round`, session-owned `random(11..22)`, durations, lists and named arguments compile to flat instructions. Wrapped call arguments have the same meaning as one-line calls. Thirty-two nested source blocks are tested; there is no eight-level .dazstory authoring limit.

## Bind content

The current assignments are already saved. Select `FirstContact.bindings.asset` beside the source. The Inspector presents the six pose choices by their story names. Import as **Pose** in DazPoseTool, inspect the result on Lara, then assign its generated **PerformerPose wrapper**. Never assign a raw `.anim` to a pose alias. `Lounging` records Sitting posture; the other five record Standing. Runtime uses this authored metadata rather than inferring posture from bones.

Dialogue audio is selected beneath the transcript. All 19 supplied WAVs are bound as engineering placeholders, retaining their original contents and GUIDs under `Assets/Production/Story/Audio/Placeholder/FirstContact`. Performance cues remain in source even when absent from the temporary voice. The source transcript determines dialogue identity; audio filenames do not. After changing a transcript, choose **Refresh dialogue entries from source** and explicitly bind the revised line's AudioClip. Its former audio is not silently reused. Ordinary authors do not enter hashes, GUIDs or generated step IDs.

Scene locations and roles are on the saved `StorySceneBindings` component. A location supplies a placement and optional seat/focus. `Lounger` therefore works as the same symbol for a standing dissolve destination and a seat request. The current standing placement is its existing authored ApproachAnchor. The user authorized movable placeholder transforms for MainPosition1 and Bedside. Full-session setup creates them under Authoring Marks, using stepforward and the accepted contact staging as starting placements. Move them in the scene as needed; refresh preserves existing transforms and positions.

The saved source and bindings are authoritative; there is no scene-generation or full-session setup command. If a saved assignment needs repair, use the explicit **DazPose > Content > Advanced > Repair First Contact Bindings** command in Edit Mode. It only repairs missing bindings in the existing saved scene and preserves authored marks, poses, audio, wardrobe, identities and artwork. The original four setup marks are `lara_start`, `player_start`, `camera_start` and `stepforward` (`camera_start` stays in the scene unused: the camera now starts on a player-body mount, and marks are never removed); inherited test markers and earlier representative camera-move marks are absent.

## Camera mounts

`Camera.StartAt(High)` and `Camera.StartAt(Low)` place the view on one of two mounts inside the player body prefab (`CameraMount_High` about 0.8 m above the head, level toward the feet; `CameraMount_Low` about 0.2 m above the head, angled down the body). The argument is a fixed word, not a scene mark; anything else is a compile error ("Camera.StartAt expects High or Low"). The cue is instant and setup-only, and it clears any earlier `Camera.Track`.

Because the mounts travel with the body, `PlayerBody.StartHiddenAt(...)` must come **before** `Camera.StartAt(...)` in `setup`; otherwise the camera lands where the body was before it was placed. `Camera.Track(Lara)` keeps the mount position and rotates the view. First or third person is just the body's visibility (`PlayerBody.DissolveIn/Out`).

`await Camera.SetHeight(High)` / `await Camera.SetHeight(Low)` change mounts during the story: the screen fades to black, the camera moves to the mount, and the screen fades back in (the fade is drawn by the camera, about 0.35 s each way; tune it on the player's `PlayerCameraController`). Asking for the height the camera is already at completes at once with no fade. If the camera is tracking Lara it keeps tracking from the new mount; otherwise it takes the new mount's direction. Only the story changes camera height. `Camera.Release()` stops tracking and turns the view smoothly back to the current mount's authored direction; like `Camera.Track` it may run without `await`. Both use the `Camera` channel, so two camera moves cannot overlap.

To create the mounts once, run **DazPose > Player > Add Camera Mounts to PlayerBody**. It is idempotent and never moves mounts that already exist: tune them by moving the two child transforms in `PlayerBody.prefab`.

## Postures

`Lara.Posture` is what `World.Posture` used to be: Lara's posture, written by her commands (`Lara.StartIdle`, `Lara.PersistentPose`, `Lara.PersistentSitPose`, `Lara.DissolveTo`, `Lara.SitAt`) and still assignable. `Player.Posture` is the player body's posture, read-only, taken from a setting on the player body (`Lying` for the one body there is today). `World.Posture` no longer exists and fails to compile with a message naming both. Cards use `requires Lara.Posture == Sitting` or `Standing`.

## Visual mode and diagnostics

`await Lara.MagicShader(GlowyWireframe)` and `await Lara.MagicShader(Regular)` use performer vocabulary. Only runtime material instances are applied. Regular restores the exact normal material references. The wardrobe refresh seam suspends the mode through hair/layer/outfit changes and reapplies it to the current surfaces; Stop invalidates a suspended mode so it cannot reappear afterward. Visual approval in the Void is still a manual acceptance item.

Import errors identify source file, declaration, line/column and the missing or wrong-type symbol. Missing content is shown with the same source information before Play. Console messages explain actions, waits, branches, confirmations, committed values, failures and cancellation in English. F8 shows the current source position.

## Domain and legacy compatibility

`.dazstory` is a frontend. Its AST is transient. The executable model consists of flat typed instructions, operands, source positions and jumps, with no Unity objects or nested StoryStep data. The same managed compiler/interpreter code is tested outside Unity. Future storage providers can construct those domain objects directly.

Legacy JSON parsing/execution remains available for existing compositions. Its old recursive serializer limit belongs to that compatibility path and does not constrain .dazstory. The active First Contact sequence saves the imported compiled source, with legacy JSON and its old scene asset/target lists inactive.

## Cards, Director and session pacing

Cards are declarations in the same source and compile to the same flat program:

    card SeatedPause
    tags [Main, Sitting, Calm]
    progress 20
    weight 1
    repeat unlimited
    cooldown 1
    requires Lara.Posture == Sitting
    {
        let beats = round(3 * Card.Intensity)
        repeat beats {
            wait 400ms
        }
    }

Classification tags select a pool; requirements separately check physical legality. Controlled classifications are Warmup, Main, Resolution, Standing, Sitting, Calm, Focus, Energetic and Recovery. Open equipment:/theme: tags support future identities without modifying existing cards. Requirements can read closed World/Session/Contract/Profile facts and Equipment.ActiveHasTag(...), Equipment.PreparedHasTag(...), Equipment.IsPrepared(...) and Contract.KinkEnabled(...).

Director.DrawAndExecute(tags: [Main, Sitting]) performs a seeded weighted draw. Director.Execute(CardName) requests a specific card while retaining hard requirements and history rules. An equipment query matches the selected item's identity/content tags, with preference for its specific equipment:<id> tag. An empty prepared-equipment query skips successfully. The complete First Contact library must cover every equipment identity eligible for preparation.

Successful cards contribute authored Progress * Contract.PacingMultiplier. Failed or cancelled work earns no progress. Card child operations settle before success is recorded. Cooldown and maximum uses are hard rejections; recency reduces eligible weights. Card.Intensity and Card.Invocation are invocation inputs; no engine-wide speed scaling occurs. Selection, setup and card-local random draws use separate streams owned by one session seed. The saved runner exposes fixed-seed and pacing settings for reproducible tests.

The delivered engineering library contains 18 card declarations: Intro plus four Warmup (one deliberately illegal standing candidate), three Main/Standing, three Main/Sitting, two Resolution and five equipment cards. These are temporary architecture/demo content. Their differences, repeats, progress and explicit effects remain readable in source. No additional dialogue clips are fabricated.

F8 shows current phase/progress, pacing, intensity, physical state, prepared/active equipment, recent history, query, every candidate's rejection reason or final weight, and the selected card. Console traces the same decisions in English. A completion-loop query with no eligible positive-progress path fails clearly; bounded selection guards catch non-advancing content.

## Seated presentation and event waits

PersistentSitPose requires a Sitting pose and stable seat ownership. The seat layer owns the authored pose and anchors its pelvis at the seat. DissolveTo(Lounger, seated: Lounging) hides Lara, establishes root/seat/pose while hidden, then reveals the seated result as one performer operation. It does not call a visible SitAt afterward.

UI.ShowAction creates a non-modal in-session button. A press emits once in that active phase; hiding, phase exit, Stop and teardown remove its UI. Wait-any subscribes to every listed event, consumes the first result and removes all losing subscriptions. Cancellation removes the wait. Old phase/action/playback generations cannot complete a new wait.

MotionDriver.Ended is emitted once for natural finite funscript completion. Stop and loop boundaries never emit it. The source owns the early-action versus finish-then-loop behavior. Finisher1_Engineering (18 seconds) and FinishLoop_Engineering (4 seconds) are labelled temporary fixtures imported by the normal FunscriptImporter. Replace their bindings with final authored motion when available.