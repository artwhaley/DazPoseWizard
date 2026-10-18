# Phase 1 follow-up retirement report

The initial C00–C10 delivery was partial. This follow-up now retires all eleven targeted obsolete scenes rather than relocating them to a lab. Controlled versioned deletion was used under F03; no Unity/application launch or computer control occurred.

| Scene | Original GUID | Disposition | Replacement |
|---|---|---|---|
| `Assets/Production/Scenes/Day2ActorValidation.unity` | `6147181d1c5f60e458a955b04fbd0627` | RETIRED | ProductionSceneEnvironment + GameSessionSceneSetup with saved template/prefabs |
| `Assets/Production/Scenes/Day3CommandValidation.unity` | `8ca719727de320f4ea5de8dd2370e555` | RETIRED | MaintainedRegressionSuite; existing command/adapter runtime tests |
| `Assets/Production/Scenes/Day4PartnerContactValidation.unity` | `2e0bf1a76e1ceb448af92d79f7655bfd` | RETIRED | PreparedActorDiagnostics; HandGripRuntimeSelfTests; retained Day4ContactSurfaceWitness |
| `Assets/Production/Scenes/Day5WardrobeValidation.unity` | `2eb7586bcdaa7c547a376aa0e22c2ff1` | RETIRED | Existing wardrobe transaction/release tests, ClothingSetup and protected reviews |
| `Assets/Production/Scenes/ProductionRuntimeSmoke.unity` | `64a54fb1efde6cf4c866b3f5351559bd` | RETIRED | Current FirstContact/MainMenu saved reference checks and managed full-session tests |
| `Assets/Production/Scenes/RemediationValidation.unity` | `d5a830258f9b59b4c80583e6a82d53d9` | RETIRED | Extracted negative-content + maintained runtime regression suites |
| `Assets/Scenes/PoseValidation.unity` | `1f3c2d21783d2d442b3802aa2c542ca8` | RETIRED | Selected-character/resolver/adapter diagnostics; asset animation preview; selected SALSA config/audit |
| `Assets/Scenes/HandGripAcceptance.unity` | `89a9289125c8278428bceb315fb7482a` | RETIRED | PreparedActorDiagnostics; hand-grip geometry and spatial proof |
| `Assets/Scenes/PartnerRigAcceptance.unity` | `25267b7dc8b0b264d98b96d5840d38cc` | RETIRED | Prepared male/contact validation + preserved source preparation and pure centerline tests |
| `Assets/Scenes/LaraHumanoidKawaiiTest.unity` | `374d21527f8ea434a8b0a6bb7f2078eb` | RETIRED | Humanoid source asset/avatar/clip diagnostics and generic animation preview |
| `Assets/Scenes/AnimationAudit.unity` | `e6dea64a9da3e3749b7d9f7394375dbe` | RETIRED | Animation catalog/controller asset checks and generic animation preview |

Actually deleted scene/meta files: **22**, **5,482,787 bytes**. No moves or prior Task 1 savings are counted. `F03_SCENE_RETIREMENT_PLAN.json` records exact hashes/bytes/recovery. Source-only wrappers/orchestrators are measured separately in F01_RETIRED_ORCHESTRATION.json and F03_RETIRED_WRAPPERS.json.

Guards passed: original scene/meta recovery hashes; tracked-version recovery; zero incoming current Assets GUID references (including metas); zero current .cs/.ps1/.py target-path consumers. Source compile after retirement: all seven assemblies PASS with warnings as errors. All 669 current protected files remain byte-identical; GUID/meta audit has no duplicates/missing metas.

Native Editor diagnostics, preview rendering, AssetDatabase disappearance and open/close/Undo checks: **NOT RUN under the latest user no-program/no-Unity-control boundary**. No source inspection or compilation is described as native runtime success. Existing historical Editor results are not reused as new passes.

Remaining candidate/orphan closure audit is in progress before final F04 signoff. All 17 remaining scenes will be individually classified; protected review identity/layer decisions, toe appearance, canonical sources and immutable releases remain protected. No arbitrary generic lab was created.
