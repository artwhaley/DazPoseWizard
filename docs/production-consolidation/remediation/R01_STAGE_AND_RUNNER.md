# R01 — stage current inputs and establish execution

Depends on baseline. Allowed changes: shared remediation runner script, validation-only runner/report helpers, packet progress/reports. No product behavior edits.

1. Read START and CONTRACT. Verify `git cat-file -t 18b268afe4df1ce11046a760b757002571a104c6` returns commit. Hash the local recovery zip and compare START; verify manifest `passed=true`. Do not recreate that commit/archive.
2. Capture current main Assets/Packages/ProjectSettings hashes and script GUID map. Record any differences since baseline; these are new user changes to preserve, not permission to revert them. Inspect second-outfit assignments: stockings/shells/shoes Layer 1, other parts Base. Keep all other current outfit edits.
3. Create `.dazposewizard/remediation-validation` by copying only current main Assets/Packages/ProjectSettings, excluding Library/Temp/build/log directories. Reuse only if its input manifest matches current main or synchronize through explicit differences. Capture all source/meta and protected input hashes before copying. Never synchronize older diagnostic drafts back into main.
4. Implement the PowerShell wrapper and `RemediationValidation.RunBatch` specified in CONTRACT. Begin with R01's import/GUID/preservation assertions; dispatch later ticket handlers explicitly. Unsupported/unimplemented IDs fail, rather than returning a vacuous pass. Launch Unity hidden with the signed-in user's license when sandbox licensing fails; use escalation only for that execution. No main-project Unity batch run.
5. Add report schema, watchdog, source/input provenance, one-process ownership guard, and hash-based changed-source sync. Sync new asmdef/asmref files and deletions/moves from a manifest; do not leave duplicate old scripts. Generated fit/scene changes remain in staging.
6. Add a validation helper that enumerates asset GUIDs/dependency paths without generating/recompiling fits. Ensure Windows long paths work. Source/hash capture excludes Library and generated test output.

Run from repository root:

```powershell
& 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard\scripts\run-remediation-ticket.ps1' -Ticket R01
```

Gate: fresh isolated import compiles; actor/scene/preset GUIDs resolve; protected/main assets are unchanged; report provenance is current; a deliberately unsupported ticket ID exits nonzero. Save report at evidence R01. Snapshot identity and user-input observations must be linked in the report. R01 does not certify runtime or toe behavior.

Stop: missing licensed/generated input, hash changing during snapshot, occupied isolated project, or compile failure. Name the exact path/error; do not fabricate replacement input or kill another process. Fix runner-only compile mistakes here; product errors belong to their numbered ticket.
