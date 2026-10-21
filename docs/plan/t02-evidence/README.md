# T02 Session 1 evidence (sandbox, headless; logic and wiring only)

- `mounts-dry-run-PlayerBody.prefab.diff`: the only asset change from the first dry run of `DazPose > Player > Add Camera Mounts to PlayerBody` (main on the left, sandbox result on the right). Additive; no `.meta` changed. The second run reported nothing changed.
- `mounts-and-posture-probe-main-prefab.json`: read-only probe of the sandbox FirstContact scene using the main prefab as Art tuned it (High 0.600 m, Low 0.200 m above the head), the linked card requirements (17 cards, 17 posture requirements, all `Lara.Posture`), and the player body posture.
- `editmode-final.xml`: EditMode results, 48 of 48 passed.
- `breathing-dry-run-PlayerBody.prefab.diff`: the only asset change from the first dry run of `DazPose > Player > Add Breathing to PlayerBody` (additive: one `PlayerBreathing` component). The second run reported nothing changed.
- `scene-dry-run-summary.txt`: the new objects in the dry-run `PlayerSetup.unity` (a 575 KB copy of the Void environment, so the full diff is the whole file) and its `.meta`. The only assets that run changed were the new scene and its `.meta`. The second run reported "already exists; nothing changed".
- `player-setup-probe.json`: read-only probe of the dry-run scene: every test-panel reference resolved, no problems, the scene not dirty.
