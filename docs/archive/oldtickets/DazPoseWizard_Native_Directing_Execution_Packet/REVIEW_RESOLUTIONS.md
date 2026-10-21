# Three-review resolution log — decisions reflected in tickets

This is a code-review reconciliation, **not native evidence**. Recheck actual source at HEAD before implementation. All items below have matching implementation/acceptance language elsewhere; this log cannot override tickets.

| Finding | Decision / implementation owner |
|---|---|
| Multiple reviewers saw a conflict between Unity-dependent gates and `AGENTS.md` no-Unity rule | **No conflict is permitted**: Art performs all Unity operations on request. Executor pauses at G1–G4; no autonomous launch, no skip-on-PARTIAL (CONTRACT, MANUAL_GATE_PROTOCOL, N01/N04/N05/N10). |
| `AGENTS.md` points to old packet; commit rule contradicted ticket order | Correct top active reference at N00 without weakening ban. Authorization for this execution permits **local commits** only; no push (AGENTS_MD_HANDOFF, CONTRACT). |
| Camera true rig may render in Edit Mode but not proven | G1 first. No N02+/custom viewport workaround before Art confirms real Game view (N01). |
| Scene copy leaks shared binding reconciliation / global-card index writes | Separate isolated **project**, not scene copy, or ask Art for explicit migration; `SaveAssetIfDirty` may bypass `OnWillSaveAssets` (N00, N08). |
| Scene dirty baseline restoration would erase unsaved authored mark edits | Stop restores **preview properties**, preserves unsaved authored mark changes and dirty state. Compare property ownership not an old dirty bit (N02, A17). |
| Save with clean scene might not serialize at all | Both normal Ctrl+S and **forced disposable SaveScene** in Art-operated menu, plus dirty mark and prefab-override tests (N02, G2, A14). |
| Original seat rule assumed baked `PelvisOffsetAt(1)` for every seated pose | Split default SitAt CrossLegs/baked stable endpoint from persistent pose/seated dissolve, which sample pose, set seat yaw and move root by world `seat - sampledHip`; repeated tests on scaled actor (ARCH §5, N05). |
| Wrong FirstContact seating step | Intro's Lounger dissolve is **standing**. First seated path: **Warmup** SitAt then PersistentSitPose(Lounging). Main has later `DissolveTo(Lounger, seated: Lounging)` after Director (A07, START_HERE). |
| Standing `DissolveTo` preserves pose in packet, but runtime resets | Reset to `initialPose`, use yaw-only facing. `StartHiddenAt`, `DissolveInAt`, `Camera.StartAt` use full mark rotation. Verify every cue against runtime method (CUE matrix, N04). |
| Gesture terminal clip frame called post-cue state | Awaited gesture completion fades overlay weight to zero; **after** is underlying persistent pose. Non-awaited invocation observation needs explicit declared policy; optional overlay audition must honor mask/additive reference (CUE matrix, N04/N06). |
| Preview can fight `[ExecuteAlways]` Lara wardrobe/outfit/heel scripts | Audit and own all affected renderer/blendshape/material writers; detect conflicts and restore. **Do not prescribe root SetActive(false)**, which can alter lifecycle/pose; test real object behavior (N00/N02/N04). |
| AnimationMode cleanup order and root curves | Snapshot before sample; terminate owned AnimationMode driver before ledger restore; test with root curves and concurrent Animation Window (N02). |
| Broad `CaptureStamp` hashes all components, stage invalidates itself | Separate logical content changes and targeted anchor/world-transform changes; sample relevant parent-transformed anchors; ignore stage-owned bones/root, test old `StudioCorrectiveChecks` expectation (N06). |
| `laraFocus` moves when Lara's head moves | It is an **actor descendant**, not an external fixed world anchor. Keep local authored offset unchanged, aim camera after pose sampling; no focus-driven feedback loop (N03/N06). |
| Preview bridge swallows failure and continues queued effects | Ordered, fail-fast transport; no later valid effect can overwrite the offending cue (N06). |
| Seat/focus mark drags create chair prefab overrides | Legitimate author overrides are expected, stage-only overrides prohibited (N02/N05/A15). |
| MagicShader and non-awaited gestures complicate acceptance | Neither silently `PASS` nor blanket permanent no-op. Preview settled material endpoint safely or mark **incomplete** until fixed. Optional audition separate; dynamic physical grip remains disclosed Play-only approximation (N04/N10). |
| Reimplementing camera geometry more than necessary | Prefer existing safe `ResetPerformanceView` **mathematics**, only minimal exposure of tracking `DesiredRotation` math, no runtime async methods; avoid invasive shared refactor if existing code is callable safely (N03). |
| Large N05–N11 scope on unproved native premise | **G1 before N02**, **G2 after standing N04**. Stop at native failure and replanning only with Art's agreement (TICKET_ORDER). |
