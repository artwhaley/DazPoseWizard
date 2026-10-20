# Ordered ticket execution and gates

| Order | ID | Purpose | Gate |
|---|---|---|---|
| 0 | F00 | Baseline, exhaustive scene and dependency classification | F0 |
| 1 | F01 | Extract eleven obsolete diagnostic scene consumers | F1 |
| 2 | F02 | Migrate actual test coverage and specialized manual diagnostics | F2 |
| 3 | F03 | Retire scenes, obsolete builders, dead data; resolve remaining candidate orphans | F3 |
| 4 | F04 | Path, GUID, import, catalog, documentation, regression, clean-state proof | Cleanup gate |
| 5 | U01 | Real Main Menu navigation and return / publishing entries | UI1 |
| 6 | U02 | Production UI architecture, prefabs, responsive panels/focus | UI2 |
| 7 | U03 | Transactional profile/edit draft API | UI3 |
| 8 | U04 | Interests browser with 100 rows | UI4 |
| 9 | U05 | Equipment browser with 100 rows | UI5 |
| 10 | U06 | Integrate in-session prompts, Settings, clothing, Stop | UI6 |
| 11 | U07 | Phase 2 integration tests / polishing | Phase 2 gate |
| 12 | S01 | Manifest-first `.dazstory` parse / compiler type requirements | S1 |
| 13 | S02 | Managed typed content/scene bindings and stable reconciliation | S2 |
| 14 | S03 | Studio shell, flow display, source navigation and diagnostics | S3 |
| 15 | S04 | Disposable renderer/stage with real prefab animation sampling | S4 |
| 16 | S05 | Contextual asset fields + audio/pose/effects/funscript preview | S5 |
| 17 | S06 | Mark, camera, seating and contact staging with explicit Undo commits | S6 |
| 18 | S07 | Rehearsal hooks in canonical StoryMachine | S7 |
| 19 | S08 | Rehearsal host, overrides, events, traces and replay | S8 |
| 20 | S09 | Director global-card selection/drilldown and nested card stepping | S9 |
| 21 | S10 | Studio to saved-scene/runtime handoff, regression and real First Contact acceptance | S10 |
| 22 | S11 | Docs, final report, integration and manual author testing script | Phase 3 gate |

Do not parallelize changing source/compiler serialized formats. U04/U05 can develop in parallel after U03 and frozen shared UI contracts; merge and run integrated tests before U06. S04 and S05 only parallelize after S03's viewport ownership and S02's binder API stabilize. Sequence critical tickets individually. Phase 1 completion is required as first milestone, not a footnote in the report.
