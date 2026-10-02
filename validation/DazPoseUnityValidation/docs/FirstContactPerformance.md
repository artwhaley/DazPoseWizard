# P0.E — First Contact

## Install and run

Baseline: `main` at `ed1c668` (Add directable Player view), with the user's locally migrated and tuned `Assets/Scenes/FirstPerformanceVoid.unity`. Implementation does not rewrite that scene on disk. The installer uses the currently loaded scene.

1. In Edit Mode, open the current FirstPerformanceVoid scene.
2. Run **Tools > DAZ Pose > First Performance Void > Install First Contact Performance**. This saves that scene, including current unsaved tuning. Do not run the room builder or Player migration.
3. Check the `FIRST_CONTACT_INSTALLED` Console entry. It records marker world positions, Speech A/B asset paths, and actual imported AudioClip durations.
4. Enter Play Mode and wait for initialization. Nothing starts automatically.
5. Click **RUN FIRST CONTACT** in the room panel. Watch the entire run without using other controls. Status and elapsed gameplay seconds appear in the panel.
6. Record the completion entry and elapsed time. Re-enter Play Mode to replay; only one attempt is allowed per session, including an aborted attempt.

The component is installed on the existing room controls object. Its explicit references own the existing A/B audio clips; the smoke harness is used only during installation. Current references point to `Assets/Generated/A.mp3` and `Assets/Generated/B.mp3`.

Installation creates only missing objects under `PerformanceMarkers/FirstContact`:

- `LaraCloseMark`: floor/root height, 2m horizontally from ViewMark_Lara toward Lara's current starting position, facing ViewMark_Lara.
- `ViewMark_Final`: 0.55m horizontally from ViewMark_Lounge toward SeatAnchor, retaining the lounge marker height and rotation.

Tune these transforms in Edit Mode if necessary. Reinstalling preserves their positions and rotations, as well as all existing scene transforms, camera framing, Player hierarchy, seating anchors, lighting, smoke, and materials. Select the performance component for marker gizmos.

## Authored sequence

| Beat | Timing / completion |
| --- | --- |
| Opening, clear Lara gaze | 2.25s hold; Player stays in the existing starting composition |
| Player notices Lara | 1.2s finite face LookAt; Lara looks at Player.HeadTransform after 0.45s; require Completed |
| Eye contact | 0.7s |
| Speech A | Require Finished; no camera or locomotion command |
| Post-line pause | 0.5s |
| Track / approach | Track acquire 0.75s, response 0.20s; WalkTo close mark; after 0.3s Player moves to Lara view over 3.5s; require Arrived and Completed |
| Proximity | 0.9s |
| Lounge | Composite SitAt CrossLegs; after 0.6s Player moves to lounge view over 4.5s; require Seated and Completed |
| Seated | 0.8s |
| Final push / Speech B | Player moves to final view over 4s; B starts after 0.5s; require Completed and Finished |
| Final hold | Track for 1s, StopTracking, hold 2s; leave Lara seated and gazing at Player |

Deliberate pauses and elapsed time use scaled gameplay seconds. Pausing gameplay pauses the authored delays. Walking, seating, speech, and finite camera actions advance through their actual completion results, not guessed durations. The target is approximately 28–38s; actual duration is reported after playback rather than forced.

### Body ownership through movement

First Contact retains the final locomotion body pose from the close approach through the proximity hold and lounge approach. Arrival still requires exact root positioning/alignment; it does not require returning to the DAZ glamour idle. SitAt independently retains its approach pose until the seating layer fully owns the body, so its alignment and sit entry cannot expose the glamour pose. The performance releases its hold once seating completes, or on abort. Separate performance and seating holds prevent either owner from accidentally releasing or restoring the other's hold.

Locomotion/seating body transitions have a minimum 0.5s weight overlap. Actual returns to standing idle take at least 1s, including short arrivals and stand-up clips that end before the fade finishes. These runtime minimums apply to existing profiles without rebaking. A repeated locomotion clip no longer blends a Playable against itself with a zero input weight.

For the rerun, watch the close arrival/proximity hold, departure for the lounge, chair alignment/turn, sit entry, and sit-to-cross-legs seam. There should be no intermediate glamour pose. Separately try an ordinary WalkTo and StandUp: their idle recovery should blend for at least 1s. No setup rerun or rebake is required for this correction if First Contact is already installed.

## Mechanical acceptance

- Opening hold, smooth Player turn, and reciprocal gaze occur in that order.
- A plays with SALSA while Lara and camera remain still.
- Lara approaches while Player moves and tracks; both settle without a snap, then pause.
- SitAt handles approach, alignment, sitting, and crossing legs. Player relocates while face tracking continues throughout.
- Lara reaches CrossLegsSeated; the seated pause occurs before the final push.
- B starts shortly after the push begins; seated SALSA works and camera finishes smoothly.
- Tracking stops near the final hold. Lara stays seated with gaze, blink, breathing, and environment motion. No systems visibly fight.
- Note actual A/B durations, total duration, and any errors, warnings, non-success results, or obvious visual problems. Console entries record beat times and expected command completions.

For a separate interference run, re-enter Play Mode and replace an active camera or performer action using its existing debug control. The sequence must report ABORTED and issue no later beats. Concurrent action results are observed independently, so either failure is reported promptly. Camera command revisions identify replacement commands, including replacement tracking of the same target. Cleanup releases only the performance's still-owned camera movement/tracking; it does not overwrite the user's replacement. A still-active performer action may finish because the performer has no semantic cancellation API for locomotion/seating; the aborted sequence schedules no further actions. Disabling the component also aborts.

## Subjective acceptance — all questions

1. Does the first mutual-look beat feel intentional?
2. Does Speech A have enough stillness around it?
3. Does Lara approaching the player feel natural or robotic?
4. Does simultaneous Lara movement + player movement feel elegant?
5. Is Lara's close stopping distance comfortable?
6. Is the proximity pause too short, too long, or right?
7. Does her decision to leave for the lounge read clearly?
8. Does camera tracking through the lounge walk/sit feel like a person following her rather than a turret?
9. Does SitAt now feel like part of a performance rather than an animation test?
10. Does the final camera composition flatter the seated pose?
11. Does moving during Speech B improve the moment or distract from it?
12. Does SALSA still look convincing in the context of the complete character?
13. Does the final silent hold feel alive?
14. What is the SINGLE biggest thing that breaks the illusion?

## Acceptance and commit status

Unity compilation and playback are left to the user's editor and manual run. No observed durations or visual acceptance results are claimed before that run. After mechanical acceptance, commit the implementation and the current migrated/installed scene with `Add first directed Lara performance`, as requested by the execution packet. Commit SHA and observed results remain pending acceptance.
