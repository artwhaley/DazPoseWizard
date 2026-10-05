# Runner and evidence contract

These runners/entry points are implementation requirements; they are not present
when this handoff is authored. E0 must create them. Do not run the shown commands
and mistake a missing script for an external blocker.

`run-wardrobe-execution.ps1 -Stage <stage>` supports Contract, Migration, Runtime,
Layers, Setup, Persistence, Integration. Each invokes the same-named public static
method on `DazPose.UnityValidation.WardrobeExecutionValidation`, with
`-wardrobeExecutionStage` and `-wardrobeExecutionReport` command-line arguments.
Outputs: `TestOutput/wardrobe-execution/<stage>/report.json`, Unity log and captures.
Use actual existing ignored output roots; add ignore entries if needed.

Contract/Migration editor checks may use -nographics/-quit. Runtime/Layers/Setup/
Integration require GPU rendering and play-mode frame sampling; omit -quit when
the harness controls completion and calls EditorApplication.Exit. Persistence
uses the mode required by its actual tests. All modes need a bounded timeout and
proper failure exit. Use installed Unity in isolation and the importer lock.

Report schema (minimum): schemaVersion=1, stage, passed, startedUtc, completedUtc,
sourceHash, inputAssetHashes, unityVersion, assertions[{name,passed,expected,actual}],
artifacts[], limitations[]. Source hash covers synchronized implementation C# and
test code; input hashes cover the configurations/packages used. Each gate has
named assertions below. Skipped required assertions fail the gate. An exit code
alone or merely compiling is insufficient. Reject reports from a prior run before
starting, then require fresh matching stage/hash evidence. Record reports in
progress.json with actual gate status; never seed success flags in this handoff.

## Required checks

| Gate | Required evidence |
| --- | --- |
| runner | success/failure/missing-report behavior and isolated lock handling |
| contract | all seven populated masks, boundary no-ops, invalid ceilings, immutable snapshots, alias/source-ID validation |
| migration | three source packages, canonical signature/morph contract, naked/full states, binding identity/count, stable second generation IDs/GUIDs |
| runtime | retained actor/body/animator identities, transformed actors, two independent actors, morph/pose preservation, FIFO/QueueFull, rollback and disable settlement |
| layers | every reachable mask, coverage union/restore, both shoe-layer fixtures, retained bent-foot hosiery with shoe contact off, walk support/rigidity/culling and particle identity |
| setup | one persistent scene, exclusive assignments, common runtime path, hair persistence, inspection controls and candidate invalidation |
| persistence | Save/reload/Revert/Variant, shared immutable data, reimport preservation, failed candidate preserves released references and bytes |
| integration | deterministic concealed multi-actor recall, authored commands, dissolve recovery/ownership, 100 cycles/disposal, Windows player results, scoped publication |

Use existing baseline reports as fixtures, not invented expected measurements.
Preserve existing accepted checks and their thresholds. Additional minimums:

- Source/body rest mapping error <= 0.00003 m and morph-preservation position
  error <= 0.000002 m where identical topology and frames are expected.
- Rigid shoe internal-distance error <= 0.0001 m; supported flat-floor contact and
  penetration error <= 0.003 m. Sample at least 181 walk frames and report actual
  supported-frame counts and rejected samples; compare to the accepted source
  fixture's sampling policy. Zero supported samples cannot pass.
- Zero observed renderer culling misses in sampled expected-visible frames.
  Check live transformed bounds, not only updateWhenOffscreen settings.
- Dissolve disappearance/recovery: use the existing image comparison method,
  <=20 erroneous pixels relative to its accepted reference policy; include images.
- Canonical anatomy/facial controls preserve requested values and resulting
  mesh positions. A blendshape count alone cannot prove matching channels.
- At 100 cycles, exactly one canonical body/animator per performer; cache inventory
  stops growing after all distinct states are visited, and owned resources are
  released on destruction. Report actual counts; do not claim zero allocation.

If a fixture cannot support an asserted metric, record the missing capability and
keep the relevant gate incomplete. Do not adjust thresholds to make a new source
pass. Tests must use actual generated assets and live runtime operations, not
duplicate production arithmetic as the only oracle. No manual artistic decision
is required to run technical fixtures; user visual review remains separate.

## Resume and delivery

Gate states: pending, running, passed, failed, blocked. Record stage, files changed,
latest command/report, source/input hashes, actual limitation and next action.
On resume confirm report hashes against current inputs. Re-run affected gates
after implementation changes, not unrelated passed suites indefinitely.

Final delivery includes current reports, player result, setup scene/control guide,
publication summary and outstanding artistic review. A clean-context import trial
is a different acceptance gate and cannot be claimed from these implementation
tests. No full DazPoseTool GUI publish/run is needed for this Unity-only assignment.
