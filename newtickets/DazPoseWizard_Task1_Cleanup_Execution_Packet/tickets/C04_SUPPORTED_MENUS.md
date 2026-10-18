# C04 — Consolidate supported commands; retire misleading legacy menus

**Dependency:** C01–C03. Do not implement the future Scene Director Studio during this ticket.

## Work

1. Inventory all `MenuItem`, CreateAssetMenu paths and currently visible `DazPose` editor commands. Compare `UNITY_PROJECT_CLEANUP_PLAN.md`'s normal 22 commands and 60 gated legacy menu attributes against current code. Distinguish **menu deletion** from **helper-code deletion**.
2. Keep supported preparation/import: New Performance Scene, Initialize Game Session, Add Lara Performer, Add Prepared Player Body, Add Partner, Reconcile Pending Pose Imports, Archive Failed Expression Imports, Bake Action, Bake Gesture, Pipeline Settings, Open Main Menu, Add Current Scene to Scene List, Build Windows Player (button only; do NOT run), current Help docs and Imported Outfits.
3. Retain `New Sequence Script` as advanced C# extension; `.dazstory` remains ordinary source.
4. Replace overlapping First Contact menu entries (`StorySceneBuilder.BuildOrRefresh`, `StoryFoundationBuilder.Prepare`, `StoryFullSessionSetup.Prepare`, random/missing-pose fixer) with **one clear explicit advanced repair/prepare action** that preserves user adjustments and binds the complete saved First Contact. Keep First Contact-specific repair in diagnostics/advanced menu, not as the model new authors must follow. Keep `CompiledStoryAsset` Inspector source/bindings navigation.
5. Unify Wardrobe > Open Clothing Setup and Outfit Configuration into one supported entry point; keep Imported Outfits browser.
6. Keep useful diagnostics/preparation operations behind `DazPose/Diagnostics` or `DazPose/Content/Advanced`: pose bone/FBX inspectors, SALSA audit/setup, contact calibration, retarget/seating baker, imported surface binding baker, partner preparation. Retire duplicated historical scene-builder menus and 1-off fixtures. Do not remove APIs depended on by importers/tests.
7. Consolidate Assets/Create naming to `DazPose` for maintained content types. Hide development-only smoke harness, obsolete audit catalog and acceptance-check assets from ordinary creation lists, keeping regression code as needed.
8. Update Help to current `.dazstory` guide, not retired plans. Preserve original historical documentation in docs.

## Tests

- Visible menu census is documented: one obvious route per supported workflow, no duplicate First Contact setup commands.
- Menu handlers are side-effect-free until clicked; repeat First Contact repair never resets `MainPosition1`, `Bedside`, imported poses/audio or selected wardrobe layers.
- No new menus suggest choosing scene-specific card libraries.

## Gate B4

Author can locate production scene creation, source navigation, supported content imports, Clothing Setup, diagnostics and advanced repair without sifting through old acceptance/test commands.

## Menu retirement must include its remaining callers

Counts in the October 17 audit are historical, not a required target. Include gated commands, context/Assets menus, inspectors and command-line entry points in the census. Preserve the newly added failed-expression archive command and its documentation; it is a supported recovery operation.

The single First Contact repair action must fill missing supported wiring without overwriting existing valid choices. `StoryFoundationBuilder.Prepare` currently reconstructs arrays, saves marks, imports source and adds the scene to the catalog: do not call it unchanged and describe that as nondestructive repair. Capture and compare all seven bound locations, six pose aliases/postures, 19 dialogue bindings, gestures/motion/shaders, actor/player IDs, selected wardrobe and custom valid bindings. Any necessary replacement is an explicit repair diagnostic; no silent re-randomization.

Remove redundant command attributes only after routing legitimate callers to the retained implementation. Remove or relocate obsolete fixture-only builders/checks as part of C02/C08 so hidden methods do not keep development assemblies load-bearing or resurrect retired scenes. Preserve helpers needed by current import, preparation, frozen reference loading or compatibility APIs.
