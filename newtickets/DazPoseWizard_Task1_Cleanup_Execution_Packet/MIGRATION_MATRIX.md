# Scene and asset disposition matrix — mandatory decision record

Relative project paths below begin at `validation/DazPoseUnityValidation/Assets/`. This is the **starting proposal** from `UNITY_PROJECT_CLEANUP_PLAN.md`; current dependency checks and user protections take precedence. For every change record `old path | new path/removal | original GUID | dependency closure | consumer strings repaired | pre/post checks | recovery location`.

## Scenes: KEEP and protect

| Current path | Task 1 disposition |
|---|---|
| `Production/Scenes/MainMenu.unity` | Keep as normal app entry; do not redesign UI. |
| `Production/Scenes/FirstContactD1D5.unity` | Keep full session; optionally rename to `Production/Scenes/FirstContact.unity` by GUID-preserving move **after all code/catalog/Build Settings path consumers are fixed**. No reauthoring, no rebuilt placements. |
| `Production/Environments/VoidEnvironment.unity` | Keep preserved visual template. |
| `Scenes/ClothingSetup.unity` | Keep as supported authoring workspace; optional GUID-preserving move to `Authoring/Scenes/ClothingSetup.unity` after all consumers are fixed. |
| `Production/Scenes/AuthoringShowcase.unity` | Keep **one** reviewed example after old fixture hierarchies are pruned; move to `Examples/Scenes/` only after `StoryFoundationBuilder.ContactStaging()` and any other consumer are independent. |
| `Scenes/FirstPerformanceVoid.unity` | KEEP AT SAME PATH, untouched, frozen artistic authority. |
| `Production/Scenes/RemediationToeValidation.unity` | HOLD in place while the human's toe-appearance observation remains pending. Do not mark approved. |

## Scenes: retire only after gates

| Current path | Condition before retiring |
|---|---|
| `Production/Scenes/AuthoringWelcome.unity` | Preserve simple introductory example in surviving Showcase/docs; keep necessary `WelcomePerformance` initialization fallback. |
| `Production/Scenes/AuthoringCompanion.unity` | Migrate meaningful movement, seating, action, magic and wardrobe no-op regression/example coverage. |
| `Production/Scenes/Day2ActorValidation.unity` | `ClothingSetupBuilder` no longer loads it. |
| `Production/Scenes/Day3CommandValidation.unity` | Relevant expression/command regressions exist outside old saved fixture. |
| `Production/Scenes/Day4PartnerContactValidation.unity` | Measured contact, solver and grip checks retained. |
| `Production/Scenes/Day5WardrobeValidation.unity` | Wardrobe validation retained in supported Clothing Setup and tests. |
| `Production/Scenes/ProductionRuntimeSmoke.unity` | Modern saved scene and regressions cover owned runner/initialization. |
| `Production/Scenes/RemediationValidation.unity` | Historical failure and regression coverage documented and preserved. |
| `Scenes/PoseValidation.unity` | Canonical pose import/browser checks no longer depend on it. |
| `Scenes/HandGripAcceptance.unity`, `Scenes/PartnerRigAcceptance.unity` | One maintained development lab or EditMode diagnostics retain useful inspection/solver capability. |
| `Scenes/LaraHumanoidKawaiiTest.unity` | Keep source/baker/retarget inputs even if scene retires. |
| `Scenes/AnimationAudit.unity` | Retain pose/clip inspection and preview capability in supported editor tools. |

**Vendor demos**: selectively retire six scenes (`Crazy Minnow Studio/Examples/Scenes/SALSA-basic3D-boxhead`, `FemaleMovementAnimsetPro/Scenes/FemaleDemo`, `FemaleMovementAnimsetPro/Scenes/FemaleTopDownDemo`, `FemaleMovementAnimsetPro/Standard Assets/Effects/TessellationShaders/Scenes/TessellationSample`, `INab Studio/Demo Assets/StarterAssets/Environment/Art/Demo Scene`, `KAWAII_ANIMATIOMS_100/Demo/KAWAII_ANIMATIOMS_100`) only if referenced code/package examples and licensing requirements remain usable. Do not delete vendor runtime files or source animation libraries wholesale.

