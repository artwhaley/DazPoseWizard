# Human Manual Play Mode Checklist — after D1–D5 source handoff

This checklist is for the user, not the executor.

## Before Play

- Open `Assets/Production/Scenes/FirstContactD1D5.unity`.
- Confirm the scene is the copied authored Void environment.
- Confirm saved `PerformanceHost` and `StoryProgramSequence` are present.
- Confirm no obvious missing object references in Story bindings.
- Open the Story debug panel if desired.

## Run

Press Play manually.

Verify:

1. Story begins through the saved host without an editor helper.
2. Lara performs the authored representative intro steps.
3. Camera tracking/movement remains sane.
4. Kink/interests fullscreen panel opens.
5. Changing frequency/sentiment can be confirmed and closes correctly.
6. Submission score appears/updates.
7. Equipment fullscreen panel opens.
8. Owned/willing + reward/punishment/both settings work.
9. If at least one owned/willing item exists, one prepared item is selected and reported.
10. Clothing panel opens with six checkboxes.
11. No clothing → mood moves from 50 to 55.
12. One/two clothing items → mood stays 50.
13. More than two → instruction removes the least-risque present item in the fixed order and Ready resumes.
14. Story reaches the explicit D1–D5 checkpoint.
15. No fake card/Director content runs after the checkpoint.
16. Stop during a prompt cancels the prompt/session cleanly.
17. Exit Play, enter Play again, and confirm persistent kink/equipment/profile changes survive while transient clothing/session state resets.
18. Player-body dissolve methods can be inspected/triggered through the intended later scene path; if the D1–D5 score does not reach climax, absence of body dissolve during this checkpoint is expected.

## Report separately

- Functional pass/fail.
- Visual/artistic notes.
- Any missing authored audio/assets.
- Any architectural bug exposed by the manual run.

Do not conflate visual preferences with runtime correctness.
