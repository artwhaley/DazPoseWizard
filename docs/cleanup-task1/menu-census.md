# Current menu census — Task 1 (C01/C04)

Inventory scope: active C# attributes in `validation/DazPoseUnityValidation/Assets`; paths below are relative to that validation project's `Assets` directory. Commented-out attributes are excluded. This is source inventory, not a claim that every command is production-facing.

## Supported DazPose workflows

`DazPose/Scene/*`, `DazPose/Game/*`, `DazPose/Help/*`, `DazPose/Settings/Pipeline Settings`, `DazPose/Story/New Global Card Source...`, `DazPose/Wardrobe/Open Clothing Setup`, and `DazPose/Wardrobe/Imported Outfits` are the main authoring/application surface. Their implementations live in `DazPose/Editor/Application/DazPoseProductionMenu.cs`, `ProductionPlayerBodyBuilder.cs`, `DazPosePipelineSettingsWindow.cs`, `GlobalStoryCardIndexBuilder.cs`, `DazPose/Editor/WardrobeImportReviewWindow.cs`, and `DazPose/Editor/Wardrobe/ClothingSetupBuilder.cs`.

The explicitly invoked content/maintenance commands currently exposed under `DazPose/Content/*` include pending import reconciliation/archive, new sequence script, action/gesture baking, first-contact binding repair, SALSA setup, hand-profile capture, surface binding bake, and published-card reindex. These are explicit mutation commands; retain only where the corresponding workflow is supported and documented. `DazPose/Diagnostics/*` exposes inspection/review windows and reports, but also old live-preview/test diagnostics listed below.

## Retained diagnostics implemented by validation-owned helpers

C04 deliberately retains these useful preparation/inspection commands. Their helper ownership does not make them obsolete fixture-creation menus:

- `DazPose/Content/Advanced/Bake Seating for Generic Lara` — `DazPose/Validation/Editor/Legacy/AnimationAudit/PerformerSeatingBaker.cs`.
- `DazPose/Content/Advanced/Bake Locomotion for Generic Lara` — `DazPose/Validation/Editor/Legacy/AnimationAudit/PerformerLocomotionBaker.cs`.
- `DazPose/Diagnostics/Seating Preview` — `DazPose/Validation/Editor/Legacy/AnimationAudit/PerformerSeatingPreviewWindow.cs`.
- `DazPose/Diagnostics/Report Humanoid Leg Setup` — `DazPose/Validation/Editor/Legacy/AnimationAudit/HumanoidLegSetupReport.cs`.
- `DazPose/Diagnostics/Inspect Selected FBX Evidence` — `DazPose/Validation/Editor/Legacy/DazFbxEvidenceInspector.cs`.
- `DazPose/Diagnostics/Compare Pose Clip Bindings` — `DazPose/Validation/Editor/Legacy/DazPoseClipBindingComparison.cs`.
- `DazPose/Diagnostics/Inspect Selected Character` and `DazPose/Diagnostics/Inspect Pose Bone Mapping` — `DazPose/Validation/Editor/Authoring/DazPoseEditorCommands.cs`.

Current non-validation diagnostic/editor entries in `DazPose/Editor` are `Validate Required Face Morphs`, `Contact Calibration`, `Audit SALSA Lip Sync`, and `Outfit Review Controls`. The cleanup request broker is now validation-owned and exposes no ordinary authoring menu.

## Asset creation menus

Active DazPose `CreateAssetMenu` definitions are authoring data types, not automation hooks: `DazPose/Scene Catalog`; `DazPose/Story/Content Bindings`; `DazPose/Performer/Partner Contact Profile`; performer Action, Dissolve Profile, Expression, Footwear Profile, Gesture, Grip Contact Surface, Hand Grip Rig Profile, Locomotion Motion/Profile, Magic Shader Profile, Motion Set/Variant, Pose, Seating Motion/Profile, Surface Bindings, Teleport Profile; Magic Aura/Catalog/Spell/Style; Wardrobe Outfit, and Wardrobe Catalog/Configuration/Fit State/Preset. Definitions are under `DazPose/Runtime/{Application,Story,Partner,Performer}`. Vendor asset menus include `Interactive Effects/Mask Settings` in `INab Studio/Interactive Dissolve/Scripts/InteractiveEffectMaskSettings.cs` and SALSA's `GameObject/Crazy Minnow Studio/SALSA LipSync/One-Clicks/boxHead-demo` in its bundled plugin.

## Vendor import commands

Preserve vendor integration commands unless the importer dependency is deliberately removed: DazToUnity Bridge window; Daz3D selected-DTU prefab/material extraction and `Assets/Daz3D/*` equivalents (in `Daz3D/Scripts/Editor/Daz3DBridge.cs`, `Daz3DDTUImporter.cs`, `DTUConverter.cs`). These are vendor UI/import operations, not DazPose authoring menu.

## Census note

The earlier 38-command count predates relocation of the cleanup inventory menu. The retained helper commands above remain supported preparation/inspection commands. The validation project also contains vendor commands. There is no separate menu manifest or code attribute that hides legacy validation entries from normal menus.

