# Reusable wardrobe and protected cards — execution packet

**October 20 update:** Art accepted Gate A's single-Lara appearance behavior and waived the second-actor check. Gate A's disposable scene/setup has been retired; its published wardrobe assets are retained. Continue at W04. Diagnostic compositions are temporary acceptance tools, never part of importing/adding outfits or hair; retire each after acceptance rather than accumulating scenes. The latest AGENTS.md entry and progress.json govern this scope update.

Prepared October 19, 2026 against `main` at `110e2b4`. Art authorized this packet's execution by saying “Continue” on October 20, 2026. W00 is underway; implementation and native acceptance remain separate statuses.

## Read in this order

1. Repository `AGENTS.md` and this entry point.
2. [CONTRACT.md](CONTRACT.md) — fixed product decisions and preservation boundaries.
3. [API_AND_OWNERSHIP.md](API_AND_OWNERSHIP.md) — fixed types, ownership and state machines.
4. [ORDER.md](ORDER.md) — dependencies and exact native gate stops.
5. The current file in [tickets](tickets/W00_BASELINE.md).
6. [CHECKS.md](CHECKS.md) and [MANUAL_GATE_PROTOCOL.md](MANUAL_GATE_PROTOCOL.md).
7. [EXECUTOR_PROMPT.md](EXECUTOR_PROMPT.md).

Design rationale and code inventory: [implementation plan](../../docs/wardrobe-directing/IMPLEMENTATION_PLAN.md). The packet resolves implementation choices left open in that plan. Product contract changes require Art's direction; routine private implementation choices do not.

## Non-negotiable result

Separate reusable hair/clothing assets, one existing Lara wardrobe controller, source-owned outfit/hair script bindings, correct First Contact startup, **every card restores its entrance presentation**, sequences retain changes, and responsive wardrobe editing in the existing Studio.

Every means direct `play`, `Director.Execute`, random draws and nested cards. No special cards, restore flags, Intro exception or Director-only protection. Intro becomes a sequence. Sequences invoked inside a card remain contained by that card's return.

## Execution boundaries

- Work on `main`; inspect current HEAD/status and retain newer changes. Local coherent implementation commits are permitted after Art authorizes execution of this packet. No push, PR, merge or remote write without separate fresh authority.
- **Executor must never launch or operate Unity, Hub, GUI, CLI/batch, Unity tests, executeMethod, Play, builds, players, computer-use or hardware.** Art performs all native operations. Standalone Roslyn/source and pure managed checks are the only planned executor-run checks.
- No subagents/delegation without explicit authorization. Luna can execute this packet in the same chat/checkout when Art assigns it; do not message or create another chat on Art's behalf.
- Do not run the old import wrappers or automation just because an older document lists them.
- Preserve authored scene/art/rig/toe/wardrobe values, existing GUIDs, immutable releases and global card membership. First Contact's named script/binding changes and Lara's named wardrobe mount are the bounded production changes.
- Current unrelated ticket archival deletions/moves and remote attachments are outside this packet. Do not stage them.
- Source readiness, Art's native technical results, and Art's visual acceptance are separate facts. No screenshots required.

## Resume / completion

Read [progress.json](../../docs/wardrobe-directing/progress.json) and [IMPLEMENTATION_REPORT.md](../../docs/wardrobe-directing/IMPLEMENTATION_REPORT.md) on each resume. A native gate without Art's explicit PASS remains `WAITING_ON_ART`; elapsed time and compilation are not approval. Repair a FAIL and return to that gate. Do not build dependent work across a failed gate.

The main Unity project is `validation/DazPoseUnityValidation`. Native diagnostics for this packet use new, isolated saved compositions **inside that project**, under `Assets/__WardrobeProtectedCards/`; builders clone the required content and never modify shared source assets. Do not copy Art's older fixture scene or its bindings into main. The existing native-directing fixture remains untouched unless Art separately requests a code-only sync.

Only after execution is authorized, add a brief top `AGENTS.md` entry identifying this packet as the current queue and linking its progress report. Keep older entries as history; preserve their absolute no-Unity and asset protections.
