# Day 2 report — reusable actors, explicit bindings and persisted defaults

Status: the user reports validation passes. The generic test lighting was explicitly rejected; the authored First Performance Void is now the required environment for future tests. Environment correction evidence is recorded below. No commit or push was made.

## Delivered

- `validation/DazPoseUnityValidation/Assets/Production/Performers/Lara.prefab`: accepted Generic Lara rig, existing playable graph, dedicated head speech source/SALSA, locomotion and teleport profiles, visibility rig, released wardrobe binding and calibrated right-hand controller. The performer is on the prefab root. Its clock and particle-body component belong to that instance. It starts visible, unclothed, with playback and contact idle. No smoke panel, acceptance harness, installer or scene-name branch initializes it.
- `Assets/Production/Scenes/Day2ActorValidation.unity`: two prefab instances, a named bedside mark and ordinary scene bindings composed into the saved authored First Performance Void environment. Its glowing floor, perimeter smoke, fog, environment lights, camera and volumes are retained. The old demonstration drivers are removed; no validation observer is required.
- `PerformanceSceneBindings`: typed actor/player/clock/device/contact references, stable named targets, declared content assets/seats and capability-specific preflight. Missing references identify their owner and serialized property. Unused movement/contact/device requirements do not block ordinary speech. Seating checks actual seat anchors/profile; visibility checks particle renderer ownership. Preflight is read-only and does not launch playback.
- Explicit player/device playback references replace the production global searches. The binding inspector can persist declared references with Undo. The optional **DazPose → Production → Bind player and toy references in selected scope** migration searches only the selected hierarchy, refuses ambiguity/conflicts before writing, and preserves explicit references.
- Grouped performer and PlayerView inspectors use the existing serialized fields. Initial-state configuration, expression intensity, motion blend and view defaults have script access; existing life/gaze/blink properties remain their instance controls. Contact edits its existing owner rather than adding duplicate facade fields. See [settings migration](DAY_2_SETTINGS_MIGRATION.md) for every inventory row and persistence policy.
- **Apply supported tuning to saved defaults** transfers only supported performer/contact values after Play Mode exits, with Undo and prefab instance overrides. The pre-Play EntityId-to-GlobalObjectId mapping identifies the saved instance across domain reload. Play-created actors/controllers are rejected. Rig references, calibration, scene placement, cameras and wardrobe drafts are excluded. This applies to the scene instance; Unity's ordinary prefab Apply remains the deliberate way to promote its overrides to the prefab asset.

The new wardrobe release is `70b0e228678e8bcd56e967d03d5bef45f63861ce4dabe6dd261afb48e14bbceb`. The builder cloned the current catalog/configuration/preset inputs and published a distinct generation. Original drafts, accepted imported meshes/materials, fit data and grip calibration were not edited. Generated release snapshots use the existing publisher's `Assets/Wardrobe/Releases` and `Assets/TestData/WardrobeRuntime/Generations` paths; their final location/boundary belongs to later consolidation work.

## Validation and evidence

All Unity work ran sequentially in `.dazposewizard/production-consolidation`, a fresh copy of main Assets/Packages/ProjectSettings. Day 1's disposable project was preserved. No computer control was used. Human observations are separate from technical results.

