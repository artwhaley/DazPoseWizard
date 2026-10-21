using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Editor.Importing;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    [Serializable]
    internal sealed class DazPoseAlwaysExportValidationReport
    {
        public string validatedAtUtc;
        public string referenceModelAssetPath;
        public int enabledPinCount;
        public int presentCount;
        public int missingCount;
        public int ambiguousCount;
        public DazPoseAlwaysExportValidationItem[] items = Array.Empty<DazPoseAlwaysExportValidationItem>();
    }

    [Serializable]
    internal sealed class DazPoseAlwaysExportValidationItem
    {
        public string name;
        public string category;
        public string purpose;
        public string status;
        public string message;
        public string[] matches = Array.Empty<string>();
    }

    internal static class DazPoseAlwaysExportMorphValidator
    {
        private const string ManifestRelativePath = ".dazposewizard/required-morphs.json";
        private const string ReportRelativePath = "TestOutput/always-export-morph-validation.json";

        private sealed class Match
        {
            public SkinnedMeshRenderer Renderer;
            public int ShapeIndex;
            public string ShapeName;
            public string RendererPath;
        }

        [MenuItem("Tools/DAZ Pose/Validate Always-Export Morphs")]
        private static void ValidateConfiguredAlwaysExportMorphs()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var manifestPath = Path.Combine(projectRoot, ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("Always-export morph validation could not find the project manifest: " + manifestPath);
                return;
            }

            DazPoseRequiredMorphManifest manifest;
            try { manifest = JsonUtility.FromJson<DazPoseRequiredMorphManifest>(File.ReadAllText(manifestPath)); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Debug.LogError("Always-export morph validation could not read the project manifest: " + exception.Message);
                return;
            }
            if (manifest == null || (manifest.schemaVersion != 1 && manifest.schemaVersion != 2) || manifest.items == null)
            {
                Debug.LogError("Always-export morph validation found an unsupported manifest schema: " + manifestPath);
                return;
            }

            var referenceModel = DazPosePipelineSettings.LoadReferenceModel(out var assetPath);
            if (referenceModel == null)
            {
                Debug.LogError("Always-export morph validation requires a refreshed reference model in Tools > DAZ Pose > Pipeline Settings.");
                return;
            }

            var renderers = referenceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .OrderBy(renderer => AnimationUtility.CalculateTransformPath(renderer.transform, referenceModel.transform), StringComparer.Ordinal)
                .ToArray();
            var prefixes = GetFigurePrefixes(referenceModel.transform);
            var bodyRenderers = renderers.Where(renderer => IsBodyRenderer(renderer, referenceModel.transform, prefixes)).ToArray();
            var pins = manifest.items.Where(item => item != null && item.alwaysExport && !string.IsNullOrWhiteSpace(item.category)
                    && !string.IsNullOrWhiteSpace(item.name))
                .GroupBy(item => item.name, StringComparer.Ordinal).Select(group => group.First())
                .OrderBy(item => item.category, StringComparer.Ordinal).ThenBy(item => item.name, StringComparer.Ordinal).ToArray();

            var report = new DazPoseAlwaysExportValidationReport
            {
                validatedAtUtc = DateTime.UtcNow.ToString("O"),
                referenceModelAssetPath = assetPath ?? string.Empty,
                enabledPinCount = pins.Length,
                items = pins.Select(item => ValidateItem(item, referenceModel.transform, renderers, bodyRenderers, prefixes)).ToArray()
            };
            report.presentCount = report.items.Count(item => item.status == "Present");
            report.missingCount = report.items.Count(item => item.status == "Missing");
            report.ambiguousCount = report.items.Count(item => item.status == "Ambiguous");

            var reportPath = Path.Combine(projectRoot, ReportRelativePath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Debug.LogError("Always-export morph validation could not write its report: " + exception.Message);
                return;
            }

            var missingNames = report.items.Where(item => item.status == "Missing").Select(item => item.name).ToArray();
            var ambiguousNames = report.items.Where(item => item.status == "Ambiguous").Select(item => item.name).ToArray();
            var summary = "Always-export morph validation: " + report.enabledPinCount + " enabled pin(s), "
                + report.presentCount + " Present, " + report.missingCount + " Missing, " + report.ambiguousCount
                + " Ambiguous. Missing: " + (missingNames.Length == 0 ? "none" : string.Join(", ", missingNames))
                + ". Ambiguous: " + (ambiguousNames.Length == 0 ? "none" : string.Join(", ", ambiguousNames))
                + ". Report: " + ReportRelativePath;
            if (report.missingCount > 0 || report.ambiguousCount > 0) Debug.LogWarning(summary);
            else Debug.Log(summary);
        }

        private static DazPoseAlwaysExportValidationItem ValidateItem(DazPoseRequiredMorphManifestItem item,
            Transform modelRoot, SkinnedMeshRenderer[] renderers, SkinnedMeshRenderer[] bodyRenderers, HashSet<string> prefixes)
        {
            var bodyOnly = !string.Equals(item.category, "Manual", StringComparison.Ordinal);
            var searchRenderers = bodyOnly ? bodyRenderers : renderers;
            var matches = FindMatches(searchRenderers, modelRoot, item.name, prefixes);
            var otherMatches = bodyOnly && matches.Count == 0 ? FindMatches(renderers.Except(bodyRenderers).ToArray(), modelRoot, item.name, prefixes) : new List<Match>();
            var duplicate = matches.GroupBy(match => match.Renderer).FirstOrDefault(group => group.Count() > 1);
            var status = duplicate != null ? "Ambiguous" : matches.Count > 0 ? "Present" : "Missing";
            var message = status switch
            {
                "Present" => matches.Count > 1 ? "Matched on multiple renderers; this is allowed." : "Matched on the configured model.",
                "Ambiguous" => "The same renderer contains multiple matching blendshapes.",
                _ when bodyOnly && otherMatches.Count > 0 => "A matching shape exists only on a non-body renderer; body categories require the figure body.",
                _ when bodyOnly && bodyRenderers.Length == 0 => "No body renderer could be identified on the configured reference model.",
                _ => "No matching blendshape exists on the configured reference model. Refresh the Lara FBX after updating its DAZ export rules."
            };
            return new DazPoseAlwaysExportValidationItem
            {
                name = item.name,
                category = item.category ?? string.Empty,
                purpose = item.purpose ?? string.Empty,
                status = status,
                message = message,
                matches = matches.Select(match => match.RendererPath + " :: " + match.ShapeName).ToArray()
            };
        }

        private static List<Match> FindMatches(IEnumerable<SkinnedMeshRenderer> renderers, Transform modelRoot, string controlName,
            HashSet<string> prefixes)
        {
            var exact = new List<Match>();
            var prefixed = new List<Match>();
            foreach (var renderer in renderers)
            {
                var mesh = renderer.sharedMesh;
                for (var index = 0; index < mesh.blendShapeCount; index++)
                {
                    var name = mesh.GetBlendShapeName(index);
                    var match = new Match
                    {
                        Renderer = renderer,
                        ShapeIndex = index,
                        ShapeName = name,
                        RendererPath = AnimationUtility.CalculateTransformPath(renderer.transform, modelRoot)
                    };
                    if (string.Equals(name, controlName, StringComparison.Ordinal)) exact.Add(match);
                    else if (prefixes.Any(prefix => string.Equals(name, prefix + "__" + controlName, StringComparison.Ordinal))) prefixed.Add(match);
                }
            }
            return exact.Count > 0 ? exact : prefixed;
        }

        private static HashSet<string> GetFigurePrefixes(Transform modelRoot)
        {
            var prefixes = new HashSet<string>(StringComparer.Ordinal);
            if (modelRoot.name.StartsWith("Genesis", StringComparison.Ordinal)) prefixes.Add(modelRoot.name);
            foreach (Transform child in modelRoot)
                if (child.name.StartsWith("Genesis", StringComparison.Ordinal)) prefixes.Add(child.name);
            return prefixes;
        }

        private static bool IsBodyRenderer(SkinnedMeshRenderer renderer, Transform modelRoot, HashSet<string> prefixes)
        {
            var rendererName = renderer.name;
            var meshName = renderer.sharedMesh.name;
            // Do not classify all descendants of the Genesis node as body: clothing
            // and hair are also commonly parented under that node in a DAZ FBX.
            return prefixes.Contains(rendererName) || prefixes.Contains(meshName)
                || prefixes.Any(prefix => string.Equals(rendererName, prefix + ".Shape", StringComparison.Ordinal)
                    || string.Equals(meshName, prefix + ".Shape", StringComparison.Ordinal));
        }
    }
}
