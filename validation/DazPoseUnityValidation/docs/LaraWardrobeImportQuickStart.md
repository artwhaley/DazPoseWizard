# Lara wardrobe source preparation and existing outfit authoring

Updated October 21, 2026. The cloned import proof of concept is retired. Art explicitly deferred completing the main-editor conversion hookup; **new outfit conversion/publication is currently unavailable through the wrapper**. Existing imported outfits, Clothing Setup and published generations are unchanged. Native verification remains Art-operated.

## What works now

Preparation uses the authoritative `validation/DazPoseUnityValidation` directly. No separate project or Unity process is needed. Save one DUF and export one direct FBX with approved Lara/body/graft, visible followers, supplied pose and intended footwear. Preserve the original source files, material/texture paths, artist settings and GUIDs. Use the existing recipe template in `configs/wardrobe`; its id and exact `Assets/TestData/Wardrobe/<id>` destination must agree.

```powershell
.\scripts\import-lara-outfit.ps1 -Recipe configs\wardrobe\third-outfit.json -Action Status
.\scripts\import-lara-outfit.ps1 -Recipe configs\wardrobe\third-outfit.json -Action Preflight
.\scripts\import-lara-outfit.ps1 -Recipe configs\wardrobe\third-outfit.json -Action Inspect
.\scripts\import-lara-outfit.ps1 -Recipe configs\wardrobe\third-outfit.json -Action Prepare
```

Status is read-only. Preflight checks DUF ownership/textures and writes preparation evidence; Inspect also checks actual FBX geometry and explicit shoe policy. Prepare runs the existing Python geometry/material-manifest/heel-reference preparation against main. It creates preparation files; it does **not** convert Unity meshes/materials, register an outfit or publish a generation. Blender’s Python FBX parser remains a preparation dependency. Run preparation deliberately; it can update owned preparation manifests, so save source/artist work first.

Import, All, Validate, Assets, Publish and ReimportTest now report that conversion integration is deferred. No fallback project is created or discovered. `run-wardrobe-execution.ps1` is retired. Agents must not launch Unity or run batch/Play/builds/players.

## Existing outfits

In Art’s main editor, use **DazPose > Wardrobe > Open Clothing Setup** for the existing catalog, layers, hair, footwear and fit/release workflow. **Imported Outfits** still browses existing packages and their historical reviews. Reusable wardrobe assets and Studio/FirstContact integration remain implemented; source preparation alone does not add a new package to that catalog.

Base/1/2 layer assignments belong to the artist. Shoes follow their layer; hair persists. Keep artist opacity/roughness/maps and footwear height/fit on future reimport. Never move the canonical source/imported material paths that feed CharacterSignature, or change immutable releases to make a trial pass.

## Preserved knowledge and future hookup

The [retired workflow archive](../../../docs/archive/p02-import-evidence/README.md) preserves the full previous procedure/scripts/recipes, acceptance evidence and clone-specific differences. The [playbook](LaraWardrobeImportPlaybook.md) links the technical decisions; [historical status](LaraWardrobeImportStatus.md) retains actual user approvals separately from technical checks. No known import procedure was discarded.

The [migration design](../../../docs/plan/P02_MAIN_PROJECT_IMPORT_PLAN.md) records the remaining work: main-editor entry point with temporary actor context, preservation/rollback of only owned assets and new-package registration in existing Clothing Setup. These are deferred, not claimed implemented. [STATUS](../../../docs/plan/STATUS.md) is the single active status record.