| Check | Result | Evidence under `.dazposewizard/day2-evidence/` |
| --- | --- | --- |
| Fresh scene, commands, two-actor isolation, supported default Apply, transient rejection, Undo/Redo, prefab override, save/reopen | 40 checks passed | `validation-final.json`, `runtime-final.json`, `root-validation.log` |
| Teleport/seating declarations, exact missing seat anchor, empty expression, wrong actor particle renderer, required services | 7 checks passed | `preflight-final.json`, `root-preflight-build-final.log` |
| Windows x64 Development player | Succeeded; Mono2x, 0 errors, 9 shader warnings | `build-final.json`, `root-preflight-build-final.log` |
| Graphics-backed standalone commands | Exit 0; pose/expression Settled, speech Finished, walk Arrived, outfit Applied; independent clocks/wardrobe/visibility and idle calibrated contact | `player-final.json`, `player-launch.json`, `root-player-final.log` |
| Accepted inputs and metadata | 3,341 original non-C# inputs byte-identical | `accepted-input-preservation.json` |
| Export closure | All Assets dependencies present in main and byte-identical to validated staging; package dependencies use unchanged package configuration | `dependencies-final.json`, `dependency-export-check.json`, `dependency-asset-hashes.json` |
| New exports/helper identities | Recorded with SHA256 | `export-manifest.json`, `evidence-ledger.json` |
| Human validation | User reports validation passes; generic lighting explicitly rejected | User message, 2026-10-05; authored environment correction below |

The command probe uses an existing voice clip and a deliberately small facial-bone expression fixture. It exercises actual public commands and runtime owners, not merely reflected method presence. Both actors initialize from serialized prefab data. The probe independently changes A's position, visibility, wardrobe and clock; B stays unchanged. The editor test changes A's supported tuning deliberately and B's temporarily, then proves A's Apply, B's discard, Undo/Redo and saved-scene reopen. The observer is present only in the disposable probe scene/build, not the production prefab or fresh scene.

Early iterations retained in the evidence folder caught fixture assumptions (expression/audio assets), an empty initial scene restoration case and the Play Mode identity mismatch. Those failures were repaired and rerun. A Unity Search index startup exception appeared in an editor run; it is outside the product/test stack. The standalone log has no corresponding exception. The final build records 9 shader warnings in existing HDRP/DAZ dissolve shaders; it reports no build errors. Runtime rendering flags and command completion do not substitute for visual approval.

## Completed manual checks (commands retained for reference)

### 1. Appearance and audible lip sync

Run this in your normal Windows PowerShell. It launches the newly built Unity player directly; no Avalonia publish is involved.

```powershell
$app = 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\production-consolidation\TestOutput\day2\player\Day2Probe.exe'
$log = 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\day2-evidence\player-manual.log'
Start-Process -FilePath $app -WorkingDirectory (Split-Path $app) -ArgumentList @('-screen-width', '1280', '-screen-height', '720', '-logFile', ('"' + $log + '"'))
```

Expected: two visible Laras; A speaks with audible lip sync, walks toward the mark, disappears/reappears and changes to first outfit. B remains visible and unclothed. The player holds the final view for 20 seconds, then closes. Confirm sensible appearance/lighting, movement/effects, clothing and no shared visibility change. The report beside the executable is `day2-player.json`; report any mismatch rather than treating a successful JSON as visual approval.

### 2. Inspector usability

