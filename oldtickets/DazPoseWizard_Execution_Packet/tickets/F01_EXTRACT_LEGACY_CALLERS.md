# F01 — Remove active callers of historical diagnostic scenes

**Depends:** F00. **Goal:** Make the eleven obsolete scenes removable, not merely movable.

For each row in `CLEANUP_DISPOSITION.md`, refactor current callers onto reusable production/editor facilities:
- `ProductionPerformerBuilder` uses saved clean Void/Lara production prefabs, no `Day2ActorValidation` dependency.
- `RemediationValidation`, `Day3Build`, `Day4SceneBuilder`, `Day5WardrobeValidation`, `RemediationBuild` retain meaningful assertions via generated temporary EditMode fixtures or maintained independent tests, not specific historical scene names.
- `DazPoseEditorCommands`, `RemediationContentPreflightRunner`, `PerformerSalsaConfigurator` operate on selected/imported performer and typed poses directly. SALSA source/pipeline untouched. No `PoseValidation` locator.
- `HandGripAcceptanceSetup` and Partner preparation take production prepared male/Lara refs or an explicit workspace context, not static `HandGripAcceptance`/`PartnerRigAcceptance` scene path.
- KAWAII locomotion and AnimationAudit bake/inspect call shared clip/pose preview and source model, not the old `LaraHumanoidKawaiiTest` or `AnimationAudit` stage.

Extract useful utilities from old scene-building shells into appropriately named, small services under production Editor/Runtime. Remove dead builder paths after all incoming references are migrated. Preserve GUID identities of *needed* content/prefab assets. Do not synthesize new pose/character/fog sources. Use exact current code search before each migration; audit any dynamic filename concatenations.

**Tests:** Open/init current production New Performance Scene; SALSA configuration on selected Lara; contact import/profile validation; walking/seat clip baker; referenced wardrobe import and Clothing Setup. None should require any of the eleven obsolete scenes. Compile Unity Editor + production assemblies without validation-source dependencies.

**Gate:** Current normal operations and validation service paths have zero runtime/file dependencies on the eleven scenes. Retained diagnostic assertions have explicit new owners; no behavioral substitute merely returns true.
