# R02 — Source-owned binding reconciliation, migration and moves

**Priority:** P0. **Targets:** `SourceBindingReconciler.cs`, `DazStoryImporter.cs`, `StoryContentBindings.cs`, and existing binding assets if necessary.

## Verified defect
At reviewed code:
```csharp
bool changed=created||(recordUndo&&content.ownerSourceGuid!=sourceGuid);
```
This allows scheduled `ReconcileAsset(..., recordUndo:false)` to skip ownership backfill when rows already exist. Both committed First Contact companions were inspected and lacked a serialized `ownerSourceGuid` value.

## Decisions
1. `ownerSourceGuid` is the canonical identity. `recordUndo` controls Undo recording **only**, not whether valid data migration happens. Set `changed=created || content.ownerSourceGuid != sourceGuid` plus actual row additions/changes.
2. Keep automatic import idempotent. Analyze syntax before any persistent mutation; reject corrupt, duplicated or conflicting claims; preserve existing references, pose posture, dialogue clips and orphaned-but-useful rows. Do not overwrite a healthy companion due to a source move or duplication.
3. `FindOwner` should accept an adjacent legacy unowned companion only when the identity is unambiguous. Once a GUID owner exists, it should be found after a `.dazstory` move even if its original companion is left behind. Avoid accidentally adopting the old source's binding on a *duplicate new source GUID*.
4. Existing owner sources may have a nonadjacent companion after relocation. The importer must depend upon the actual owner asset so edits reimport/refresh correctly. Preserve stable GUIDs and add diagnostic messages if ownership is inconsistent.
5. Reimport ordering must not create an infinite importer ↔ reconciler loop. No dirty write / serialized asset save if content/owner already matches; perform batch legacy backfill safely once. Do not use manually editing YAML as the primary workflow.
6. Do not change `.dazstory` syntax or force the source to be bound for syntax/manifest compilation.

## Tests
T05–T09. Include a fixture where all content rows already exist but owner GUID is empty; schedule non-Undo migration and assert source GUID written. Move source while leaving companion behind; exact previous typed reference fields must survive. Also test existing user First Contact legacy assets via non-destructive migration (or independent fixture, if user assets remain untouched until user opens Unity).

**Done:** source moves work by GUID, automatic reconciliation converges, existing bindings remain intact and no manual copy/paste is needed.
