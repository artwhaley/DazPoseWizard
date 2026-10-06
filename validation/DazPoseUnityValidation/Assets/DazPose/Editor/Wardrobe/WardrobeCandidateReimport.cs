using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Registers a refreshed import as editable candidate data without touching a release.</summary>
    public static class WardrobeCandidateReimport
    {
        public static string ComputeInputEntry(WardrobeOutfitDefinition imported)
        {
            if (imported == null) throw new ArgumentNullException(nameof(imported));
            string path = AssetDatabase.GetAssetPath(imported);
            string dependency = !string.IsNullOrEmpty(path) ? AssetDatabase.GetAssetDependencyHash(path).ToString() :
                HashInMemoryPackage(imported);
            return "import:" + imported.id + ":" + dependency;
        }

        public static bool Synchronize(WardrobeCatalog candidate, WardrobeConfiguration configuration,
            WardrobeOutfitDefinition imported, out string error)
        {
            error = null;
            if (candidate == null || configuration == null || imported == null)
            { error = "Candidate catalog, configuration and imported outfit are required."; return false; }
            if (candidate.IsReleased)
            { error = "Released catalogs are immutable; synchronize a working candidate."; return false; }
            if (candidate.AuthoringConfiguration != null && candidate.AuthoringConfiguration != configuration)
            { error = "The supplied configuration is not this catalog's authoring configuration."; return false; }

            if (!WardrobeCatalog.IsValidId(imported.id))
            { error = "Imported package requires a valid stable source ID."; return false; }
            // Release snapshots and mutable imports are distinct assets carrying the same source ID.
            // Variants intentionally have different preset IDs, so match the package's identity.
            var matching = candidate.Presets.Where(p => p != null && p.Package != null &&
                string.Equals(p.Package.id, imported.id, StringComparison.Ordinal)).ToArray();
            if (matching.Length == 0)
            { error = "No candidate preset references this imported package identity: " + imported.id; return false; }

            string inputEntry;
            try { inputEntry = ComputeInputEntry(imported); }
            catch (Exception exception) { error = exception.Message; return false; }
            string inputPrefix = "import:" + imported.id + ":";
            string[] hashes = configuration.InputHashes ?? Array.Empty<string>();
            string previousEntry = hashes.FirstOrDefault(value => value != null && value.StartsWith(inputPrefix, StringComparison.Ordinal));
            bool sourceChanged = !string.Equals(previousEntry, inputEntry, StringComparison.Ordinal);

            var importedNonHair = (imported.pieces ?? Array.Empty<WardrobeOutfitDefinition.Piece>())
                .Where(piece => piece != null && !string.Equals(piece.role, "hair", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (importedNonHair.Any(piece => string.IsNullOrWhiteSpace(piece.id)) ||
                importedNonHair.Select(piece => piece.id).Distinct(StringComparer.Ordinal).Count() != importedNonHair.Length)
            { error = "Needs Repair: imported non-hair pieces have missing or duplicate stable source IDs."; return false; }
            var importedPieces = importedNonHair.ToDictionary(piece => piece.id, StringComparer.Ordinal);

            var allAssignments = configuration.Assignments;
            var replacementAssignments = allAssignments.ToList();
            var updates = new List<(WardrobePreset preset, WardrobePreset.Part[] parts, string[] newPieces)>();
            var problems = new List<string>();
            foreach (var preset in matching)
            {
                var parts = preset.Parts;
                if (parts.Any(part => part == null || string.IsNullOrWhiteSpace(part.sourcePieceId)) ||
                    parts.Select(part => part.sourcePieceId).Distinct(StringComparer.Ordinal).Count() != parts.Length)
                { problems.Add("candidate has missing or duplicate stable source IDs"); continue; }
                var partById = parts.ToDictionary(part => part.sourcePieceId, StringComparer.Ordinal);
                var rows = allAssignments.Where(row => row != null && row.presetId == preset.PresetId).ToArray();
                var rowById = new Dictionary<string, WardrobeConfiguration.PieceAssignment>(StringComparer.Ordinal);
                foreach (var row in rows)
                {
                    if (string.IsNullOrWhiteSpace(row.sourcePieceId))
                    { problems.Add("assignment has an empty source piece ID"); continue; }
                    if (rowById.ContainsKey(row.sourcePieceId))
                        problems.Add("duplicate assignment " + row.sourcePieceId);
                    else rowById.Add(row.sourcePieceId, row);
                }

                var missingParts = partById.Keys.Where(id => !importedPieces.ContainsKey(id)).ToArray();
                var missingAssignments = rowById.Keys.Where(id => !importedPieces.ContainsKey(id)).ToArray();
                bool missingSetHair = preset.HairAction == WardrobeHairAction.Set &&
                    !(imported.pieces ?? Array.Empty<WardrobeOutfitDefinition.Piece>()).Any(piece => piece != null &&
                        piece.id == preset.HairId && string.Equals(piece.role, "hair", StringComparison.OrdinalIgnoreCase));
                if (missingParts.Length > 0 || missingAssignments.Length > 0)
                {
                    var missing = missingParts.Concat(missingAssignments).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal);
                    problems.Add("removed source pieces " + string.Join(", ", missing));
                    continue;
                }
                if (missingSetHair)
                { problems.Add("removed explicitly selected hair piece " + preset.HairId); continue; }

                var newIds = importedPieces.Keys.Where(id => !partById.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                var mergedParts = new List<WardrobePreset.Part>();
                foreach (var piece in importedPieces.Values.OrderBy(piece => piece.id, StringComparer.Ordinal))
                {
                    if (partById.TryGetValue(piece.id, out var priorPart))
                    {
                        int layer = rowById.TryGetValue(piece.id, out var row) ? row.layer : priorPart.layer;
                        if (layer < 0 || layer > 2)
                        { problems.Add("invalid existing layer for " + piece.id); continue; }
                        mergedParts.Add(new WardrobePreset.Part
                        { sourcePieceId = piece.id, layer = layer, requiredOwnerPieceIds = priorPart.requiredOwnerPieceIds });
                        if (!rowById.ContainsKey(piece.id))
                            replacementAssignments.Add(new WardrobeConfiguration.PieceAssignment
                            { presetId = preset.PresetId, sourcePieceId = piece.id, layer = layer });
                    }
                    else
                    {
                        int layer = rowById.TryGetValue(piece.id, out var existingAssignment) ? existingAssignment.layer : 0;
                        if (layer < 0 || layer > 2)
                        { problems.Add("invalid existing layer for " + piece.id); continue; }
                        mergedParts.Add(new WardrobePreset.Part
                        { sourcePieceId = piece.id, layer = layer, requiredOwnerPieceIds = Array.Empty<string>() });
                        if (existingAssignment == null)
                            replacementAssignments.Add(new WardrobeConfiguration.PieceAssignment
                            { presetId = preset.PresetId, sourcePieceId = piece.id, layer = 0 });
                    }
                }
                updates.Add((preset, mergedParts.ToArray(), newIds));
            }

            if (problems.Count > 0)
            {
                if (sourceChanged) InvalidateCompile(candidate);
                error = "Needs Repair for " + imported.id + ": " + string.Join("; ", problems) +
                    ". Existing assignments and overrides were retained.";
                return false;
            }

            bool structuralChange = updates.Any(update => update.newPieces.Length > 0);
            bool stateChanged = sourceChanged || structuralChange || updates.Any(update => update.preset.Package != imported) || updates.Any(update =>
                !SameParts(update.preset.Parts, update.parts));
            if (!stateChanged)
            {
                if (candidate.AuthoringConfiguration == null)
                { candidate.SetAuthoringConfiguration(configuration); EditorUtility.SetDirty(candidate); }
                return true;
            }

            if (!configuration.TrySetWorkingAssignments(replacementAssignments.ToArray(), out error)) return false;
            configuration.ConfigureGenerated(configuration.Assignments, configuration.MaterialOverrides, configuration.FootwearOverrides);
            string[] updatedHashes = hashes.Where(value => value == null || !value.StartsWith(inputPrefix, StringComparison.Ordinal))
                .Concat(new[] { inputEntry }).ToArray();
            if (structuralChange)
            {
                string generation = inputEntry + "|" + string.Join("|", updates.SelectMany(update => update.newPieces));
                configuration.CommitRevision(generation, updatedHashes);
            }
            else configuration.SetInputHashesWithoutRevision(updatedHashes);

            foreach (var update in updates)
            {
                int revision = structuralChange ? configuration.Revision : update.preset.ConfigurationRevision;
                update.preset.ConfigureGenerated(update.preset.PresetId, update.preset.DisplayName,
                    update.preset.CharacterSignature, revision, imported, update.parts,
                    update.preset.Aliases, update.preset.HairAction, update.preset.HairId, imported.footwear);
                // Keep last-good fit references available for rollback; NeedsCompile blocks their use.
                EditorUtility.SetDirty(update.preset);
            }
            candidate.SetAuthoringConfiguration(configuration);
            InvalidateCompile(candidate);
            EditorUtility.SetDirty(configuration);
            return true;
        }

        private static void InvalidateCompile(WardrobeCatalog catalog)
        {
            catalog.SetCompiledConfigurationHash(string.Empty);
            EditorUtility.SetDirty(catalog);
        }

        private static bool SameParts(WardrobePreset.Part[] left, WardrobePreset.Part[] right)
        {
            if (left.Length != right.Length) return false;
            var byId = left.ToDictionary(part => part.sourcePieceId, StringComparer.Ordinal);
            return right.All(part => byId.TryGetValue(part.sourcePieceId, out var prior) && prior.layer == part.layer &&
                (prior.requiredOwnerPieceIds ?? Array.Empty<string>()).SequenceEqual(part.requiredOwnerPieceIds ?? Array.Empty<string>()));
        }

        private static string HashInMemoryPackage(WardrobeOutfitDefinition package)
        {
            var text = new StringBuilder(package.sourceFbxHash).Append('|').Append(package.sourceDufHash).Append('|').Append(package.recipeHash);
            foreach (var piece in (package.pieces ?? Array.Empty<WardrobeOutfitDefinition.Piece>()).OrderBy(piece => piece.id, StringComparer.Ordinal))
            {
                text.Append('|').Append(piece.id).Append(':').Append(piece.role).Append(':');
                AppendAsset(text, piece.mesh);
                foreach (var material in piece.materials ?? Array.Empty<Material>()) AppendAsset(text, material);
            }
            AppendAsset(text, package.footwear);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        private static void AppendAsset(StringBuilder text, UnityEngine.Object asset)
        {
            if (asset == null) { text.Append("<null>"); return; }
            string path = AssetDatabase.GetAssetPath(asset);
            text.Append(path);
            if (!string.IsNullOrEmpty(path)) text.Append(':').Append(AssetDatabase.GetAssetDependencyHash(path));
        }
    }

    /// <summary>Waits until imports settle, then refreshes only mutable candidate catalogs.</summary>
    internal sealed class WardrobeCandidateReimportPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> Pending = new HashSet<string>(StringComparer.Ordinal);

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets ?? Array.Empty<string>())
            {
                if (!IsOutfitPackagePath(path) || !Pending.Add(path)) continue;
                EditorApplication.delayCall += () => SynchronizeImportedPath(path);
            }
        }

        private static bool IsOutfitPackagePath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            const string root = "Assets/TestData/Wardrobe/";
            if (!normalized.StartsWith(root, StringComparison.Ordinal) || !normalized.EndsWith("/Outfit.asset", StringComparison.Ordinal)) return false;
            string relative = normalized.Substring(root.Length);
            string[] segments = relative.Split('/');
            return segments.Length == 2 && segments[1] == "Outfit.asset" && WardrobeCatalog.IsValidId(segments[0]);
        }

        private static void SynchronizeImportedPath(string path)
        {
            Pending.Remove(path);
            var imported = AssetDatabase.LoadAssetAtPath<WardrobeOutfitDefinition>(path);
            if (imported == null) return;
            string directoryId = path.Replace('\\', '/').Split('/')[3];
            if (!string.Equals(imported.id, directoryId, StringComparison.Ordinal))
            { Debug.LogWarning("Wardrobe import identity does not match its source directory: " + path); return; }
            foreach (string guid in AssetDatabase.FindAssets("t:WardrobeCatalog"))
            {
                string catalogPath = AssetDatabase.GUIDToAssetPath(guid);
                var catalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(catalogPath);
                if (catalog == null || catalog.IsReleased) continue;
                var configuration = catalog.AuthoringConfiguration;
                if (configuration == null && catalogPath == ClothingSetupBuilder.CatalogPath)
                    configuration = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(ClothingSetupBuilder.ConfigurationPath);
                if (configuration == null || !catalog.Presets.Any(p => p != null && p.Package != null &&
                    string.Equals(p.Package.id, imported.id, StringComparison.Ordinal))) continue;
                if (!WardrobeCandidateReimport.Synchronize(catalog, configuration, imported, out string error))
                { Debug.LogWarning("Wardrobe candidate reimport needs attention: " + error); continue; }
                AssetDatabase.SaveAssets();
            }
        }
    }
}
