# P0.B1 — Responsive Cross-Legs Exit

Baseline: `c319e5ceaf5f9f72d1dba94bb86d457c092d7ce0` on `main`; worktree was clean before editing.

## Implemented behavior

`AwaitingUncrossSeam` was renamed to `PreparingUncross`, preserving its enum position. A Basic or StandUp request from stable CrossLegs starts preparation immediately. The seating mixer captures the loop Playable at its current time and targets CrossLegs_End at time zero. Both Playables have speed zero. During preparation only the existing mixer blend clock advances; neither clip time nor actor root is advanced.

`PerformerSeatingProfile.CrossLegsExitBlendSeconds` defaults to 0.5 seconds and is separately configurable from BodyBlendSeconds. The baker's Configure defaults it to 0.5. Existing profiles receive the field initializer when loaded; no rebake or seat-contact migration is required for this ticket. The transition duration uses gameplay deltaTime without multiplying by PlaybackSpeed. At zero duration preparation completes immediately.

When the mixer reaches full target weight, the state becomes UncrossingLegs at motion time zero. The next update advances the End clip normally. No second BeginMotion/crossfade occurs. The actor root remains fixed during preparation and uncrossing. The existing baked seat-contact rebase is unchanged.

Basic requests complete with Seated only after the End clip finishes and Basic is restored. Stand requests uncross first, then run Sit_End, and complete with Standing only after standing finishes. Repeated identical Basic or Stand requests join existing waiters without restarting preparation. Existing restrictions on incompatible transitions and chair changes remain.

Runtime no longer reads CrossLegsExitLoopPhase. The profile and measured report retain that metadata for future use; the numerical seam measurement has not been discarded.

The smoke harness displays state, current motion, motion time, exit blend progress and configured duration. During PreparingUncross, expect End motion time 0.00 and progress rising from 0 to 1, then UncrossingLegs with increasing time.

## Manual Unity acceptance

No Unity execution, automated tests, or visual acceptance was performed by the coding agent. The items below remain manual checks.

1. Enter Play Mode in the existing performer acceptance scene and sit CrossLegs. Wait for stable CrossLegsSeated.
2. After a loop wrap, press Stand Up. Preparation should begin by the next evaluated frame, last approximately 0.5 gameplay seconds, and then play the uncross clip from zero. It must not wait nearly 13 seconds for phase 0.999.
3. Repeat exits near loop phases 0/20/40/60/80%, roughly motion times 0/2.6/5.2/7.8/10.4 seconds for the 13-second loop. Evaluate the pose interpolation, foot sliding, chair contact and any visible pop. No multi-seam or IK solution is included.
4. Repeat those phase checks with the Basic style request: PreparingUncross → UncrossingLegs → BasicSeated, completion Seated. For Stand Up: PreparingUncross → UncrossingLegs → Basic → StandingUp → Standing, completion Standing.
5. Click the same Basic or Stand button again during preparation and uncrossing. Progress must continue rather than restart; the duplicate waiters should complete with the same final result. An incompatible request should retain the existing transition restriction.
6. Inspect actor root position/rotation through preparation and the End clip. It should remain at the solved seated frame. Inspect the return to Basic seat contact.
7. Exercise gaze, expression, blink, breathing and Say/SALSA during preparation and uncrossing. These downstream systems should continue operating; they were not modified by this ticket.
8. Change the profile PlaybackSpeed and confirm preparation still takes its configured duration while End playback changes speed. Restore the desired tuning afterwards. Optionally try exit blend duration zero and confirm End begins without becoming stuck.
9. Repeat Sit CrossLegs → Stand → Sit CrossLegs → Basic → CrossLegs → Stand. Check completions, no deadlock, no stale waiter, no permanently frozen clip, and no accumulated seat drift.

Command-to-uncross playback latency is designed to be the configured preparation duration (default 0.5 seconds), plus frame scheduling. Actual latency, visual quality at the five phases, contact stability, downstream life/SALSA behavior and repeated-cycle results are unverified until these checks are performed. No visible failure has been observed because no visual test was run.

## Deferred requirements

Seated foot/floor adaptation remains a known future requirement. The current chair has been adjusted manually. Multiple seating profiles per performer runtime remain deferred; the existing one-profile restriction is unchanged. No IK, foot diagnostics, animation replacement, package restoration or multi-exit pose matching was added.
