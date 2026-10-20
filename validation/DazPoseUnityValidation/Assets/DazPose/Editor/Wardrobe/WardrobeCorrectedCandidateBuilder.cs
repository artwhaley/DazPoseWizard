using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Forks the production release/configuration into a diagnostic candidate and changes only outfit 1 layers.</summary>
    public static class WardrobeCorrectedCandidateBuilder
    {
        public const string CandidateRoot = "Assets/__WardrobeProtectedCards/Candidates/CorrectedFirstOutfit";
        public const string CandidateParent = "Assets/__WardrobeProtectedCards/Candidates";
        // The W00 source audit records these stable IDs and garment meanings. Imported role labels
        // are recipe vocabulary and are not guaranteed to use the user-facing garment names.
        private const string FirstOutfitDressId = "PeekaBoo_16572";
        private const string FirstOutfitPantiesId = "CharlenesCloset_Panty_1981";
        public sealed class Result { public WardrobeCatalog catalog; public WardrobeConfiguration configuration; public string message; }
        public static Result Prepare(WardrobeCatalog released, WardrobeConfiguration releasedConfiguration)
        {
            if (released == null || releasedConfiguration == null || !released.IsReleased)
                throw new InvalidOperationException("Select production Lara's released catalog and matching configuration.");
            if (releasedConfiguration.GenerationHash != released.ReleasedGenerationId)
                throw new InvalidOperationException("Production catalog/configuration generation mismatch: " + released.ReleasedGenerationId + " vs " + releasedConfiguration.GenerationHash);
            if (TryRecover(released, releasedConfiguration, out var recovered)) return recovered;
            var sourcePresets = released.Presets;
            if (sourcePresets.Length != 3) throw new InvalidDataException("Production wardrobe catalog must retain exactly three preset entries; found " + sourcePresets.Length + ".");
            var sourceFirst = sourcePresets.SingleOrDefault(x => x.PresetId == "first-outfit");
            if (sourceFirst == null || sourceFirst.Package == null) throw new InvalidDataException("Production wardrobe catalog does not contain required preset/package first-outfit.");
            var sourceDress = RequirePiece(sourceFirst, FirstOutfitDressId, "dress");
            var sourcePanties = RequirePiece(sourceFirst, FirstOutfitPantiesId, "panties");
            RequireSingleAssignment(releasedConfiguration, sourceDress.id, "dress");
            RequireSingleAssignment(releasedConfiguration, sourcePanties.id, "panties");
            // Publication advances this catalog in place. Both a corrected draft and its published
            // release are resumable. Incomplete/unrelated outputs are preserved at their old paths.
            string root = AssetDatabase.GenerateUniqueAssetPath(CandidateRoot);
            var config = UnityEngine.Object.Instantiate(releasedConfiguration);
            config.name = "Corrected First Outfit Diagnostic Configuration";
            var assignments = config.Assignments;
            foreach (var row in assignments.Where(x => x.presetId == "first-outfit"))
            {
                if (row.sourcePieceId == sourceDress.id) row.layer = 2;
                else if (row.sourcePieceId == sourcePanties.id) row.layer = 1;
            }
            if (!config.TrySetWorkingAssignments(assignments, out string error))
            { UnityEngine.Object.DestroyImmediate(config); throw new InvalidDataException("Corrected assignments are invalid: " + error); }
            config.ConfigureGenerated(assignments, config.MaterialOverrides, config.FootwearOverrides);
            string draftHash = WardrobeCandidateWorkflow.DraftHash(config);
            config.CommitRevision(draftHash, releasedConfiguration.InputHashes);
            EnsureFolder(root + "/Presets");
            var presets = released.Presets.Select(source =>
            {
                var clone = UnityEngine.Object.Instantiate(source);
                clone.name = source.PresetId + " (Corrected diagnostic candidate)";
                AssetDatabase.CreateAsset(clone, root + "/Presets/" + source.PresetId + ".asset");
                return clone;
            }).ToArray();
            var catalog = UnityEngine.Object.Instantiate(released);
            catalog.name = "Corrected First Outfit Diagnostic Catalog";
            catalog.ConfigureCandidate(presets);
            AssetDatabase.CreateAsset(catalog, root + "/Catalog.asset");
            catalog.SetAuthoringConfiguration(config);
            catalog.SetCompiledConfigurationHash(string.Empty);
            AssetDatabase.CreateAsset(config, root + "/Configuration.asset");
            AssetDatabase.SaveAssets();
            return new Result { catalog = catalog, configuration = config, message = "Prepared " + root + "; retained all " + presets.Length + " production presets; corrected dress (" + sourceDress.id + ")=2 and panties (" + sourcePanties.id + ")=1. Earlier outputs were preserved." };
        }
        public static bool TryRecover(WardrobeCatalog released, WardrobeConfiguration releasedConfiguration, out Result result)
        {
            result = null;
            if (released == null || releasedConfiguration == null || !AssetDatabase.IsValidFolder(CandidateParent)) return false;
            var matches = AssetDatabase.FindAssets("t:WardrobeCatalog", new[] { CandidateParent })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new { path, catalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(path),
                    config = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(Path.GetDirectoryName(path).Replace('\\', '/') + "/Configuration.asset") })
                .Where(item => IsCorrect(item.catalog, item.config, released, releasedConfiguration))
                .OrderByDescending(item => item.catalog.IsReleased).ToArray();
            if (matches.Length == 0) return false;
            var match = matches[0];
            result = new Result { catalog = match.catalog, configuration = match.config,
                message = "Recovered corrected " + (match.catalog.IsReleased ? "published release " + match.catalog.ReleasedGenerationId : "editable candidate") +
                    " at " + match.path + ". Existing assets retained; publication is skipped for an existing release." };
            return true;
        }
        private static bool IsCorrect(WardrobeCatalog catalog, WardrobeConfiguration config,
            WardrobeCatalog source, WardrobeConfiguration sourceConfig)
        {
            if (catalog == null || config == null || catalog == source || !catalog.Validate(out _) ||
                catalog.Presets.Length != source.Presets.Length || config.SchemaVersion != sourceConfig.SchemaVersion) return false;
            if (!WardrobeAppearanceDeliveryWorkflow.CanRecover(catalog.IsReleased, catalog.ReleasedGenerationId,
                config.GenerationHash, catalog.AuthoringConfiguration == config)) return false;
            var expected = sourceConfig.Assignments;
            foreach (var row in expected.Where(row => row != null && row.presetId == "first-outfit"))
            { if (row.sourcePieceId == FirstOutfitDressId) row.layer = 2; else if (row.sourcePieceId == FirstOutfitPantiesId) row.layer = 1; }
            Func<WardrobeConfiguration.PieceAssignment, string> key = row => row == null ? "<null>" : row.presetId + "/" + row.sourcePieceId + ":" + row.layer;
            if (!expected.Select(key).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(config.Assignments.Select(key).OrderBy(x => x, StringComparer.Ordinal))) return false;
            foreach (var original in source.Presets)
            {
                var preset = catalog.Presets.SingleOrDefault(item => item.PresetId == original.PresetId);
                if (preset == null || preset.CharacterSignature != original.CharacterSignature || preset.Package == null || original.Package == null ||
                    preset.Package.sourceFbxHash != original.Package.sourceFbxHash || preset.Package.sourceDufHash != original.Package.sourceDufHash ||
                    preset.Package.recipeHash != original.Package.recipeHash || preset.HairAction != original.HairAction || preset.HairId != original.HairId) return false;
                if (!preset.Package.pieces.Select(piece => piece.id).OrderBy(x => x, StringComparer.Ordinal)
                    .SequenceEqual(original.Package.pieces.Select(piece => piece.id).OrderBy(x => x, StringComparer.Ordinal))) return false;
                if (catalog.IsReleased && preset.Parts.Any(part => !expected.Any(row => row.presetId == preset.PresetId && row.sourcePieceId == part.sourcePieceId && row.layer == part.layer))) return false;
            }
            var first = catalog.Presets.SingleOrDefault(item => item.PresetId == "first-outfit");
            return FindPiece(first, FirstOutfitDressId) != null && FindPiece(first, FirstOutfitPantiesId) != null;
        }
        private static WardrobeOutfitDefinition.Piece RequirePiece(WardrobePreset preset, string pieceId, string meaning)
        {
            var matches = (preset.Package.pieces ?? Array.Empty<WardrobeOutfitDefinition.Piece>())
                .Where(piece => piece != null && string.Equals(piece.id, pieceId, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
            {
                string available = string.Join(", ", (preset.Package.pieces ?? Array.Empty<WardrobeOutfitDefinition.Piece>())
                    .Where(piece => piece != null).Select(piece => piece.id + " [role=" + piece.role + "]"));
                throw new InvalidDataException("first-outfit expected exactly one " + meaning + " with stable source ID '" + pieceId + "'; found " + matches.Length + ". Available pieces: " + available);
            }
            if (string.Equals(matches[0].role, "hair", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("first-outfit source ID '" + pieceId + "' is classified as hair, but the W00 baseline records it as " + meaning + ".");
            return matches[0];
        }
        private static WardrobeOutfitDefinition.Piece FindPiece(WardrobePreset preset, string pieceId)
        {
            if (preset == null || preset.Package == null || preset.Package.pieces == null) return null;
            var matches = preset.Package.pieces.Where(piece => piece != null && piece.id == pieceId).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        private static void RequireSingleAssignment(WardrobeConfiguration config, string pieceId, string meaning)
        {
            int count = config.Assignments.Count(row => row.presetId == "first-outfit" && row.sourcePieceId == pieceId);
            if (count != 1)
                throw new InvalidDataException("Production configuration must contain exactly one first-outfit " + meaning + " assignment for '" + pieceId + "'; found " + count + ".");
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))) && !AssetDatabase.IsValidFolder(path))
                throw new IOException("Could not create diagnostic candidate folder: " + path);
        }
    }
}
