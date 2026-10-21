# Acceptance matrix — proof required before saying "done"

Each gate requires **evidence**. Allowed evidence status: `PASS`, `FAIL`, `PARTIAL`, `NOT RUN`; the workflow state `WAITING ON ART` means executor has paused for the user's native check. **Only Art may open/operate Unity**, including manual compile, import and diagnostic menu clicks. Executor prepares source/menus and requests Art's observed results at **G1 after N01, G2 after N04, G3 after N05, G4 in N10**. A gate not run **blocks dependent tickets**; it is not merely a later report caveat. All native rows below say what **Art** does in an isolated Unity project. See `MANUAL_GATE_PROTOCOL.md`.

| ID | Requirement | Execution procedure | Required evidence | Mandatory |
|---|---|---|---|---|
| A01 | Existing saved Unity scene selected, no duplicate stage | **Art:** open disposable FirstContact scene in independent test project, Studio+Scene+Game | Hierarchy/camera refs, screenshot; no PreviewRenderUtility | G1 |
| A02 | Real Game view in Edit Mode | **Art G1:** click G1 Camera Proof; show shot, temporary real PlayerView root/rig change, compare Game frame; no Play | Art screenshot/report + inspected real MainCamera ref | **G1 blocker** |
| A03 | Standing end cue | Stage `LocomoteTo(stepforward)` then `PersistentPose(SexyVamp)` | Actual Scene/Game screenshots, visual location/pose | Yes |
| A04 | Persistent standing mark editing | Drag stepforward native gizmo while cue selected | Game follows continuously; scene dirty only from mark | Yes |
| A05 | Camera endpoint | Stage `Camera.StartAt(camera_start)`, adjust camera mark | Actual view tracks endpoint; unchanged FOV/HDRP | Yes |
| A06 | Persistent target tracking | Stage `Camera.Track(Lara)`, change Lara location twice and move focus | Game aim changes; camera root remains current anchor | Yes |
| A07 | **Three actual Lounger states** | **Art G3:** Intro `DissolveTo(Lounger)` **standing**; Warmup first seat `SitAt(Lounger)` → `PersistentSitPose(Lounging)`; Main later seated `DissolveTo(Lounger, seated: Lounging)` after Director | Separate default CrossLegs vs sampled hip-corrected Lounging and seated dissolve in Scene+Game, source/visit citation | **G3 blocker** |
| A08 | Seat mark semantics | **Art G3:** move SeatAnchor then ApproachAnchor, repeat and scale disposable actor | Hip reaches seat for pose-specific endpoint; ApproachAnchor alone does not move settled hip; no drift | **G3 blocker** |
| A09 | Other commands retain composition | Step Say, Popup, Wait, Director wrapper | Camera/pose remain; logic trace/current cue advances | Yes |
| A10 | Global card selection untouched | **Art G4:** add scratch tagged selectable card **only in separately copied project/index**, query existing scene without editing it | Original global index hashes unchanged; isolated canonical candidate list and selected card's real visual cue | Yes |
| A11 | Per-encounter overrides | Force only second of three same-line Warmup draws, replay | History/selection RNG and visual cue parity; no repeated override leak | Yes |
| A12 | Script-before/after/Back | Stage source cue 1, cue 2, revisit cue 1, compare | Deterministic scene pose/camera; no cumulative root drift | Yes |
| A13 | Re-anchor without re-execution | Record card-selection count, drag mark | Identical seed/draws/history, changed physical placement | Yes |
| A14 | Save with only preview staged | **Art G2/G3/G4:** baseline, Ctrl+S clean scene **and forced SaveScene on isolated disposable fixture** to guarantee serialization, reopen | Zero preview properties in YAML/prefab overrides, no dirty/Undo leakage, baseline refs intact | **Safety blocker** |
| A15 | Save with authored mark change | **Art G2/G3/G4:** drag standing/camera/seat prefab mark, Ctrl+S during preview, reload | Authored mark + legitimate new Validated Lounge Chair prefab overrides persist; temporary Lara/camera/pose/material/blendshape do not | **Safety blocker** |
| A16 | Native Undo/Redo | Move mark, Undo, Redo while staging | Mark and live staged image follow Undo; stage writes absent from undo list | **Safety blocker** |
| A17 | Stop/close/reload restoration | **Art:** stage pose/camera/body, deliberately edit mark **without saving**, Stop, close/reload/recompile | Preview-owned values restored; **unsaved authored mark and dirty state preserved**; scene/camera/renderer original refs intact | **Safety blocker** |
| A18 | AnimationMode coexistence | Open Animation Window preview then attempt Studio stage | Safe refusal or separate ownership, no stolen mode or dirty prefab | Yes |
| A19 | Invalid seat/camera/binding | Use disposable fixture with each missing link | Source-specific blocking diagnostic, no last-good false PASS | Yes |
| A20 | No game side effects | Rehearse with toy device disconnected/profile baseline snapshot | No `GameApplication` session, writes, hardware output or prompt | Yes |
| A21 | Preserve content/GUIDs | Hash original FirstContact, card assets, Lara, Void, male/wardrobe | Baseline vs output diff, no unexpected meta changes | **Safety blocker** |
| A22 | No custom Studio renderer | Search production Editor StoryStudio refs | No `PreviewRenderUtility` performance scene, cloned environment, old viewport UI | Yes |
| A23 | Compiler/Director regression | Run Story/Rehearsal/Player/Application tests, current source assembly compilation | Exit code, pass totals, command environment | Yes |
| A24 | Native import and Editor safety | **Art manually** opens isolated Unity 6000.5.9f1 project, permits compilation/import, opens scene and invokes G1–G4 menus | Art-provided real Console/import report, no missing script; **never executor-launched Unity** | **G1–G4 blocker** |
| A25 | First-use usability | Follow author guide without tribal knowledge | Actual author can find source/host/Start/Next/mark/Save using native windows | Yes |
| A26 | Native lifecycle transitions | Change source, host, close scene, trigger compilation; inspect Preview lease | No surviving staged performer/camera / queued stale effect; controlled diagnostics | Yes |
| A27 | Existing MainMenu/player UI intact | Source/scene smoke and managed profile tests | Catalog/UI tests; no unexpected UI diff | Regression |
| A28 | Entire native workflow obeys no-Unity-by-executor rule | Audit executor commands/activity + separate Art G1–G4 confirmations | No GUI/CLI/batch/Play/build/remote Unity invocation by executor; exact manual request and Art responses | **Execution blocker** |
| A29 | Protected asset/index isolation | Before any native import/reindex, verify **independent project** includes its own binding and global-card index assets | Original FirstContact, binding `ownerSourceGuid`, card index and content hashes unchanged; or explicit Art-approved migration log | **Safety blocker** |
| A30 | Runtime seating solver parity | **Art G3:** compare default SitAt CrossLegs baked endpoint and pose-specific post-sample `hip→seat` correction, including scaled/rotated fixture | Hip position/orientation/tolerance, repeated-restage no drift, separate stable cue states | **G3 blocker** |
| A31 | Command-specific facing/pose reset | **Art G2/G3:** StartHiddenAt/DissolveInAt/Camera.StartAt full rotation; standing DissolveTo yaw and `initialPose`; LocomoteTo yaw | Runtime-method mapping and native pose/root measurements | Yes |
| A32 | Gesture overlay lifecycle and non-awaited semantics | **Art G4:** inspect awaited vs non-awaited FirstContact gesture; test no terminal-frame contamination | Awaited after = base persistent pose; non-awaited observation explicitly labelled; optional audition correct additive/mask/time | Yes |
| A33 | MagicShader/cloned material and wardrobe writer safety | **Art G4:** stage glowy/regular endpoint, `[ExecuteAlways]` renderer/blendshape writers and save/Stop/reload | Correct visual settled endpoint or incomplete cue; no sharedMaterial asset/renderer/prefab leaked value | **Safety blocker** |
| A34 | Revision separation / laraFocus no loop | Move parent chair and authored marks while stage updates head-child `laraFocus`; inspect history/stamp | Spatial restage without logical stamp/draw count change; focus aimed after pose; no event storm | Yes |
| A35 | Fail-fast visual queue | Invalid seat/camera visual cue immediately followed by valid movement and Director command | Transport halts at offending source/visit, no queued later visual/Director effect masks failure | **Safety blocker** |
| A36 | Incremental property-family transaction proof | Add camera, bone, renderer, material, seat staging in order and rerun G2/G3/G4 save/Undo checks | Specific ledger ownership and Art native reports per family; no blanket inference from early G2 | **Safety blocker** |

