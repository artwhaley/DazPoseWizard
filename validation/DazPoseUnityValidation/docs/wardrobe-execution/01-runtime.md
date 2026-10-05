# E0–E3: runner, assets and runtime

These instructions specify work to implement, not APIs that already exist. Read
[CONTRACT.md](CONTRACT.md) and [VALIDATION.md](VALIDATION.md) first.

## E0: executable checks

Create `scripts/run-wardrobe-execution.ps1`, using `scripts/import-lara-outfit.ps1`
as the reference for isolated-project synchronization, signed-in Unity licensing,
process handling and locking. Reuse the importer's actual lock path; inspect it,
do not invent a second lock. Never kill an editor or run batch checks in the main
project. Sync changed C# and their matching metas into isolation. Preserve GUIDs.

Create `Assets/DazPose/Editor/Wardrobe/WardrobeExecutionValidation.cs` with the
entry points listed in VALIDATION. A gate that is not implemented must explicitly
fail. The runner must reject missing/stale reports and nonzero exit codes. E0 passes
when an intentional failed assertion produces a failed runner result, a successful
small assertion produces a successful result, and a missing report fails. Keep
these runner self-checks distinct from product gates.

## E1: contract and data

Implement the CONTRACT types under `Assets/DazPose/Runtime/Performer/Wardrobe/`.
Use separate files for assets, layer arithmetic, immutable snapshots/results and
the controller. Keep serialized authoring data separate from instance state.
Validate aliases, stable IDs, signatures, left/right shoe-pair layer assignments and
shell dependencies. Do not require hosiery and shoes to be visible together.
Implement pure layer arithmetic before introducing renderers. Test every populated
mask 1..7 through full removal/addition, empty masks, invalid ceilings and repeated
boundary commands. Test snapshot defensive copying and alias collisions.

## E2: migrate existing packages

Create editor `WardrobeCatalogBuilder.cs`. Its inputs are the three existing
`WardrobeOutfitDefinition` packages and canonical scene named in START. Inspect
`WardrobeImporter.cs`, `WardrobeGeometry.cs` and `LaraFirstOutfitBuilder.cs` before
using their mesh mapping, shape transfer, coverage and particle baking helpers.
Refactor shared helpers rather than copying an outfit-specific builder.

Generate authored presets/catalog and immutable fitted assets in the locations
specified in START. All clothing starts Base. Keep first-outfit hairstyle outside
strip layers and make its supplied hair an explicit Set operation. The other two
presets Keep hair. Do not assign artistic layers on the user's behalf. Include a
canonical naked state and each package's accepted fully dressed state. Preserve
canonical morph names, material slot order and bind conventions. Build signatures
from actual ordered data. Use source piece IDs, not renderer names, for matching.

Generate particle surface bindings for each actual body mesh using
`PerformerSurfaceBindingBaker`; required count comes from
`PerformerSurfaceBindingAsset.RequiredBindingCount`. Record its source identity.
Migration must be idempotent: same inputs preserve IDs/GUIDs and content. Do not
modify accepted source packages or scenes. E2 initially validates Base-only states;
arbitrary layered fitting belongs to E4.

## E3: atomic controller and facade

Implement `PerformerWardrobe` and the SuccubusPerformer methods in CONTRACT.
Prepare assets, materials, bones and effect bindings before committing. Keep the
same body renderer, actor, animator and canonical skeleton. Resolve canonical
bones against the live actor; own accessory bones explicitly. Validate under a
translated/rotated actor, not only an origin fixture. Capture and restore morph
values by semantic name when swapping sharedMesh; preserve active animation,
breathing, facial expression and anatomy controls.

Important existing seams:

- `LaraWardrobe` supplies attachment morph following and bounds behavior. Reuse
  it through a shared binder or adapt it; support zero clothing and persistent hair.
- `LaraAnatomyControls.Configure` resets requested values. Add a preserving rebind
  path instead of using Configure on every outfit change.
- `LaraHeelReview` writes shared profile settings. Replace it on new runtime hosts
  with `FootwearPoseDriver` instance settings; never run two correction owners.
- `PerformerParticleBody`, `PerformerDissolveRig` and `PerformerDissolveProfile`
  currently lack the necessary production rebind surface. Add narrow runtime
  instance APIs that validate target mesh, bindings and material/profile identity.
  Editor-only SerializedObject/baking helpers cannot serve runtime switches.

Use per-actor material/profile instances where required; retain immutable asset
references otherwise. Cache prepared states with explicit ownership and cleanup.
On failed preparation leave the old state intact. On failed commit restore the old
state before rendering and return an actionable result. FIFO capacity is 32;
settle all accepted requests exactly once, including disable/destruction. Setup
selection cancellation must not cancel authored relative commands.

E3 validates Cut transitions. Dissolve transitions must either be complete or
explicitly unavailable until E7; never silently substitute Cut for Dissolve.
Test two actors, morph preservation, transformed rigs, no-op results, invalid
assets, queue ordering and disable during pending work. Record actual object
identities before/after. Do not reset unrelated performer subsystems.
