# Fixed implementation contract

The user has settled these decisions. Implement them; do not redesign them.

## Layer state

Exactly three available layer slots: Base=0, 1, 2. Each clothing/shoe mesh has one
assignment. New meshes default Base; stable source IDs retain old assignments.
Persistent hairstyle meshes are not undressing layers. Shoes belong to outfits.
Related footwear pair meshes share a layer; explicit shell dependencies cannot
leave a dependent mesh visible without its required owner.

Represent populated/visible layers as three-bit masks. A layer ceiling is -1..2.
VisibleMask = PopulatedMask & ((1 << (ceiling+1)) - 1), with -1 yielding zero.
Load selects the outfit, ceiling=2 and all populated layers. Remove clears the
highest visible bit; its next ceiling is the highest remaining bit or -1. Add
sets the lowest populated hidden bit and sets the ceiling to that index. Empty
layers never create an operation. FullyDressed compares masks, not ceiling==2.

At naked, retain the selected outfit so Add dresses it again. Explicit
Outfit("unclothed") clears that remembered clothing selection. Hair remains
unless a new outfit explicitly Sets/Clears it. Calling the currently selected
outfit while partly dressed restores all layers. Ordinary changes preserve
anatomy, expression, speech, gaze, pose, placement and animator/rig identities.

## Exact public surface

Use these type names in namespace DazPose.Performer:

- `WardrobePreset`, `WardrobeCatalog`, `WardrobeFitState`, `WardrobeConfiguration`:
  persistent assets. Retain `WardrobeOutfitDefinition` as the legacy import adapter.
- `WardrobeLayerState`: pure mask/ceiling operations. No renderer/assets dependencies.
- `WardrobeState`: immutable defensive snapshot, not a mutable serialized asset.
- `WardrobeTransition`: Cut default; Dissolve with explicit out/in timing.
- `WardrobeChangeResult`, `WardrobeLayerChangeResult`, `WardrobeChangeStatus`.
- `PerformerWardrobe`: single runtime owner attached to the existing performer.
- `SceneWardrobeSnapshot`, `SceneWardrobeBinding`: focused authoring initialization.
- `FootwearPoseDriver`: production instance adapter for reviewed heel support.

Add to SuccubusPerformer:

```csharp
public void Outfit(WardrobePreset preset);
public void Outfit(string presetId);
public Awaitable<WardrobeChangeResult> OutfitAsync(WardrobePreset preset,
    WardrobeTransition transition = default);
public Awaitable<WardrobeChangeResult> OutfitAsync(string presetId,
    WardrobeTransition transition = default);
public void TryRemoveLayer();
public void TryAddLayer();
public Awaitable<WardrobeLayerChangeResult> TryRemoveLayerAsync(
    WardrobeTransition transition = default);
public Awaitable<WardrobeLayerChangeResult> TryAddLayerAsync(
    WardrobeTransition transition = default);
public WardrobeState CurrentWardrobe { get; }
```

Void methods observe the SAME queued async implementation and report unexpected
failures; boundary no-ops are not warnings. Queue accepted authored commands FIFO;
relative operations resolve against the state at execution. Cap pending requests
at 32 and return Failed/QueueFull before accepting another. Setup may explicitly
supersede its own not-yet-committed absolute selection; it may not drop authored
Remove/Add commands. Every accepted request completes once on apply, no-op,
cancellation, failure or performer disable. No unawaited unfinished work survives
disable/destruction. Commit is atomic before the next rendered frame.

Statuses: Applied, AlreadyEquipped, NoChange, Superseded, Cancelled,
PerformerDisabled, Failed. No-change reasons: AlreadyNaked, FullyDressed,
NoOutfitSelected. Failure codes: UnknownPreset, AmbiguousAlias, NotReleased,
IncompatibleCharacter, ConflictingItems, FitNotAvailable, MissingAsset,
InvalidBindings, EffectRebindFailed, QueueFull. Return prior state on failed commit.

State fields: OutfitId, DisplayName, ConfigurationRevision, IsNaked, IsChanging,
LayerCeiling, PopulatedMask, VisibleMask, HighestVisibleLayer (nullable),
CanRemoveLayer, CanAddLayer, EffectiveFootwearId ("barefoot" when absent),
EffectiveHairId, and copied per-layer mesh IDs/names. OutfitId is remembered while
naked through Remove; null/empty only when no outfit is selected. IsChanging is
advisory; other fields describe committed visibility. Publish WardrobeChanged
after successful committed state changes. Query flags do not require callers
to pre-check the safe Try commands.

## Schema and ownership

SchemaVersion=1. Preset: stable ID/name/aliases, character signature, configuration
revision, parts(package reference, stable source piece ID, layer 0..2), material
variant references, hair Keep/Set/Clear with optional hair item, included footwear
config and compiled fit states keyed by VisibleMask. Source IDs are not filenames
or generated renderer names. IDs are lowercase ASCII; aliases trim/case-normalize
and must be unique. Do not fuzzy-match names. Seed first/second/third import IDs;
add `maid` as an alias for third-outfit, retaining the source ID.

Fit state: canonical-compatible body mesh/bind data, attachment mesh/bind overrides,
active coverage, active footwear/barefoot state and pre-baked particle bindings/
profile. At most four distinct states per preset; empty-layer duplicates share
data. Character signature includes canonical vertex/triangle ordering, bone order/
rest convention, material slot order and semantic body morph contract. A count
alone is insufficient. Runtime uses generated data, not AssetDatabase/DUF/FBX.

Actor state/property blocks/material instances belong to that performer. Never
write shared configurations/materials through runtime setters. Setup edits preview
copies; explicit Save persists working configs. Release advances a validated
catalog snapshot; a failed candidate cannot overwrite the last released assets.
Generated meshes/bindings go under Assets/TestData/WardrobeRuntime; authored
configs/catalog/presets under Assets/Wardrobe. Keep existing source assets/GUIDs.

## Scene recall and Setup

Scene snapshots contain performer binding IDs, preset/variant IDs, layer ceiling,
and resolved hair/appearance selections. They store no independent shoe override.
Prepare every binding before reveal. Init can directly commit the specified ceiling
while concealed; it must not first flash a fully dressed state. Preserve live
KeepHair for ordinary changes; capture explicit resolved hair in scene snapshots
so recall is deterministic after unrelated prior scenes. No new director/language.

One Assets/Scenes/ClothingSetup.unity. Same runtime controller in edit/play preview.
Layer 1/2 checkboxes are mutually exclusive; neither checked means Base. Show
runtime Outfit/Remove/Add/query state plus inspection, morph, walk, material and
footwear controls. Save/Revert/Variant are explicit. Pose, anatomy, camera and
piece-isolation probes are not saved outfit defaults. Every load starts all layers.

Body coverage, fit, toe/bind patches, lift/walk and particle binding follow the
effective layer state. Hair is not hidden by Hide Clothes. Footwear floor support
follows visible shoes, while bent-foot pose follows any visible piece marked
`requiresBentFootPose` (including hosiery). Shoes may be removed with stockings
still visible: preserve the bent pose and disable shoe contact, even if the result
looks awkward. Return to bare feet only after every pose-dependent piece is hidden.
This is an accepted content-authoring responsibility, not an import/setup blocker.
Shoe calibration is optional: with no profile, keep the baked bent-foot body shape
and use zero shoe lift/shrink/support. The result may look awkward without blocking
fit or runtime application.
FinalIK/terrain/hand targets, sheer genital fitting and arbitrary shoe mixing are
outside this implementation. Do not claim them from flat-floor support tests.
