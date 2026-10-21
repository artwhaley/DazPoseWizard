# W02 — Reusable hair/clothing rig, binder and delivery tool

Depends W01. Read API sections 1 and 8, manual Gate A, CHECKS AP09–AP12.

## Files

Create WardrobeAttachmentDescriptor, WardrobeRigManifest, WardrobeRigBinding, WardrobeAttachmentBinder/Lease under Runtime/Performer/Wardrobe. Edit PerformerWardrobe inventory construction and only the required renderer-registration hooks in LaraWardrobe. Add deterministic generation services and WardrobeAppearanceDeliveryWindow under Editor/Wardrobe. Add Gate A saved-runner/composer under Validation/WardrobeProtectedCards and Validation/Editor/WardrobeProtectedCards. Production runtime references no validation code.

## Steps

1. Extract canonical/local bone binding, material overrides and inventory records into one binder. New prefab path and synthesized legacy descriptors both use it. Store owner explicitly; do not move PerformerWardrobe off its current root.
2. Generate prefab descriptors from released package parts. Clothing excludes hair; hair contains its complete piece/local-bone closure. Validate stable keys, roots, missing/duplicate canonical bones, local parent cycles, mesh/bone/material count and signature/generation before instance mutation.
3. Implement per-owner MRU/pin policy from API. Prepare target offscreen/disabled until commit; rollback retains previous usable resources. Repeated switches release unused instances/materials and never scan the catalog per handle drag.
4. Build delivery window with fixed controls. Inspect inputs is read-only. Generate reusable prefabs references released meshes/materials, preserves same-output metas and refuses mismatching existing outputs. Do not write unused WardrobePreset.HairPrefab.
5. Implement Gate A composer: diagnostic copy of saved FirstContact art/body/performer references with isolated mutable bindings and auto-story runner disabled. Attach saved AppearanceGateRunner, assign corrected or baseline diagnostic wardrobe inputs explicitly; no source prefab Apply. Create-once scene path in API. W03 supplies the corrected release.
6. Implement runner's visible controls from manual Gate A. It uses normal scoped/wardrobe runtime APIs, displays current outfit/hair/layer and emits JSON assertions. No automatic toy/hardware output.

## Checks / exit

AP09–AP12 validate manifest keys, scoped bone maps, owner cache disposal/pinning and generation idempotence model. Run standalone source compilation, GUID/meta audit, protected manifest comparison. No Unity generation or invocation by executor.

W02 becomes SOURCE_READY after these checks. Do not claim prefabs were generated until Art clicks the delivered action. Native completion is Gate A after W03; continue W03 tooling without asking for a redundant earlier gate.
