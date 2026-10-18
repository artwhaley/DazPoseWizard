# Clearing rejected expression imports

Use **DazPose > Content > Archive Failed Expression Imports** in Unity Edit Mode.
This archives failed, unchanged expression import copies, their Unity metas and
their failure-status records outside the watched import roots. Original DAZ
presets and generated animation/wrapper assets are untouched. Changed inputs are
left alone, since their previous failure no longer describes the current data.

Recovery copies are under the Unity project's
.dazposewizard/archived-expression-imports/ directory. Each archive preserves the
original relative paths and includes manifest.tsv with SHA-256 hashes. Restore
only the files you want at those original project-relative paths; retain their
metas to preserve GUIDs. Correct missing morphs or unsuitable content before retrying.

Then click **Clear** in Unity's Console once to remove messages already recorded.
Archiving stops processing; clearing the Console only removes old display entries.

Automatic script/reference-model refresh does not force failed imports to retry.
A changed canonical input or **DazPose > Content > Reconcile Pending Pose Imports**
can retry failures still in the watched folders. Archived imports stay archived.

## October 17 cleanup

Archived eleven failed expression imports (eight lacking required Lara morphs and
three earlier body-only presets), comprising 33 input/meta/status files.
All archived bytes and metas were verified unchanged; zero failed expression jobs
remain in the watched inputs/status records. Recovery archive:

C:/Users/artwh/OneDrive/Documents/DazPoseWizard/validation/DazPoseUnityValidation/.dazposewizard/archived-expression-imports/20261018T011858766Z-464f73d745ac432c87f220387192a915

The archive helper passed nine managed safety checks, including path containment,
full input validation and rollback after a locked-file failure. Affected Editor
assemblies compile with warnings as errors. No Unity Play or standalone build/launch
was performed. The existing Console's old messages have not been cleared by the agent.
