# Day 5 — wardrobe data and Clothing Setup

Authority: `../SEVEN_DAY_PRODUCTION_CONSOLIDATION_PLAN.md`, Day 5. The user explicitly authorized starting Day 5 while Day 4's human visual check remains pending; this does not accept or complete that check.

## Execution order

1. Resolve preset plus authoring configuration into a defensive candidate snapshot. Use it for Save validation, compilation, preview and release. Preserve stable IDs, three slots, source assets and GUIDs.
2. Separate Save Draft from Compile/Validate. Draft persistence survives unsupported fit states. Fork releases before editing; do not change live binding on a failed compile. Track generated attempts separately.
3. Publish immutable self-contained snapshots, check generation mismatch explicitly, preserve the last release on every failed candidate.
4. Rebuild existing setup window: outfit list, garment rows grouped Base/1/2, explicit selectors, garment-name sequence, preview controls, revision/stale status, Undo/Redo, Save Draft/Revert/Compile/Publish, advanced calibration panels.
5. Execute current contract/migration and Day 5 transactional round trips with real imports in isolation. Check reimport preservation, fresh actor runtime sequence, exact saved fixture/player where claimed. Keep Day 6 integration/stress gates distinct.

## Acceptance

Change layer → Save Draft → close/reopen → compile/preview removal → publish → fresh actor repeats the sequence. Repeat from a release; failed candidate preserves previous bytes/references and bound state. Reimport preserves assignments/material/footwear edits. Human workflow review is separate and pending until the user reports it.

## Scope protections

No GUI automation, no main-project batch run or scene regeneration, no framework migration, new interpreter, import pipeline or wardrobe semantics. Preserve authored Void environment and accepted hair/hosiery/footwear. No commit/push. Luna receives bounded editor UI and regression-source work; root owns data/publishing/runtime and execution evidence.