## Proof discipline

- **Source-only**: `git diff --check`, parser/managed tests and grep establish code shape, **not** Unity rendering.
- **Native Editor:** always **Art-run only**, via manually opened Edit Mode project and temporary menu diagnostics. Even Unity batch-mode/EditMode automation is **forbidden to executor**. Report Art-provided text/screenshots and state who actually clicked.
- **Human judgment**: pose/seat beauty/comfort and intended director composition need user visual acceptance; the executor cannot self-certify taste.
- **No executor-run Unity of any kind**, including GUI, CLI, batchmode, Play, build or hardware. Art may separately choose a Play/timing/physical-grip test; it is not an executor instruction and not required for the native settled-endpoint workflow.
- **No source art refresh:** all native tests in **independent project copies plus disposable scenes**; protected original assets and index remain unchanged.

## Baseline tests (adjust if repo changes)

```bash
dotnet test tests/DazPose.Story.Tests/DazPose.Story.Tests.csproj --no-restore
dotnet test tests/DazPose.Player.Tests/DazPose.Player.Tests.csproj --no-restore
dotnet test tests/DazPose.Rehearsal.Tests/DazPose.Rehearsal.Tests.csproj --no-restore
dotnet test tests/DazPose.Tests/DazPose.Tests.csproj --no-restore
python scripts/check-unity-source.py
python scripts/audit_p2_p3_assets.py
git diff --check
```

The last pushed pre-packet corrective report claimed **196 managed tests** and 7 assemblies passing, with native acceptance pending. Treat these as historical baseline claims only; rerun and record current totals, do not copy previous PASS as new evidence.
