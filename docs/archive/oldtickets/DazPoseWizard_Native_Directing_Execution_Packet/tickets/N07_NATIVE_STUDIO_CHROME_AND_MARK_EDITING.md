# N07 — Docked directing chrome, simple workflow, real mark editing

**Depends on:** N06. **Deliverable:** straightforward Studio UI using existing Scene/Game/Hierarchy/Inspector.

## Execute

1. Remove center-stage custom viewport layout from the **visible** Studio workflow. Present a sensible docked `EditorWindow` with source/scene selector, `Start directing`, cue tree/phase, progress/status, `Next`, `Previous`, `Stage selected`, `Before/After`, selected-visit selector and source line. Existing typed asset binding controls and Director candidate/history/state inspection remain. No invented in-Studio orbit gizmos.
2. Studio auto-finds exactly one valid `StorySceneBindings` in open saved scene; if ambiguous, asks user to select existing host. Provide `Select mark` and `Frame mark in Scene view` that select the real native Transform. Use Unity Selection/Inspector and standard Move/Rotate handles, not custom X/Y/Z mini-widget. Keep optional SceneView overlay (cue label, current target) small/nonessential.
3. Give clear status: `EDIT MODE — LIVE STAGING` **only after Art-validated gates**, otherwise `UNVERIFIED` / `WAITING ON ART`; active .dazstory owner/selected cue/line/visit, actual performer root+pose/posture, camera anchor/track, preview vs authored changes and source-aware error state. Real Game window is monitor.
4. Author changes use Unity Undo and dirty handling. Preview-only changes are not authored. Moving a mark triggers bounded reprojection while dragging, never recomputes Director selection. If the selected seat is a chair prefab, **expected author-created new prefab overrides** are allowed; don't erase on Stop. Editing actor instead of semantic mark prompts appropriate selection. Mark editing of `laraFocus` uses authored **local head-child offset** and recomputes aim after pose.
5. First-run experience: with saved FirstContact open, click menu `DazPose > Story > Scene Director Studio`, auto-resolve host/source or explain a single missing selection; one `Start directing` click stages first cue; clicking source cue drives Scene/Game. No requirement to understand `.bindings.asset` before viewing an already-bound production source.
6. Keep typed source binding reconciliation and drag/drop assignments, but **audit unintended importer metadata saves** (`ownerSourceGuid`, `SaveAssetIfDirty`); live native tests only in independent project. Never create scene card-library fields.

## Gate

- An author unfamiliar with the window can select FirstContact and view its current camera shot in **under a handful of obvious actions**, without building a private scene, an extra rig or manually finding hidden tabs.
- Click `SexyVamp`, see real Lara in both native views. Select/edit `stepforward`, `SeatAnchor`, `camera_start` via Hierarchy/Inspector/handles; shot updates live. Undo works.
- Error messages identify missing source, host, binding, scene, camera or seat precisely. Nonvisual cues don't replace current shot.
- Studio stays useful docked beside Scene/Game windows at ordinary screen resolution without requiring custom layout or proprietary render window.
- All native workflow claims based on **Art-provided G2/G3 and final G4**, not an executor-launched Editor. No unrequested new Unity startup.
