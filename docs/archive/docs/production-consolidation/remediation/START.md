> **Historical completed stack:** R01–R13 technical evidence remains valid as recorded. The user's October 7 authoring-readiness revision is now driven by [the root build plan](../../../root/AUTHORING_READINESS_BUILD_PLAN.md), starting with B01. Do not reopen this numbered stack or apply conflicting old API/validation requirements to the new sprint. The pending toe appearance observation remains an observation, not evidence of approval.

# Mechanical remediation ticket stack

Read this file, `CONTRACT.md`, `SOURCE_INDEX.md`, `progress.json`, then the first unfinished numbered ticket. Execute R01–R13 in numerical order. Do not start the all-up authored scene in this stack. These tickets implement the user-approved remediation scope; the prose plan is rationale, not a competing assignment.

## Recovery baseline already created

- Local baseline commit: `18b268afe4df1ce11046a760b757002571a104c6`.
- Commit subject: `Checkpoint accepted consolidation days 1-5 before remediation`.
- Includes current tracked/untracked nonignored Days 1–5 work, documents, production scenes/prefabs and the user's current saved layer edits.
- Git intentionally excludes many generated/licensed Unity inputs. Their verified local snapshot is `C:\Users\artwh\OneDrive\Documents\DazPoseWizard\.dazposewizard\recovery\18b268afe4df1ce11046a760b757002571a104c6\unity-inputs.zip`.
- Snapshot SHA256: `eb3f7b480bd7a6e6fccce7b8927ec00ed8d54806a82b9fdbb1125e937a2575b7`. Its manifest is beside the archive. It contains 7,979 files from Assets, Packages and ProjectSettings; CRC validation passed. Library, Temp, builds and evidence are excluded. Preserve the archive.
- The baseline and snapshot are complete. Do not create a replacement baseline that incorporates partially repaired source.

| Ticket | Action | Depends on |
| --- | --- | --- |
| [R01](R01_STAGE_AND_RUNNER.md) | Current-input staging, preservation and shared test runner | Baseline |
| [R02](R02_TOE_REPRODUCTION.md) | Reproduce and attribute second-outfit foot residuals | R01 |
| [R03](R03_TOE_REPAIR.md) | Repair the measured cause and footwear ownership | R02 |
| [R04](R04_CONTACT_WARDROBE_PREFLIGHT.md) | Contact and wardrobe readiness | R03 |
| [R05](R05_CONTENT_PREFLIGHT_AND_ERRORS.md) | Content declarations and production diagnostics | R04 |
| [R06](R06_SCOPED_FINITE_COMMANDS.md) | Exact finite-call adapter and failure handling | R05 |
| [R07](R07_PERSISTENT_VIEW_PLAYBACK_TOYS.md) | Persistent ownership, view, playback and toys | R06 |
| [R08](R08_FACADE_COVERAGE_FIRST_CONTACT.md) | Complete surface and migrate existing First Contact | R07 |
| [R09](R09_CONTENT_LOCATION.md) | Remove gesture/action folder-prefix coupling | R08 |
| [R10](R10_SOURCE_MOVE_MANIFEST.md) | Classify every source and generate exact move manifest | R09 |
| [R11](R11_ASSEMBLY_BOUNDARIES.md) | Apply specified production/editor/validation boundary | R10 |
| [R12](R12_BUILD_AND_REGRESSION_GATES.md) | Current regression gates and both player boundaries | R11 |
| [R13](R13_DELIVERY_AND_HANDOFF.md) | Export, manual foot check and author handoff | R12 |

R02 is successful when it reproduces the known failure and records its cause; it is not a fixed-product pass. Mark R03 complete only after repaired behavior executes. Every other implementation ticket requires its stated executed checks, not just code inspection.

Evidence-driven branches in R02/R03 are fixed repair rules, not deferred product decisions. If evidence contradicts the specified cause branches, record the exact new cause; do not invent a repair or weaken the gate. Preserve manual appearance-dependent issues for the final handoff; execute independent technical tickets while that observation is pending.

No concurrent editing of the same files. Root owns design/ownership/assembly work. A bounded Luna task may execute one ticket's explicit subset; it must return its diff, evidence and deviations. It must not spawn further agents or expand scope. No agent is authorized to force-push, reset the repository, overwrite the recovery snapshot, retune lighting/calibration, or substitute another-character work.

Update this folder's `progress.json` and shared consolidation progress at each handoff. Source/technical pass, human appearance approval and unrelated independent import acceptance are distinct statuses.

## Recovery without erasing new work

Do not run `git reset --hard` or recursively delete the checkout. A recovery operator first records `git status`, saves the new diff/untracked files, and uses a separate checkout or targeted `git restore --source=18b268afe4df1ce11046a760b757002571a104c6 -- <explicit reviewed paths>`. Restoring main files is a deliberate recovery action, not part of ordinary ticket execution. Extract the local snapshot into a separate directory and compare hashes before restoring ignored inputs. Never bulk-extract it over the live project.

Start commands in Windows PowerShell:

```powershell
Set-Location -LiteralPath 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard'
git status --short
Get-Content -LiteralPath 'C:\Users\artwh\OneDrive\Documents\DazPoseWizard\docs\production-consolidation\remediation\progress.json'
```