Open the main project with this command after the standalone run finishes:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe'
$project = 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard\validation\DazPoseUnityValidation'
Start-Process -FilePath $unity -ArgumentList @('-projectPath', ('"' + $project + '"'))
```

Open `Assets/Production/Scenes/Day2ActorValidation.unity`, select **Performer A**, and inspect the grouped `SuccubusPerformer` fields and `PerformanceSceneBindings` readiness. Confirm the layout is usable and the expected controls are present. Play Mode tuning is temporary unless you click **Apply supported tuning to saved defaults**, then exit Play Mode. Save only deliberate tuning changes. PlayerView's separate inspector groups rig bindings and view defaults. Automated validation already covers persistence/Undo; this check is for the actual editing experience.

## Handoff and boundaries

- Instantiate the entire Lara prefab; the root directly exposes `SuccubusPerformer`. Scene bindings can replace its clock explicitly **before enabling** when a sequence intentionally shares playback. Defaults leave the local clock idle. Commands do not require the migration/builder menus.
- Bind scene marks and declare only the capabilities/assets/services the sequence needs. Required contact correctly reports the missing runtime `IGripTarget`; the partner adapter remains Day 4. A calibrated hand graph is not proof of partner integration.
- Existing legacy scenes are not rewritten. Player/device callers that previously relied on global searches must assign their explicit references, use a scene-binding wire action or run the scoped migration on an unambiguous hierarchy. No hardware connection/output was exercised by these tests.
- Shared profiles/assets are read-only during ordinary playback. Setup-only rig/profile references stay in inspectors; per-command options and instance properties are the script path for behavior. The new defaults Apply excludes shared asset editing.
- The new release includes all three accepted outfit inputs and their snapshots, so the diagnostic build is larger than Day 1's one-actor packaging fixture. This is not a measured performance regression; fit snapshot duplication and production asset placement remain appropriate Day 5/6 review targets.
- Day 3 still owns command cancellation/reset/replay and shared-clock lifetime. Day 4 owns partner contact. Day 5 owns the wardrobe data/UI redesign. Day 6 owns assembly/dependency boundaries and integration. Day 7 owns playable authored content and performance gates.
- The reusable assets still depend on the accepted local character/import data already present in this project; this work does not turn licensed/generated content into a fresh-checkout distribution.

The user reported validation passes. Do not require a repeat of those checks merely because the lighting correction reuses existing authored settings. The corrected composition has not received a new visual observation; original authored settings are the authority. Proceed to Day 3 only when requested. Independent import acceptance remains a separate task.



## Authored environment correction and completion

The user reports validation passes, but explicitly rejects the generic lighting. Day 2 is complete after restoring the authored environment. This direction is recorded in root AGENTS.md and the authoritative plan for all future performance tests.

`ProductionTestEnvironmentBuilder.Compose` opens the saved `Assets/Scenes/FirstPerformanceVoid.unity` and writes the separate production validation scene. It retains the glowing floor, perimeter rising smoke, fog banks, furniture, environment lights, authored camera/view hierarchy and global volume. It removes the old demonstration sequence, external playback/device roots and original demo actor. Production instances supply their own clocks and body effects. `ProductionPerformerBuilder` now uses this composition instead of creating a generic camera/directional light. It never regenerates the authored environment from builder defaults.

The prefab already owned `Character Visibility Lights`; both lights and their hierarchy/settings exactly match the authored scene, including their Lara light-layer restriction. No prefab lighting change was needed. The original environment scene, accepted Lara candidate scene and production prefab remained byte-identical. The corrected composition has no new user visual observation; preserved authored settings are the authority, and the generic lighting rejection is not relabeled as approval.

Correction evidence under `.dazposewizard/day2-evidence/`:

- `environment-correction.json`: seven checks; 99 environment records preserved, camera/view and volume settings unchanged, authored light ownership verified, original scene unchanged and two production actors only.
- `environment-validation.json` / `environment-runtime.json`: all 40 editor checks pass again in the restored environment, including commands, actor isolation and default persistence/Undo.
- `environment-preflight.json`: all seven targeted capability checks pass.
- `environment-build.json`: rebuilt Windows player succeeds with zero errors and nine existing shader warnings.
- `environment-player.json` / `environment-player.log`: all 16 standalone checks pass, exit zero, Direct3D 12.
- `environment-export-check.json`: 436 Assets dependencies match main and staging; no differences, 27 package paths.
- `environment-input-preservation.json`, `environment-evidence-ledger.json` and `manual-completion.json`: preservation, correction output hashes and user observations.

The PowerShell launch command above now opens the rebuilt player with the authored environment. Its camera settings are deliberately retained; the two-actor independence check does not require changing the authored framing. Earlier `*-final` reports describe the initial generic scene; the `environment-*` evidence supersedes them for the corrected build. Their historical evidence is retained. Day 3 remains unstarted.

User resolution: accepts the identified runner omission. The delivered scene initializes both actors but has no demonstration caller; the prior player used Day2Probe.Start in a separate generated scene. Future test scenes must contain their saved runner so Play starts the test. Day 2 is accepted with that known omission; this is not a claim the missing runner was repaired.
