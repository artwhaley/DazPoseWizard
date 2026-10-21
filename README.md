# DazPoseWizard / DazPoseTool V0

DazPoseWizard combines a Genesis 8 Female pose converter with a Unity performance authoring/runtime system. The converter writes canonical `.dazpose.json`, approximate `.bvh` previews and readable reports.

Read [AGENTS.md](AGENTS.md) for current instructions, [the architecture sprint plan](docs/plan/ARCHITECTURE_SPRINT_PLAN.md) for direction, and [STATUS](docs/plan/STATUS.md) for current work and acceptance.

## Unity authoring

The authoritative project is `validation/DazPoseUnityValidation`, using Unity 6000.5.9f1. Art operates Unity; agents do not launch it or run Play/builds/players.

- First Contact: **`Assets/Scenes/FirstContact.unity`**.
- Clothing Setup: `Assets/Scenes/ClothingSetup.unity`.
- Main Menu: `Assets/Production/Scenes/MainMenu.unity`.

Tools live under **DazPose**. Use **DazPose > Story > Scene Director Studio** to author static visual cues in the real saved scene. First Contact source is `Assets/Production/Story/Programs/FirstContact.dazstory`; tagged cards publish from `Assets/Production/Story/Cards/` into the shared Director pool.

**New Performance Scene** copies the saved art-only `Assets/Production/Environments/VoidEnvironment.unity`. The original FirstPerformanceVoid is a frozen reference. AuthoringShowcase under `Assets/Examples/Scenes` remains an example.

Start with [story authoring](docs/story-runtime/DAZSTORY_AUTHORING.md), [Studio](docs/story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md), and [the Unity guide](validation/DazPoseUnityValidation/README.md). Import instructions and the remaining staging limitation are in [wardrobe quickstart](validation/DazPoseUnityValidation/docs/LaraWardrobeImportQuickStart.md) and [the P02 migration plan](docs/plan/P02_MAIN_PROJECT_IMPORT_PLAN.md). Implementation evidence is separate from Art’s visual approval.

## Run the conversion app

These are user-operated instructions; they do not authorize an agent launch.

1. Build with `dotnet build .\DazPoseTool.sln -c Release` or publish with `.\scripts\publish-win-x64.ps1`.
2. After publishing, double-click `artifacts\win-x64\DazPose.App.exe`.
3. Browse to `Genesis8Female.dsf` and a supported pose such as `Cherish Genesis 8 Female 16.duf`.
4. Choose an existing output directory, click **Convert**, read diagnostics, then **Open Output Folder**.
5. Follow [the Unity pipeline guide](validation/DazPoseUnityValidation/README.md) for the canonical JSON and neutral DAZ-exported G8F FBX.

The old published binary was retired during T01; publish before running it again. Private fixtures remain in `fixtures/private`, `G8F-Base` and `G8F-inCherishReferencePose`; proprietary inputs are not committed.

## Managed tests

`dotnet test .\DazPoseTool.sln -c Release` runs the converter suite. Run all five suites explicitly:

```powershell
Get-ChildItem tests -Recurse -Filter *.csproj | ForEach-Object { dotnet test $_.FullName -c Release }
```

Historical plans, packets and evidence are under [docs/archive](docs/archive/README.md), outside the active execution queue.
