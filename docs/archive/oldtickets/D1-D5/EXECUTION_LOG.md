# D1–D5 execution log

Starting commit: `ca5acab4e6e161985c96710fb63c10a3cd9597fc`.
Branch: `codex/d1-d5-story-runtime`.

- T01: DTOs, versioned JsonUtility serializer and contextual exceptions implemented. Compile/static checks pending the complete dependency stack.
- Play Mode: not run; reserved for the user by this packet.
- T02: strict symbolic binding registry and explicit recursive opcode contract validation implemented; magicShader rejected because no matching typed shader verb exists.
- T03: serialized stories now adapt to the existing PerformanceSequence/host path, reparsing for each fresh Start.
- T04: explicit main-thread dispatcher, awaited flow, per-invocation context, cancellation ownership, and read-only snapshot added. State accessor extension is scheduled in T09.
- T05: invocation-owned Awaitable event rendezvous and cancellable single-modal uGUI prompt service implemented. Runtime references the existing installed Unity.ugui assembly; no new package.
- T06: scoped player body presence uses the existing PerformerDissolve and authored rig/profile without modifying the Lara effect pipeline. Saved body configuration follows in T11.
- T07: application-owned profile, isolated atomic storage, recoverable malformed-file diagnostics, transactional commits, and assignment/history DTOs implemented.
- T08: separated contract/session/world/local state, versioned catalogs and symmetric tag conflicts with seeded compatible selection implemented. Representative items declare no invented conflicts.
- T09: explicit scoped state access/mutations, typed conditions, profile memory commits and catalog-based submission score wired; no reflection setters.
- T10: reusable interests, equipment and clothing panels implemented; existing Settings flow gains profile editor buttons while retaining its original connection/audio controls.
- T11: First Contact serialized negotiation/clothing branches and idempotent Edit Mode scene composer implemented. Exact delivered scene generation/validation is next; no playback.
- T12 in progress: Editor/development F8 snapshot overlay implemented; static and compile gates now begin.
- T12 investigation: Unity inline DTO input deeper than 10 levels truncates silently; managed-reference JSON changes the locked format. Added a lexical nesting guard (8 nested blocks) before JsonUtility so deeper programs fail explicitly. All 45 authored steps round-trip, including the deepest clothing removal branch. Unity still emits its recursive-type metadata warning.

## Final source gate

T01–T12 source/setup complete. Final Unity import/Editor compile has zero errors; 20 bounded Edit Mode data/setup assertions passed. All 49 changed/new compiled source and JSON inputs match the delivered main-project files by hash. Six generated scene/configuration artifacts were copied and hash-verified. The exact saved scene reopens with its host/sequence, six targets, three assets, one prompt service and a bound hidden player body; repeated refresh preserves object count and scene/actor identities.

All three production JSON files parse normally; no TODO/NotImplemented/reflection dispatcher or direct actor/player bypass was found in production Story code; no package dependency changed. The Runtime assembly now references the already installed Unity.ugui assembly. Original Void/Lara/Partner and package manifest hashes match; 4,238 asset GUIDs are unique. Recovery commit 18b268afe4df1ce11046a760b757002571a104c6 still exists.

Known limitations: eight nested blocks (explicit guard before native truncation), documented Unity recursive DTO metadata warning, unsupported magicShader opcode, and missing supplied transcript for retained clip B. New negotiation lines use captions. No new audio was generated.

Play Mode NOT RUN. No standalone build/player playback or user appearance approval. Final status: SOURCE COMPLETE — AWAITING USER PLAYBACK.

Handoff: docs/story-runtime/D1_D5_HANDOFF.md. Exact manual checklist: docs/story-runtime/MANUAL_TEST_CHECKLIST.md.

## October 17 manual observation / repair

User manually played the scene: “Otherwise, it looked just fine,” with the orphaned player-body SALSA error. Fixed the saved scene and builder by removing the bridge/Salsa/QueueProcessor from the non-speaking player-body copy only. Speaking Lara's setup validates and source prefab is unchanged. Compilation and 22 bounded Edit Mode assertions passed. No executor Play Mode; repaired scene awaits user retest. Evidence: .dazposewizard/d1-d5-evidence/20261017-player-body-lipsync/.

## October 17 — production-shape correction

User rejected test marker/controller inheritance and the Lara player-body substitution. Delivered scene now removes the old stage/marker hierarchy and empty ToyControl fixture, retains four used static poses, and binds the actual prepared male/penis/contact setup through a reusable production player prefab variant. New Performance Scene copies a clean saved art-only environment and adds that same body. English Console tracing covers program flow, branches, confirmations, state, completion and cancellation; no per-frame/toy-error output. Documentation and root instructions updated. Unity compile and 34 bounded Edit Mode checks passed, including normal new-scene creation. Executor Play Mode/player build/playback NOT RUN. Revised scene awaits user retest. Evidence: .dazposewizard/d1-d5-evidence/20261017-production-shape/.

## October 17 — authorized Git delivery

The user explicitly authorized commit and push of this work to artwhaley/DazPoseWizard on branch codex/d1-d5-story-runtime. See Git history for the delivery revision. Technical checks and pending manual acceptance remain separate.