**Ignored review/DAZ scenes**: preserve published first/second outfit review scenes and associated release evidence, canonical `TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity` while importers point there, `TestData/Wardrobe/*/Review*.unity` while package review depends on them. The correct first step is pointer migration, not deletion. `unity/SubtranceB` is a separate ignored project; inventory/archive source, do not delete its `Library` in this packet without explicit authorization.

## Live assets that must not be classified as disposable

- `TestCharacter/lara.fbx` (pose reference input); `TestCharacter/playererect.fbx` (real prepared player model source).
- Void materials and assets currently in `FirstPerformanceVoid/Materials/`, `Meshes/StageGlowRing.asset`, `Prefabs/ValidatedLoungeSeat.prefab`, `Settings/V_FirstPerformanceVoid.asset`, `Shaders/PerimeterSmokeLit.shadergraph`, `Textures/T_FogDensity32.asset`, `Textures/T_SmokeBillow.asset` (plus license dependencies).
- Prepared male mesh `PartnerProof/Generated/G8M_ErectBody.asset`, `G8M_ErectShell.asset`; `PartnerProof/PartnerShell.shadergraph`; live `TestData/PartnerCharacter` materials/metadata/contact.
- Lara body/surfaces/support data in `TestData/LaraCandidate` (including pose/wardrobe sources currently loaded by importers).
- `Production/Wardrobe/Remediation/R03/second-outfit/` and its exact released/draft consumers, immutable wardrobe generations, accepted historical outfits and published reviews.
- Pose/motion, grip profiles, masks, imported content under `DazPose/Generated`, `Animations/Performer`, `Generated/DazPoses/G8F` and `DazPose/Generated/Magic` when referenced/needed by tools.
- Current `Production/Story/Programs/FirstContact.dazstory`, `FirstContact.bindings.asset`, `FirstContact.dialogue-map.json`, `Production/Story/FirstContactMarks.prefab`, all 19 audio/meta bindings, six imported pose wrappers, Disapproval gesture, two funscripts, MagicShader profiles.
- `motiondrive.funscript`, `teleportsound.mp3` while referenced.
- `Wardrobe/WardrobeConfiguration.asset` **draft** and `Production/Wardrobe/LaraConfiguration.asset` **released**; do not merge or pick one arbitrarily.

Move production-owned assets only after updating all hardcoded path consumers, import roots/recipes and companion/source naming conventions. Use sensible destinations under `Production/Environments/Void`, `Production/Performers/Lara`, `Production/Player`, `Production/Content`, `Production/Story`, `Authoring/Wardrobe/Drafts`; immutable releases remain at their existing paths throughout this pass.

## Individually verified disposal candidates (not blanket authority)

- `Content/Remediation/Folder With Spaces/20261006-125650-623/FolderIndependentAction.anim/.asset` and `FolderIndependentGesture.anim/.asset` (~91.8 MiB combined): verify tests no longer require persistent assets; generate temporary fixtures in test scratch instead.
- `Production/ValidationData/Day3Face.anim` and `Day3Expression.asset`: only after command regression transfer.
- `PartnerProof/PartnerCharacter.prefab`, old proof generated body/shell materials: only if runtime/player preparation and tooling have no incoming references.
- Seven unused generated `FirstPerformanceVoid/Materials/LaraSkin/M_LaraSkin_*.mat`: only if unused by serialized plus editor generation paths.
- `Production/Wardrobe/Candidate-first-outfit.asset`, `Candidate-second-outfit.asset`, `Candidate-third-outfit.asset`: verify no incoming references and preserve authoring snapshot.
- `Production/Wardrobe/Day5-20261006-001818/`, `Production/Wardrobe/Remediation/R03/ToeFixture/`, `.../third-outfit/`: HOLD while toe observation is pending; any later retirement requires the recorded user observation and dependent-scene/coverage gates, and must not include immutable or live release data.
- `Generated/A.mp3`, `B.mp3`, `C.mp3`: only after example/audio consumers migrate.
- `Production/Story/Programs/FirstContactD1D5.story.json`: remove from active content after confirming no live scene uses it; preserve historical schema/sample in docs. **Keep** legacy runtime API.
- Four abandoned `TestData/WardrobeRuntime/DraftAttempts/02aa66ebd298461cb41043fd44d637e1`, `94c58ce304554ea4bef67c908ad40e6c`, `ffc251e0c3f54ba5b4d2306f9d6a7c98`, `SetupCandidates/685f686eff13122c46493b5558faed839b468b0bf5bca4cf471f025637a21854` (~614 MiB total) if dependency-free and backed up. **KEEP** `DraftAttempts/a6595a7b018d4a14937b9c70e9c3b8f7`: audit found 14 live files.
- Large `JumpForJoy`/`Celebration-Clip` duplicates: do **not** presume byte identity or identical utility; hash/clip/configure and compare actual dependencies first.

