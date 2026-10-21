# Superseded — actual scene retirement follow-up

The new user-authorized packet replaces the lab move proposal below. All eleven target scene/meta pairs were retired after caller extraction and recovery/reference guards. See [follow-up report](../archive/docs/p2-p3/PHASE1_RETIREMENT_REPORT.md) and [active progress](../archive/docs/p2-p3/PROGRESS.md). No lab move was retried. Original rejected proposal remains historical below.

# Pending approval: consolidate retained diagnostic scenes

This plan was not executed. Automatic approval review rejected the grouped relocation and source/script path rewrites because these scenes have active legacy consumers. Existing scenes and callers remain untouched. This blocks complete Task 1 lab consolidation; it does not introduce a broken reference in First Contact.

The proposed destination is `Assets/DazPose/Validation/Scenes/DevelopmentLab/`. Preserve each scene and meta byte-for-byte, preserve its GUID, update every exact and dynamically constructed producer/loader path, and use Unity AssetDatabase moves with rollback on failure. Do not run these scenes, Play, builds or players. Rerun compilation, retained references, catalog/build-list and byte/GUID checks afterward. No content or art regeneration is proposed.

| Current path | Proposed destination | GUID | Bytes | Current consumer |
|---|---|---|---|---|
| `Assets/Production/Scenes/Day2ActorValidation.unity` | `Assets/DazPose/Validation/Scenes/DevelopmentLab/Day2ActorValidation.unity` | `6147181d1c5f60e458a955b04fbd0627` | 590757 | ProductionPerformerBuilder.cs |
| `Assets/Production/Scenes/Day3CommandValidation.unity` | `Assets/DazPose/Validation/Scenes/DevelopmentLab/Day3CommandValidation.unity` | `8ca719727de320f4ea5de8dd2370e555` | 597994 | RemediationValidation.cs; Day3Build.cs |
| `Assets/Production/Scenes/Day4PartnerContactValidation.unity` | `Assets/DazPose/Validation/Scenes/DevelopmentLab/Day4PartnerContactValidation.unity` | `2e0bf1a76e1ceb448af92d79f7655bfd` | 595804 | RemediationValidation.cs; Day4SceneBuilder.cs |
| `Assets/Production/Scenes/Day5WardrobeValidation.unity` | `Assets/DazPose/Validation/Scenes/DevelopmentLab/Day5WardrobeValidation.unity` | `2eb7586bcdaa7c547a376aa0e22c2ff1` | 589607 | RemediationValidation.cs; Day5WardrobeValidation.cs |
| `Assets/Production/Scenes/ProductionRuntimeSmoke.unity` | `Assets/DazPose/Validation/Scenes/DevelopmentLab/ProductionRuntimeSmoke.unity` | `64a54fb1efde6cf4c866b3f5351559bd` | 866339 | RemediationBuild.cs |
| `Assets/Production/Scenes/RemediationValidation.unity` | `Assets/DazPose/Validation/Scenes/DevelopmentLab/RemediationValidation.unity` | `d5a830258f9b59b4c80583e6a82d53d9` | 872986 | RemediationBuild.cs |
| `Assets/Scenes/PoseValidation.unity` | `Assets/DazPose/Validation/Scenes/DevelopmentLab/PoseValidation.unity` | `1f3c2d21783d2d442b3802aa2c542ca8` | 377892 | DazPoseEditorCommands.cs; RemediationContentPreflightRunner.cs; PerformerSalsaConfigurator.cs |
| `Assets/Scenes/HandGripAcceptance.unity` | `Assets/DazPose/Validation/Scenes/DevelopmentLab/HandGripAcceptance.unity` | `89a9289125c8278428bceb315fb7482a` | 873694 | HandGripAcceptanceSetup.cs; validation runner |
| `Assets/Scenes/PartnerRigAcceptance.unity` | `Assets/DazPose/Validation/Scenes/DevelopmentLab/PartnerRigAcceptance.unity` | `25267b7dc8b0b264d98b96d5840d38cc` | 71987 | PartnerRigAcceptanceBuilder.cs; PartnerCharacterPreparation.cs |
| `Assets/Scenes/LaraHumanoidKawaiiTest.unity` | `Assets/DazPose/Validation/Scenes/DevelopmentLab/LaraHumanoidKawaiiTest.unity` | `374d21527f8ea434a8b0a6bb7f2078eb` | 17577 | LaraHumanoidKawaiiTestSetup.cs |
| `Assets/Scenes/AnimationAudit.unity` | `Assets/DazPose/Validation/Scenes/DevelopmentLab/AnimationAudit.unity` | `e6dea64a9da3e3749b7d9f7394375dbe` | 26445 | AnimationAuditSceneSetup.cs |

The exact preserved-scene and consumer evidence is in `remaining-retirement-decisions.json` and `retirement-manifest.csv`; C00 recovery hashes cover all scenes/metas. Retention is intentional because equivalent specialized diagnostic coverage has not replaced these fixtures. Historical records keep their original paths and are not executable migration targets.
