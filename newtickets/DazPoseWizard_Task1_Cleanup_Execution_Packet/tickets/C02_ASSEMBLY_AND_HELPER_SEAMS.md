# C02 — Clean production Editor compilation dependencies

**Dependency:** C01. The current `Assets/DazPose/Editor/DazPose.Editor.asmdef` references validation/example GUID assemblies; supported Editor tooling must not compile through fixtures.

## Work

1. Resolve each GUID reference in `DazPose.Editor.asmdef` to its actual assembly name (do not delete guessed GUIDs). Draw dependency graph for `DazPose.Runtime`, `DazPose.Content`, `DazPose.Presentation`, `DazPose.Editor`, `DazPose.Validation`, `DazPose.Validation.Editor`, `DazPose.Examples` and third-party dependencies.
2. Identify direct types used by production Editor code from validation/examples. Audit at minimum:
   - `ClothingSetupBuilder` → `ClothingSetupPreviewRunner`/Day 2 fixture;
   - `StoryFoundationBuilder` and performer/player prep → any `DazPose.UnityValidation` type;
   - `LaraCandidateBuilder`, `LaraFirstOutfitBuilder`, `LaraSecondOutfitBuilder`, `LaraSecondOutfitRepair` shared geometry/material/import helpers;
   - contact/partner import and calibration logic;
   - current pose/wardrobe editor tooling and saved scene wiring.
3. Extract stable *helpers* to supported Editor or runtime/content assemblies at correct dependency level. A genuine runtime type referenced by gameplay must live in a production runtime assembly, not an Editor-only or validation assembly. A legacy namespace is not an assembly boundary and does not itself require renaming. Preserve public component/serialization identity when moving MonoBehaviours: if Unity requires an assembly/type migration, provide safe `MovedFrom`/script remapping with tests and unchanged GUIDs. Avoid silently replacing serialized component types.
4. Remove only **now-unneeded** validation/example references from `DazPose.Editor.asmdef`. The dependency direction should be validation/tests → production, not production → validation. Keep legitimate third-party/runtime dependencies.
5. Do not change performer, contact, toy, pose, wardrobe or story behavior as an excuse for refactor. Prefer small extraction with preserved method semantics and tests.

## Tests

- Unity Editor C# compilation (no new CS errors or warnings); supported menu commands instantiate and inspect correctly.
- Asmdef DAG asserts no production Editor dependency on `DazPose.Validation*` or `DazPose.Examples`; resolve actual referenced assemblies, including define/platform variants. Do not reject the legitimate legacy `DazPose.UnityValidation` namespaces in production code.
- Open/validate **saved** First Contact, Clothing Setup, Void and PlayerBody prefab references in Edit Mode; verify script components and source GUIDs survive.
- Managed tests unchanged; targeted importer + contact/wardrobe checks passing.

## Gate B2

Report eliminated dependency edges, moved helper methods and component identity/guid mapping. Do not call a fix complete if it requires adding a new validation dependency in a different location.

## Concrete extraction and retention constraints

- `Assets/DazPose/Runtime/DazPoseCharacterState.cs` and `Runtime/Partner/{PartnerBody,PartnerContactTarget,PartnerContactProfile}.cs` already belong to `DazPose.Runtime`, despite their `DazPose.UnityValidation` namespaces. Retain their public/serialized identity. Many current importer helpers in `Assets/DazPose/Editor` also use this namespace; blanket namespace deletion/renaming is out of scope.
- `Assets/DazPose/Editor/Performer/ProductionTestEnvironmentBuilder.cs` directly uses `DazPose.FirstPerformanceVoid` component types. Move fixture-only builders/checks behind the validation Editor boundary, or retire them after caller audit; extracting only Clothing Setup will not remove the example assembly edge.
- Extract `ClothingSetupPreviewRunner`'s supported startup in C02 with explicit serialization migration; C03 integrates workspace behavior and review metadata. Preserve saved actor/initial-outfit fields. Do not simply change its namespace and leave the existing scene with a missing component.
- Keep `Assets/FirstPerformanceVoid/Runtime/FirstContactPerformance.cs` and its assembly while the frozen `Assets/Scenes/FirstPerformanceVoid.unity` and retained review scenes reference its script GUID. These references are present in the reviewed checkout. Retire its production use/menu/builders, not the protected scene's component.
- Audit methods invoked by shell `-executeMethod`, test projects with linked source, reflection/string type names, and legacy builders. Move their callers/tests with the ownership boundary, update entry-point names, and check that no retired builder can silently recreate a deleted fixture.
- Check both ordinary editor and development-symbol compilation plus the runtime assembly graph with validation symbols absent, without invoking a player build. Do not delete validation defines simply to hide broken compilation. Protected reference functionality may remain in a retained examples assembly; production assemblies must not depend on it.
