# W01 — Shared wardrobe selection and preparation

Depends W00. Read API sections 1–2 and CHECKS appearance cases.

## Files

Create WardrobeSelection.cs, WardrobeSelectionResolver.cs, WardrobeSelectionPreparer.cs under Runtime/Performer/Wardrobe. Edit PerformerWardrobe.cs, SuccubusPerformer wardrobe facade and Authoring/ScopedPerformer.cs. Add pure tests at tests/DazPose.Wardrobe.Tests/ with explicit linked production pure sources (no Unity initialization); move a shared enum into its own source file if necessary, preserving namespace/numeric values.

## Steps

1. Extract selection reduction from the existing queue into the pure resolver. Immutable metadata captures ID/alias resolution, populated mask, preset hair policy and valid hair keys; runtime adapter builds metadata once per catalog revision. Resolver results carry target/no-op/failure, not renderer changes.
2. Extract existing fit/attachment preparation into side-effect-free WardrobeSelectionPreparer. Preserve released generation/configuration validation, naked selection/remembered outfit distinction and all current footprint/coverage dependencies.
3. Keep queue/atomic transaction and rollback in PerformerWardrobe. Both old Outfit and new clothing-only SetOutfit request the same resolver/preparer and commit path. Pass cancellation ownership through existing scoped methods.
4. Resolve complete requested hair/clothing target before no-change comparison. Visible new DSL smooth policy is provided later by the host; existing default transitions remain compatible. Hidden application must remain hidden.
5. Retain existing inventory backend until W02 replaces construction through the common binder. No runtime assets are changed here.

## Required checks

AP01–AP08 in CHECKS: sparse masks, no-op extremes, remembered naked vs explicit unclothed, independent/legacy hair behavior, fully dressed repeated outfit with changed hair, invalid generation/fit, immutable state and rollback model. Existing managed story/rehearsal checks remain green; standalone Roslyn compile.

Do not replace fit preparation with renderer toggles or introduce a second transaction. SOURCE_READY/COMPLETE once pure decisions and compilation pass; native fitting evidence belongs Gate A. Next W02.
