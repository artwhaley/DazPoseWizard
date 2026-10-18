# C03 — Make Clothing Setup a stable authoring workspace

**Dependency:** C02. Not a redesign of the Clothing Setup UI, wardrobe editor, or player-facing wardrobe/interest screens.

## Work

1. Inspect `Assets/DazPose/Editor/Wardrobe/ClothingSetupBuilder.cs`, `ClothingSetupWindow.cs`, `Assets/Scenes/ClothingSetup.unity`, the current preview runner and source/review pipeline.
2. Replace any dependency on `Production/Scenes/Day2ActorValidation.unity` as staging/environment source with the existing **saved** `Production/Environments/VoidEnvironment.unity` and the intended production Lara prefab/pipeline. Preserve the current Clothing Setup scene's authored configuration and outfit/user choices. Do not rebuild it wholesale if conversion can occur in place.
3. Move startup/preparation behavior out of validation assertions/report-writing code into supported authoring code. Keep actual regression assertions in validation assembly. Keep the ability to inspect an outfit, commit explicit draft changes, and review release differences without blending draft and released configs.
4. Make the existing supported opening workflow deterministic/idempotent; it should open the saved authoring workspace and authoring window together without creating new test scenes. Do not silently publish candidate wardrobe data.
5. Make published wardrobe review pointers first-class package/review metadata rather than relying on a transient `TestOutput` report path. Repair `WardrobeImportReviewWindow.cs` and preserve migration/old-pointer fallback until reviewed evidence is validated. Do not delete old reports yet.
6. Ensure no normal Clothing Setup action triggers historical rig/surface/material rebake or retune unexpectedly.

## Tests

- Idempotent open/prepare in Edit Mode: same workspace, no duplicate Lara/runner/hierarchy, no unwanted material changes.
- All draft/release layer values and original metadata remain intact; current first and second published review entries still resolve.
- Code dependency on Day 2 validation scene and validation runner removed; archived review pointer loss test passes.
- If references prevent moving `Scenes/ClothingSetup.unity` to `Authoring/Scenes/`, leave the scene at its current path and record blocker rather than creating a second scene.

## Gate B3

Clothing Setup is a supported editor workflow with no Day 2 or `TestOutput` runtime dependency; authored wardrobe distinctions are provably preserved. Human aesthetic reapproval is not implied.

## Review corrections to the concrete workflow

- Changing `EnvironmentScenePath` alone is insufficient: VoidEnvironment is art-only, while the current builder searches Day2ActorValidation for its existing performers. The missing-workspace path must copy the saved Void and explicitly instantiate/configure the supported Lara prefab. Never save changes into the template. Existing ClothingSetup must migrate in place, retaining camera, actor, draft references and user choices; do not rerun its original camera/position defaults.
- The current preview `Start()` initializes/undresses/redresses the actor, writes `TestOutput/day5/setup-runtime.json`, and can stop Play/quit in batch mode. Supported startup retains only readiness/visibility/outfit initialization, respects the saved selection and normal cancellation/lifecycle; regression actions/reporting/exit behavior belong exclusively in tests. It must remain a saved scene component so manual Play needs no helper injection.
- Migrate published review metadata through the **publisher and reader**, not just `WardrobeImportReviewWindow`. Locate every writer of `publishedReviewScene` in repository and local configured tooling. Key a review by package/release/generation identity, not outfit ID alone; preserve timestamped published paths and all currently resolved reviews (including third-outfit history where present).
- Use mutable authoring/package metadata or a sidecar outside immutable release payloads for migrated pointers. Never rewrite a release to add a field. New metadata takes precedence; read old report pointers only when no authoritative new entry exists, and never overwrite a current pointer from stale reports. Migration must be explicit/idempotent, not an OnGUI/on-open rewrite.
- `WardrobeImportReviewWindow` currently calls `LaraFirstOutfitBuilder.ConfigurePresentation()` immediately after opening a review. Decouple useful viewer controls from historical retuning. Opening a published review must not apply replacement artistic defaults.
- Prove pointer migration with temporary metadata/report fixtures: after the simulated report is unavailable the same published scene still resolves; absent/malformed metadata gives an actionable diagnostic without guessing a different generation. Keep the real reports until their complete consumer closure is retired.

Opening the tool in Edit Mode and inspecting its saved component wiring is technical evidence only; dressed/visible startup on actual Play remains the user's manual check.
