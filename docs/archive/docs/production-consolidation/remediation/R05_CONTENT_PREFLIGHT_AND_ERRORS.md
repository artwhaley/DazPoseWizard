# R05 — declared content and production errors

Depends R04. Allowed seams: PerformanceSceneBindings, asset readiness helpers and error strings in existing owners; validation cases. Leave working Lara discovery/rig behavior intact.

1. Append capability bits `Action=4096`, `Magic=8192` to PerformerCapability; do not renumber existing flags. Keep Gesture=512. Add serialized arrays for gestures, actions, spells and auras, default empty. Add a new declaration method for these arrays; do not change existing DeclareAssets signature/serialized names.
2. Required Gesture checks declared gesture.IsReady plus existing actor mask compatibility. Required Action checks action.IsReady/root metadata and compatible animator/body paths. Required Magic checks each declared spell/aura.IsReady and required style/prefab data. Validate bound target references separately; live target loss remains runtime behavior. Reuse existing validators; do not instantiate a VFX effect during preflight.
3. Add exact production errors: missing speech source → actor/speechAudioSource; missing teleport → actor/teleportProfile; missing dissolve → actor/dissolveProfile or dissolveRig as applicable. Explain assigning the existing production reference/profile. Remove directions to acceptance installers and PoseValidation.unity from ordinary runtime failures. Do not rename menus used for legacy diagnostics.
4. Search runtime public-command failure paths for acceptance/install/test-scene instructions and replace this bounded category. Do not rewrite logs unrelated to authoring, alter expected Busy semantics or catch all exceptions as success.

Run `-Ticket R05`: missing clip, invalid gesture mask/clip, invalid action trajectory, missing magic style/prefab/target, valid prepared assets, missing speech/teleport/dissolve, optional undeclared capabilities. Assert required-capability failures identify actor/asset/property. A valid speech-only declaration with no action/magic assets passes. Verify messages contain no acceptance setup requirement.

Gate: Begin's required asset checks cover the full declared content surface. Folder-prefix rejection remains until R09; do not remove structural curve checks here. Document exact message changes for reviewers. This is suitable for bounded Luna string/test work after R04 interfaces are fixed.
