# Repository Map and Preservation Rules

## Existing authoritative seams

Existing code to reuse, not replace:

- `Assets/DazPose/Runtime/Performer/Authoring/PerformanceHost.cs`
- `Assets/DazPose/Runtime/Performer/Authoring/PerformanceSequence.cs`
- `Assets/DazPose/Runtime/Performer/Authoring/PerformanceRun.cs`
- scoped performer/player/toy facades under the same Authoring area
- `Assets/DazPose/Runtime/Application/GameApplication.cs`
- `Assets/DazPose/Runtime/Application/GameSettingsService.cs`
- `Assets/Player/Runtime/PlayerController.cs`
- `Assets/Player/Runtime/PlayerView.cs`
- the existing application prefab/main menu/settings presentation
- existing accepted Lara prefab and dissolve implementation
- `Assets/Scenes/FirstPerformanceVoid.unity` as environment authority

## New runtime folders

Create:

```text
Assets/DazPose/Runtime/Story/
    Serialization/
    Runtime/
    State/
    Content/
    UI/
```

Do not create a new asmdef unless compilation boundaries make it strictly necessary. Prefer the existing `DazPose.Runtime` assembly.

Create application story persistence alongside existing application services:

```text
Assets/DazPose/Runtime/Application/
    PlayerProfile.cs
    PlayerProfileService.cs
```

## New production content folders

Create:

```text
Assets/Production/Story/
    Programs/
    Catalogs/
```

Use:
- `Programs/FirstContactD1D5.story.json`
- `Catalogs/kink-catalog.json`
- `Catalogs/equipment-catalog.json`

## First Contact integration scene

Create a **copy** of the authored void environment for the D1–D5 integration.

Recommended target:

`Assets/Production/Scenes/FirstContactD1D5.unity`

Never mutate `Assets/Scenes/FirstPerformanceVoid.unity`.

Use existing DazPose scene initialization conventions and saved host/bindings. If an Editor setup command is needed to create/configure the scene deterministically, add one under the top-level `DazPose` menu. It may be invoked in Edit Mode/batch mode. It must not enter Play Mode.

## Preserve

Do not:
- change Lara rig conventions;
- regenerate the Void environment;
- retune lights/smoke/volume/camera as part of this sprint;
- change toy semantics;
- change tuning persistence;
- change wardrobe release content;
- generalize to multiple actor types;
- replace `PerformanceHost`;
- remove existing C# sequence support;
- alter historical accepted scenes solely to adopt serialized stories.

C# `PerformanceSequence` remains supported. Serialized stories are an additional authoring path sharing the same runtime.
