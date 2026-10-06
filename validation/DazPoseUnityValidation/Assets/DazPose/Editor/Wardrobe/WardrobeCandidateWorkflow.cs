using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Durable drafts and explicitly compiled candidates. No runtime binding is changed here.</summary>
    public static class WardrobeCandidateWorkflow
    {
        public static bool TrySaveDraft(WardrobeCatalog catalog, WardrobeConfiguration configuration,
            WardrobeEditSession session, SkinnedMeshRenderer body, out WardrobeCatalog savedCatalog,
            out WardrobeConfiguration savedConfiguration, out string error)
        {
            savedCatalog = catalog; savedConfiguration = configuration; error = null;
            if (catalog == null || configuration == null || session == null)
            { error = "Catalog, configuration and working session are required."; return false; }
            var working = UnityEngine.Object.Instantiate(configuration);
            try
            {
                if (!working.TrySetWorkingAssignments(session.Assignments, out error)) return false;
                working.ConfigureGenerated(session.Assignments, session.MaterialOverrides, session.FootwearOverrides);
                foreach (var preset in catalog.Presets) WardrobeCandidateSnapshot.Resolve(preset, working);
                string hash = DraftHash(working);
                if (catalog.IsReleased)
                {
                    // Each fork has its own mutable presets and configuration. Release references remain untouched.
                    string folder = "Assets/Wardrobe/Candidates/Drafts/" + Guid.NewGuid().ToString("N");
                    EnsureFolder(folder + "/Presets");
                    var presets = catalog.Presets.Select(p =>
                    {
                        var copy = UnityEngine.Object.Instantiate(p);
                        AssetDatabase.CreateAsset(copy, folder + "/Presets/" + p.PresetId + ".asset");
                        return copy;
                    }).ToArray();
                    savedCatalog = ScriptableObject.CreateInstance<WardrobeCatalog>();
                    savedCatalog.ConfigureCandidate(presets);
                    AssetDatabase.CreateAsset(savedCatalog, folder + "/Catalog.asset");
                    savedConfiguration = UnityEngine.Object.Instantiate(configuration);
                    AssetDatabase.CreateAsset(savedConfiguration, folder + "/Configuration.asset");
                }
                string prior = EditorJsonUtility.ToJson(savedConfiguration);
                string priorCatalog = EditorJsonUtility.ToJson(savedCatalog);
                try
                {
                    savedConfiguration.ConfigureGenerated(working.Assignments, working.MaterialOverrides, working.FootwearOverrides);
                    savedConfiguration.CommitRevision(hash, configuration.InputHashes);
                    savedCatalog.SetAuthoringConfiguration(savedConfiguration);
                    savedCatalog.SetCompiledConfigurationHash(string.Empty);
                    EditorUtility.SetDirty(savedCatalog);
                    EditorUtility.SetDirty(savedConfiguration);
                    AssetDatabase.SaveAssets();
                }
                catch
                { EditorJsonUtility.FromJsonOverwrite(prior, savedConfiguration); EditorJsonUtility.FromJsonOverwrite(priorCatalog, savedCatalog); throw; }
                session.MarkCommitted();
                return true;
            }
            catch (Exception exception) { error = "Draft save failed: " + exception.Message; return false; }
            finally { UnityEngine.Object.DestroyImmediate(working); }
        }

        public static bool TryCompile(WardrobeCatalog catalog, WardrobeConfiguration configuration,
            SkinnedMeshRenderer body, out string error)
        {
            error = null;
            if (catalog == null || configuration == null || body == null || body.sharedMesh == null)
            { error = "Open the setup scene with its canonical body before compiling."; return false; }
            if (catalog.IsReleased) { error = "Save a working draft before compiling a released outfit."; return false; }
            var resolved = new Dictionary<WardrobePreset, WardrobeCandidateSnapshot>();
            var compiled = new Dictionary<WardrobePreset, WardrobeFitState[]>();
            // Unique attempt roots prevent a failed compile overwriting fit assets still used by a prior binding.
            string root = "Assets/TestData/WardrobeRuntime/DraftAttempts/" + Guid.NewGuid().ToString("N");
            try
            {
                foreach (var preset in catalog.Presets)
                {
                    resolved[preset] = WardrobeCandidateSnapshot.Resolve(preset, configuration);
                    EditorUtility.DisplayProgressBar("Compile wardrobe draft", preset.DisplayName,
                        (float)compiled.Count / Math.Max(1, catalog.Presets.Length));
                    var fit = WardrobeFitCompiler.Compile(preset, configuration, body, root, false);
                    if (fit.needsFitStates != 0) throw new InvalidOperationException(string.Join("; ", fit.issues));
                    if (fit.populatedMask != resolved[preset].PopulatedMask ||
                        !fit.reachableMasks.SequenceEqual(resolved[preset].ReachableMasks) ||
                        !fit.fitStates.Select(s => s.VisibleMask).OrderBy(x => x).SequenceEqual(resolved[preset].ReachableMasks.OrderBy(x => x)))
                        throw new InvalidOperationException("Resolved layers and compiled masks disagree: " + preset.PresetId);
                    compiled[preset] = fit.fitStates;
                }
                var prior = catalog.Presets.ToDictionary(p => p, p => EditorJsonUtility.ToJson(p));
                string priorCatalog = EditorJsonUtility.ToJson(catalog);
                string priorConfiguration = EditorJsonUtility.ToJson(configuration);
                try
                {
                    foreach (var preset in catalog.Presets)
                    {
                        preset.ConfigureGenerated(preset.PresetId, preset.DisplayName, preset.CharacterSignature,
                            configuration.Revision, preset.Package, resolved[preset].Parts, preset.Aliases,
                            preset.HairAction, preset.HairId, preset.Footwear);
                        preset.SetGeneratedFitStates(compiled[preset]);
                        EditorUtility.SetDirty(preset);
                    }
                    catalog.SetCompiledConfigurationHash(CompilationHash(catalog, configuration));
                    catalog.SetAuthoringConfiguration(configuration);
                    string[] inputHashes = configuration.InputHashes;
                    foreach (var package in catalog.Presets.Select(p => p.Package).Distinct())
                    {
                        string prefix = "import:" + package.id + ":";
                        inputHashes = inputHashes.Where(h => h == null || !h.StartsWith(prefix, StringComparison.Ordinal))
                            .Concat(new[] { WardrobeCandidateReimport.ComputeInputEntry(package) }).ToArray();
                    }
                    configuration.SetInputHashesWithoutRevision(inputHashes);
                    EditorUtility.SetDirty(configuration);
                    EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
                }
                catch
                {
                    foreach (var entry in prior) EditorJsonUtility.FromJsonOverwrite(entry.Value, entry.Key);
                    EditorJsonUtility.FromJsonOverwrite(priorCatalog, catalog);
                    EditorJsonUtility.FromJsonOverwrite(priorConfiguration, configuration);
                    throw;
                }
                return true;
            }
            catch (Exception exception)
            { error = "Compile failed; draft is saved and the previous binding is retained. " + exception.Message + " (attempt: " + root + ")"; return false; }
            finally { EditorUtility.ClearProgressBar(); }
        }

        public static bool IsCompiled(WardrobeCatalog catalog, WardrobeConfiguration configuration)
        {
            if (catalog == null || configuration == null || catalog.IsReleased) return false;
            try
            {
                if (catalog.CompiledConfigurationHash != CompilationHash(catalog, configuration)) return false;
                return catalog.Presets.All(p => p.ConfigurationRevision == configuration.Revision &&
                    WardrobeCandidateSnapshot.Resolve(p, configuration).ReachableMasks.OrderBy(x => x)
                        .SequenceEqual(p.FitStates.Where(f => f.Status == WardrobeFitStatus.Validated).Select(f => f.VisibleMask).OrderBy(x => x)));
            }
            catch { return false; }
        }

        public static string DraftHash(WardrobeConfiguration configuration)
        {
            string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);
            var text = new StringBuilder("wardrobe-draft-v2|");
            foreach (var row in configuration.Assignments.OrderBy(x => x.presetId, StringComparer.Ordinal).ThenBy(x => x.sourcePieceId, StringComparer.Ordinal))
                text.Append(row.presetId).Append('/').Append(row.sourcePieceId).Append(':').Append(row.layer).Append('|');
            foreach (var row in configuration.MaterialOverrides.OrderBy(x => x.presetId, StringComparer.Ordinal).ThenBy(x => x.sourcePieceId, StringComparer.Ordinal).ThenBy(x => x.materialSlotId, StringComparer.Ordinal))
            {
                string path = row.material != null ? AssetDatabase.GetAssetPath(row.material) : string.Empty;
                text.Append(row.presetId).Append('/').Append(row.sourcePieceId).Append('/').Append(row.materialSlotId)
                    .Append(':').Append(path).Append(':').Append(string.IsNullOrEmpty(path) ? "default" : AssetDatabase.GetAssetDependencyHash(path).ToString())
                    .Append(':').Append(row.opacityOverride).Append(':').Append(Float(row.opacity)).Append('|');
            }
            foreach (var row in configuration.FootwearOverrides.OrderBy(x => x.presetId, StringComparer.Ordinal))
                text.Append(row.presetId).Append(':').Append(row.standingHeightOverride).Append(':').Append(Float(row.standingHeight))
                    .Append(':').Append(row.footShrinkOverride).Append(':').Append(Float(row.footShrink)).Append('|');
            return Hash(text.ToString());
        }

        private static string CompilationHash(WardrobeCatalog catalog, WardrobeConfiguration config) => Hash(DraftHash(config) + "|" + config.Revision + "|" +
            string.Join("|", catalog.Presets.OrderBy(p => p.PresetId, StringComparer.Ordinal).Select(p =>
                p.PresetId + ":" + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(p.Package)) + ":" +
                (p.Footwear != null ? AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(p.Footwear)).ToString() : "barefoot"))));
        private static string Hash(string text)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash <= 0) throw new InvalidOperationException("Invalid Assets folder: " + path);
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
