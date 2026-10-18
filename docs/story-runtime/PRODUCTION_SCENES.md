# Production scene composition

The October 17 user correction is authoritative: a production scene must contain production content. The old validation scene is an artistic source, never a runtime fixture dependency. The player body is the prepared male model, including its measured penis/contact-column setup. Lara remains the speaking performer.

## New scenes

Use **DazPose > Scene > New Performance Scene...**. This copies the saved `Assets/Production/Environments/VoidEnvironment.unity`, then saves the session host, Lara, player view, shared clock, production controls and prepared male player body. The template contains only `Environment`, `SceneVolumes` and `Player`. Its floor, smoke, lights, camera and volumes were extracted from the user's saved Void scene without regenerating or retuning them. The original `Assets/Scenes/FirstPerformanceVoid.unity` remains untouched.

`Initialize Game Session` preserves an existing scene's choices; it does not replace an existing composition. **Add Prepared Player Body** adds/binds the reusable player body to an initialized scene. **Add Partner** remains available for a separately authored additional partner. A new scene already has its player contact target, so do not add another partner just to obtain that binding.

Keep choreography marks under `Authoring Marks` and create only marks used by the authored sequence. No PerformanceMarkers, TeleportAcceptance, old performance controller, or acceptance runner belongs in a production scene. Test scenes may have their own saved runners; production scene creation must never copy them.

## Player body

`Assets/Production/Player/PlayerBody.prefab` is a prefab variant of `Assets/Production/Partners/Partner.prefab`. It retains the prepared structural mesh, shell, skeleton, shaft bones, erection channels, material assignments and measured `PreparedContact.asset` / `PreparedSurface.asset`. It has no speaking performer, SALSA bridge, acceptance controller, or synthetic rod substitute. The saved session contact binding points to this body's `PartnerContactTarget`.

The variant starts hidden and retains the prepared root placement/rotation and unit scale. Its three skinned surfaces share one finite dissolve owner, reusing the existing dissolve engine, particle graph and authored style. Surface addresses are baked for the actual male meshes. Original materials are restored at stable visibility, including the shell's authored continuous root fade. Effect materials are separate assets; the shell dissolve shader derives from the exact prepared shell graph. Stop cancels the finite transition through the existing scoped player API.

The full D8 source includes the body-reveal beat. Prepared male geometry/contact bindings are saved; visual acceptance remains the user's manual Unity test. No reimport, contact recalibration or rig replacement was performed.

## Story tracing

Unity Console logs normal story flow by default, with a `[Story <id>]` prefix and clickable scene context. Each step reports what begins and finishes. Logs include voice/pose/target names, timed waits, branch inputs and the selected branch, repeat iterations, incoming events, prompt waits/confirmations, committed preferences, score, selected equipment, clothing, state mutations, checkpoints, cancellation and failure. These are event messages, not per-frame output. Toy/backend failures remain silent as required.

For example:

```
[Story first-contact-d1-d5] Begin: Lara walks to 'step_forward'. [intro-walk]
[Story first-contact-d1-d5] Clothing confirmed: 3 items worn: shoes, shirt, pants.
[Story first-contact-d1-d5] Branch: world clothing count is 3; checking whether it is greater than 2. Result: yes; take the first branch.
[Story first-contact-d1-d5] Waiting for your Ready confirmation. Message: Please remove your shoes.
[Story first-contact-d1-d5] Set world clothing shoes to no.
```

The compiled interpreter also logs Director queries, candidate eligibility/rejection, weights, selected cards and committed progress. Tagged cards are globally published; see [the source guide](DAZSTORY_AUTHORING.md).

## Delivered First Contact composition

`Assets/Production/Scenes/FirstContact.unity` retains the saved art and seven semantic locations: `lara_start`, `player_start`, `camera_start`, `stepforward`, `Lounger`, `MainPosition1`, `Bedside`. Lara has a separately assigned live focus target; the typed location records also resolve the Lounger seat. The representative camera-move bindings are removed. The saved setup marks also live in `Assets/Production/Story/FirstContactMarks.prefab` so rebuilding this composition does not rediscover test markers. The inherited `FirstPerformanceVoid` wrapper and its complete marker tree are removed.

Review the current authored source at `Assets/Production/Story/Programs/FirstContact.dazstory` and the [source guide](DAZSTORY_AUTHORING.md). The inactive JSON asset is retired to a historical sample; runtime JSON compatibility remains. Review console tracing in `Assets/DazPose/Runtime/Story/Runtime/StoryRunner.cs` and `StoryFlowLog.cs`. Edit Mode checks and manual playback acceptance are recorded separately in the handoff.