# R09 — prepared clips independent of folder prefix

Depends R08. Allowed seams: PerformerGesture.IsReady and PerformerAction.IsReady, their readiness tests. No new asset schema/migration or baking pipeline changes.

Fixed decision: remove ONLY the `StartsWith(Assets/DazPose/Generated/...)` requirement. Keep a standalone project-owned `.anim`, not an FBX subasset; retain nonlegacy Generic preparation, finite skeletal curves, nonlooping/reference-pose rules, no object-reference curves/events, and all action trajectory metadata. Existing content already stores what these validators need; do not invent a preparation hash registry.

1. Delete folder-prefix branches and amend messages to describe compatible baked clips without a generated-folder requirement. Preserve existing EditorCurveBinding validation under UNITY_EDITOR and common player metadata validation.
2. Copy one existing valid prepared gesture/action clip+owning asset into staging `Assets/Content/Remediation/Folder With Spaces/`. Configure owning assets to those copies using existing editor methods. Do not move/modify source prepared assets.
3. Save/reload and execute each through scoped methods; record result. In negative copies corrupt one requirement at a time: legacy flag, looping/reference pose, empty clip, nonfinite keys, nonskeletal/event/object-ref curve, invalid root trajectory. Each fails for its actual defect rather than its path.
4. Editor-only deep curve validation is not a claim that a player reconstructs editor curves. Player checks execute prevalidated serialized data and reject missing clips/common invalid metadata. Verify no UnityEditor call in compiled player body.

Run `-Ticket R09` for source/reload/editor checks; graphics player execution joins R12's exact saved remediation scene. Gate: valid ordinary-folder assets pass; all existing compatibility invariants remain. No broad asset relocation or other-character changes.
