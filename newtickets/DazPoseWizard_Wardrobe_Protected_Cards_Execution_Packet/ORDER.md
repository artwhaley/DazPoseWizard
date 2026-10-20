# Execution order and status rules

Do not start until Art authorizes execution. Then inspect HEAD/status and execute on main:

| Order | Ticket | Dependencies | Stop |
| --- | --- | --- | --- |
| 0 | [W00 Baseline](tickets/W00_BASELINE.md) | Authorization | Source evidence recorded |
| 1 | [W01 Shared selection](tickets/W01_SHARED_SELECTION.md) | W00 | Source checks |
| 2 | [W02 Reusable assets](tickets/W02_REUSABLE_ASSETS.md) | W01 | Source/tooling ready; native assets still pending |
| 3 | [W03 Correct release](tickets/W03_RELEASE_AND_GATE_A.md) | W02 | **Gate A: WAITING_ON_ART** |
| 4 | [W04 Playable sequences](tickets/W04_PLAYABLE_SEQUENCES.md) | Gate A PASS | Source checks; Intro conversion |
| 5 | [W05 Wardrobe vocabulary](tickets/W05_WARDROBE_VOCABULARY.md) | W04 | Source checks |
| 6 | [W06 Snapshots and scopes](tickets/W06_SNAPSHOTS_AND_SCOPES.md) | W05 | Coverage and lifecycle checks |
| 7 | [W07 Smooth return](tickets/W07_CARD_RETURN_AND_GATE_B.md) | W06 | **Gate B: WAITING_ON_ART** |
| 8 | [W08 Studio](tickets/W08_STUDIO_AND_GATE_C.md) | Gate B PASS | **Gate C: WAITING_ON_ART** |
| 9 | [W09 Production](tickets/W09_FIRST_CONTACT_AND_GATE_D.md) | Gate C PASS | **Gate D: WAITING_ON_ART**; complete only after PASS |

W04/W05/W06 can be designed earlier, but do not implement the next dependent ticket across an unpassed gate. A source/tooling-ready ticket is not native-complete. W02 is closed only when its generated assets are covered by Gate A.

At each ticket: read its dependencies, implement bounded changes, add/run its planned managed checks after execution authority, use standalone source compile, inspect owned diff, update evidence and status, commit coherent owned changes. Preserve unrelated files. Report actual check outputs; do not invent totals.

Status vocabulary in progress.json:

- `NOT_STARTED`: no implementation.
- `IN_PROGRESS`: implementation incomplete.
- `SOURCE_READY`: source/checks complete, native result still pending where applicable.
- `WAITING_ON_ART`: named gate handed off with concrete steps/revision.
- `FAILED`: evidenced check/gate failure; repair this ticket/gate.
- `COMPLETE`: all ticket criteria, including dependent native evidence, satisfied.

Gate status: `NOT_READY`, `WAITING_ON_ART`, `PASS`, `FAIL`, `BLOCKED`. BLOCKED means a specific missing input/capability, not compile errors or slow work. Gate response must match tested revision and artifact; if code changes affect an accepted gate, record invalidation and repeat only the affected gate cases before passing dependencies.

Update docs/wardrobe-directing/progress.json and IMPLEMENTATION_REPORT.md at each checkpoint. Logs and generation/asset inventories go under docs/wardrobe-directing/evidence/; large captures/build outputs are not required. Raw reports produced by native diagnostics are imported/read by the executor only after Art generates them.

Do not change the user's current task/model/chat or invoke an automation to continue later. Execution stays in the assigned chat unless Art directs otherwise.
