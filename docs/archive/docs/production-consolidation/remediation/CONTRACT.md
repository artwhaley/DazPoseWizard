# Remediation contract — historical execution packet

The completed R01–R13 tickets used the execution/evidence procedures below. For new work, the October 7 user revision in [the root build plan](../../../root/AUTHORING_READINESS_BUILD_PLAN.md) is authoritative. The current [command contract](../COMMAND_CONTRACT.md) replaces conflicting API policies. Do not apply this historical isolated-runner procedure as a new authoring acceptance gate.

## Paths and scope

Repository: `C:\Users\artwh\OneDrive\Documents\DazPoseWizard`.
Main project: repository + `validation\DazPoseUnityValidation`.
Isolated project: repository + `.dazposewizard\remediation-validation`.
Evidence: repository + `.dazposewizard\remediation-evidence\Rxx\<UTC-run-id>`.
Unity: `C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe`.

Main source may be edited inside each ticket's allowed seams. Execute Unity only in the isolated project. Never kill an unrelated Unity process; if the isolated project has an owner, wait or stop with its identity recorded. Do not use computer control. Author-generated staging assets are exported only in R13 after hash comparison; do not copy a whole changed staging Assets tree into main.

Preserve original Void environment, Lara prefab artistic settings, source imports/materials, script meta GUIDs, current user layer assignments and immutable releases. Character-specific Lara conventions remain supported. No generic rig/controller work, new content interpreter/timeline, squeeze/compliance, other contact geometry, automatic pruning or broad rename/relocation.

## Evidence and runner contract

R01 creates `scripts/run-remediation-ticket.ps1 -Ticket Rxx`, with R01–R13 as its only allowed ticket IDs. Entry point: `DazPose.Remediation.RemediationValidation.RunBatch`, CLI argument `-remediationTicket Rxx`. This runner does not exist at packet creation; implement it in R01 rather than claiming it already ran.

Use unique report directories. Each report records ticket ID, source hash, current input-manifest hash, start/end UTC, assertion names/expected/actual/pass, exception and process exit. Exit 0 requires all positive assertions; R02 instead requires an explicitly recorded failing product witness plus passing attribution checks. An external watchdog terminates only the process it launched after 300 seconds for checks, 1,200 seconds for builds. Player checks use 180 seconds. Do not read stale reports after a failed launch.

CLI source hash includes `.cs`, `.cs.meta`, `.asmdef`, `.asmref`, their metas and package/config files. Input hashes cover scene/prefab/catalog/config/preset and recursively used fit/body/surface dependencies. Editor Play Mode/player checks use real graphics; headless import or unit checks may use `-nographics`. No report says appearance passed without a user observation.

Every ticket writes `docs/production-consolidation/remediation/reports/Rxx.md`: changed paths, measured outcome, executed commands/report paths, failures and exact remaining work. Do not put generated binary logs/assets in Git. New Unity scripts get meta files; moved scripts keep their original metas.

## Current author API decisions — October 7 revision

The root [API guide](../../../root/CONTENT_AUTHORING_API.md) distinguishes implemented
methods from planned replacements. Preserve existing typed actor/player verbs and
one execution kernel; build the common saved host described in B04.

- Begin uses basic explicit references for the known Lara setup, not a generalized
  capability/content preflight. No toy requirement can prevent playback.
- Toys is always callable; output is fire and forget. All toy errors are silent
  successful no-ops to the application. No backend acknowledgement waits or
  output-result failure exceptions. Intiface owns device troubleshooting.
- Explicit session Stop cancels all local accepted/queued work, including the
  failing operation, ramps, followers and pending toy dispatch. It issues a
  best-effort hardware stop without waiting for a result.
- Non-toy finite commands retain useful typed results/errors. Each Awaitable is
  consumed once. Existing graph/channel ownership and speech/wardrobe queues stay.
- Pose without an explicit transition uses the actor's configured smooth default.
- Tuning changes are persistent, never restored by cleanup. Replace temporary
  OverrideTuning behavior with direct SetTuning. Stop/Start do not ResetPerformance.
- Application owns connection/settings and scene navigation; scene owns actor,
  player view, clock and sequence. Presentation invokes these services.
- Read-only author state is exposed through scoped properties. Retire competing
  scope patterns from production authoring; do not duplicate execution kernels.

The fixed assembly layout below records remediation extraction. The new plan adds
Presentation and Content assemblies referencing Runtime without introducing a
Runtime dependency on either, Editor or Validation.

## Fixed assembly layout

- `DazPose.Runtime`: asmdef at `Assets/DazPose/Runtime/`; all actual runtime there plus `Assets/Player/Runtime/` through an asmref.
- `DazPose.Editor`: asmdef at `Assets/DazPose/Editor/`, Editor-only; Player/Editor and FirstPerformanceVoid/Editor use asmrefs to it. References Runtime and Validation; external package references follow R10's exact type-import manifest.
- `DazPose.Validation`: asmdef at `Assets/DazPose/Validation/`; references Runtime, compiled in Editor or when `DAZPOSE_VALIDATION` is defined for an explicit validation player. No production reference to it.
- `DazPose.Validation.Editor`: nested Editor-only asmdef at `Assets/DazPose/Validation/Editor/`; references Runtime, Validation and Editor. Editor must never reference Validation.Editor. Shared editor utilities remain Editor; validation menu handlers/runners move to Validation.Editor.
- `DazPose.Examples`: asmdef at `Assets/FirstPerformanceVoid/Runtime/`; references Runtime only. Existing FirstContactPerformance moves here with its meta. Existing FirstPerformanceVoidControls moves to Validation/Legacy with its meta.

Use Unity GUID references between first-party asmdefs/asmrefs, generated once, recorded in R10's manifest. Do not change class/namespace names merely to remove the word Validation; actual production dependencies determine placement. `GripContactRod` remains a production-compatible generic analytic target. `LaraHeelReview`, `LaraWardrobe`, `LaraAnatomyControls` remain production.

Precompiled SALSA/Buttplug DLLs stay automatically referenced (`overrideReferences=false`). Retain verified plugin/platform settings. Add only imported package assemblies, identified from local package asmdefs in R10; no speculative reference list. Runtime may contain correctly guarded UnityEditor-only asset validation, but cannot reference first-party editor helpers. Use named friend assemblies for internal Editor/Validation/Validation.Editor access, not broad public test APIs.

Assembly extraction is one coherent graph transaction as specified in R11; compile after all mutually dependent definitions/moves are present. No unsupported shortcut of moving all test code into Editor, disabling required tests, or leaving a runtime dependency in Assembly-CSharp.
