# C06 — Normalize public scenes, catalog and surviving examples

**Dependency:** C02–C05. This is structural scene work, not Main Menu visual redesign. C06 prepares retirements and completes retained scene/catalog changes; actual candidate deletions belong to C08 after C07.

## Main Menu / catalog

1. `Assets/Production/Application/SceneCatalog.asset` is the authoritative existing public scene list, used by `MainMenuController` and `GamePlayerBuild.cs`. Record current IDs/paths, then remove `AuthoringWelcome`, `AuthoringShowcase`, `AuthoringCompanion` and validation fixtures from **public navigation**, without destroying their scenes before dependencies are retired. Keep First Contact publicly launchable; keep Main Menu as application entry point. Do not create a second manually maintained scene registry.
2. Make Unity Editor Build Settings and the existing build helper consistent with the scene catalog, so excluding an example from the catalog also excludes it from a future player package. The menu entry `Game/Build Windows Player` stays present but is **not run** in Task 1.
3. Where safe, rename `Production/Scenes/FirstContactD1D5.unity` → `Production/Scenes/FirstContact.unity` by Unity AssetDatabase move; update `StorySceneBuilder.ScenePath`, `StoryFoundationBuilder`, `StoryFullSessionSetup`, catalog, build settings, tests, docs, hardcoded paths and any saved references **before/with the move**. Preserve original GUID, actor identities, symbolic mark transforms, player contact rig, source/audio bindings and persisted settings. If a specific caller cannot safely migrate, keep the existing scene path and record that blocker; don't duplicate production First Contact scenes.

## Example and development scene shape

4. Keep exactly **one supported example**: start from `AuthoringShowcase.unity`, review its useful movement/seating/action/magic/wardrobe coverage, remove inherited `FirstPerformanceVoid`, `TeleportAcceptance`, `PerformanceMarkers` and redundant acceptance-runner hierarchies where no longer used. Preserve any necessary saved example behavior/coverage and asset GUIDs.
5. First extract `StoryFoundationBuilder.ContactStaging()`'s exact hardcoded dependency on `Production/Scenes/AuthoringShowcase.unity`: use intended production contact/staging data, current bound marks or an explicit supported source rather than silently loading an example. Then, if dependency scan permits, GUID-preserving move Showcase → `Assets/Examples/Scenes/AuthoringShowcase.unity`. The main menu must never expose it.
6. Consolidate HandGrip/PartnerRig/AnimationAudit manual visual inspection into **at most one maintained development lab** if pure diagnostic windows/tests are insufficient. Lab must not enter game catalog, must not be required to launch normal production, and must not copy current performers from protected source scenes.
7. Record other feature-per-scene validation scenes for deletion in C08 only after C07/C08 dependency and coverage gates. C06 does not wait for or perform those deletions. See `MIGRATION_MATRIX.md` for all named scenes and prerequisites. Hold `RemediationToeValidation` and `Scenes/FirstPerformanceVoid`.

## Tests

- Main Menu's catalog has only actual public gameplay scene(s), not authoring/fixture/example entries; the current First Contact ID resolves through the app controller to the intended saved scene in **non-Play structural tests**. This proves resolution, not an actual launch.
- Catalog entries, Build Settings, `GamePlayerBuild`'s computed scene list and references are consistent. No current scenes reference missing paths.
- First Contact scene retains original GUID; saved host, pose/voice bindings, original transform data/positions, full sequence and no preview cutoff. No blank references on renamed/moved scenes.
- Future new production scene creation still copies `VoidEnvironment` rather than `FirstPerformanceVoid`/test staging.

## Gate D1

Small, obvious everyday scene set: Main Menu and First Contact gameplay; Clothing Setup authoring; one Showcase example; Void template and frozen artistic source separate; pending toe fixture held. The public app menu is correct without implementing the Task 4 UI redesign.

## Additional preservation and packaging gates

Preserve `SceneCatalog.Entry.id` **and** `PerformanceSceneBindings.SceneId`/`ActorId` on rename. `GameSettingsService` keys saved tuning by scene/actor IDs, not file paths. Never regenerate IDs, reset tuning or copy user profile files into fixtures to make navigation tests pass.

`GamePlayerBuild.BuildStartupChecks()` explicitly builds a Showcase-startup player even after catalog removal. Retire/relocate that historical helper and update its callers; do not execute it. Inspect all startup-scene overrides, Editor Build Settings, Resources/StreamingAssets, configured preloaded/assets inclusion, and direct runtime loading. Compute lists through a pure helper/test; do not call `Build()` just to inspect a list because it changes defines and invokes BuildPipeline.

Removing inherited example objects requires an explicit hierarchy/component inventory and saved-reference check. Do not delete by broad name or namespace filters: a required seat, bound transform, body/contact target, light or supported performer component may live under a historical name. Retain the example's saved runner/host and wiring. The optional single lab has the same requirement; never inject its runner at Play time.

`StoryFoundationBuilder.ContactStaging()` currently silently returns approximate coordinates when Showcase or its mark is absent. Remove that dependency **and** fallback from production preparation: preserve existing Bedside/MainPosition1 transforms and derive missing marks only from recorded production staging data, with an actionable diagnostic when unavailable. Do not open/resave a protected reference to recreate those values.
