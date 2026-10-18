# DazPoseTool V0

Standalone Genesis 8 Female pose conversion proof of concept. It writes a canonical `.dazpose.json`, an approximate `.bvh` interoperability preview, and a readable `.report.txt`.

## Unity authoring

Open `validation/DazPoseUnityValidation` in Unity 6000.5.9f1. Start with [scene/content ownership](docs/cleanup-task1/SCENE_AND_CONTENT_OWNERSHIP.md), [story authoring](docs/story-runtime/DAZSTORY_AUTHORING.md), and the [Unity guide](validation/DazPoseUnityValidation/README.md). Tools live under top-level **DazPose**. [Cleanup status](docs/cleanup-task1/EXECUTION_REPORT.md) remains in progress.
The supported saved gameplay scenes are `Assets/Production/Scenes/MainMenu.unity` and `Assets/Production/Scenes/FirstContact.unity`. Use **DazPose > Scene > New Performance Scene...** to start a composition from the saved art-only Void environment, or open **DazPose > Wardrobe > Open Clothing Setup** for outfit authoring. Import poses through DazPoseWizard configured roots and Unity automatic reconciliation. Author First Contact in `.dazstory`; tagged cards publish from `Assets/Production/Story/Cards/` and enter the shared Director pool automatically. Contact and motion preparation is under **DazPose > Content**. The current Help menu opens this API guide and the `.dazstory` guide.

The saved `AuthoringShowcase` under `Assets/Examples/Scenes` is an example. The original `FirstPerformanceVoid` scene is a frozen reference, not the template for new production scenes. `AuthoringWelcome` and `AuthoringCompanion` have been retired. See the ownership page for protected historical import roots, draft holds and pending reviews. Cleanup is not complete; First Contact visual acceptance remains for the user.

## Run the conversion app

1. Build with `dotnet build .\DazPoseTool.sln -c Release` or publish with `.\scripts\publish-win-x64.ps1`.
2. Double-click `artifacts\win-x64\DazPose.App.exe`.
3. Browse to `Genesis8Female.dsf`.
4. Browse to `Cherish Genesis 8 Female 16.duf`.
5. Select an existing output directory.
6. Click **Convert**.
7. Read the diagnostics.
8. Click **Open Output Folder**.
9. Follow [the Unity validation guide](validation/DazPoseUnityValidation/README.md) to apply the canonical JSON directly to the neutral DAZ-exported G8F FBX.

Run automated tests with `dotnet test .\DazPoseTool.sln -c Release`. Local proprietary fixtures belong in `fixtures\private`, `G8F-Base`, and `G8F-inCherishReferencePose`; they are not committed.
