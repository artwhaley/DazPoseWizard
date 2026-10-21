# Day 5 manual checks

This is the human review for the Day 5 wardrobe player and Clothing Setup workflow. Automated checks passed: 11 transaction, 13 contract, 10 migration assertions; the saved wardrobe scene passed 39 checks and Clothing Setup passed 9 in both editor and Windows players. Both builds succeeded and both players exited 0. These results do not replace human UI/appearance review.

## Run the saved wardrobe player

In Windows PowerShell, launch the built player with its own saved scene runner:

```powershell
$player = 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\production-consolidation\TestOutput\day5\player\Day5Validation.exe'
if (-not (Test-Path -LiteralPath $player -PathType Leaf)) { throw "Day 5 player has not been built: $player" }
Start-Process -FilePath $player -WorkingDirectory (Split-Path -Parent $player) -ArgumentList '-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -force-d3d12'
```

Leave the player window open while the test runs. Its saved runner starts the wardrobe check on launch; wait for the on-screen status to report completion. If it remains ready, click **Run saved wardrobe test**. Close the window after recording whether it passed or showed a visible defect. The player writes its runtime report beside the player data folder as `day5-runtime.json`.

## Review Clothing Setup in Unity

In the main Unity validation project, open the saved scene `Assets/Production/Scenes/Day5WardrobeValidation.unity`. Its full path is:

`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\validation\DazPoseUnityValidation\Assets\Production\Scenes\Day5WardrobeValidation.unity`

For the artist workflow, open this scene, then use the exact menu **Tools/DAZ Pose/Wardrobe Setup/Configuration Window**:

`C:\Users\artwh\OneDrive\Documents\DazPoseWizard\validation\DazPoseUnityValidation\Assets\Scenes\ClothingSetup.unity`

Select an outfit and inspect the garment rows grouped under Base, Layer 1 and Layer 2. Change one garment's layer with its selector. Confirm that the named removal sequence updates immediately in the order Layer 2 → Layer 1 → Base, skipping empty layers, and that the add sequence runs in reverse. Use **Undo** and **Redo** to check the edit history.

Click **Save Draft**, close the window, reopen it from the same menu, and confirm the assignment remains. Click **Compile / validate preview** explicitly and wait for completion or a visible error. Saving a draft and compiling are separate actions.

Enter Play Mode in Clothing Setup after a successful compile. Its saved startup runner should immediately equip the first outfit and reveal Lara in the authored Void environment. Hosiery keeps the accepted bent-foot pose after shoes are removed; shoe support should be off. Use **Dress / redress**, **Remove next**, and **Add next** to inspect the runtime sequence. **Preview naked (keep selection)** should await removals until the outfit is naked while retaining its selection, so **Add next** can dress it again. **Select Unclothed** is a separate action that clears the remembered outfit selection. Check that hair remains independent while clothing layers are removed; observe effective hair and footwear in the preview state.

Stop Play Mode before publishing. With no unsaved edits, a successful explicit compile and no displayed compile error, use **Publish validated generation** only when you intend to promote that candidate as the local release. Confirm the window reports the result. Do not publish while Play Mode is active.

Open **Advanced · material opacity** to inspect per-slot opacity controls. If the selected outfit has footwear, open **Advanced · footwear calibration** and confirm its optional lift and foot-shrink settings are understandable. Keep any fit or display defect distinct from whether the controls were easy to use.

## Report the human review

Please report UI usability and visible defects separately. Useful details include which action felt unclear, whether the sequence names matched the garments shown, whether Save Draft survived reopening, any compile or publish message, and what happened to hair and footwear during the preview. Report your human observations separately from the automated results above.
