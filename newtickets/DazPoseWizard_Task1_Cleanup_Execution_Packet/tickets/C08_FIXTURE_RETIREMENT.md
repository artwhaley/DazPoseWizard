# C08 — Retire redundant scenes and individually verified disposable assets

**Dependency:** C00–C07. This ticket should actually simplify the active project, but deletion is conditional on proof, not on a desired count.

## Steps

1. Use the `MIGRATION_MATRIX.md` candidate lists; create `docs/cleanup-task1/retirement-manifest.csv` with `asset | category | current GUID | reference count / consumers | former test responsibility | replacement test/tool | backup path | decision | reason`.
2. **Retire redundant scenes only when replacement is explicit:** AuthoringWelcome, AuthoringCompanion, Day2ActorValidation, Day3CommandValidation, Day4PartnerContactValidation, Day5WardrobeValidation, ProductionRuntimeSmoke, RemediationValidation, PoseValidation, HandGripAcceptance, PartnerRigAcceptance, LaraHumanoidKawaiiTest, AnimationAudit. Do not remove before their builders and maintained regressions are detached. If a meaningful manual diagnostic lacks a replacement, retain it temporarily in the single development lab.
3. Verify the six vendor demo scenes are unused by importer/runtime and licensing. Delete only demonstration scenes, not runtime package components, animation sources for KAWAII bakers, installed shaders or supported vendor tooling. Preserve package provenance.
4. **Exact candidate deletion** from the audit: temporary `FolderIndependentAction`/`Gesture` assets, Day 3 face/expr fixtures, unused proof prefab/materials, obsolete generated skin materials, abandoned candidate wardrobe .assets, fixture-only Day 5 content, obsolete 3 example MP3s, inactive JSON First Contact asset, and four unreferenced 2026 wardrobe attempt directories. Each requires serializer + importer/config + code-path + review-pointer dependency checks. Use Unity delete APIs for asset+meta, record GUID and backup.
5. Never delete `DraftAttempts/a6595a7b018d4a14937b9c70e9c3b8f7` or `Production/Wardrobe/Remediation/R03/second-outfit`: they are known live. Keep all immutable releases and outfit review records. `RemediationToeValidation` plus its fixture assets stay HOLD until the user's pending observation is satisfied.
6. Remove `Production/Story/Programs/FirstContactD1D5.story.json` from active shipped content only after checking the current saved runner uses compiled `.dazstory`; preserve historical format documentation and runtime JSON compatibility adapters. Do not strip `StoryRunner`'s legacy path under this ticket.
7. Do not presume huge action clips are identical just because they have similar names. Compare byte hashes and animation semantic import settings; keep required prepared source/content and move transient test fixture creation into temporary test folders if needed.
8. The audit's ignored review scenes, second ignored Unity project, `TestOutput`, Unity Library, builds and `.dazposewizard` are **inventory/retention recommendation only** in this packet. Do not bulk-delete them. Fix review pointer dependency first (C03).

## Tests / evidence

- After every removal group, Unity `AssetDatabase.GetDependencies` plus path checks report no dangling serialized, script, importer, scene catalog, review pointer or `ScriptedImporter` companion dependencies.
- Normal pose import, SALSA, motion follower, grip/contact, Clothing Setup, global card catalog and First Contact compiled source validation remain intact.
- All retained tests/diagnostics can execute without a deleted scene; no production builders reference retired scene paths.
- GUID uniqueness unchanged except intentionally deleted assets; no missing-component errors in retained scenes; all candidate decisions recorded even if BLOCKED.

## Gate D3

The everyday Unity project is measurably smaller/saner, with a **deletion manifest** and objective replacement coverage. No count-based acceptance or unauthorized deletion of ignored/published/protected data.

## Retire complete fixture ownership, preserve reference loaders

C08 performs the scene deletions planned by C06, after C07 and the per-item recovery/coverage gates. For each retired scene/fixture, map its runner, builder, editor/menu/CLI entry points, generated data, linked tests and active documentation. Delete or relocate fixture-only code when no live caller remains; do not merely hide its MenuItem and leave a callable builder that recreates the retired scene. Update the regression consumer before deleting the original data.

Conversely, retained frozen/review scenes remain deletion roots. In particular the frozen original and published review scenes reference `FirstContactPerformance.cs` (GUID `a8569445824149b08a9e5d9a863f180e` in the reviewed checkout); preserve that script and required examples assembly even after production usage is removed. Keep `WelcomePerformance`, still used by `GameSessionSceneSetup` for a newly initialized scene. Preserve live runtime types in legacy namespaces and JSON/C# compatibility APIs.

Required orphan checks include missing script GUIDs, nonzero fileID references that no longer resolve, broken prefab variants, code/string load paths, review metadata, configured import roots and future producer outputs. A missing-retained-reference failure blocks that deletion, even if normal gameplay does not use it.

Keep `.dazposewizard/archived-expression-imports/` and the prior import-cleanup evidence. Do not rehydrate those archived failed expressions into Assets, prune their recovery data, or claim their prior removal as Task 1 savings. Explicit protected HOLDs (releases, toe closure, reference scripts) are expected retained output, not a deletion target.
