# Legacy consumer extraction

The source baseline is main 935fbd23. F00 inventory is being corrected to use actual Assets-relative paths. This follow-up changes executable code; original records remain under docs/cleanup-task1 and Git.

| Former consumer | Current owner / replacement |
|---|---|
| ProductionPerformerBuilder / ProductionTestEnvironmentBuilder | Removed one-time migration/copy shells. ProductionSceneEnvironment + GameSessionSceneSetup already create from the saved art-only template and production prefabs. No production prefab/release generation is repeated. |
| RemediationValidation / RemediationBuild / run-remediation-ticket | Historical ticket dispatcher and build/automatic-Play orchestration removed. MaintainedRuntimeRegressionSuite + MaintainedRegressionSuite retain real expression/speech/motion/dissolve/magic/grip and adapter/morph assertions. Existing runtime R04–R09 regressions and contact witness code remain. |
| R09 negative clip tests | ContentContractDiagnostics extracts the original actual defect/rejection cases with unique owned clip copies and cleanup; inputs are typed gesture/action assets rather than path constants or a saved scene. |
| DazPoseEditorCommands | Saved PoseValidation setup/open and canonical asset-regeneration smoke removed. Selected-character diagnostics, importer/resolver and adapter/morph assertions remain. ImportPipelineChecks already tests copied-input conversion/retry/output identity. |
| SALSA | Configurator resolves selected or active performer; error help no longer directs to PoseValidation. Source pipeline remains unchanged. |
| PartnerCharacterPreparation | Unused acceptance ScenePath removed. Source SHA/topology/weights/endpoints/material/prefab code retained. |
| HandGrip / Partner builder wrappers | PreparedActorDiagnostics provides selected-prefab profile/spatial/contact/centerline checks and direct menus. Old thin forwarding shells become removable once incoming references are absent. |
| Animation audit / Humanoid scene constructors | Asset-level catalog/avatar/controller/clip assertions preserved. AnimationContentPreviewWindow renders only copied visual/rig components with explicit model/clip inputs. No saved per-feature scene needed. |
| FirstPerformanceVoidBuilder | Reduced to frozen source identity used by historical inspection helpers. All rebuild/copy-from-PoseValidation/art-retuning methods removed. Frozen scene untouched. |
| Day5WardrobeValidation | Existing transaction suite already uses a disposable imported candidate context rather than Day5WardrobeValidation scene; assertions retained. Broader draft fixture safety remains tracked separately. |

All current .cs/.ps1/.py sources were scanned: no references to any of the eleven target .unity paths remain. Runtime type namespaces and frozen/reference script GUIDs are preserved. Production Editor retains no validation/example assembly edge.

Seven source assemblies compile using installed Unity compiler with warnings as errors. The new or ported Unity-native diagnostic commands are NOT RUN this turn: the user forbids running the program/control of Unity. Source compilation and 129 pure managed baseline tests are actual results; native diagnostic execution and visual previews are separate pending checks. No mock or unconditional pass replaces an assertion.
