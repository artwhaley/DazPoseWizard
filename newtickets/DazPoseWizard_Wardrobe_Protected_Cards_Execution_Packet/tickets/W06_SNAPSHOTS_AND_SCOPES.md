# W06 — Complete presentation snapshots and scoped ownership

Depends W05. Read all API sections 4–6, W00 writer coverage and CHECKS RT01–RT10.

## Files

Language/StoryCardPresentation and StoryMachine active-task tracking; PerformanceRun + new Authoring/PerformanceOperationScope; new Authoring/PerformancePresentationSnapshot, PerformancePresentationService, CardPresentationScope; SuccubusPerformer snapshot partial and owned pose/seating/gaze/material/aura/grip components; Player/Runtime PlayerView and PlayerBodyPresence snapshot partials; PerformanceStoryHost; prompt/action/signal ownership where needed. Explicit recording interface implementations in managed hosts, including retained rehearsal diagnostic hosts. Studio still does not execute these.

## Steps

1. Implement immutable subsystem snapshots with typed readable control state and exact refs, not cancellation closures, mode-only enums or hierarchy serialization. Add persistent seated overlay pose identity, exact shader profile and camera tracking settings capture inside their owners.
2. Implement run-local scope stack, per-operation captured owner, bounded active-operation collection, root-specific service cleanup and scoped action/subscription tokens. Completed persistent commands move into owned handles rather than remain historical active operations.
3. Implement finite entrance barrier excluding current/ancestor invocation tasks. Stable tracking/idle continues. Capture exact target/asset state after settlement; no source replay.
4. Implement scoped cleanup and ownership adoption after restoration. New command generations replace stale parent cleanup; child disposal cannot cancel restored parent ownership. Stop invalidates every scope and cancels pending return immediately.
5. Implement prepare/diff/verify domain APIs and snapshot pins. Validate parent/seat/target/generation identity before mutation. Add exact endpoint data types and body/view restore API shape; W07 completes coordinated choreography.
6. Update W00 coverage with implemented APIs/check IDs. Cover current persistent facade controls including expression/gaze/aura/grip/follow. No blanket unsupported-active-grip rejection to call full state complete. Hardware/output is never replayed; parent motion ownership rule is fixed in API.

## Checks / exit

RT01–RT10 with pure state/recording adapters linked to production scope protocol and decisions. Cover parent/child owner isolation, delayed completion, entry self-wait, pins, generation adoption, Stop and fault disposal. Source compile all affected assemblies. Managed stubs validate protocol only, never native visual evidence.

Do not enable protected card completion until W07 can actually restore. Do not implement scope exit by stopping the session. Next W07.
