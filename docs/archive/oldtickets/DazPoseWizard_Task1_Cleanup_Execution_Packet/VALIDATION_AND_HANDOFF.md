# Validation, stop conditions and executor reporting

## Actual validation commands (from repository root)

```powershell
# Guard: do not proceed from an unexpected dirty working tree without recording ownership.
git rev-parse HEAD
git status --short --branch
git diff --check

# Existing managed tests. Do not fabricate a result if the .NET SDK is missing.
dotnet test tests/DazPose.Story.Tests/DazPose.Story.Tests.csproj
dotnet test tests/DazPose.Tests/DazPose.Tests.csproj

# Read-only source scan; expand exact patterns after C00 discovers consumers.
rg -n 'FirstContactD1D5|Day2ActorValidation|AuthoringShowcase|StoryFoundationBuilder|FirstContact\.bindings|TestOutput' validation/DazPoseUnityValidation/Assets validation/DazPoseUnityValidation/ProjectSettings src tests scripts docs --glob '!*.unity' --glob '!*.meta'

# Also examine PlayerBuild catalog and assemblies.
rg -n 'SceneCatalog|scenePath|EditorBuildSettings|DazPose\.Validation|DazPose\.Examples' validation/DazPoseUnityValidation/Assets/DazPose
```

Unity Editor batch mode may run **imports/compilation and edit-only non-playing tests** if installed and safe. Do not invoke `-executeMethod` on unknown legacy migration handlers, `-buildTarget`, `-buildWindows64Player`, a method entering Play, or unattended scene playback. A new narrowly scoped Editor validation entry point is acceptable if it only inspects/compiles. Record the full command and exit code.

## Evidence requirements

- Baseline plus final `HEAD`, branch, all ticket commits, tool versions and host context.
- Exact byte hashes of frozen scenes and immutable releases; GUID/fileID and explicit serialized-field comparisons for legitimate editable asset/path changes.
- Asset/scene **move manifest**, **retirement manifest**, **blocked preservation list**.
- Dependency closure scans: serialized GUID refs plus code/config/import roots/review pointers and scene build/catalog membership.
- Import/reload hook audit and proof normal opens do not mutate protected data.
- Scene/card compiler validation and a new globally tagged card discovered by old scene without author edits. Preserve full source diagnostics.
- `dotnet test` and Unity EditMode/compilation logs with actual pass/fail/not-run status.
- A clear statement: **human Unity Play/visual acceptance pending**.

## Hard stop / rollback triggers for a specific change

- Loss of Lara/PlayerBody scripts, contact target, animator, or references; missing scene component.
- First Contact transcript/order, clipping, prepared equipment, session score, poses, original user marks, wireframe or funscript binding changed without direct authorization.
- Rebuild changes Void lighting/fog/materials or wardrobe draft/release layer choices.
- New per-scene card library declarations or additional scene-specific card registration.
- `.meta` GUID accidentally regenerated for retained moved asset.
- Automatic migration/import reload makes unexpected edits.
- Old project art/source files only exist in ignored data with no verified recovery copy.
- Pending toe-appearance fixture is removed before the user observes/approves it.
- Accidentally initiating gameplay, standalone build/player, or toy actuation.

Stop/rollback the offending ticket's precise edit; proceed with independent safe work where possible. If globally critical source/baseline is not recoverable, stop destructive execution altogether and report. Never silently use `git reset --hard` on unknown user changes.

## Status vocabulary

`PASS`: checked directly, with evidence and exact command/path.  
`FAIL`: a check ran and did not satisfy the expected result; distinguish baseline failures from regressions.  
`BLOCKED`: cannot safely perform; explain exact dependencies and what must change.  
`NOT RUN`: test/environment unavailable or prohibited; not equal to PASS.  
`HOLD`: intentionally retained protected/out-of-scope data, with owner and reason; not a failed deletion.  
`MANUAL PENDING`: explicit user-controlled Play/appearance approval remains.

No claim of full technical completion is allowed if a required gate is BLOCKED, FAIL or NOT RUN. Deliberate documented HOLDs do not prevent completion of the agreed safe scope; manual acceptance remains separately pending. The deliverable is a reliably maintained project with truthful residual inventory, not an artificially empty `TestData` folder.

## Reviewed check safety and evidence scope

The commands above are examples, not permission to reset a dirty checkout or bypass the user's testing boundary. On this machine Git may require a per-command `-c safe.directory=<verified-repository-path>`; do not change global Git configuration. For network/tool limitations report the actual result. A successful external C# compilation is not an actual Unity import or saved-scene check.

Inspect checks before invoking them:
- `StoryFoundationChecks.Run()` calls `StoryFoundationBuilder.Prepare()` and saves/rebuilds wiring.
- `StoryFullSessionChecks.SourceChecks()` calls setup plus foundation checks; it is not read-only despite its name and has an isolated-project guard.
- `StorySourceChecks` contains `StorySceneBuilder.BuildOrRefresh()` and scene mutations.
- `GamePlayerBuild.BuildStartupChecks()` performs actual builds; `Build()` is not a scene-list query.
- Test reload observers can resume from SessionState; C01 must isolate them before reload checks.

Do not run these existing mutating checks against the user's authored project as preservation gates. Extract read-only validation or exercise mutation behavior on disposable fixtures/an isolated source copy, with no Play, builds or launches even there. Inspect final delivered scene data and references separately; never claim isolated-copy results establish actual delivered-scene playback. Do not start a second Unity process on the same open project. Use only inspected EditMode tests that never enter Play, actuate hardware, publish releases or change the user's profiles.

Keep root config, linked test sources, ignored import recipes/reviews, scripts and historical folder moves in the path scan; the sample rg command is not exhaustive and omits ignored data. Read-only raw scans supplement, not replace, Unity resolution. If the dependency database is unavailable, mark the associated move/deletion verification NOT RUN and defer that operation.

For G1–G7, capture fixture hashes before/after, asserted outcomes and cleanup. Restore only temporary test additions; never remove a user's newly authored card as teardown. Compare both the application's public SceneCatalog and the generated global-card index—the former stays unchanged during G1, while the latter must update. Catalog membership checks do not prove player packaging; inspect the runtime reference closure without calling BuildPipeline, and leave actual player validation NOT RUN.

Frozen scene and release hashes must match exactly. For editable assets record allowed field/path changes, original and final GUID/fileID references, scene/actor IDs and prefab overrides. Record actual technical results, user visual approval and source-inspection findings as three distinct evidence types.
