# Day 3 manual checks (Windows PowerShell)

The delivered scene starts its own runner when you press Play. No editor injection is required. Automatic software checks have already passed; these remaining checks concern the visible/audio result.

## Standalone player

Paste into Windows PowerShell on your normal desktop:

```powershell
$exe = 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\production-consolidation\TestOutput\day3\player\Day3Validation.exe'
$log = 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\production-consolidation\TestOutput\day3\player\manual.log'
if (-not (Test-Path -LiteralPath $exe)) { throw "Missing Day 3 build: $exe" }
Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -ArgumentList @(
    '-force-d3d12', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720',
    '-logFile', ('"' + $log + '"')
)
```

The scene initially exercises cancellations, briefly pauses gameplay, changes outfits and replays First Contact. Wait about 1–2 minutes for **Day 3 regressions passed** in the upper-left panel. In a normal graphical launch the player remains open. Click **Run First Contact** to review an uninterrupted performance; it resets to the initial state before starting.

1. Confirm the original Void floor/glow, smoke and lighting are present, and character visibility remains acceptable.
2. Confirm speech is audible and lip sync works. Check approach, proximity hold, lounge seating and the final camera move for obvious pops or stranded animation.
3. During speech, click **Stop First Contact**, then **Run First Contact**. Repeat during approach. The new run should restart from its initial placement, with speech/body/view state usable. Click these after the automatic suite finishes so you do not deliberately interrupt its expected assertions.
4. Report the upper-left status and any visual/audio defect. A failure report is written beside the EXE as `day3-runtime.json`; `manual.log` contains details.

## Main Unity project alternative

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe'
$project = 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard\validation\DazPoseUnityValidation'
Start-Process -FilePath $unity -ArgumentList @('-projectPath', ('"' + $project + '"'))
```

In the Project panel open **Assets/Production/Scenes/Day3CommandValidation.unity**, then press Play and follow the same checks. The absolute scene file is:
`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\validation\DazPoseUnityValidation\Assets\Production\Scenes\Day3CommandValidation.unity`.

Do not use Day2ActorValidation for this test; its accepted runner omission remains documented.

## Optional physical hardware

The saved suite uses a fake backend for transport/ownership tests and First Contact's no-device alternate. No real device result is claimed. On your established connected hardware fixture, check that gameplay pause submits the configured neutral position/zero intensity, resume restores current intent, and stop/disconnect still completes while paused. Timed-position neutral uses the existing backend duration fallback (50 ms), so physical hold needs observation. This is separate from the 23 software checks and does not block reviewing the scene without hardware.
