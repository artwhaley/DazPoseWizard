# R06 — Selecting a source command stages its resulting visual state

**Priority:** P1. **Targets:** `SceneDirectorStudioWindow.cs` and `.Rehearsal.cs`, `RehearsalSession.cs`, canonical snapshots/entry identities as needed.

## User contract
Author selects `await Lara.PersistentPose(SexyVamp)` in First Contact after locomotion. Preview should display Lara at the correct location **in** SexyVamp, from orbit or Player-camera view. Selecting `DissolveTo(Lounger, seated: Lounging)` should stage the seating placement plus pose, not a generic empty Transform.

## Implementation decisions
1. The clickable flow tree must navigate to a specific `(source owner, entry, program counter, visit)` in the *linked canonical executable program*, not just set `selectedPc` or pick the nearest symbol. Offer first visit by default; if looped/repeated, show visit choices or use the current rehearsal trace/selected scenario to choose an occurrence.
2. On selection, perform **deterministic virtual Run Through Selected**: replay from the current compatible checkpoint/path (or safely Reset/replay from start) using the real `StoryMachine`, the current seed/scenario and author decisions. Run the selected instruction, wait for its *virtual logical completion*, apply queued preview effect on Editor thread, then pause before the next instruction. Label `before` vs `after` state clearly. Do not build a second position/pose inference engine.
3. If reaching an instruction requires a branch, UI response, equipment condition, card selection or motion race not resolved by the current scenario, surface the choice and wait. Do not guess a path, silently skip a decision, or allow an endless background loop. Provide a bounded instruction budget and cancellable operation.
4. When a Director selects a globally published card, the tree should expose that card's source text/entry and clickable source locations from the correct source owner. `Open source line` uses `StorySource.file`, retaining the card's source-specific content/pose/audio binding owner. Do not copy the card body into the scene's local file.
5. A source statement that cannot be reached on the chosen path must give an informative unreachable/path message; it must not preview a floating pose and claim the script reached that step.
6. Implement explicit scene-role and seated placement behavior through existing semantic `StoryLocationBinding`, `PerformerSeat`, `PerformerPose` metadata. The preview may display known limitations, but must not author random transforms or modify saved scene state.
7. Keep existing Step/Continue/Pause/Back and `Run to selected`, but make the selection workflow obvious and stable after source rebuilds. Include proper cancellation/teardown for an in-flight navigation on selection/host change.

## Tests
T19–T21, T24. Check both after-command visual position/pose, branch-dependent selection, repeated visited line, card selected through global tags, and correct nested source owner and binding. Native Scene view interaction and aesthetic staging remain user acceptance.

**Done:** direct source click is an actual rehearsal navigation action, not merely a text highlight.
