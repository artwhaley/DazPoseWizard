# Manual toe appearance check

Automated R03 Editor and Windows player checks pass. This is the only remaining user check for the remediation stack: confirm the toes look natural in the actual second-outfit preview. The exact player build already ran with `-force-d3d12 -screen-width 1280 -screen-height 720 -screen-fullscreen 0`; no automated rerun is needed for this visual review.

## Open the real Clothing Setup scene

In normal Windows PowerShell, verify the project and exact scene paths, then launch Unity in your signed-in desktop:

```powershell
$root = 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard'
$project = Join-Path $root 'validation\DazPoseUnityValidation'
$scene = Join-Path $project 'Assets\Scenes\ClothingSetup.unity'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe'

foreach ($path in @($project, $scene, $unity)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required Unity path was not found: $path"
    }
}

Start-Process -FilePath $unity -ArgumentList @('-projectPath', $project) -WindowStyle Normal
```

If the project is already open in Unity, use that window instead of starting a second editor. Open `Assets/Scenes/ClothingSetup.unity` from the Project panel. The authored void environment, including its floor, smoke and lighting, is the review environment. Do not save changes to the scene during this check.

Open **Tools/DAZ Pose/Wardrobe Setup/Configuration Window**. In Play Mode, select `second-outfit` and choose **Dress / redress**. Once the shoes and stockings are visible, click **Remove next** exactly once to remove Layer 1 while Base garments remain.

Check both feet in a close view, then return to a wider view. Pass when shoes and stockings are gone, Base garments remain, pelvis height returns, and the toes look relaxed and aligned without a persistent curl, twist or raised pose. Use **Add next** to restore Layer 1, then remove Layer 1 once more. The toe pose should change cleanly in both directions without needing to remove Base.

The automated hosiery-only check uses a separate R03 fixture catalog whose copied third-outfit shoe is assigned to Layer 1. The production third-outfit, catalog and configuration were preserved byte-for-byte; do not change those production assignments to reproduce that isolated fixture.

Reply with `passed` if the second-outfit toes look natural after Layer 1 removal, or describe what remains wrong. Keep this appearance approval separate from the numeric R03 evidence and the unrelated physical-toy and clean-context-import gates.
