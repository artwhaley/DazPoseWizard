# Main Unity project authoring handoff

Art reported the requested practical visual-authoring check worked on October 19 and asked about moving to real FirstContact tuning.

## Code is already in production source

Main Unity project: `C:/Users/artwh/OneDrive/Documents/DazPoseWizard/validation/DazPoseUnityValidation`.

The implementation was authored here on branch main (07fbeea; evidence f824e28). The independent G1Project received a verified code-only copy. Nothing needs copying back for the Studio to work in the main project. The actual Studio has no fixture-identity requirement; only the temporary diagnostic windows do.

Open `Assets/Production/Scenes/FirstContact.unity` and `DazPose > Story > Scene Director Studio`. Exact author directions: `docs/story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md`.

## Fixture content stays separate

Read-only comparison at handoff found:

- Fixture `stepforward` local position is (-0.945, 0, -2.041); main retains (0.371647, 0, -2.9785175).
- Fixture `camera_start` rotation differs from main.
- Fixture chair/prefab transform overrides differ, and there is an additional renderer `m_UpdateWhenOffscreen` override. These are not a code migration.
- Root and engineering-card binding assignment text matches main apart from fixture `ownerSourceGuid` backfill. No different saved pose assignment was identified in those two companions.
- Empty-string scene serialization normalization also differs.

No scene, mark, prefab, binding, art or index was copied back. Main retains the protected original content. Keep the independent fixture available as a record. If any fixture staging edits become desired artistic choices, transfer specifically selected fields after review; do not overwrite the production scene wholesale.

## Start content work

Direct one pass through Intro first: initial camera/arrival, stepforward/SexyVamp, dialogue framing, standing Lounger arrival and LeaningForward. Adjust marks and pose bindings from the selected cue's adaptive inspector. Then continue Warmup seating and subsequent phases. Record each rough spot by exact cue, action and expected/observed result.

Art operates Unity, native transforms, Save/Undo and later Play. The executor handles source/script/tool fixes from those observations; no launch or operation is implied. Preserve the exact fixed dialogue unless Art explicitly changes it. Keep authoring endpoints distinct from timed Play observations (locomotion/dissolve/speech/contact/motion).

Practical workflow is user-accepted. Extended native safety/lifecycle edge cases have not all been individually reported; do not claim blanket certification or final artistic/runtime approval.
