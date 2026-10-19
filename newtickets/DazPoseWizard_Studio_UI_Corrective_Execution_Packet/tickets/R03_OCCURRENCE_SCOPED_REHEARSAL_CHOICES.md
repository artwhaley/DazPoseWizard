# R03 — Per-occurrence choices for Director, conditionals and races

**Priority:** P0. **Targets:** `StoryRehearsal.cs`, `StoryMachine.cs`, `RehearsalSession.cs`, `RehearsalStoryHost.cs` (scenario DTO) and Studio rehearsal UI.

## Verified defect
`DirectorKey(source)` stores `source-unit|declaration|line`; the source location stays identical for every execution of a `while` loop. Existing dictionaries for Director, branch and WaitAny overrides consequently apply to repeated visits. A one-off Force button can poison later card draws.

## Fixed identity design
1. Add a **visit identity** to the canonical pre-instruction/snapshot hook. Model `InstructionVisit = (source identity, declaration, program-counter/site, dynamic encounter ordinal within this rehearsal execution)`. If nested invocation ambiguity requires it, include call-stack invocation identity; maintain one stable computation inside `StoryMachine` for all decision types. Never derive identity from wall clock or actor position.
2. Assign encounter ordinal on reaching `BeforeInstruction` (once per execution), reuse the same visit ID in branch, Director and WaitAny decision callbacks, trace, and `AfterInstruction`. Repeated draws at the same PC get distinct IDs. Reset/replay with the same seed/scenario/path produces the same IDs.
3. Studio's **Force** button applies to the *currently paused instruction visit*. Save scenario selections by visit ID. Consume a forced choice **once**, atomically; no implicit site-wide persistence. A different future visit to the same source line must draw normally unless explicitly overridden.
4. Use the same visit-scoped semantics for branch true/false and race event choices. When a user selects an event in a paused wait, ensure the event is valid for that wait. If a decision hasn't been configured, the simulator should pause with an explicit choice rather than invent an outcome.
5. Keep Director's canonical eligibility, weight, cooldown, progress and RNG untouched. Forced eligible choice uses the existing `StoryDirector.Select(request, forcedCard)` contract and consumes exactly one normal selection draw; ineligible force is rejected. Do not rewrite the random service or make a second Director.
6. Scenario serialization should include a schema/version and source/program fingerprint. For old site-key scenario files, import safely as *explicitly marked legacy*, require user review or convert only if unambiguous; never silently apply a site choice to every loop visit. A source edit invalidates/reconciles old visit IDs.
7. Clear one-off transient choices on Stop/Reset as appropriate; persisted scenario choices remain available for intentional replay. Keep cancellation and owned async task behavior intact.

## Tests
T10–T14. At one exact Warmup PC execute at least three iterations and force only #2. Include a branch and WaitAny in a repeat loop; prove only selected visits are overridden. Test rollback/back replay, ineligible card rejection and RNG continuity.

**Done:** source-site identity and execution-visit identity are distinct, and a forced selection does not accidentally affect subsequent loop iterations.