## Storage policy

The audit estimated Assets ~8.43 GiB, Unity Library cache ~7.6 GiB, builds ~1.5 GiB, and `.dazposewizard` ~77.8 GiB. These are historical measurements, not a deletion target. **Do not prune `.dazposewizard`, `TestOutput`, `Library`, builds or ignored working data wholesale**; separately inventory and propose a retention policy. `TestOutput` currently supplies a published wardrobe-review pointer and must not be cleaned until pointer ownership is repaired.

## Reviewed overrides and additional rows (18 October)

These dispositions apply to every earlier candidate row; no broad folder/namespace deletion is authorized.

| Item | Task 1 disposition / gate |
|---|---|
| Immutable releases and `Production/Wardrobe/Remediation/R03/second-outfit/` | **HOLD in place**, including metas, generation identity, payload and pending toe closure. C07 does not relocate them. |
| `FirstPerformanceVoid/Runtime/FirstContactPerformance.cs` and required `DazPose.Examples` scripts/asmdef | **KEEP**: frozen original and retained published review scenes reference the component. Remove production Editor dependency edges and fixture-only callers, not reference loading support. |
| `DazPose/Runtime/DazPoseCharacterState.cs`, `DazPose/Runtime/Partner/` | **KEEP** live runtime types despite the `DazPose.UnityValidation` namespace. No blanket namespace migration. |
| `DazPose/Runtime/Performer/Authoring/WelcomePerformance.cs` | **KEEP** initializer fallback even when AuthoringWelcome scene is retired. |
| `DazPose/Validation/ClothingSetupPreviewRunner.cs` | Extract supported startup to authoring ownership with saved serialization migration; relocate assertions/reporting/batch exit to tests. Do not leave a missing scene script. |
| `DazPose/Editor/Performer/ProductionTestEnvironmentBuilder.cs` and fixture-only builders/checks | Move behind test ownership or retire with caller/coverage manifest; hiding their menu leaves assembly dependencies and regeneration paths alive. |
| `DazPose/Editor/Application/GamePlayerBuild.BuildStartupChecks()` | Retire/relocate historical Showcase-startup build path; public catalog removal alone does not remove this caller. Do not invoke it. |
| `DazPose/Editor/DazPoseBrowserImportProcessor.cs` / Archive Failed Expression Imports command | **KEEP** supported import/recovery tooling and unchanged-failure suppression. |
| Project `.dazposewizard/archived-expression-imports/` | **KEEP recovery archive**, outside watched Assets; never bulk-prune or restore to import queue. |
| Global tagged card sources | Move 17 current tagged declarations into `Production/Story/Cards/`, preserve local Intro, companion bindings and combined oracle. C05 specifies index/link/runtime migration. |
| `*.dazstory` + same-basename `*.bindings.asset` | Adjacent source/companion pair; do not move bindings alone into a generic Bindings folder. |
| Retired scene runner/builder/tests/menu/CLI paths | Record individual keep/extract/retire decisions in C08, including nonserialized callers and protected reference consumers. |

C06 only prepares scene retirements; C08 deletes them after C07. Published reference scenes, retained importer inputs and immutable data are dependency roots even when absent from SceneCatalog. Baseline storage numbers are historical; measure actual current candidate sizes and count new removals separately from moved or already archived data.
