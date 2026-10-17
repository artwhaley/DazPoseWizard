# DazPoseWizard — D1–D5 Story Runtime Execution Packet

**Repository:** `artwhaley/DazPoseWizard`  
**Target:** current `main` unless the human operator gives a different branch  
**Unity project:** `validation/DazPoseUnityValidation`  
**Purpose:** implement D1–D5 of the story/director roadmap far enough for the human operator to perform the first Play Mode acceptance.

This packet is intentionally an **implementation packet**, not a brainstorming document. Decisions that are still open for later sprints are explicitly out of scope. Do not redesign the system while executing this packet.

## Outcome

At the end of this packet the project must have:

1. A versioned, human-readable **serialized story program** format.
2. A runtime that executes that format through the existing `PerformanceRun`/scoped vocabulary.
3. Awaitable story events and reusable fullscreen story prompts.
4. Player-body dissolve-in/dissolve-out vocabulary using Lara's existing dissolve visual language.
5. Persistent player profile/memory plus per-session contract/state.
6. Extensible kink and equipment catalogs with the agreed tag/state hybrid.
7. Settings surfaces for interests and equipment.
8. Scene-time negotiation surfaces for interests, equipment, and clothing.
9. A **D1–D5 First Contact integration program** that executes setup/negotiation and deliberately stops at the first Director window with a visible/debuggable checkpoint. D6 cards and D7 Director logic do not exist yet.
10. An executor handoff that distinguishes source/static validation from the **human operator's later Play Mode acceptance**.

## Critical execution rule

**DO NOT ENTER UNITY PLAY MODE AUTOMATICALLY. DO NOT CLAIM PLAYBACK.**

The human operator will perform Play Mode acceptance after all D1–D5 work is complete.

Unity batch mode may be used only for deterministic import/compile/editor-setup work if needed. It must not execute the game, enter Play Mode, or be presented as gameplay acceptance.

## Read first

Read these repository files before changing code:

- `AGENTS.md`
- `AUTHORING_READINESS_BUILD_PLAN.md`
- `CONTENT_AUTHORING_API.md`
- `docs/production-consolidation/HANDOFF.md`
- `docs/production-consolidation/progress.json`

Then execute tickets **T01 through T12 in order**.

Do not skip ahead because later tickets rely on types and ownership established earlier.
