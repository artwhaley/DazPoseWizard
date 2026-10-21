# G2 failed native run and correction

**Native status: FAIL reported by Art. Repair source ready for retry; no native PASS claimed. N05 remains held.**

## Observed evidence

Art reported that Save left the shot at baseline, moving stepforward stopped posing, and rotating camera_start did not change the shot. Restarting the diagnostic after a save sampled the pose at the newly edited mark. Stop/reopen acceptance could not be checked. No screenshots requested.

Read-only inspection of the existing project-relative Unity log found:

- One `Clean forced save changed serialized scene bytes` exception from the diagnostic's forced-save check. The exception flowed through Guard → Stop, so the promised automatic resume was explicitly cancelled by our diagnostic.
- Five `Another PlayableGraph owns Lara's Animator` exceptions from NativePreviewLease.Sample called by the live mark watcher. The old adapter destroyed/recreated its graph on each projection, then interpreted the Animator's still-bound native flag as foreign ownership.
- Five subsequent clean-baseline Start refusals, consistent with the workaround Art described. These are consequences, not evidence that the authored marks were invalid.

G2_FAILURE_EVIDENCE.json preserves the relevant messages/stacks and the pre-sync saved fixture scene hash. G2_FAILURE_FIXTURE_SCENE_DIFF.txt shows the actual saved deltas: Art's stepforward translation and camera_start rotation/Euler hint, Unity's empty stopBeforePhase string normalization, and a redundant Lara `m_UpdateWhenOffscreen: 0` override. No agent Unity launch or operation was used to collect this evidence.

## Changes

1. **Sampling lifetime:** NativePoseSampler owns one manual graph through the entire preview lease. PreviewResourceOwner retains it across handle reprojections and save suspension. Baseline restoration stops owned AnimationMode first, detaches/pauses the output, then restores the per-property ledger. The graph is rebound on the next sample and destroyed once at final teardown. The foreign Animator graph check occurs at initial acquisition only; reusing our own graph never repeats that check. Clip changes reconnect a clip in the same graph.
2. **Save resume:** the window waits while the lease is save-suspended or Unity is importing; it no longer attempts a context acquisition that can throw during transient refresh. The lease resumes the same projection after save. Internal restore/save callbacks cannot be mistaken for author edits by postprocessModifications.
3. **Serialization proof:** raw byte equality is still recorded, but Unity's narrow empty-string spelling and CRLF/BOM normalization is compared separately. All other differences—including a redundant preview-created prefab override, actor transforms, materials, references or actual string data—remain failures. Existing snapshot/prefab checks remain mandatory. A semantic mismatch still stops the proof; cosmetic normalization alone does not.
4. **Renderer write:** removed the standing adapter's updateWhenOffscreen assignment, which was unnecessary to the visible endpoint and introduced a redundant saved override. The previous fixture override is left intact as existing fixture data; no offline scene/prefab cleanup or author edit was overwritten.
5. **Camera:** v1 always restarted with Track(Lara), which correctly supersedes Camera.StartAt orientation but made the rotation test misleading. v2 starts with **Use camera_start rotation** and applies its full quaternion. **Track Lara — overrides mark rotation** is a separate, explicitly labelled orientation choice. Runtime tracking/smoothing behavior is unchanged. Previous camera-mark edits are preserved; if that saved orientation faces away from Lara, it now visibly faces away rather than being silently overwritten by tracking.
6. **Repeated Start:** clicking Start while already running reprojects through the existing lease, preserving the baseline and avoiding a same-event graph ownership handoff. Ordinary dragging and Save require no Start click.

The panel header is **G2 v2 — standing and normal save**. It displays live/suspended state. The normal Scene/Game views remain the only viewport.

## Regressions and limits

Eleven new pure managed checks link the production resource owner, save comparator and camera endpoint helper. They cover 100 reprojections with a deliberately stale native-bound flag; normal/forced save suspend/resume without restart; initial foreign ownership, final disposal, empty-string normalization versus real prefab/transform/material/reference/string changes; and exact full camera-mark rotation versus explicit tracking. These checks model the ownership seam; they do not execute Unity native graphs or prove rendering/save ordering.

G2_REPAIR_MANAGED_RESULTS.json and logs record the full managed suite. G2_REPAIR_SOURCE_COMPILE.json records standalone Roslyn compilation against installed references. Original protection and static asset audit are recorded independently. Current code/metas are synchronized into the same isolated project; its saved mark/angle edits remain byte-identical across sync. N05 starts only after Art's G2 PASS. No push.

Repair validation: **223 managed tests PASS** (90 Story, 24 Player, 40 Rehearsal, 69 Application), zero failures/skips; seven source assemblies compile; static asset/meta audit PASS with zero issues; all 7,211 protected originals unchanged. These are source/managed checks, not native Unity acceptance.
