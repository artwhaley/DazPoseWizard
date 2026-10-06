# Day 4 manual checks

All 96 automated checks passed in the editor and final Windows player; the player exited with code 0. Human visual approval remains pending.

Paste into Windows PowerShell on your normal desktop:

```powershell
$exe = 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\production-consolidation\TestOutput\day4\player\Day4Validation.exe'
$log = 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\production-consolidation\TestOutput\day4\player\manual.log'
if (-not (Test-Path -LiteralPath $exe)) { throw "Missing Day 4 build: $exe" }
Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -ArgumentList @(
    '-force-d3d12', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720',
    '-logFile', ('"' + $log + '"')
)
```

Wait for **Day 4 Contact Regression: Passed** and **96 checks recorded**. A normal graphical launch remains open after the suite; batch launches exit automatically.

1. Confirm both actors are visible immediately, the partner lies on his back on the one-metre-high table, and your authored glowing floor, smoke and lighting remain present.
2. Click **Preview held contact**, wait for **Holding contact at 0.5**, then click **Close-up**. Check hand placement, fingers and wrist for visible clipping, floating or an obviously wrong orientation. The camera buttons remain available throughout.
3. Click **Follow motion** and observe the moving contact. Check for popping, loss of contact or obvious limb distortion. This follows the existing MotionDriver rather than starting a second playback clock.
4. Click **Release preview**, then **Preview held contact** again. Release once during acquisition and try again. Confirm the hand and walking ownership do not become stuck. Preview release restores fixture placement immediately; this is a test reset.
5. Click **Run suite** after preview. It restores fixture placement, selects the overview camera and reruns the saved scene checks. Report the status and visual defects, especially any issue with the close-up or partner visibility.

The report beside the EXE is `day4-runtime.json`. Preview controls do not stamp visual approval or overwrite the automatic report.

The scene is also available in the main Unity project. Open this scene through the Project panel and press Play:

`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\validation\DazPoseUnityValidation\Assets\Production\Scenes\Day4PartnerContactValidation.unity`

The runner is saved in that scene. No editor injection, computer control or separate temporary scene is required.

