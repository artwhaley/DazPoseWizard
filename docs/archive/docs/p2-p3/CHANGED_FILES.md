# Changed implementation files

Compared with starting SHA `935fbd23a9aecf90a495176503ed8bd0337c22c4`; final evidence/guide files are in the following documentation commit. The user ZIP remains untracked and excluded.

```text
M	.gitignore
M	AGENTS.md
M	docs/cleanup-task1/EXECUTION_REPORT.md
M	docs/cleanup-task1/PENDING_LAB_CONSOLIDATION.md
M	docs/cleanup-task1/SCENE_AND_CONTENT_OWNERSHIP.md
A	docs/p2-p3/F00_SCENE_INVENTORY.json
A	docs/p2-p3/F01_EXTRACTION_MAP.md
A	docs/p2-p3/F01_RETIRED_ORCHESTRATION.json
A	docs/p2-p3/F02_DIAGNOSTIC_CASES.md
A	docs/p2-p3/F03_ADDITIONAL_PREVIEW_PLAN.json
A	docs/p2-p3/F03_ORPHAN_AUDIT.json
A	docs/p2-p3/F03_ORPHAN_RETIREMENT_PLAN.json
A	docs/p2-p3/F03_RETIRED_WRAPPERS.json
A	docs/p2-p3/F03_SCENE_RETIREMENT_PLAN.json
A	docs/p2-p3/F04_SOURCE_AUDIT.json
A	docs/p2-p3/PHASE1_BASELINE.md
A	docs/p2-p3/PHASE1_DEPENDENCY_GRAPH.json
A	docs/p2-p3/PHASE1_RETIREMENT_REPORT.md
A	docs/p2-p3/PHASE1_SCENE_DISPOSITION.csv
A	docs/p2-p3/PHASE2_REPORT.md
A	docs/p2-p3/PROGRESS.md
A	docs/p2-p3/PROTECTED_BASELINE.json
A	docs/p2-p3/TMP_RESOURCES.md
A	docs/p2-p3/UI_COMPONENT_IDENTITIES.json
M	docs/production-consolidation/HANDOFF.md
M	docs/production-consolidation/progress.json
A	newtickets/DazPoseWizard_Execution_Packet/ARCHITECTURE.md
A	newtickets/DazPoseWizard_Execution_Packet/CHANGELOG_AND_REVIEW.md
A	newtickets/DazPoseWizard_Execution_Packet/CLEANUP_DISPOSITION.md
A	newtickets/DazPoseWizard_Execution_Packet/CONTRACT.md
A	newtickets/DazPoseWizard_Execution_Packet/EXECUTOR_PROMPT.md
A	newtickets/DazPoseWizard_Execution_Packet/GATES.md
A	newtickets/DazPoseWizard_Execution_Packet/MANIFEST.json
A	newtickets/DazPoseWizard_Execution_Packet/ORDER.md
A	newtickets/DazPoseWizard_Execution_Packet/REHEARSAL_CONTRACT.md
A	newtickets/DazPoseWizard_Execution_Packet/START_HERE.md
A	newtickets/DazPoseWizard_Execution_Packet/STUDIO_SPEC.md
A	newtickets/DazPoseWizard_Execution_Packet/UI_SPEC.md
A	newtickets/DazPoseWizard_Execution_Packet/templates/MANUAL_ACCEPTANCE.md
A	newtickets/DazPoseWizard_Execution_Packet/templates/PHASE1_RETIREMENT_REPORT.md
A	newtickets/DazPoseWizard_Execution_Packet/templates/PROGRESS.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/F00_BASELINE_AND_EXHAUSTIVE_INVENTORY.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/F01_EXTRACT_LEGACY_CALLERS.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/F02_PRESERVE_MEANINGFUL_DIAGNOSTICS.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/F03_RETIRE_SCENES_AND_ORPHAN_FIXTURES.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/F04_CLEANUP_REGRESSION_AND_REPORT.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/S01_SOURCE_REQUIREMENTS_ANALYSIS.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/S02_BINDINGS_RECONCILE_AND_SOURCE_OWNERSHIP.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/S03_STUDIO_WINDOW_AND_SCRIPT_NAVIGATION.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/S04_ISOLATED_PREVIEW_STAGE.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/S05_BINDING_DRAG_DROP_AND_CONTENT_PREVIEW.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/S06_SEMANTIC_MARKS_CAMERA_SEATING_CONTACT.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/S07_CANONICAL_INTERPRETER_STEP_HOOKS.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/S08_REHEARSAL_HOST_STATE_OVERRIDES.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/S09_GLOBAL_DIRECTOR_CARD_REHEARSAL.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/S10_REAL_SCENE_STUDIO_INTEGRATION.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/S11_DOCUMENTATION_DELIVERY_AND_MANUAL_HANDOFF.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/U01_MENU_LAUNCH_AND_NAVIGATION.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/U02_SHARED_UI_FOUNDATION.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/U03_TRANSACTIONAL_PROFILE_EDIT.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/U04_INTERESTS_CATALOG.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/U05_EQUIPMENT_CATALOG.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/U06_MODAL_SETTINGS_AND_INSESSION.md
A	newtickets/DazPoseWizard_Execution_Packet/tickets/U07_PHASE2_FINAL_VERIFICATION.md
A	scripts/generate-production-ui-assets.py
D	scripts/generate-remediation-source-move-manifest.ps1
D	scripts/open-handgrip-manual.ps1
D	scripts/run-remediation-ticket.ps1
A	tests/DazPose.Player.Tests/CatalogSearchTests.cs
A	tests/DazPose.Player.Tests/ClothingEditSessionTests.cs
A	tests/DazPose.Player.Tests/DazPose.Player.Tests.csproj
A	tests/DazPose.Player.Tests/LargeCatalogTransactionTests.cs
A	tests/DazPose.Player.Tests/ProfileEditSessionTests.cs
A	tests/DazPose.Player.Tests/ProfilePersistenceTests.cs
A	tests/DazPose.Player.Tests/ProfileTestSupport.cs
A	tests/DazPose.Player.Tests/UnityEngineShims.cs
A	tests/DazPose.Rehearsal.Tests/DazPose.Rehearsal.Tests.csproj
A	tests/DazPose.Rehearsal.Tests/FirstContactRehearsalTests.cs
A	tests/DazPose.Rehearsal.Tests/RehearsalTestSupport.cs
A	tests/DazPose.Rehearsal.Tests/UnityTestShims.cs
M	tests/DazPose.Story.Tests/DazPose.Story.Tests.csproj
A	tests/DazPose.Story.Tests/NavigationTests.cs
A	tests/DazPose.Story.Tests/RehearsalTests.cs
A	tests/DazPose.Story.Tests/SourceAnalysisTests.cs
D	validation/DazPoseUnityValidation/Assets/Content/Remediation/Folder With Spaces/20261006-125650-623/FolderIndependentAction.anim
D	validation/DazPoseUnityValidation/Assets/Content/Remediation/Folder With Spaces/20261006-125650-623/FolderIndependentAction.anim.meta
D	validation/DazPoseUnityValidation/Assets/Content/Remediation/Folder With Spaces/20261006-125650-623/FolderIndependentAction.asset
D	validation/DazPoseUnityValidation/Assets/Content/Remediation/Folder With Spaces/20261006-125650-623/FolderIndependentAction.asset.meta
D	validation/DazPoseUnityValidation/Assets/Content/Remediation/Folder With Spaces/20261006-125650-623/FolderIndependentGesture.anim
D	validation/DazPoseUnityValidation/Assets/Content/Remediation/Folder With Spaces/20261006-125650-623/FolderIndependentGesture.anim.meta
D	validation/DazPoseUnityValidation/Assets/Content/Remediation/Folder With Spaces/20261006-125650-623/FolderIndependentGesture.asset
D	validation/DazPoseUnityValidation/Assets/Content/Remediation/Folder With Spaces/20261006-125650-623/FolderIndependentGesture.asset.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/AnimationContentPreviewWindow.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/AnimationContentPreviewWindow.cs.meta
M	validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/DazStoryImporter.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/GameUiAssetBuilder.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/StoryContentBindingsEditor.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Editor/LaraCandidateBuilder.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Editor/Partner/PartnerCharacterPreparation.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Editor/PerformerSalsaConfigurator.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/RehearsalSession.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/RehearsalSession.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/RehearsalStoryHost.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/RehearsalStoryHost.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/SceneDirectorStudioWindow.Rehearsal.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/SceneDirectorStudioWindow.Rehearsal.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/SceneDirectorStudioWindow.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/SceneDirectorStudioWindow.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/SceneMarkAuthoring.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/SceneMarkAuthoring.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/ScenePreviewSession.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/ScenePreviewSession.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/SourceBindingReconciler.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Editor/StoryStudio/SourceBindingReconciler.cs.meta
M	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/DazPose.Presentation.asmdef
M	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/MainMenuController.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/StorySettingsProfileController.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/CatalogDetailView.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/CatalogDetailView.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/EquipmentRowView.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/EquipmentRowView.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/InterestRowView.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/InterestRowView.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/LabeledToggleRowView.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/LabeledToggleRowView.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ModalShellView.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ModalShellView.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ProductionModalFactory.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ProductionModalFactory.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ProductionUiLibrary.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ProductionUiLibrary.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ProfileCatalogPanelView.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ProfileCatalogPanelView.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ProfileEditorPresenter.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ProfileEditorPresenter.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/RecycledListView.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/RecycledListView.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/SceneBrowserView.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/SceneBrowserView.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/SceneCardView.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/SceneCardView.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ThemeGraphic.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/ThemeGraphic.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/UiTheme.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Presentation/UI/UiTheme.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/CatalogSearch.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/CatalogSearch.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/ClothingEditSession.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/ClothingEditSession.cs.meta
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/GameApplication.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/PlayerProfileEditSession.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/PlayerProfileEditSession.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/PlayerProfileJson.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/PlayerProfileJson.cs.meta
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/PlayerProfileService.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/SceneNavigationCoordinator.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Application/SceneNavigationCoordinator.cs.meta
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Language/DazStoryCompiler.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Language/StoryCards.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Language/StoryMachine.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Language/StoryRehearsal.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Language/StoryRehearsal.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Language/StorySourceAnalysis.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Language/StorySourceAnalysis.cs.meta
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Runtime/PerformanceStoryHost.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Runtime/StoryContentBindings.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Runtime/StoryRunner.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Runtime/StoryValueAccessor.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Serialization/StoryValue.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/Serialization/StoryValue.cs.meta
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/State/EquipmentDefinition.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/State/KinkDefinition.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/UI/StoryModalPresentation.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/UI/StoryModalPresentation.cs.meta
D	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/UI/StoryProfilePanels.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/UI/StoryProfilePanels.cs.meta
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/UI/StoryPromptResult.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/UI/StoryPromptService.Editors.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Runtime/Story/UI/StoryPromptService.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/DazPoseEditorCommands.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/ProductionPerformerBuilder.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/ProductionPerformerBuilder.cs.meta
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/ProductionTestEnvironmentBuilder.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Authoring/ProductionTestEnvironmentBuilder.cs.meta
M	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/DazPose.Validation.Editor.asmdef
A	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Diagnostics/ContentContractDiagnostics.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Diagnostics/ContentContractDiagnostics.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Diagnostics/MaintainedRegressionSuite.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Diagnostics/MaintainedRegressionSuite.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Diagnostics/PreparedActorDiagnostics.cs
R083	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/RemediationValidation.cs.meta	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Diagnostics/PreparedActorDiagnostics.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Diagnostics/ProductionUiLayoutChecks.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Diagnostics/ProductionUiLayoutChecks.cs.meta
M	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/AnimationAudit/AnimationAuditSceneSetup.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/AnimationAudit/AnimationAuditValidation.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/AnimationAudit/HumanoidLegSetupReport.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/AnimationAudit/LaraHumanoidKawaiiTestSetup.cs
M	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/DazGraftPairProbe.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/HandGrip/HandGripAcceptanceSetup.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/HandGrip/HandGripAcceptanceSetup.cs.meta
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/HandGrip/HandGripLiveProof.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/HandGrip/HandGripLiveProof.cs.meta
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/HandGrip/HandGripValidationRunner.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/HandGrip/HandGripValidationRunner.cs.meta
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/HandGrip/HandGripVisualProof.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/HandGrip/HandGripVisualProof.cs.meta
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/Partner/PartnerRigAcceptanceBuilder.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/Partner/PartnerRigAcceptanceBuilder.cs.meta
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/Legacy/RemediationValidation.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/RemediationBuild.cs
D	validation/DazPoseUnityValidation/Assets/DazPose/Validation/Editor/RemediationBuild.cs.meta
A	validation/DazPoseUnityValidation/Assets/DazPose/Validation/MaintainedRuntimeRegressionSuite.cs
A	validation/DazPoseUnityValidation/Assets/DazPose/Validation/MaintainedRuntimeRegressionSuite.cs.meta
M	validation/DazPoseUnityValidation/Assets/DazPose/Validation/RemediationContentPreflightRunner.cs
M	validation/DazPoseUnityValidation/Assets/FirstPerformanceVoid/Editor/FirstPerformanceVoidBuilder.cs
D	validation/DazPoseUnityValidation/Assets/PartnerProof/Generated/PartnerProof_Body.mat
D	validation/DazPoseUnityValidation/Assets/PartnerProof/Generated/PartnerProof_Body.mat.meta
D	validation/DazPoseUnityValidation/Assets/PartnerProof/Generated/PartnerProof_Shell.mat
D	validation/DazPoseUnityValidation/Assets/PartnerProof/Generated/PartnerProof_Shell.mat.meta
M	validation/DazPoseUnityValidation/Assets/Production/Application/SettingsPanel.prefab
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/Day2ActorValidation.unity
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/Day2ActorValidation.unity.meta
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/Day3CommandValidation.unity
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/Day3CommandValidation.unity.meta
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/Day4PartnerContactValidation.unity
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/Day4PartnerContactValidation.unity.meta
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/Day5WardrobeValidation.unity
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/Day5WardrobeValidation.unity.meta
M	validation/DazPoseUnityValidation/Assets/Production/Scenes/MainMenu.unity
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/ProductionRuntimeSmoke.unity
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/ProductionRuntimeSmoke.unity.meta
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/RemediationValidation.unity
D	validation/DazPoseUnityValidation/Assets/Production/Scenes/RemediationValidation.unity.meta
A	validation/DazPoseUnityValidation/Assets/Production/UI.meta
A	validation/DazPoseUnityValidation/Assets/Production/UI/ChoiceButton.prefab
A	validation/DazPoseUnityValidation/Assets/Production/UI/ChoiceButton.prefab.meta
A	validation/DazPoseUnityValidation/Assets/Production/UI/ClothingRow.prefab
A	validation/DazPoseUnityValidation/Assets/Production/UI/ClothingRow.prefab.meta
A	validation/DazPoseUnityValidation/Assets/Production/UI/EquipmentRow.prefab
A	validation/DazPoseUnityValidation/Assets/Production/UI/EquipmentRow.prefab.meta
A	validation/DazPoseUnityValidation/Assets/Production/UI/InterestRow.prefab
A	validation/DazPoseUnityValidation/Assets/Production/UI/InterestRow.prefab.meta
A	validation/DazPoseUnityValidation/Assets/Production/UI/MainMenuScreen.prefab
A	validation/DazPoseUnityValidation/Assets/Production/UI/MainMenuScreen.prefab.meta
A	validation/DazPoseUnityValidation/Assets/Production/UI/ModalShell.prefab
A	validation/DazPoseUnityValidation/Assets/Production/UI/ModalShell.prefab.meta
A	validation/DazPoseUnityValidation/Assets/Production/UI/ProfileCatalogPanel.prefab
A	validation/DazPoseUnityValidation/Assets/Production/UI/ProfileCatalogPanel.prefab.meta
A	validation/DazPoseUnityValidation/Assets/Production/UI/SceneCard.prefab
A	validation/DazPoseUnityValidation/Assets/Production/UI/SceneCard.prefab.meta
A	validation/DazPoseUnityValidation/Assets/Production/UI/UiTheme.asset
A	validation/DazPoseUnityValidation/Assets/Production/UI/UiTheme.asset.meta
D	validation/DazPoseUnityValidation/Assets/Production/ValidationData/Day3Expression.asset
D	validation/DazPoseUnityValidation/Assets/Production/ValidationData/Day3Expression.asset.meta
D	validation/DazPoseUnityValidation/Assets/Production/ValidationData/Day3Face.anim
D	validation/DazPoseUnityValidation/Assets/Production/ValidationData/Day3Face.anim.meta
D	validation/DazPoseUnityValidation/Assets/Production/Wardrobe/Candidate-first-outfit.asset
D	validation/DazPoseUnityValidation/Assets/Production/Wardrobe/Candidate-first-outfit.asset.meta
D	validation/DazPoseUnityValidation/Assets/Production/Wardrobe/Candidate-third-outfit.asset
D	validation/DazPoseUnityValidation/Assets/Production/Wardrobe/Candidate-third-outfit.asset.meta
A	validation/DazPoseUnityValidation/Assets/Resources/DazPose/ProductionUi.asset
A	validation/DazPoseUnityValidation/Assets/Resources/DazPose/ProductionUi.asset.meta
D	validation/DazPoseUnityValidation/Assets/Scenes/AnimationAudit.unity
D	validation/DazPoseUnityValidation/Assets/Scenes/AnimationAudit.unity.meta
D	validation/DazPoseUnityValidation/Assets/Scenes/HandGripAcceptance.unity
D	validation/DazPoseUnityValidation/Assets/Scenes/HandGripAcceptance.unity.meta
D	validation/DazPoseUnityValidation/Assets/Scenes/LaraHumanoidKawaiiTest.unity
D	validation/DazPoseUnityValidation/Assets/Scenes/LaraHumanoidKawaiiTest.unity.meta
D	validation/DazPoseUnityValidation/Assets/Scenes/PartnerRigAcceptance.unity
D	validation/DazPoseUnityValidation/Assets/Scenes/PartnerRigAcceptance.unity.meta
D	validation/DazPoseUnityValidation/Assets/Scenes/PoseValidation.unity
D	validation/DazPoseUnityValidation/Assets/Scenes/PoseValidation.unity.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Fonts.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Fonts/LiberationSans.ttf
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Fonts/LiberationSans.ttf.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Fonts & Materials.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/LineBreaking Following Characters.txt
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/LineBreaking Following Characters.txt.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/LineBreaking Leading Characters.txt
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/LineBreaking Leading Characters.txt.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Style Sheets.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/TMP Settings.asset
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Resources/TMP Settings.asset.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/SDFFunctions.hlsl
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/SDFFunctions.hlsl.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_Bitmap-Custom-Atlas.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_Bitmap-Custom-Atlas.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_Bitmap-Mobile.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_Bitmap-Mobile.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_Bitmap.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_Bitmap.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF SSD SpaceWarp.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF SSD SpaceWarp.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF SSD.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF SSD.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF SpaceWarp.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF SpaceWarp.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Masking.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Masking.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Overlay.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Overlay.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD SpaceWarp.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD SpaceWarp.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SpaceWarp.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SpaceWarp.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile-2-Pass.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile-2-Pass.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Surface-Mobile.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Surface-Mobile.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Surface.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF-Surface.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_SDF.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_Sprite.shader
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMP_Sprite.shader.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMPro.cginc
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMPro.cginc.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMPro_Mobile.cginc
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMPro_Mobile.cginc.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc.meta
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMPro_Surface.cginc
A	validation/DazPoseUnityValidation/Assets/TextMesh Pro/Shaders/TMPro_Surface.cginc.meta
```
