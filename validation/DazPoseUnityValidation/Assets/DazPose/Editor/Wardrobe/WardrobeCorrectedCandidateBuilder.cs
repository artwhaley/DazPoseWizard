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
        public sealed class Result { public WardrobeCatalog catalog; public WardrobeConfiguration configuration; public string message; }
        public static Result Prepare(WardrobeCatalog released, WardrobeConfiguration releasedConfiguration)
        {
            if (released == null || releasedConfiguration == null || !released.IsReleased)
                throw new InvalidOperationException("Select production Lara's released catalog and matching configuration.");
            if (releasedConfiguration.GenerationHash != released.ReleasedGenerationId)
                throw new InvalidOperationException("Production catalog/configuration generation mismatch: " + released.ReleasedGenerationId + " vs " + releasedConfiguration.GenerationHash);
            if (AssetDatabase.IsValidFolder(CandidateRoot))
            {
                var existingCatalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(CandidateRoot + "/Catalog.asset");
                var existingConfig = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(CandidateRoot + "/Configuration.asset");
                if (existingCatalog != null && existingConfig != null && IsCorrect(existingCatalog, existingConfig))
                    return new Result { catalog = existingCatalog, configuration = existingConfig, message = "Reused matching diagnostic candidate; no files overwritten." };
                throw new IOException("Candidate output root already exists with different or incomplete content; preserved without overwrite: " + CandidateRoot);
            }
            var sourcePresets = released.Presets;
            if (sourcePresets.Length != 3) throw new InvalidDataException("Production wardrobe catalog must retain exactly three preset entries; found " + sourcePresets.Length + ".");
            var sourceFirst = sourcePresets.SingleOrDefault(x => x.PresetId == "first-outfit");
            if (sourceFirst == null || sourceFirst.Package == null) throw new InvalidDataException("Production wardrobe catalog does not contain required preset/package first-outfit.");
            var sourceDress = sourceFirst.Package.pieces.SingleOrDefault(x => string.Equals(x.role, "dress", StringComparison.OrdinalIgnoreCase));
            var sourcePanties = sourceFirst.Package.pieces.SingleOrDefault(x => string.Equals(x.role, "panties", StringComparison.OrdinalIgnoreCase));
            if (sourceDress == null || sourcePanties == null) throw new InvalidDataException("first-outfit must identify exactly one dress and one panties garment by role.");
            if (!releasedConfiguration.Assignments.Any(x => x.presetId == "first-outfit" && x.sourcePieceId == sourceDress.id) ||
                !releasedConfiguration.Assignments.Any(x => x.presetId == "first-outfit" && x.sourcePieceId == sourcePanties.id))
                throw new InvalidDataException("Production configuration is missing the first-outfit dress or panties assignment.");
            EnsureFolder(CandidateRoot + "/Presets");
            var presets = released.Presets.Select(source =>
            {
                var clone = UnityEngine.Object.Instantiate(source);
                clone.name = source.PresetId + " (Corrected diagnostic candidate)";
                AssetDatabase.CreateAsset(clone, CandidateRoot + "/Presets/" + source.PresetId + ".asset");
                return clone;
            }).ToArray();
            var catalog = UnityEngine.Object.Instantiate(released);
            catalog.name = "Corrected First Outfit Diagnostic Catalog";
            catalog.ConfigureCandidate(presets);
            AssetDatabase.CreateAsset(catalog, CandidateRoot + "/Catalog.asset");
            var config = UnityEngine.Object.Instantiate(releasedConfiguration);
            config.name = "Corrected First Outfit Diagnostic Configuration";
            var assignments = config.Assignments;
            foreach (var row in assignments.Where(x => x.presetId == "first-outfit"))
            {
                if (row.sourcePieceId == sourceDress.id) row.layer = 2;
                else if (row.sourcePieceId == sourcePanties.id) row.layer = 1;
            }
            if (!config.TrySetWorkingAssignments(assignments, out string error)) throw new InvalidDataException("Corrected assignments are invalid: " + error);
            config.ConfigureGenerated(assignments, config.MaterialOverrides, config.FootwearOverrides);
            string draftHash = WardrobeCandidateWorkflow.DraftHash(config);
            config.CommitRevision(draftHash, releasedConfiguration.InputHashes);
            catalog.SetAuthoringConfiguration(config);
            catalog.SetCompiledConfigurationHash(string.Empty);
            AssetDatabase.CreateAsset(config, CandidateRoot + "/Configuration.asset");
            AssetDatabase.SaveAssets();
            return new Result { catalog = catalog, configuration = config, message = "Forked all " + presets.Length + " production presets; changed only first-outfit dress=2 and panties=1." };
        }
        private static bool IsCorrect(WardrobeCatalog catalog, WardrobeConfiguration config)
        {
            if (catalog.Presets.Length != 3 || catalog.IsReleased) return false;
            var rows = config.Assignments.Where(x => x.presetId == "first-outfit").ToArray();
            var preset = catalog.Presets.SingleOrDefault(x => x.PresetId == "first-outfit");
            if (preset == null) return false;
            var dress = preset.Package.pieces.SingleOrDefault(x => string.Equals(x.role, "dress", StringComparison.OrdinalIgnoreCase));
            var panties = preset.Package.pieces.SingleOrDefault(x => string.Equals(x.role, "panties", StringComparison.OrdinalIgnoreCase));
            return dress != null && panties != null && rows.Any(x => x.sourcePieceId == dress.id && x.layer == 2) && rows.Any(x => x.sourcePieceId == panties.id && x.layer == 1);
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
