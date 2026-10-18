# Published outfit review metadata

C03 now stores review pointers in mutable project authoring metadata at ProjectSettings/DazPose/WardrobeReviewMetadata.json, outside outfit release payloads and transient TestOutput.

Each entry contains packageId, releaseId, generationId, scenePath, and publishedAtUtc. The package ID and release path come from the existing outfit ID and report definition path. The generation key is the existing recipeHash|fbxHash|dufHash tuple used by the importer reports and outfit asset; it identifies source and recipe bytes and does not represent a new acceptance state.

WardrobeReviewMetadata.MigrateLegacyReports() is an explicit, idempotent Editor operation. It imports only published pointers from existing TestOutput/wardrobe-import/**/report.json files whose scene still exists, preserving the recorded scene path, including timestamped reviews such as the current third-outfit pointer. It never runs on window open or repaint. TryResolve(outfit, out scenePath, out error) is the reader API.

The Imported Outfits window checks the exact outfit identity in metadata first. If no entry exists for that package/release, it may resolve an identity-matching legacy report. Malformed metadata, a missing authoritative scene, or known package/release metadata for a different generation returns an actionable error instead of falling back to an older report. Opening a review no longer calls LaraFirstOutfitBuilder.ConfigurePresentation(), so it does not retune saved presentation settings.

Future Publish actions in scripts/import-lara-outfit.ps1 append the same identity and pointer to ProjectSettings/DazPose/WardrobeReviewMetadata.json, while keeping the legacy report pointer for migration compatibility. New timestamped review paths remain as distinct entries.