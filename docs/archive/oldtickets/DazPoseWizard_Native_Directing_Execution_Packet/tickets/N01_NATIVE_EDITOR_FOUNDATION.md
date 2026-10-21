# N01 — Bind native open scene, saved performer, and real Game-view camera

**Depends on:** N00. **Deliverable:** minimal `NativeStageContext` and user-invoked **G1 Camera Proof** menu/report; **HARD PAUSE FOR ART** before N02.

## Execute

1. Add an Edit Mode-only stage context that resolves the selected open saved `StorySceneBindings` (one active scene; reject ambiguous cross-scene host), its `PerformanceSceneBindings.Player`, `PlayerController.View`, real `MainCamera`, Lara, body, and typed scene locations. Validate loaded/saved scene, actor identity, camera hierarchy, correct root and rig and optional inactive objects. Fail with actionable field-level diagnostics.
2. Add read-only, edit-safe accessors for `PlayerView` rig, head, camera if needed; only expose established references, not runtime control APIs. Do not rely permanently on fragile serialized-property name reflection. Keep serialized instance compatibility and camera runtime behavior unchanged.
3. Add Studio top-level explicit source/sequence/scene host selectors and `Start directing` / `Stop directing` commands. The Studio editor window becomes a **dockable inspector/control strip**. Give obvious prompt to open the authored scene if missing. Do not activate the obsolete `ScenePreviewSession` when native context is selected.
4. Implement explicit `MenuItem("DazPose/Story/Native Directing Diagnostics/G1 Camera Proof")` in an Editor script. **Art**, not executor, opens independent project/disposable scene and invokes it. The small diagnostic panel validates real saved refs/render settings and has separate **Shift Camera Rig** (hold staged change across Game repaint) and **Restore Camera Rig** buttons with snapshot/recovery on panel close, scene close and assembly reload. **Refuse/warn on Save while shifted** because N02 does not yet exist. Export `Temp/NativeDirectingDiagnostics/G1_.../`. A one-call `try/finally` that restores before the second frame draws is **not valid proof**. No extra Camera/RenderTexture, no Play. Art returns two actual Game view frames/Console. If code won't import, request Console error and repair.
5. Integrate `EditorApplication.QueuePlayerLoopUpdate()` / Game view repaint via documented version-supported API; use only if needed and do not drive perpetual repaint when idle.

## Gate

- Studio selects and displays the real authored scene's scene host, Lara, PlayerView and existing camera.
- **Art-reported G1 PASS** that Scene shows real world and standard Game shows real camera with distinct frames as rig moves in disposable scene. Executor-produced source checks alone cannot establish this.
- If no camera renders, ask Art for Console/report; diagnose HDRP, active status, targetDisplay/enable and rig wiring using those results. No proprietary viewport. **STOP before N02** on FAIL, BLOCKED or NOT RUN.
- No calls to `GameApplication.EnsureStarted`, `PlayerController` async movement, hardware, Play mode or live runtime performer setup.

## Failure handling

**Pause and request Art's G1 action following `MANUAL_GATE_PROTOCOL.md`.** The executor must **never launch/operate Unity**, including batch-mode, even to diagnose the blocker. G1 NOT RUN => `WAITING ON ART`, N02+ untouched.
