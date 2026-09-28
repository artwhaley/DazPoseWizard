# Unity pose validation status

## Phase 1 — direct G8F pose application

**Passed visual review by the user on 2026-09-27.** The user confirmed that multiple Genesis 8 Female poses applied correctly and the result “worked great.” Treat the DAZ-to-Unity conversion and direct Apply output as the accepted Phase 1 baseline. Do not reopen its transform math without concrete evidence of downstream corruption.

## Phase 2 — native Unity AnimationClip

The Unity harness now resolves the Phase 1 adapter result into one shared local-transform pose. Direct Apply and generated `.anim` assets consume those same resolved values. Automated clip checks and the validation-only Playables smoke test are described in [the Unity harness guide](../validation/DazPoseUnityValidation/README.md). Manual visual confirmation of direct-versus-clip parity is still required before Phase 2 is considered complete.

## Phase 1 review record

The user completed the original DAZ-reference comparison across multiple G8F direct-pose applications on 2026-09-27 and confirmed they worked great. The checklist is complete; Phase 1 is accepted.

Keep these diagnostics for any future discrepancy:

- A high rest-landmark fit residual suggests an FBX hierarchy, skeleton asset, root scale, or coordinate boundary mismatch. Do not apply pose data until that report is understood.
- If the root aligns but limbs diverge, investigate DSON evaluation, coordinate conversion, and FBX rest-basis mapping separately.
- Correct large joints with finger/twist differences may reflect missing secondary pose data or a remaining rest-basis mismatch.
