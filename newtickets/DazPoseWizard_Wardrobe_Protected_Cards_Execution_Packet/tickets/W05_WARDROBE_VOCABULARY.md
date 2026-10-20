# W05 — Typed wardrobe scripting and binding authoring

Depends W04. Read CONTRACT C2, API sections 1–3, CHECKS LN08–LN13.

## Files

Language vocabulary/compiler/descriptions/source analysis; StoryContentBindings; GlobalStoryCardCatalog/StoryLinkedContent diagnostics; PerformanceStoryHost; SourceBindingReconciler; StoryContentBindingsEditor; Studio typed asset field/assignment helpers; importer dependency tracking; scoped performer facade. Keep native appearance projection for W08.

## Steps

1. Append binding enum values 8/9, add wardrobe/hair arrays and exact accessors/diagnostics. Reconciler creates missing rows with preserved ownerSourceGuid, rejects cross-kind collisions, never erases old rows/assignments or duplicates other sources.
2. Implement all five exact canonical commands, parameter checks and mandatory await. Host resolves content by command.Source.unit. For visible actors pass smooth 0.25+0.25 transition; hidden actor commits hidden. Keep layer no-op statuses successful.
3. Add centralized required-channel arrays and adapt existing dissolve channel handling to that helper. Wardrobe requires Body+Visual+Wardrobe; conflict errors identify source owner and instruction to await. Tracking/independent speech may continue when compatible.
4. Hair assignment validates project prefab descriptor/manifest/generation/rig compatibility. Add typed drag/object fields through existing requirement UI, not a second bindings panel. W08 wires preview.
5. Derive readiness from reachable source bindings; extend mandatory and drawn-candidate diagnostics without requiring manual hidden declaration arrays. An unavailable candidate is rejected with reason; a mandatory explicit resource errors before its body.

## Checks / exit

LN08–LN13: typed syntax, no-await rejection, enum compatibility, aliases/source ownership, unknown/cross-generation assets, source reconciliation invariants, multi-channel release and benign extremes. Existing managed tests/static compile remain green. No startup asset assignment in main yet. Next W06.
