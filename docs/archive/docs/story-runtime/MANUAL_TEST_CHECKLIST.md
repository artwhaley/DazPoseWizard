# Human Manual Play Mode Checklist — after D1–D5 source handoff

This checklist is for the user, not the executor.

## Before Play

- Open `Assets/Production/Scenes/FirstContactD1D5.unity`.
- Confirm the authored floor, smoke, lighting, camera and volumes are intact, with no FirstPerformanceVoid/PerformanceMarkers/TeleportAcceptance hierarchy or empty ToyControl fixture root.
- Confirm saved `PerformanceHost` and `StoryProgramSequence` are present.
- Confirm no obvious missing object references in Story bindings.
- Inspect Actors > Player Body: prepared male Partner variant, original shell/penis rig and measured contact profile; the session Contact Target points here.
- Open Unity Console, enable normal Log messages, and filter by [Story to follow the script. F8 also toggles the optional snapshot.

## Run

Press Play manually.

Verify:

1. Story begins through the saved host without an editor helper. Console states what each step is doing, when it finishes, and why each branch was selected.
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

Delivered setup notes: no Inspector assignments remain. In the main menu the entry is **First Contact · session setup**. F8 toggles the Story snapshot. The top Stop control remains accessible during fullscreen prompts. After a mid-intro Stop, use **Restart scene** to restore the authored hidden configuration before a new run. At the final checkpoint phase is Warmup with progress 0; no cards have run.

Unity may print the documented inline DTO type-depth warning on first registration. The delivered JSON round-tripped all 45 steps and deepest arguments; input beyond eight nested blocks is explicitly rejected.
Production creation regression: use **DazPose > Scene > New Performance Scene...** with a new path. It should contain the authored art, production host/controls, Lara, prepared male player body/contact target and empty Authoring Marks. It should contain no old performance/test markers or acceptance controllers. This is separate from visual approval of the original art.
