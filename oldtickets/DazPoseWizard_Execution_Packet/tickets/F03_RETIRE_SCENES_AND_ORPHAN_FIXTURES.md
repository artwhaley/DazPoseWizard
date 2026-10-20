# F03 — Retire obsolete scenes and verified orphaned assets

**Depends:** F01/F02. **Goal:** Actually finish Phase 1 cleanup.

1. Take post-migration dependency snapshots. For each of the eleven `CLEANUP_DISPOSITION.md` scenes, prove zero active dependencies, source content recovery available, equivalent meaningful checks exist; delete asset and `.meta` by `AssetDatabase.DeleteAsset` (or controlled versioned repo deletion) rather than move to a lab folder. Validate disappearance from disk/Unity AssetDatabase and no missing GUID refs.
2. Check all remaining active scenes, including TestData/wardrobe reviews and existing Showcase. Eliminate dead one-purpose scenes/objects once provenance protected. Freeze the canonical FirstPerformanceVoid original at same path; retain pending Toe fixture until user judges it; published review evidence remains protected.
3. Audit and retire old *builder shell* classes, menus, old scene-script constants, ignored smoke fixtures/recordings/material duplicates. Preserve useful tooling extracted under F01. Review actively referenced previews/candidate assets before deciding whether `DraftAttempts/ffc251...` and `SetupCandidates/685f...` are newly orphaned; if no active/release consumer, delete their assets and metas with recovery proof. Retain actual live draft `a659...` and immutable releases. No indiscriminate `rm -rf TestData`.
4. Find dead editor menu entries/obsolete `InitializeOnLoad` handlers and remove them, not merely hide the buttons. Remove stale catalog/build paths and content index references. Keep operational menu count coherent under DazPose.
5. Use isolated commits per removal category. Track original/destination GUIDs and destroyed GUIDs in machine-readable report. Delete old scene files only after independent coverage is green; no surprise scene rewrites.

**Gate:** All eleven retired, or explicit live indispensable exception documented as **PARTIAL**, not a success. Current scene list shows no unused product scenes. Missing references and protected hashes unchanged. Removed file/bytes measured honestly separately from moves and historical archive sizes.
