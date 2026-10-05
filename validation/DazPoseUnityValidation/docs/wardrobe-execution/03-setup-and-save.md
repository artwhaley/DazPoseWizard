# E5–E6: one setup scene and durable configuration

## E5: setup

Create editor `ClothingSetupBuilder.cs` and `ClothingSetupWindow.cs`, plus a
runtime preview host if needed. Generate ONE `Assets/Scenes/ClothingSetup.unity`
in isolation, using the same PerformerWardrobe controller as gameplay. Subsequent
imports add catalog data, not scenes. Rebuilding data must preserve the existing
setup scene and any saved scene authoring changes.

Provide catalog selection, candidate/released status, layer rows with exclusive
1/2 checkboxes (neither = Base), Remove/Add/Naked/Redress preview, live query state,
hair selection and body morph/walk inspection. Shoe meshes remain outfit parts.
Expose material opacity and reviewed footwear calibration controls. Show fit-state
validation results and Needs Fit details. Keep hair visible when hiding clothes.
Capture technical fixtures and ensure switching uses the production async path.

Editing a layer assignment invalidates the compiled candidate. Compile/validate
before allowing release. Preview temporary values on instance copies. Pose,
camera, piece isolation and anatomy probes are session inspection state, not saved
outfit defaults. Save/Revert/Create Variant are distinct explicit actions.
Add a Setup entry to `WardrobeImportReviewWindow`; keep existing review diagnostics
accessible. Do not silently replace an open dirty scene.

## E6: persistence and publication

Implement `WardrobeEditSession` and editor `WardrobeGenerationPublisher` (names
are new files to create). Serialize configuration schema version, stable piece
assignments, material overrides, footwear tuning, explicit hair policy and input
hashes. Revision advances on successful Save, not on every slider drag. Revert
restores durable configuration. A variant gets a unique stable preset ID and
shares unchanged immutable assets; do not copy entire import directories.

Working candidate configuration and released runtime snapshots are separate.
Generate content-addressed snapshots under
`Assets/TestData/WardrobeRuntime/Generations/<preset-id>/<generation-hash>/`.
Snapshot mutable imported meshes, materials, profiles and bindings used by a
release: current import tooling may update those source assets in place. Sharing
their references would break last-good-release protection. Share immutable
textures only where source identity/content is stable. Equal content may reuse
snapshots; changed content must not overwrite a referenced released generation.
Advance the catalog's released reference only after all reachable-state checks
pass. Failed builds leave previous release references and bytes unchanged.

Extend the shared importer publication path to update/register candidates in this
catalog. Match authored assignments/tuning by stable source piece IDs. New pieces
start Base; missing or changed incompatible pieces produce Needs Repair with a
specific diff. Preserve user overrides through the existing source/artist merge
rules. Imported candidates cannot automatically erase prior artist overrides or
advance a release without validation. Keep source imports and legacy GUIDs intact.

Tests must cover Save/reload, Revert, variant independence/shared unchanged data,
new/missing pieces, material/profile overrides and failed reimport protection.
Run the existing importer reimport-preservation check as a regression. Compare
released asset byte hashes and references before/after a deliberately failed
candidate. Previewing in Play must not mutate serialized source materials or
profiles. Record which edits survive reload and which are deliberately temporary.
