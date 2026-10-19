# Reviewed implementation map — `6ae1b81`

Project root: `validation/DazPoseUnityValidation` (Unity 6000.5.9f1).

**Catalog UI**
- `Assets/DazPose/Presentation/UI/ProfileEditorPresenter.cs`: frequency/ownership/reward/punishment handlers call `refresh()`.
- `Assets/DazPose/Presentation/UI/RecycledListView.cs`: `SetItems(..., bool resetScroll=true)` immediately resets anchoredPosition and normalizedPosition by default.
- `Assets/DazPose/Presentation/UI/InterestRowView.cs`, `EquipmentRowView.cs`, `SceneBrowserView.cs`: row controls and selection.
- `Assets/DazPose/Runtime/Application/PlayerProfileEditSession.cs`, `PlayerProfileService.cs`: authoritative draft/commit/score behavior.

**Source and bindings**
- `Assets/DazPose/Editor/Application/DazStoryImporter.cs`: script importer calls Analyze and schedules reconciliation.
- `Assets/DazPose/Editor/StoryStudio/SourceBindingReconciler.cs`: `FindOwner`, `Schedule`, `ReconcileAsset`; bug is `bool changed=created||(recordUndo&&content.ownerSourceGuid!=sourceGuid)`.
- `Assets/DazPose/Runtime/Story/Runtime/StoryContentBindings.cs`: `ownerSourceGuid`, content arrays.
- `Assets/Production/Story/Programs/FirstContact.bindings.asset` and `Assets/Production/Story/Cards/FirstContactEngineering.bindings.asset`: sampled reviewed text has no serialized ownerSourceGuid; preserve all assignments.

**Rehearsal and global cards**
- `Assets/DazPose/Runtime/Story/Language/StoryMachine.cs`: canonical execution, `StoryDirector`, per-instruction hooks, `InspectDirector`.
- `Assets/DazPose/Runtime/Story/Language/StoryRehearsal.cs`: snapshot keys and rehearsal controller. Current `DirectorKey(source)` and `BranchKey(source,pc)` are source-site keys reused on loop visits.
- `Assets/DazPose/Editor/StoryStudio/RehearsalSession.cs`, `RehearsalStoryHost.cs`: deterministic detached virtual host/replay and scenario.
- `Assets/DazPose/Runtime/Story/Runtime/GlobalStoryCardCatalog.cs`, `Assets/DazPose/Editor/Application/GlobalStoryCardIndexBuilder.cs`: single global publication index; preserve unchanged.

**Editor and preview**
- `Assets/DazPose/Editor/StoryStudio/SceneDirectorStudioWindow.cs`: source flow, requirements, staging and `Tick()` source timestamp refresh. The host ObjectField changes without releasing the preview.
- `Assets/DazPose/Editor/StoryStudio/SceneDirectorStudioWindow.Rehearsal.cs`: transport, global Director inspector, visual command staging.
- `Assets/DazPose/Editor/StoryStudio/ScenePreviewSession.cs`: isolated preview; `TrackLara()` currently computes a single orientation, `Arrival()` moves clone.
- `Assets/DazPose/Editor/StoryStudio/SceneMarkAuthoring.cs`: explicit Undoable scene mark commits; preserve transactional semantics.

**Tests and scripts**
- `tests/DazPose.Story.Tests/`, `tests/DazPose.Rehearsal.Tests/`, `tests/DazPose.Player.Tests/`, `tests/DazPose.Tests/`.
- `scripts/check-unity-source.py`, `scripts/audit_p2_p3_assets.py`.
- `docs/p2-p3/IMPLEMENTATION_REPORT.md`, `docs/p2-p3/MANUAL_ACCEPTANCE.md`, `docs/story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md`.

Paths relative to Unity project unless prefixed `tests/`, `scripts/` or `docs/`.
