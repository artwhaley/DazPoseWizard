# F00 — Grounded baseline and scene/dependency inventory

**Goal:** Give the actual executable a correct baseline; no cleanup from memory or folder names.

**Implementation**
1. Record latest `main` commit, branch/worktree, user-owned modifications, Git LFS status. Compare with `935fbd23...` and note any subsequent delta. Do not reset/stash unrelated material. Set a feature branch if appropriate.
2. Read Phase 1 audit artifacts: `docs/cleanup-task1/{EXECUTION_REPORT,PENDING_LAB_CONSOLIDATION,SCENE_AND_CONTENT_OWNERSHIP}.md`, `completed-moves.json`, remaining-retirement decisions if present, `UNITY_PROJECT_CLEANUP_PLAN.md`, `AGENTS.md`.
3. Enumerate every current `.unity` asset (expected from Phase 1 report: 28), its GUID, serialized incoming references, YAML scripts, Unity dependencies, literal/dynamic path consumers, menu builders, static `InitializeOnLoad` side effects, user-published review refs, and build/catalog presence.
4. Classify every scene **player/authoring/template/showcase/frozen-reference/pending-protected-review/obsolete**. For all obsolete scenes, identify their exact remaining producer/loader/validator callers and the substitute test/tool to be built. Record content/asset/FBX dependencies for potential orphan closure separately; current path-sensitive `WardrobeCatalogBuilder.CharacterSignature` remains a real invariant.
5. Hash irreproducible scenes/rigs/materials/canonical FBXs/released wardrobes/toe evidence. Record C00 recovery pointers and off-repo ignored assets. Establish tests and seed 82017 golden run baseline without Unity Play.

**Outputs:** `docs/p2-p3/PHASE1_BASELINE.md`, `PHASE1_SCENE_DISPOSITION.csv`, `PHASE1_DEPENDENCY_GRAPH.json`, protected hashes, test baseline. A future deletion must have an entry here.

**Gate:** Full current scene inventory exists; no ambiguous "test-looking means dead" assumptions, and explicit deferred *user-protected* artifacts are separated from removable scaffolding. No source mutation needed to pass F00.
