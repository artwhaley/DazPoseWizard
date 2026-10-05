# Lara wardrobe pipeline: implementation and next gate

Updated 2026-10-04. The recipe importer is implemented and regression-tested with
both existing outfits. [Operator manual](LaraWardrobeImportQuickStart.md) is the
front door; [detailed playbook](LaraWardrobeImportPlaybook.md) holds exceptions.

## Implemented production shape

Entry point: `scripts/import-lara-outfit.ps1`. Data recipes live in
`configs/wardrobe`; first/second examples and a third-import template are included.

Mechanical operations:
- DUF material/texture preflight, early FBX body/policy inspection, compact source
  contracts, source inventories and content-hash caching.
- Exact node/surface matching in Unity's actual submesh order.
- Owned dissolve material conversion, explicit transparent graph variants,
  source baselines and preservation of artist-edited runtime properties.
- Canonical rig binding, namespaced attachment-local bone hierarchy, inherited
  shell skinning, semantic source morphs and missing body-morph transfer.
- Geometry-derived morph normals, rest-skinning checks, shared padded bounds.
- Explicit coverage policies, combined-pair shoe articulation/fit/contact
  calibration (automatic supported default), captured foot geometry and toe
  references in footwear data. Rigid footwear automatically skips body transfer.
- OutfitDefinition equipment assets, disposable review hosts and refreshed
  particle bindings. No FBX donor body/rig is serialized into the review host.
- Asset checks, material merge and coverage checks, morph/dissolve renders,
  181-frame walking/culling checks and rigid-pump/contact checks.
- Reimport preservation tests, compact reports, owned-asset backup/publication,
  concurrent-edit detection and preserved user review scenes.

LLM responsibilities:
- Prove ambiguous FBX-to-DUF ownership from labels, asset identity, hierarchy,
  visibility and surface lists, then record mappings in the recipe.
- Classify equipment roles and articulation, choose reviewed coverage policies,
  and establish semantic aliases where source naming differs.
- Inspect previews, diagnose a specific failed stage and identify a genuinely
  unsupported capability. Seek the user's artistic judgement or necessary Daz
  action with concrete evidence. Record verified exceptions once.

Unchanged sources use cached preparation; source generation stays distinct from
artist runtime materials. Reimport merges changed source properties only where
an artist has kept the previous baseline. It keeps GUIDs and footwear height/fit.
Publish rejects changed main-project overrides and creates a new review revision
when a prior review scene exists. Runtime vocabulary/equipping is not implemented
by the importer and remains scheduled after the third test.

## Verified fixtures

First: three pieces, seventeen surfaces, two local hair bones, 124 character
channels and independent underwear/dress coverage. Second: seventeen pieces,
sixty-one surfaces, five shells, 126 character channels and captured heel/fit
reference data. Both pass walking/culling and full dissolve/restore. Both preserve
asset GUIDs and edited opacity on rebuild; the second also preserves height/fit.
See published reports under `TestOutput/wardrobe-import` for current metrics.

This proves the common path, not every possible Daz product. Current limits:
matching canonical body topology/shape; one coverage owner per UV3 component;
single-frame exported position morphs; skinned followers or compatible shells;
one combined shoe pair using the supported baked-pose convention. Skinned shoe
appearance needs review; rigid shape invariance applies to rigid pumps, while
floor penetration and supported contact checks apply to both shoe policies.
Paired tiptoe exports now use a reference-locked empirical lower-leg calibration
with per-point residual checks, instead of treating every change above 0.4 m as
an unrelated body deformation. Unknown patterns/asymmetric poses still stop.
Measured heel/toe contacts regenerate on reimport; artist height/fit persist.
Extend shared policy support for split shoes, segmented boots or rigid accessories
when encountered. Avoid outfit-specific C# and routine extra bridge exports.

## Third import result and remaining independent-agent gate

The user approved the maid import's working result. The initial clean-context
Luna attempt failed on empty body geometry and then an unsupported tiptoe
lower-leg pattern; the parent agent extended shared tools and completed it.
This is functional/technical evidence, **not a passed independent-agent trial**.
The repaired path now includes actual-body inspection before conversion, default
pose calibration, mechanical rigid-shoe body-morph exclusion, floor checks for
both articulation types, refreshed contact geometry, restored-profile reporting
and safe identical republication. The operator manual records the relevant
decisions and failures. These changes still require a clean-context trial.

Use a new DUF/FBX pair (or replay the maid in an isolated destination) and the recipe template. Run the manual as a fresh-context
workflow, with recipe/data changes only. Verify inventory, materials, canonical
anatomy/facial channels, fit/layering, culling, walking and dissolve. Reimport must
retain artistic edits and references. Produce one compact report and obtain
artistic approval. If a new feature is unsupported, record and extend the shared
capability; do not hide a bespoke patch as pipeline success.

Pass criteria: a clean-context agent reaches published technical checks and a
user-review preview using recipe/data changes only, without importer C# changes
or another agent taking over. Necessary Daz correction or artistic decisions are
explicitly logged. Mechanical regression/replay does not replace this trial.

## Runtime wardrobe and persistent setup scene: specified next milestone

The authoritative implementation specification is
[WardrobeRuntimeAndSetupSpec.md](WardrobeRuntimeAndSetupSpec.md). It supersedes
the earlier sketch in this section. This milestone is specified, not implemented.

Confirmed user rules: an outfit has at most three ordered layers (Base, 1, 2).
New clothing/shoe mesh pieces default to Base. The artist assigns individual mesh
pieces with Layer 1/2 checkboxes in Setup. Loading an outfit displays all populated
layers. TryRemoveLayer removes the highest populated visible layer, skipping
empty layers; TryAddLayer restores the lowest populated hidden layer. Removing
the final layer reaches naked while remembering the outfit for re-dressing.
Shoes are outfit pieces and their effects follow their assigned layer. Retain
hair unless the incoming outfit explicitly specifies it.

One wardrobe controller supports the performer vocabulary, outfit/layer queries,
authored-scene wardrobe snapshots and one persistent ClothingSetup scene. The
spec defines assets, atomic switching, queued relative commands, per-layer
coverage/footwear/garment fitting, save/revert/variants, reimport preservation,
migration and acceptance tests. It removes normal per-outfit production scene
generation once Setup is available. Import hosts remain internal test fixtures.

The remaining clean-context import trial is tracked separately; it does not
revoke the user's request to specify this runtime milestone. FinalIK, general
scene directing and arbitrary independent shoe mixing are outside this milestone.
