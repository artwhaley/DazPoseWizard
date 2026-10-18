# T08 — Session Contract World State and Equipment Tag Model

**Roadmap mapping:** D4  
**Depends on:** T07

## Goal

Create short-lived session/story state and the data-driven equipment compatibility model.

## Add

Under `Story/State/`:

- `SessionContract.cs`
- `SessionState.cs`
- `PlayerWorldState.cs`
- `StoryLocalState.cs`
- `EquipmentDefinition.cs`
- `EquipmentCatalog.cs`
- `EquipmentCompatibility.cs`
- `KinkDefinition.cs`
- `KinkCatalog.cs`

## Session contract

Fresh for each new performance session.

Fields:
- `sessionId`
- `randomSeed`
- `pacingMultiplier` default 1.0
- snapshot of current kink preferences
- `preparedEquipmentIds[]`

Seed:
- generate once when session created;
- display in debug snapshot;
- use `System.Random(seed)` for all D1–D5 selection/randomness.

No `UnityEngine.Random` for story selection.

## Session state

Fields:
- `phase` string
- `phaseCompletion` float, default 0
- `mood` float, default 50
- `intensityMultiplier` float, default 1
- `activeEquipmentIds[]`
- `PlayerWorldState world`
- transient values if required

D1–D5 does not add Director intent.

## Universal world state

Enums/fields:

- posture: `Unknown`, `Standing`, `Sitting`, `Lying`, `Kneeling`
- hands: `Free`, `Occupied`, `Restricted`
- ankles: `Free`, `Restricted`
- oral: `Free`, `Occupied`
- anal: `Free`, `Occupied`
- genital: `Free`, `Occupied`

Clothing booleans:
- shoes
- socks
- shirt
- pants
- bra
- underwear

Do not add equipment-specific booleans.

## Equipment definition

Fields:
- `id`
- `displayName`
- `identityTags[]`
- `themeTags[]`
- `excludesTags[]`

All tags lower-case, trimmed, namespace-prefixed.

### Representative catalog

Create definitions for:

1. `butt-plug`
   - identity: `equipment:butt-plug`, `equipment:anal`
   - themes: `theme:buttstuff`
2. `rope`
   - identity: `equipment:rope`, `equipment:restraint`
   - themes: `theme:restraint`
3. `dildo-anal`
   - identity: `equipment:dildo-anal`, `equipment:anal`
   - themes: `theme:buttstuff`
4. `dildo-oral`
   - identity: `equipment:dildo-oral`, `equipment:oral`
   - themes: `theme:oral`
5. `ice`
   - identity: `equipment:ice`
   - themes: `theme:sensory`

Do not invent conflicts among these five unless physically necessary for the D1–D5 setup. The conflict mechanism is the feature; arbitrary content policy is not.

## Compatibility

Build exposed tags for an item as identity + theme tags.

`AreCompatible(a,b)` returns false when:
- any `a.excludesTags` intersects tags exposed by b, OR
- any `b.excludesTags` intersects tags exposed by a.

This symmetry is mandatory.

Provide helpers to select/filter compatible sets.

## Catalog storage

Author catalogs as JSON TextAssets under `Assets/Production/Story/Catalogs/`.
Catalog DTOs must be `JsonUtility` compatible and versioned.

Bind/load them through the application/story configuration; do not hard-code the representative items in selector logic.

## Acceptance

A hypothetical future definition can exclude `equipment:rope` and become incompatible with today's rope without editing today's rope definition.
