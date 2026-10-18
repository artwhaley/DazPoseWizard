# Current path consumers and completed move map

The 163 successful source-to-destination moves are recorded per asset in [completed-moves.json](completed-moves.json): 50 prior records plus 113 entries from the executed final production move plan, deduplicated by original source. Its destination fields are current paths. Historical C00 dependency evidence remains unchanged and must not be relabeled to destination paths.

Current moved content roots:

- Void shared art: `Assets/Production/Environments/Void` (10 files).
- Performer locomotion/seating: `Assets/Production/Content/Motions/Locomotion` and `.../Seating`.
- Prepared male output: `Assets/Production/Player/Meshes`, `Shaders`, `Support`, and `Materials/Imported` (32 runtime materials, 32 source materials, 21 textures).
- Lara core: `Assets/Production/Performers/Lara/Rig/LaraBody.asset`, `Surfaces/LaraSurfaceBindings.asset`, and `Configuration/LaraDissolveProfile.asset`.
- Published content: `Assets/Production/Content/{Magic,Gestures,Motions,Audio}` and `Assets/Production/Performers/Lara/Contact`.

Remaining path-sensitive consumers must be kept separate from these verified moves: Clothing Setup saved-scene/catalog/draft APIs; wardrobe candidate, catalog and configuration authoring paths; import recipe and configured pose output roots; canonical Lara/Partner FBXs and manifests; `WardrobeImportReviewWindow` and `scripts/import-lara-outfit.ps1` published review metadata. The Lara imported/source materials, textures and manifests remain at their current paths because `WardrobeCatalogBuilder.CharacterSignature` includes material `AssetDatabase` paths. Immutable wardrobe releases and second-outfit toe fixtures remain protected.

Use the exact evidence sources for detailed decisions: [asset-dispositions.json](asset-dispositions.json), [final-production-move-plan.json](final-production-move-plan.json), and the C00 recovery manifest. Do not treat historical consumer arrays as current path mappings.