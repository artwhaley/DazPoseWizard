# Review against pushed Phase 1 and correction to the earlier draft

## Repository evidence from remote main

On October 18 the remote main head was `935fbd23a9aecf90a495176503ed8bd0337c22c4`, commit title "Record cleanup implementation commit in handoff". Implementation prior commit: `a9a31beb497e`, title "Consolidate production authoring and global story cards". `docs/cleanup-task1/EXECUTION_REPORT.md` states **PARTIAL**; 11 diagnostic scenes were neither retired nor moved; 163 asset moves, two retired wardrobe draft roots, eight scenes removed, and global selectable cards delivered. The rejected attempt was an automated approval decision about *one grouped relocation*, not a user instruction. The user now insists on thorough real cleanup.

## Exact discrepancies needing correction, not a rewrite

1. `DazStoryImporter` still calls compiler with `asset.content.Symbols()` and generically creates transient bindings only; current source-first authoring flow still needs preassigned symbol names. Fix S01/S02.
2. `StoryContentBindingsEditor` still labels its pose choices "First Contact" and presents manually refreshed dialogue entries. Make generic and reconcile automatically S02/S05.
3. `StorySceneBindings` typed scene objects are legitimate but only Inspector-level; create Studio staging through S06; remove FirstContact-only default resolver from generic new scene flow.
4. Global catalog is already genuinely auto-indexed via `GlobalStoryCardIndexBuilder`/`StoryLinkedContent`. Preserve it and demonstrate runtime new-card-on-old-scene behavior, do **not** implement scene library membership.
5. `MainMenuController` is a simple button-per-entry view, UI forms are giant imperative rows, settings and session share only basic callbacks. Phase 2 transforms presentation without changing profile semantics.
6. Eleven obsolete scene consumers were intentionally left unresolved by a prior agent. F00–F04 replaces old grouped relocation with actual caller extraction, coverage migration and deletion of dead scenes.
7. Wardrobe signature hashes material **paths**; do not relocate imported Lara material roots blindly. Archived published outfit history and pending toe acceptance are concrete protected user work, not miscellaneous fixture hold.

## Change in packet compared with previous unsubmitted Phase 2/3 draft

Old draft: "do not attempt rejected legacy lab scene migration; keep scenes untouched unless separate authorization." **Removed.** New F-series requires actual retirement after dependency elimination. The user explicitly requested this correction. Automated tool access limits, if encountered again, must be described accurately but cannot be misrepresented as user preference.
