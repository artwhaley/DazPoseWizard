# Executor Protocol

This packet is written for a lower-capability implementation agent. Follow this order exactly.

## Before coding

1. Read `AGENTS.md`.
2. Read the current authoring plan/API/handoff/progress.
3. Inspect the current versions of every file named by a ticket before editing it.
4. Create a branch unless the human operator explicitly requested direct-main work.
5. Record the starting commit SHA in the final handoff.

## For each ticket

1. Read the ticket completely.
2. Inspect existing source at the seams named in the ticket.
3. Implement only that ticket.
4. Keep public names exactly as specified unless an existing type collision makes that impossible.
5. If a specified name collides, use the nearest clear name and document the deviation; do not redesign the model.
6. Do not enter Play Mode.
7. Review the diff for accidental scope expansion.
8. Update a local packet checklist / final handoff notes.
9. Continue to the next ticket.

## Error-handling policy

Story/content errors must fail loudly and locally with actionable context.

Do not:
- silently skip malformed story steps;
- silently substitute a different scene target;
- search hierarchies by fuzzy names;
- auto-pick an arbitrary duplicate binding;
- swallow profile persistence errors without exposing a diagnostic.

Do:
- preserve current toy "silent no-op" policy;
- wrap story errors with story/step/op IDs;
- expose current story execution state for debugging.

## Debuggability policy

At any moment a developer should be able to inspect:
- current story ID;
- current step ID/opcode;
- current session phase;
- phase completion;
- mood;
- intensity modifier;
- session seed;
- prepared equipment IDs;
- active equipment IDs;
- clothing state;
- last emitted/received story event;
- current checkpoint/message.

Implement one compact runtime snapshot object and one optional debug overlay/presenter. Do not create a full developer console.

## Final status

The final executor report must enumerate:
- changed files;
- new files;
- schema version;
- known authored bindings needed in the saved scene;
- static/compile checks actually performed;
- checks not performed;
- exact manual Play Mode checklist for the human;
- status `SOURCE COMPLETE — AWAITING USER PLAYBACK` if source work is complete.
