# T02 Session 1 evidence (sandbox, headless; logic and wiring only)

- `mounts-dry-run-PlayerBody.prefab.diff`: the only asset change from the first dry run of `DazPose > Player > Add Camera Mounts to PlayerBody` (main on the left, sandbox result on the right). Additive; no `.meta` changed. The second run reported nothing changed.
- `mounts-probe-with-dry-run-prefab.json`: read-only probe of the sandbox FirstContact scene with that prefab applied. The sandbox was then restored to byte-identical main files.
- `editmode-session1.xml`: EditMode results, 20 of 20 passed.
