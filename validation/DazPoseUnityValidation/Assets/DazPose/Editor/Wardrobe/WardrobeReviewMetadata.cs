using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Mutable authoring pointers to published outfit review scenes.</summary>
    public static class WardrobeReviewMetadata
    {
        public const string MetadataRelativePath = "ProjectSettings/DazPose/WardrobeReviewMetadata.json";
        private const int CurrentSchemaVersion = 1;

        [Serializable]
        public sealed class Document
        {
            public int schemaVersion = CurrentSchemaVersion;
            public Entry[] entries = Array.Empty<Entry>();
        }

        [Serializable]
        public sealed class Entry
        {
            public string packageId;
            public string releaseId;
            public string generationId;
            public string scenePath;
            public string publishedAtUtc;
        }

        [Serializable]
        private sealed class LegacyReport
        {
            public string id = string.Empty;
            public string definition = string.Empty;
            public string recipeHash = string.Empty;
            public string fbxHash = string.Empty;
            public string dufHash = string.Empty;
            public string publishedReviewScene = string.Empty;
        }

        public static bool TryResolve(WardrobeOutfitDefinition outfit, out string scenePath, out string error)
        {
            scenePath = null;
            error = null;
            if (!TryGetIdentity(outfit, out Entry identity, out error)) return false;

            string metadataPath = Path.Combine(ProjectRoot(), MetadataRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(metadataPath))
            {
                Document document;
                try { document = ReadDocument(metadataPath); }
                catch (Exception exception)
                {
                    error = "Wardrobe review metadata is invalid at " + MetadataRelativePath
                        + ". Repair or explicitly migrate it before reviewing outfits. " + exception.Message;
                    return false;
                }

                Entry match = document.entries.LastOrDefault(entry => SameIdentity(entry, identity));
                if (match == null && document.entries.Any(entry =>
                    string.Equals(entry.packageId, identity.packageId, StringComparison.Ordinal)
                    && string.Equals(NormalizeAssetPath(entry.releaseId), identity.releaseId, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "Published review metadata exists for " + identity.packageId + " but not this recipe/source generation. Publish or explicitly migrate the matching generation; no legacy pointer was substituted.";
                    return false;
                }
                if (match != null)
                {
                    if (!IsProjectAssetPath(match.scenePath) || !File.Exists(ProjectFile(match.scenePath)))
                    {
                        error = "The authoritative published review scene is missing: " + match.scenePath
                            + ". Metadata matched " + Describe(identity) + "; no older report pointer was substituted.";
                        return false;
                    }
                    scenePath = NormalizeAssetPath(match.scenePath);
                    return true;
                }
            }

            string reportPath = Path.Combine(ProjectRoot(), "TestOutput", "wardrobe-import", outfit.id, "report.json");
            if (!File.Exists(reportPath))
            {
                error = "No published review metadata or legacy report exists for " + Describe(identity)
                    + ". Run the explicit review metadata migration or publish the outfit.";
                return false;
            }

            LegacyReport report;
            try { report = JsonUtility.FromJson<LegacyReport>(File.ReadAllText(reportPath)); }
            catch (Exception exception)
            {
                error = "Could not read legacy review report " + reportPath + ": " + exception.Message;
                return false;
            }

            if (!Matches(report, identity))
            {
                error = "The legacy review report does not identify the currently loaded outfit generation: "
                    + Describe(identity) + ". Run the explicit metadata migration after checking the report.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(report.publishedReviewScene))
            {
                error = "The matching legacy report has no publishedReviewScene pointer: " + reportPath;
                return false;
            }
            if (!IsProjectAssetPath(report.publishedReviewScene) || !File.Exists(ProjectFile(report.publishedReviewScene)))
            {
                error = "The matching legacy published review scene is missing: " + report.publishedReviewScene;
                return false;
            }

            scenePath = NormalizeAssetPath(report.publishedReviewScene);
            return true;
        }

        /// <summary>Explicit, idempotent migration of currently published pointers from legacy reports.</summary>
        public static int MigrateLegacyReports()
        {
            string root = ProjectRoot();
            string metadataPath = Path.Combine(root, MetadataRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Document document = File.Exists(metadataPath) ? ReadDocument(metadataPath) : new Document();
            string reportRoot = Path.Combine(root, "TestOutput", "wardrobe-import");
            if (!Directory.Exists(reportRoot)) return 0;

            int added = 0;
            foreach (string reportPath in Directory.GetFiles(reportRoot, "report.json", SearchOption.AllDirectories))
            {
                LegacyReport report;
                try { report = JsonUtility.FromJson<LegacyReport>(File.ReadAllText(reportPath)); }
                catch (Exception exception)
                {
                    Debug.LogWarning("Skipped unreadable wardrobe review report " + reportPath + ": " + exception.Message);
                    continue;
                }

                if (!TryGetIdentity(report, out Entry identity) || string.IsNullOrWhiteSpace(report.publishedReviewScene))
                    continue;
                string scenePath = NormalizeAssetPath(report.publishedReviewScene);
                if (!IsProjectAssetPath(scenePath) || !File.Exists(ProjectFile(scenePath)))
                {
                    Debug.LogWarning("Skipped legacy review pointer whose scene is missing: " + scenePath);
                    continue;
                }

                bool identityAlreadyAuthoritative = document.entries.Any(entry => SameIdentity(entry, identity));
                if (identityAlreadyAuthoritative) continue;
                bool sameReviewAlreadyPresent = document.entries.Any(entry =>
                    string.Equals(NormalizeAssetPath(entry.scenePath), scenePath, StringComparison.OrdinalIgnoreCase));
                if (sameReviewAlreadyPresent) continue;

                identity.scenePath = scenePath;
                identity.publishedAtUtc = string.Empty;
                document.entries = document.entries.Concat(new[] { identity }).ToArray();
                added++;
            }

            if (added > 0) WriteDocument(metadataPath, document);
            return added;
        }

        private static bool TryGetIdentity(WardrobeOutfitDefinition outfit, out Entry entry, out string error)
        {
            entry = null;
            error = null;
            if (outfit == null) { error = "Select a WardrobeOutfitDefinition."; return false; }
            string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(outfit));
            if (string.IsNullOrWhiteSpace(outfit.id) || string.IsNullOrWhiteSpace(assetPath)
                || !HasGenerationHashes(outfit.recipeHash, outfit.sourceFbxHash, outfit.sourceDufHash))
            {
                error = "The outfit is missing its package, release path, or recipe/source hashes required to identify a published generation.";
                return false;
            }
            entry = MakeIdentity(outfit.id, assetPath, outfit.recipeHash, outfit.sourceFbxHash, outfit.sourceDufHash);
            return true;
        }

        private static bool TryGetIdentity(LegacyReport report, out Entry entry)
        {
            entry = null;
            if (report == null || string.IsNullOrWhiteSpace(report.definition)
                || !HasGenerationHashes(report.recipeHash, report.fbxHash, report.dufHash)) return false;
            entry = MakeIdentity(report.id, report.definition, report.recipeHash, report.fbxHash, report.dufHash);
            return !string.IsNullOrWhiteSpace(entry.packageId);
        }

        private static Entry MakeIdentity(string packageId, string releaseId, string recipeHash, string fbxHash, string dufHash)
        {
            return new Entry
            {
                packageId = packageId.Trim(),
                releaseId = NormalizeAssetPath(releaseId),
                // Existing import hashes form an identity key; this does not assert acceptance.
                generationId = recipeHash.Trim().ToLowerInvariant() + "|" + fbxHash.Trim().ToLowerInvariant()
                    + "|" + dufHash.Trim().ToLowerInvariant()
            };
        }

        private static bool Matches(LegacyReport report, Entry identity)
        {
            return TryGetIdentity(report, out Entry reportIdentity) && SameIdentity(reportIdentity, identity);
        }

        private static bool SameIdentity(Entry left, Entry right)
        {
            return left != null && right != null
                && string.Equals(left.packageId, right.packageId, StringComparison.Ordinal)
                && string.Equals(NormalizeAssetPath(left.releaseId), NormalizeAssetPath(right.releaseId), StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.generationId, right.generationId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasGenerationHashes(params string[] hashes)
        {
            return hashes.All(value => !string.IsNullOrWhiteSpace(value));
        }

        private static bool IsProjectAssetPath(string path)
        {
            string normalized = NormalizeAssetPath(path);
            return normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                && !normalized.Split('/').Any(part => part == "..");
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string ProjectFile(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot(), assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string Describe(Entry entry)
        {
            return entry.packageId + " / " + entry.releaseId + " / generation " + entry.generationId;
        }

        private static Document ReadDocument(string path)
        {
            Document document = JsonUtility.FromJson<Document>(File.ReadAllText(path));
            if (document == null || document.schemaVersion != CurrentSchemaVersion || document.entries == null)
                throw new InvalidDataException("Expected schemaVersion " + CurrentSchemaVersion + " and an entries array.");
            foreach (Entry entry in document.entries)
                if (entry == null || string.IsNullOrWhiteSpace(entry.packageId) || string.IsNullOrWhiteSpace(entry.releaseId)
                    || string.IsNullOrWhiteSpace(entry.generationId) || !IsProjectAssetPath(entry.releaseId) || !IsProjectAssetPath(entry.scenePath))
                    throw new InvalidDataException("Every entry must include packageId, releaseId, generationId, and scenePath.");
            return document;
        }

        private static void WriteDocument(string path, Document document)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(document, true));
        }
    }
}