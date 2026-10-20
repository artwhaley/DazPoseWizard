# F02 — Migrate true diagnostic coverage before retirement

**Depends:** F01. **Goal:** Replace the *purpose* of useful diagnostic scenes without retaining their per-feature serialized hierarchy.

1. Derive a case catalog from legacy checks: pose/retargeting, SALSA, hand grip/contact solver, prepared partner anatomy, locomotion/seating, wardrobe/draft/release state, magic/dissolve, story/script runner, hardware offline facade. Mark each item **automated test**, **asset preview command**, **generic lab interactive fixture** (only if truly necessary) or **historical report only**.
2. Move deterministic assertions to current `DazPose.Validation`/Editor EditMode tests; move animation browsing/audit display to general content preview; make hand/contact diagnostics operate on reusable prepared characters/anchors. Maintain test coverage; avoid tests reaching scene path strings.
3. One deliberate `DevelopmentLab.unity` MAY exist only if manual in-editor calibration genuinely requires a saved inspector layout not already offered by Showcase/Clothing Setup/new Studio. If created, composition must be minimal reusable prefab actors, art template, tools; generic lab with tabs/targets, not eleven hidden sub-scenes. Record rationale. Otherwise no lab.
4. Keep immutable **documentation/evidence** for historical observations including toe/published wardrobe reviews, not executable copies of abandoned acceptance scenes.

**Acceptance examples:** Pose import and SALSA check without PoseValidation; hand contact geometry test without HandGripAcceptance; facade + cancellation checks without runtime smoke scene; author can still inspect useful action/gesture animation.

**Gate:** Actual important diagnostics still execute and report; targeted scene dependency graph now empty except source archives. Document any diagnostic that cannot be ported before deleting its scene—an explicit failed gate until resolved.
