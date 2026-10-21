# N10 — Native Unity acceptance and code/test regression gates

**Depends on:** N01–N09. **Deliverable:** completed `ACCEPTANCE_MATRIX.md` with actual test evidence and remaining failures.

## Execute

1. Run `dotnet test` projects for Story, Player, Rehearsal, Application and `python scripts/check-unity-source.py`, `python scripts/audit_p2_p3_assets.py` from repo root (adjust only if layout changed; record exact versions). Add focused checks for pure camera math, typed projection, visit/replay, save isolation and mark re-projection. Verify global indexed cards and original profile/UI regressions.
2. Prepare **user-invoked** `G4 Full Regression Export` in Editor plus a concise checklist; **Art**, not executor, opens the independent project in Unity 6000.5.9f1, allows import/compile, observes real Scene+Game views, reports screenshots and Console. Don't invoke batchmode/Unity test runners, GUI, Play or build yourself.
3. Ask Art to verify actual FirstContact progression: hidden/dissolve; stepforward+SexyVamp; camera_start+Track; **Warmup** `SitAt(Lounger)` → `PersistentSitPose(Lounging)`; **Main** seated `DissolveTo` after Director; standing return; male reveal; at least one global Warmup card. Explicitly compare `DissolveTo` yaw/initialPose behavior, full-rotation hidden/dissolve/camera, awaited/non-awaited gestures and MagicShader material endpoint. Native mark dragging and live Game shot required.
4. Art tests normal Ctrl+S **without authored edit** plus **forced SaveScene on disposable copy** (otherwise clean save can no-op); with authored stand/seat/camera change and newly created chair prefab overrides; Undo/Redo, reopen, stop while unsaved dirty mark remains. Hash/semantic-diff only preview-owned fields as forbidden; allow intentional mark changes. Re-run safety for bone, material, visibility and seat properties introduced since G2.
5. Art tests Stop/close/reopen/source swap, scene close, **manual compile/domain reload**, attempted Play transition **without executor or menu entering Play**, invalid camera/seat/bindings, Animation Window interference, `[ExecuteAlways]` wardrobe values and exceptional path cleanup. Diagnostic error followed by valid movement/Director must **stop at offender** (N06). Report exact evidence, no assumed success.
6. Verify saved FirstContact originals unchanged; no new per-scene global-card registrations, no hardware connections or persistent data writes; run static GUID/meta and scene catalog/build membership checks.

## Gate

- If native Game-view compositing, camera alignment, seated pelvis contact, save safety or full restoration lacks actual native evidence, overall remains **PARTIAL** even if 100% managed tests pass.
- **HARD PAUSE G4:** executor requests Art's report/screenshots and waits. Missing Art response = native `NOT RUN` / `WAITING ON ART`, **not authorization to self-run Unity or declare PASS**. If Art reports FAIL, repair and request G4 again.
- If an Edit Mode test failure causes preview data to leak into a scene or prefab, treat as **blocker**, revert using known baseline, fix and rerun before proceeding.
- Results contain filenames/screenshots, test commands, file hashes and genuine status labels `PASS/FAIL/PARTIAL/NOT RUN`; no fabricated rendered captures.
