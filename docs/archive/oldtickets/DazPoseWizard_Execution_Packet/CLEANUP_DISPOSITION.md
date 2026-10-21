# Concrete Phase 1 completion: disposition matrix

Grounding: `docs/cleanup-task1/PENDING_LAB_CONSOLIDATION.md` describes **11 scenes left in place**. Its rejected grouped relocation is HISTORICAL evidence, **not** the desired action. The user now requires actual retirement of dead scaffolding. The list below is a work manifest, not blanket permission to discard a functional dependency.

| Obsolete scene currently active | Known historical consumer(s) | Required replacement before retirement |
|---|---|---|
| `Assets/Production/Scenes/Day2ActorValidation.unity` | `ProductionPerformerBuilder.cs` | Refactor to instantiate production Lara/prefab and canonical Void; no scene-copy as default setup |
| `Assets/Production/Scenes/Day3CommandValidation.unity` | `RemediationValidation.cs`, `Day3Build.cs` | Pure command/regression tests + saved Showcase or isolated test fixture for remaining manual behavior |
| `Assets/Production/Scenes/Day4PartnerContactValidation.unity` | `RemediationValidation.cs`, `Day4SceneBuilder.cs` | Reusable prepared contact fixture + geometry/solver automated assertions; manual contact preview via Showcase/studio |
| `Assets/Production/Scenes/Day5WardrobeValidation.unity` | `RemediationValidation.cs`, `Day5WardrobeValidation.cs` | Production ClothingSetup and immutable review evidence; migrated wardrobe tests |
| `Assets/Production/Scenes/ProductionRuntimeSmoke.unity` | `RemediationBuild.cs` | Direct tests of saved FirstContact, MainMenu, Showcase and ordinary assembly/scene validation |
| `Assets/Production/Scenes/RemediationValidation.unity` | `RemediationBuild.cs` | Retire historical remediation harness after extracting current validation assertions; retain reports separately |
| `Assets/Scenes/PoseValidation.unity` | `DazPoseEditorCommands.cs`, `RemediationContentPreflightRunner.cs`, `PerformerSalsaConfigurator.cs` | Generic pose preview asset/EditorWindow and SALSA targeting selected performer; content preflight independent of scene |
| `Assets/Scenes/HandGripAcceptance.unity` | `HandGripAcceptanceSetup.cs`, validation runner | Manual contact diagnostics in maintained Showcase/studio; contact unit/integration assertions |
| `Assets/Scenes/PartnerRigAcceptance.unity` | `PartnerRigAcceptanceBuilder.cs`, `PartnerCharacterPreparation.cs` | Character preparation from prefab/source asset and measured contact checks, no acceptance scene prerequisite |
| `Assets/Scenes/LaraHumanoidKawaiiTest.unity` | `LaraHumanoidKawaiiTestSetup.cs` | Retain canonical animation source/proxy/baker, validate output clips directly |
| `Assets/Scenes/AnimationAudit.unity` | `AnimationAuditSceneSetup.cs` | Reusable animation browser/preview and clip checks, no saved audit-scene dependency |

**Default target:** the eleven scene assets themselves and their `.meta` are removed from active Assets once the producer calls and coverage are replaced. An old consumer is not sufficient rationale to move a scene into `DevelopmentLab` and stop. The cleanup pass must also inspect *all* remaining `.unity` scenes from latest project, not merely these eleven.

## Required keep categories

- Product scenes: `Assets/Production/Scenes/MainMenu.unity`, `.../FirstContact.unity` and future authored sessions.
- Authoring: `Assets/Scenes/ClothingSetup.unity` until product authoring studio supersedes its special-case UI; retain its released/draft distinction.
- Authoring example: `Assets/Examples/Scenes/AuthoringShowcase.unity` if it still has real coverage; remove duplicate accepted fixtures from it and never pollute public scene catalog.
- Reusable scene art template: `Assets/Production/Environments/VoidEnvironment.unity`.
- Frozen artistic origin: `Assets/Scenes/FirstPerformanceVoid.unity`; MUST NOT overwrite or delete because the accepted environment originated there.
- Protected pending review: `Assets/Production/Scenes/RemediationToeValidation.unity` until user manually judges toe appearance. Do not claim it is dead without that acceptance.
- Immutable published outfit review scenes / release evidence: retain intact in archival/reference area or source path until release/review metadata is migrated and validated; never destroy acceptance provenance for aesthetic cleanup.

## Other cleanup candidates

- Audit `TestData/WardrobeRuntime/DraftAttempts/ffc251...` and `SetupCandidates/685f...`; currently retained due to real references in Day5 previews/candidate assets. When retire/migrate the dependent *obsolete* preview/candidate assets, re-scan these closures. Remove stale drafts if all live consumers are gone; retain live `a659...` closure. Do not delete any published or actual user-chosen wardrobe configuration.
- Audit `Generated`/`TestOutput`/`ValidationData`/per-feature smoke `.anim`/`.asset` for active consumer closure. Remove obsolete generated fixture outputs after test data moves. Do not confuse generated *production* content with generated *trash*.
- Remove or archive obsolete builder C# shells only after explicit consumers are updated. Preserve reusable portions in production authoring services; keep historical report documents as evidence, not runnable code.
- Do not move source Lara material paths simply to appear tidier: current wardrobe `CharacterSignature` hashes AssetDatabase paths. Design a migration that preserves accepted identity *before* moving, or retain with an explicit real-path-identity contract.

## True completion

Provide all remaining active scene paths, classified ownership and why each exists. All eleven targeted obsolete scenes must be **retired** unless an exact independently indispensable diagnostic purpose is demonstrated and user-visible: if any retained, it's a failed/partial cleanup gate, not 'clean'. Keep one *general* lab only after listing concrete features that no existing example or preview can supply; this is not permission to keep all eleven as children.
