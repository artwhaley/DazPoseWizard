# Testing and Validation Rules for This Packet

The project is still discovering architecture. This sprint must not fossilize provisional behavior behind a large permanent test suite.

## Absolutely prohibited

- Automated Unity Play Mode.
- Automated "press Play" acceptance.
- Claiming that source inspection, compilation, batch mode, or unit checks prove the scene plays correctly.
- Creating a new permanent test framework.
- Creating a large matrix of behavioral tests for provisional story semantics.
- Duplicating the existing production/performance validation harness.
- Generating disposable scenes and then claiming the delivered saved scene works.
- Rewriting the accepted `FirstPerformanceVoid.unity` environment.

## Desired validation posture

Write simple code with:
- explicit types;
- small owners;
- useful exceptions;
- deterministic selection where random behavior exists;
- snapshot/debug readouts;
- logs only at meaningful story boundaries/errors;
- no hidden magic.

### Automated checks allowed

Use existing repository checks where they already exist.

It is acceptable to add **a very small number of pure-data checks** only if they materially protect:
- JSON parse/validation;
- atomic profile persistence;
- symmetric equipment conflict evaluation.

Do not add a new test assembly for them. Prefer adding to an existing suitable EditMode test location if one exists.

These checks are optional. Source clarity is more important than maximizing test count.

### Existing tests

If existing tests fail because public contracts were intentionally changed:
- update only the directly affected expectations;
- do not bulk rewrite old validation infrastructure;
- do not keep obsolete architecture alive merely to satisfy a historical test.

### Static gate before handoff

Executor must at minimum perform:
- repository diff inspection;
- search for accidental new dependencies;
- search for direct gameplay bypasses of `PerformanceRun`;
- search for TODO/throw stubs in D1–D5 production paths;
- JSON syntax validation for authored `.json` assets;
- verify no automated Play Mode was executed;
- if Unity compile/import diagnostics are available without Play Mode, record them separately.

### Human gate after packet

Only the user may mark D1–D5 gameplay accepted after manually pressing Play in the saved D1–D5 integration scene.

The executor's status must say one of:
- `SOURCE COMPLETE — AWAITING USER PLAYBACK`
- `SOURCE INCOMPLETE — <reason>`

Never say `PLAYBACK PASSED`.
