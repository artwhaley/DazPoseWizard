# C09 — Current authoring navigation, repo naming, and ownership documentation

**Dependency:** C01–C08.

## Work

1. Make repo README and Unity authoring README point to the currently supported workflows: production New Performance Scene, First Contact entry, `.dazstory`, pose importer, Clothing Setup, application Main Menu, global tags/Director, contact/motion preparation, current Docs/help menus. Remove pointers that ask normal authors to start from old `Tools > DAZ Pose` or legacy one-off scenes.
2. Publish `docs/cleanup-task1/SCENE_AND_CONTENT_OWNERSHIP.md` explaining gameplay vs authoring vs examples vs frozen reference vs validation, paths actually delivered, and why protected imported assets may temporarily remain at historical paths.
3. Document exactly how a **new tagged global selectable card** is published/discovered, with one concise example. It must not mention scene-declared card libraries, `SceneDefinition.allowedCardLibraries`, manual catalog registration or a scene-specific card import list.
4. Update `.dazstory` authoring guide with the distinction: an explicit local story beat vs a globally selectable tagged card. Preserve source readability and current `submissionScore`, session pacing and tagged equipment semantics.
5. Update any file/command paths changed in the cleanup, including First Contact scene rename if performed, Clothing Setup location, example path, compiler/binder path, asset import roots and scene catalog. Cross-check editor menu Help targets.
6. Historic plans, Day1–Day5 and R01–R13 reports, D1–D8 packets and archived acceptance evidence are **history**, not current implementation instructions. Preserve them but clearly link the current entry point. Do not rewrite historical documents to pretend they predicted the delivered runtime.
7. Explicitly mark still-pending: First Contact full human visual acceptance, second-outfit toe appearance review, the player UI modernization, Scene Director Studio binding/placement/rehearsal, and the three new authored scenes.
8. Record any unchanged but misleading folder names as intentional holdovers with known consumers and suggested later action; no false claim that all cleanup is complete.

## Gate D4

A new engineer/author can identify the right Unity project, ordinary supported tools, normal game scenes, reusable global cards, and known protected/artifact holdovers from the current README without browsing historical ticket stacks.

## Current-authority handoff and preservation

Update `AGENTS.md` current-work pointers, `docs/production-consolidation/progress.json`, `HANDOFF.md`, `docs/story-runtime/D6_D8_HANDOFF.md` and relevant active import/wardrobe guides **during implementation**, with only verified cleanup results. This packet review alone must not advance implementation state. Preserve existing handoff edits and historical acceptance evidence; do not overwrite those documents wholesale.

Resolve current links affected by the user's D1-D5 → oldtickets move against actual paths, while keeping historical event descriptions intact. The named Post_D8 roadmap is unavailable in this checkout and is not a prerequisite; START_HERE and ROADMAP_CORRECTION define this phase's scope.

Document `DazPose/Content/Archive Failed Expression Imports`, archive recovery and the difference between automatic unchanged-failure suppression and explicit retry. Link the preserved `docs/IMPORT_FAILURE_CLEANUP.md` instead of inventing another queue-clearing workflow.

Global-card documentation must name the publication root, automatic companion/index workflow, source-local vs public IDs, asset/scene-role resolution, invalid-content diagnostics, index invalidation and how to remove a published card. Keep the author workflow free of pasted GUIDs and manual per-scene registration. Preserve the current Inspector's source/bindings navigation; the Studio redesign remains future work.
