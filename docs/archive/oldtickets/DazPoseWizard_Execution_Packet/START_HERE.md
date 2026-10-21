# DazPoseWizard — COMPLETE PHASE 1 CLEANUP + PHASE 2 PLAYER UI + PHASE 3 SCENE DIRECTOR STUDIO

**Execution status:** ready for implementation. **Repository:** https://github.com/artwhaley/DazPoseWizard  
**Verified remote main:** `935fbd23a9aecf90a495176503ed8bd0337c22c4` (18 October 2026, 17:20:39 UTC); implementation commit `a9a31beb497e`. **Unity project:** `validation/DazPoseUnityValidation` — Unity 6000.5.9f1.

## The user correction that overrides the previous plan

The prior cleanup stopped short because an automated agent's grouped-move request was rejected. **That was NOT an author decision to retain eleven old test scenes.** Do not misrepresent it as such. The user now explicitly asks for **careful, thorough cleanup**: if something is dead, remove it; if it has real functionality, extract the useful functionality, then remove its obsolete scaffolding. An arbitrary relocation of eleven independent test scenes is not a clean outcome. This packet REPLACES the former "keep lab untouched unless another approval" clause in any draft packet. Respect actual runtime/tool permission errors, but do not treat the prior agent's rejection as an enduring product requirement.

## Target outcome

**Phase 1 completion (F-series)** — eliminate obsolete per-feature scenes and obsolete producers once supported behavior is moved to reusable code/tests; protect irreproducible authored work. Cut production dependencies on validation scenes. Maintain only useful scene categories (product scenes, template, Clothing Setup, one intentional example, frozen reference, narrowly justified protected reviews/toe evidence). Clean documentation and migration maps, not merely folders.

**Phase 2 (U-series)** — Main Menu is actual user-facing entry point; attractive, responsive reusable uGUI interfaces for interests, equipment and clothing with 100-entry acceptance; proper Apply/Cancel transactions, accessible from Settings and scene prompts; preserved score, profile semantics and player behavior.

**Phase 3 (S-series)** — Scene Director Studio: open any `.dazstory`; compile and discover unresolved typed requirements before assets are assigned; automatically reconcile source-owned content bindings and scene-owned locations; drag and drop resources; place Lara and camera by visible preview; step through **the actual StoryMachine and Director** with safe simulated external effects, branches, Director choices, nested global cards, loops, and deterministic replay; edit real locations via Undo-backed author action only.

**Not included:** authoring the three future new scenes, changing the submission score, rewriting the whole wardrobe system, changing intimacy/consent decisions, adding device preflight gates, running hardware from Editor, publishing code externally, or automatically entering Play/building the game.

## Read first

1. Repository `AGENTS.md`, `UNITY_PROJECT_CLEANUP_PLAN.md`, `docs/cleanup-task1/EXECUTION_REPORT.md`, `docs/cleanup-task1/PENDING_LAB_CONSOLIDATION.md`, `docs/cleanup-task1/SCENE_AND_CONTENT_OWNERSHIP.md` and the current main-branch code.
2. `docs/story-runtime/FIRST_CONTACT_D8_ORACLE.md`, `DAZSTORY_AUTHORING.md`, `D6_D8_HANDOFF.md`; the current FirstContact saved scene and its bindings; current global card index and publishing rules.
3. This packet: `CONTRACT.md`, `ARCHITECTURE.md`, `UI_SPEC.md`, `STUDIO_SPEC.md`, `REHEARSAL_CONTRACT.md`, `CLEANUP_DISPOSITION.md`, `GATES.md`, then `ORDER.md` and each ticket.

## Deliverables

- Executed F tickets, fully honest active-project scene inventory, removal list and retained exceptional items with *concrete* purposes; no dead diagnostic scenes disguised as a lab.
- Finished Phase 2 UI reachable from Main Menu and in-session prompts; proper responsive reusable prefabs; real 100-entry catalog tests.
- Generic EditorWindow `DazPose > Story > Scene Director Studio` with compiler manifest, binding automation, spatial preview and branch/Director rehearsal on real First Contact content.
- Tests, report, author manual checklist and migration data. Each ticket has a pass gate. Code should be checked and committed in small coherent chunks; no push without fresh authorization. Do not claim human visual approval from tests.

## One global selectable card pool

**No scene declares libraries of cards.** `Assets/Production/Story/Cards/` auto-publishes globally tagged selectable cards. `Director.DrawAndExecute(tags: ...)` matches tags and runtime eligibility. New cards work in old unchanged scenes. Explicit narrative `play Intro` remains source-local; the public Director still uses one session RNG/history and canonical selection logic. The Scene Director Studio must read the *same* global index and never create a scene membership registry.
