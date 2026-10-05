# E7: authored recall, effects and final delivery

Implement SceneWardrobeSnapshot/SceneWardrobeBinding integration without creating
a general scene director. Capture resolved outfit/variant, layer ceiling and hair.
Prepare all actor bindings before revealing any actor; a failed binding keeps the
previous visible state or the scene concealed with an explicit error. Validate
recall after an unrelated outfit/hair change and direct partially dressed recall
without a fully dressed flash. Test two bound performers independently.

Use an authored fixture following `FirstContactPerformance`/RunSequence conventions
to call Outfit, TryRemoveLayer, TryAddLayer and CurrentWardrobe. Preserve existing
accepted performance scenes. Implement Dissolve transitions through the existing
effect lifecycle: acquire its transition ownership, dissolve out, commit validated
bindings while concealed, dissolve in, settle once. Serialize contention with an
already active effect; never overwrite particle buffers/profile during active
ownership. Use existing lifecycle hooks after inspecting them. Preserve expression,
speech, breathing, gaze and walk requests across switches and effect recovery.

Run at least 100 outfit/layer cycles, including invalid requests, queued relative
commands and disable/re-enable. Verify object/material/buffer inventory reaches a
bounded cache size and returns to its baseline on disposal. Render representative
walks and dissolve recovery for each release and layered footwear fixtures.

Build a Windows player with a fixture that includes catalog assets directly and
executes the authoring calls/reports results. Runtime must not depend on editor
AssetDatabase, source FBX/DUF or source filesystem paths. Missing build support is
an evidenced external blocker, not a passed player test. See VALIDATION for reports.

After all gates pass, implement/use `scripts/publish-wardrobe-runtime.ps1` to copy
only generated wardrobe/catalog data, matching metas and the new setup scene from
isolation into the main project. Preserve existing main configuration edits; fail
with an exact conflict instead of overwriting divergent authored data. Copy the
setup scene only on its first creation; later publications refresh data. Never
copy Library, Temp, source models, captures, old review scenes or whole projects.
Main source edits already belong in the main repository. Refresh/import the main
editor normally; no competing batch invocation on its project.

Update import QuickStart/Playbook/Status and runtime vocabulary documentation to
describe the implemented paths and known limits. Keep the independent clean-context
Luna import trial explicitly unpassed until actually rerun without rescue. Update
progress with final reports, setup path, UI instructions and remaining human review.
Open the setup scene for manual review only through an interactive signed-in Unity
session following root AGENTS.md. Do not describe a batch process as a visible
editor. Deliver a concrete preview and request artistic review; do not mark it
approved or replace the accepted performance scene before that review.
