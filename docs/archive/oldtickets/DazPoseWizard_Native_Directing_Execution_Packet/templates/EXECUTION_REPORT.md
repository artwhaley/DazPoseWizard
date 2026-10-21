# Native Directing Execution Report — fill with actual evidence

**Current branch/HEAD:**
**Starting HEAD and current difference:**
**Unity exact version / installed packages:**
**Tester, native Unity availability and testing boundary:**
**Explicit no-Unity-by-executor compliance (commands/processes):**
**Independent project fixture location, protected originals and index hashes:**
**Original saved scene/source hashes:**
**Overall:** PASS / PARTIAL / FAIL; **workflow status:** ACTIVE / WAITING ON ART (G#) / COMPLETE / BLOCKED

## Manual gate handoff ledger (Art operates all Unity)

| Gate | Source ready commit | Requested exact menu/clicks + date | Art response/evidence | Native result | Dependent ticket held until PASS |
|---|---|---|---|---|---|
| G1 (after N01) | | | | NOT RUN | N02 |
| G2 (after N04) | | | | NOT RUN | N05 |
| G3 (after N05) | | | | NOT RUN | N06 |
| G4 (during N10) | | | | NOT RUN | N11 final acceptance |

If Art has not supplied G# evidence, **stop** and send `MANUAL_GATE_PROTOCOL.md` handoff. No executor Unity GUI/CLI/batch/Play/build invocation. Native PASS must attribute report/screenshots to Art, not the implementation agent.

## Ticket outcomes

| Ticket | Source/managed | Native Editor | Evidence artifacts / blockers |
|---|---|---|---|
| N00 | | | |
| N01 | | | |
| N02 | | | |
| N03 | | | |
| N04 | | | |
| N05 | | | |
| N06 | | | |
| N07 | | | |
| N08 | | | |
| N09 | | | |
| N10 | | | |
| N11 | | | |

## Native Game view & scene proof

- Actual saved PlayerView/MainCamera serialized link:
- Scene/Game screenshot locations (native, not generated):
- Stepforward/SexyVamp standing cue:
- Lounger seated pelvis/contact/facing:
- Warmup `SitAt` default/CrossLegs compared to `PersistentSitPose(Lounging)` sampled hip correction:
- Main `DissolveTo(Lounger, seated: Lounging)` distinct from Intro standing Lounger:
- Camera StartAt / persistent Track / mark dragging:
- Actor-head `laraFocus` local offset and after-pose tracking, no revision feedback:
- Gesture awaited after-state/base pose, non-awaited observation policy:
- MagicShader / ExecuteAlways wardrobe renderer/blendshape writers:
- No Play mode entered (or user-directed separate check):

## Preview save/Undo safety

- Without authored edit: saved `.unity` diff and hashes after staged Ctrl+S **and Art-triggered forced disposable SaveScene**:
- With authored mark edit: exact persisted fields/new legitimate chair prefab overrides, excluded preview values:
- Undo/Redo while staged:
- Stop/window close/scene close/reload/Play transition:
- Original vs newly authored prefab overrides; scene dirty before/after and outstanding **unsaved** author edits preserved on Stop:
- AnimationMode stop-then-ledger restore, root curve and material reference clone safety:

## Preserved behavior and data

- Tagged global cards add/remove without per-scene registration:
- Protected shared binding `ownerSourceGuid` and global index untouched in original project:
- Canonical story/Director/scenario tests:
- Invalid visual cue followed by valid move/Director **fail-fast halt**:
- Source-first bindings retained:
- Original FirstContact, Void, Lara, body/contact, wardrobe preserved:
- Exact removed custom Studio renderer code:
- New source files and assembly dependencies:

## Repro commands & results

Paste exact commands and exit statuses. Do not reuse historic pass counts without rerunning.

## Native/human work still pending

List each not-run, explicitly blocked or visually unacceptable gate and exact follow-up. No "all complete" claim if any safety blocker native gate is absent.

## Git and handoff

- `git diff --check`:
- Commit(s) and push state:
- Top-of-root AGENTS.md active packet reference updated; Unity ban intact:
- Intentional user-authored changes (should be none to protected production assets):
- Step-by-step updated author guide path:
