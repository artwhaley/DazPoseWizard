# N04 — Real Lara in Scene/Game: location, visibility, pose and look

**Depends on:** N02 and N03. **Deliverable:** native performer visual-state adapter and standing proof.

## Execute

1. Resolve Lara root/Animator/skinned renderer hierarchy and competing `[ExecuteAlways]` wardrobe/outfit/heel writers. Preserve/restore root, bone, renderer visibility (`forceRenderingOff`, enabled), `sharedMaterials`, property blocks and morph/blendshape state without disabling actor root by default. Detect conflicting writer updates; validate actual saved object, no clone.
2. **Differentiate runtime commands**: `StartHiddenAt`/`DissolveInAt` use **full** mark rotation; standing `DissolveTo` uses **yaw-only** and snaps `initialPose`; `LocomoteTo` uses destination-facing yaw and its documented rest behavior. `StartIdle` requires a configured initial pose; no fabricated neutral fallback. Write traceable runtime-method citations into cue tests.
3. Sample actual `PerformerPose` on real Lara using owned AnimationMode with root-curve isolation. **Awaited** `PlayOneShotGesture` after-state is underlying persistent pose after additive/masked weight fades to zero; never permanently replace pose with gesture clip terminal frame. For **non-awaited** FirstContact gesture, explicitly label the observation boundary as `in-progress / indeterminate from end-frame projection`, or offer a separate **correctly masked/additive** audition at specified clip time; do not silently sample standalone clip as persistent end-state.
4. Stage actual MagicShader material endpoint only via reversible ledger of **renderer sharedMaterial reference substitutions/material clone lifetimes** as well as property blocks. Never modify shared Material assets. If not yet supported or safe, mark that cue **INCOMPLETE** (not a silent v1 no-op or PASS). Art will verify at G4; no premature aesthetic sign-off.
5. Changing `stepforward` or standing target with Unity's Transform handles/Inspector immediately recomputes Lara root and live Game shot from projection. No button labelled Commit is needed for viewport refresh. User saves/undoes target via Unity. **Before N06 exists**, `G2 Standing and Save Proof` must call the real N02/N03/N04 lease/adapter with a tiny fixed source-bound projection, clearly labelled as a **technical fixture, not full cue navigation**.
6. Add tests for staging idempotence, clip sampling on saved Lara, visibility/material teardown, null/missing asset errors and unaffected male body.

## Gate

- Select real FirstContact `Lara.LocomoteTo(stepforward)` then `Lara.PersistentPose(SexyVamp)`; both Scene and Game show **actual** Lara at the correct position/pose.
- Drag/rotate stepforward while Studio holds the cue: Lara follows instantly, camera tracks if enabled, no additional Director draws, no root drift on repeat reprojection.
- Speech cue retains pose and camera. Standing DissolveTo correctly resets `initialPose`. Stop restores bone/material/root/active state **without erasing outstanding authored mark edits or their dirty state**.
- The old `ScenePreviewSession.Sample()` may be used as behavioral reference, but must not remain a dependency.
- **HARD PAUSE G2:** provide Art exact project/menu (`G2 Standing and Save Proof`), stepforward and `camera_start` drag steps, Ctrl+S + forced save + Undo/Stop/reopen checklist, report path/screenshot request. **Do not proceed to N05 until Art reports PASS**. Neither executor nor its tests may launch Unity.
