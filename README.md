# DazPoseTool V0

Standalone Genesis 8 Female pose conversion proof of concept. It writes a canonical `.dazpose.json`, an approximate `.bvh` interoperability preview, and a readable `.report.txt`.

## Run

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
