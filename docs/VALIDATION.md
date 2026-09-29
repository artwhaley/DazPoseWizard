# Unity pose validation status

## Phase 1 — direct G8F pose application

**Passed visual review by the user on 2026-09-27.** The user confirmed that multiple Genesis 8 Female poses applied correctly and the result “worked great.” Treat the DAZ-to-Unity conversion and direct Apply output as the accepted Phase 1 baseline. Do not reopen its transform math without concrete evidence of downstream corruption.

## Phase 2 — native Unity AnimationClip

The Unity harness now resolves the Phase 1 adapter result into one shared local-transform pose. Direct Apply and generated `.anim` assets consume those same resolved values. Automated clip checks and the validation-only Playables smoke test are described in [the Unity harness guide](../validation/DazPoseUnityValidation/README.md). Manual visual confirmation of direct-versus-clip parity is still required before Phase 2 is considered complete.

## Stage 5 — direct blendshape pipeline

The user visually inspected the real FUNtasy direct application and generated clip and approved the expression. Exact renderer counts and numeric values were intentionally not recorded. Canonical v2 figure controls remain renderer-agnostic; Unity resolves the exact imported shape name against the configured reference and may bind it on multiple renderers. The DAZ value maps through each imported shape's full-value frame weight. Transform and blendshape values share one resolved-pose path and one direct/clip parity validator.

The project Required Morph Manifest combines controls required by converted content with enabled always-export pins. Use **Tools > Manage Always-Export Morphs…** to add, edit, disable, or remove project pins; each entry keeps its category and purpose and shows whether converted content also requires it. Disabling or removing a pin leaves a content-required morph in the CSV. **Tools > Generate DAZ Morph Export Rules** writes a deterministic, exact-name CSV with no duplicates and one final `Anything,Bake` fallback, and reports enabled-pin, content-required, and unique-rule counts. Import the CSV into DAZ Studio and refresh the Lara FBX as a separate manual step. Pins request exported shapes; they do not automatically create `.anim` clips. Unity's **Tools > DAZ Pose > Validate Always-Export Morphs** reports enabled pin shapes as Present, Missing, or Ambiguous against the configured reference; matching shapes on multiple renderers are allowed, while body categories must match the figure body renderer. The current implementation proves direct blendshapes only; ERC/formula controllers and geograft-specific handling remain deferred. Reference FBX import automatically retries failed/pending canonical imports, with **Tools > DAZ Pose > Process Pending Browser Imports** as the manual retry command.

## Phase 1 review record

The user completed the original DAZ-reference comparison across multiple G8F direct-pose applications on 2026-09-27 and confirmed they worked great. The checklist is complete; Phase 1 is accepted.

Keep these diagnostics for any future discrepancy:

- A high rest-landmark fit residual suggests an FBX hierarchy, skeleton asset, root scale, or coordinate boundary mismatch. Do not apply pose data until that report is understood.
- If the root aligns but limbs diverge, investigate DSON evaluation, coordinate conversion, and FBX rest-basis mapping separately.
- Correct large joints with finger/twist differences may reflect missing secondary pose data or a remaining rest-basis mismatch.
