# Responsive visual authoring — execution handoff

Status: **practical authoring workflow accepted by Art; main-project handoff delivered**. Art reported the requested check worked on October 19. Evidence and exact acceptance scope: `docs/visual-authoring/IMPLEMENTATION_REPORT.md`. Extended native cases remain unverified where not individually reported; blanket G4/N11 certification is not claimed.

Inspected baseline: `main`, `e36a6d2ee7255d91c61a690a264f37617596f7af`.
Compare actual HEAD and working tree before executing; preserve newer changes.

## Product decision

Art explicitly corrected the Studio's scope:

> This is an editor for exactly asset bindings, poses, and 3d transforms.
> We do not need to calculate story variables, choices or random state.
> I will make it an authoring rule never to permanently change state inside a choice block.
> If current state is rendered ambiguous by a logic block above the card, just tell me.

Build a source-based visual authoring index for Lara, the player body, and the saved player camera. Selecting a cue retrieves a compact description of their visual state and applies it to the real scene. No story execution is involved.

This decision supersedes the **authoring navigation** requirements in N06–N10 that required canonical rehearsal, scenario choices, RNG, nth visits, and Director execution. It does not change the Play mode interpreter or authorize changing authored stories to fit the editor. Retain the native scene, ownership, source-binding, asset protection, and save/Undo rules from N00–N05. Previous G1–G3 acceptance remains historical; G4 remains unaccepted.

## Read and execute

1. [BUILD_PLAN.md](BUILD_PLAN.md): inspected code, exact behavior, ordered tickets and file changes.
2. [CHECKS_AND_ACCEPTANCE.md](CHECKS_AND_ACCEPTANCE.md): managed checks, performance evidence, and Art's native acceptance.
3. [EXECUTOR_PROMPT.md](EXECUTOR_PROMPT.md): standalone prompt for the implementation agent.
4. Repository `AGENTS.md`, then `docs/native-directing/IMPLEMENTATION_REPORT.md` for history and current preservation boundaries. Older documents' runtime-rehearsal navigation is superseded as described above.

Art instructed execution. Continue from the implementation report and current HEAD; do not restart the packet or discard newer work.

## Execution boundaries

- Work on **main**, with coherent local commits when executing. No push, merge, PR, or other remote write without fresh explicit authorization.
- **Never launch or operate Unity**, Unity Hub, GUI/computer-use, CLI/batch, EditMode runner, `-executeMethod`, Play, builds, players, or hardware. Art is the Unity operator.
- Standalone source compilation and pure managed .NET checks are allowed. A source compile is not native acceptance.
- Preserve production scenes, art, bound source assets, global index, rig, wardrobe, toes, GUIDs/metas, and existing user edits. A scene copy alone does not isolate shared assets.
- Reuse the independent fixture at `.dazposewizard/native-directing/G1Project`, scene `Assets/__NativeDirectingFixtures/G1_FirstContact.unity`. Preserve Art's existing fixture marks and bindings. Code-only sync; never copy fixture content back.
- Leave pre-existing packet archival deletions/moves, `oldtickets/` additions, and `.codex-remote-attachments/` outside owned changes.
- No screenshots required. Ask for exact cue/action observations and PASS / FAIL / BLOCKED.
- Do not spawn other agents without Art's explicit instruction.

## Delivery

The executor must deliver source, meaningful managed checks, code-only fixture synchronization, measured diagnostics, and an exact Art-operated checklist. Stop at `WAITING ON ART` for G4. Do not infer acceptance from compilation or fast synthetic timings.

The completion condition is a single usable cue list and adaptive inspector: immediate selection anywhere in the script, direct pose assignment, mark creation/binding/selection, responsive transform editing, clear local ambiguity, and safe Save/Undo/Stop.
