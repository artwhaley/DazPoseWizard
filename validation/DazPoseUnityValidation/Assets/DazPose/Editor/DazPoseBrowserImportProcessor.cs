using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DazPose.Performer;
using DazPose.UnityValidation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.Importing
{
    [Serializable]
    internal sealed class DazPoseProjectBridge
    {
        public int schemaVersion;
        public string importRoot;
        public string outputRoot;
        public string poseImportRoot;
        public string poseOutputRoot;
        public string expressionImportRoot;
        public string expressionOutputRoot;
    }

    [Serializable]
    internal sealed class DazPoseBrowserJobStatus
    {
        public int schemaVersion = 2;
        public string assetKind;
        public string canonicalImportPath;
        public string sourcePosePath;
        public string destinationRelativeFolder;
        public string expectedAnimPath;
        public string expectedPerformerPosePath;
        public string expectedWrapperAssetPath;
        public string state;
        public string timestamp;
        public string errorMessage;
        public string canonicalContentHash;
    }

    [InitializeOnLoad]
    internal static class DazPoseBrowserImportQueue
    {
        private const string CanonicalSuffix = ".dazpose.json";
        private static readonly Queue<string> Pending = new Queue<string>();
        private static readonly HashSet<string> Queued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> RetryFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _pumpInstalled;
        private static bool _processing;

        static DazPoseBrowserImportQueue()
        {
            EditorApplication.delayCall += ReconcileAtStartup;
        }

        [MenuItem("Tools/DAZ Pose/Reconcile Pending Pose Imports")]
        private static void ProcessPendingBrowserImports()
        {
            Reconcile(true, true);
        }

        internal static void OnAssetsImported(string[] importedAssets)
        {
            if (importedAssets == null || importedAssets.Length == 0) return;
            DazPoseProjectBridge bridge;
            if (!TryLoadBridge(out bridge, false)) return;
            string poseImportRoot, poseOutputRoot, expressionImportRoot, expressionOutputRoot;
            try { ValidateBridge(bridge, out poseImportRoot, out poseOutputRoot, out expressionImportRoot, out expressionOutputRoot); }
            catch (Exception exception)
            {
                Debug.LogError("DAZ Pose browser import is paused because DazPoseWizard.project.json is invalid: " + exception.Message);
                return;
            }

            var assetPaths = new List<string>(importedAssets.Length);
            foreach (var importedPath in importedAssets)
            {
                if (TryNormalizeImportedAssetPath(importedPath, out var assetPath))
                    assetPaths.Add(assetPath);
            }

            var referenceModel = DazPosePipelineSettings.LoadReferenceModel(out var referenceAssetPath);
            var referenceWasImported = referenceModel != null && assetPaths.Any(assetPath =>
                string.Equals(assetPath, referenceAssetPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));

            foreach (var assetPath in assetPaths)
            {
                if (assetPath.EndsWith(CanonicalSuffix, StringComparison.OrdinalIgnoreCase)
                    && (IsWithinRoot(assetPath, poseImportRoot)
                        || (!string.IsNullOrEmpty(expressionImportRoot) && IsWithinRoot(assetPath, expressionImportRoot)))) Enqueue(assetPath, false);
            }
            if (referenceWasImported) Reconcile(false, true);
        }

        private static void ReconcileAtStartup()
        {
            Reconcile(false, false);
        }

        private static void Reconcile(bool logMissingConfiguration, bool retryFailed)
        {
            DazPoseProjectBridge bridge;
            if (!TryLoadBridge(out bridge, logMissingConfiguration)) return;
            string poseImportRoot, poseOutputRoot, expressionImportRoot, expressionOutputRoot;
            try { ValidateBridge(bridge, out poseImportRoot, out poseOutputRoot, out expressionImportRoot, out expressionOutputRoot); }
            catch (Exception exception)
            {
                if (logMissingConfiguration) Debug.LogError("DAZ Pose browser import is paused because DazPoseWizard.project.json is invalid: " + exception.Message);
                return;
            }

            foreach (var importRoot in new[] { poseImportRoot, expressionImportRoot }.Where(root => !string.IsNullOrEmpty(root)))
            {
                var importDiskPath = AssetToDiskPath(importRoot);
                if (!Directory.Exists(importDiskPath)) continue;
                foreach (var diskPath in EnumerateCanonicalFiles(importDiskPath))
                {
                    try { var assetPath = DiskToAssetPath(diskPath); var status = ReadStatus(assetPath); var contentHash = ContentHash(diskPath); if (NeedsProcessing(diskPath, assetPath, status, contentHash, retryFailed)) Enqueue(assetPath, retryFailed); }
                    catch (Exception exception) { Debug.LogError("DAZ Pose could not reconcile canonical import " + diskPath + ": " + exception.Message); }
                }
            }
            InstallPump();
        }

        private static bool TryLoadBridge(out DazPoseProjectBridge bridge, bool logIfMissing)
        {
            bridge = null;
            var path = Path.Combine(ProjectRoot, "DazPoseWizard.project.json");
            if (!File.Exists(path))
            {
                if (logIfMissing) Debug.LogWarning("No browser bridge file was found. Configure this Unity project in DazPoseWizard, then use Reconcile Pending Pose Imports after setup.");
                return false;
            }
            try
            {
                bridge = JsonUtility.FromJson<DazPoseProjectBridge>(File.ReadAllText(path));
                if (bridge == null || (bridge.schemaVersion != 1 && bridge.schemaVersion != 2)) throw new InvalidDataException("Only schemaVersion 1 and 2 are supported.");
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is InvalidDataException)
            {
                if (logIfMissing) Debug.LogError("Could not load DazPoseWizard.project.json: " + exception.Message);
                return false;
            }
        }

        private static void ValidateBridge(DazPoseProjectBridge bridge, out string poseImportRoot, out string poseOutputRoot,
            out string expressionImportRoot, out string expressionOutputRoot)
        {
            poseImportRoot = NormalizeAssetRoot(bridge.schemaVersion == 1 ? bridge.importRoot : bridge.poseImportRoot);
            poseOutputRoot = NormalizeAssetRoot(bridge.schemaVersion == 1 ? bridge.outputRoot : bridge.poseOutputRoot);
            expressionImportRoot = bridge.schemaVersion == 1 ? string.Empty : NormalizeAssetRoot(bridge.expressionImportRoot);
            expressionOutputRoot = bridge.schemaVersion == 1 ? string.Empty : NormalizeAssetRoot(bridge.expressionOutputRoot);
            var roots = bridge.schemaVersion == 1
                ? new[] { poseImportRoot, poseOutputRoot }
                : new[] { poseImportRoot, poseOutputRoot, expressionImportRoot, expressionOutputRoot };
            for (var i = 0; i < roots.Length; i++) for (var j = i + 1; j < roots.Length; j++)
                if (IsWithinRoot(roots[i], roots[j]) || IsWithinRoot(roots[j], roots[i]))
                    throw new InvalidDataException("All pose and expression roots must be separate, non-overlapping Assets folders.");
        }

        private static string NormalizeAssetRoot(string value)
        {
            var normalized = NormalizeAssetPath(value).TrimEnd('/');
            if (normalized.Length == 0 || normalized == "Assets")
                throw new InvalidDataException("Both configured roots must be child folders beneath Assets.");
            return normalized;
        }

        private static void Enqueue(string assetPath, bool retryFailed)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (Queued.Add(assetPath)) Pending.Enqueue(assetPath);
            if (retryFailed) RetryFailed.Add(assetPath);
            InstallPump();
        }

        private static void InstallPump()
        {
            if (_pumpInstalled) return;
            _pumpInstalled = true;
            EditorApplication.update += ProcessOnePendingImport;
        }

        private static void ProcessOnePendingImport()
        {
            if (_processing || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Pending.Count == 0)
            {
                EditorApplication.update -= ProcessOnePendingImport;
                _pumpInstalled = false;
                return;
            }

            var assetPath = Pending.Dequeue();
            var retryFailure = RetryFailed.Remove(assetPath);
            Queued.Remove(assetPath);
            _processing = true;
            try { ProcessCanonicalImport(assetPath, retryFailure); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally { _processing = false; }
        }

        private static void ProcessCanonicalImport(string canonicalAssetPath, bool retryFailed)
        {
            DazPoseProjectBridge bridge;
            if (!TryLoadBridge(out bridge, true)) return;
            string poseImportRoot, poseOutputRoot, expressionImportRoot, expressionOutputRoot;
            ValidateBridge(bridge, out poseImportRoot, out poseOutputRoot, out expressionImportRoot, out expressionOutputRoot);
            var isExpression = IsWithinRoot(canonicalAssetPath, expressionImportRoot);
            var importRoot = isExpression ? expressionImportRoot : poseImportRoot;
            var outputRoot = isExpression ? expressionOutputRoot : poseOutputRoot;
            if (!canonicalAssetPath.EndsWith(CanonicalSuffix, StringComparison.OrdinalIgnoreCase)
                || !IsWithinRoot(canonicalAssetPath, importRoot)) return;

            var canonicalDiskPath = AssetToDiskPath(canonicalAssetPath);
            if (!File.Exists(canonicalDiskPath)) return;
            var contentHash = ContentHash(canonicalDiskPath);
            var storedStatus = ReadStatus(canonicalAssetPath);
            var status = storedStatus ?? new DazPoseBrowserJobStatus();
            var expectedKind = isExpression ? "Expression" : "Pose";
            var storedKind = status.schemaVersion >= 2 && !string.IsNullOrWhiteSpace(status.assetKind)
                ? status.assetKind : "Pose";
            if (storedStatus != null && !string.Equals(storedKind, expectedKind, StringComparison.OrdinalIgnoreCase))
            {
                status.state = "Failed";
                status.timestamp = DateTime.UtcNow.ToString("O");
                status.errorMessage = "Browser status assetKind '" + storedKind + "' does not match the "
                    + expectedKind + " import root. Move the canonical asset to the matching root or requeue it from the intended destination.";
                try { WriteStatus(status); }
                catch (Exception statusException) { Debug.LogError("Could not write DAZ Pose kind-mismatch status: " + statusException.Message); }
                Debug.LogError("DAZ Pose browser import rejected " + canonicalAssetPath + ": " + status.errorMessage);
                return;
            }
            if (storedStatus != null && status.schemaVersion >= 2 && string.IsNullOrWhiteSpace(status.assetKind))
            {
                status.state = "Failed";
                status.timestamp = DateTime.UtcNow.ToString("O");
                status.errorMessage = "Schema-2 browser status is missing assetKind; Unity will not infer the requested product from its folder.";
                try { WriteStatus(status); }
                catch (Exception statusException) { Debug.LogError("Could not write DAZ Pose invalid-status record: " + statusException.Message); }
                Debug.LogError("DAZ Pose browser import rejected " + canonicalAssetPath + ": " + status.errorMessage);
                return;
            }
            var expectedAnimPath = ResolveExpectedAnimPath(canonicalAssetPath, importRoot, outputRoot, status);
            var expectedPerformerPosePath = ResolveExpectedWrapperPath(expectedAnimPath, outputRoot, status);
            var destinationFolder = ResolveDestinationFolder(canonicalAssetPath, importRoot, status);
            if (!NeedsProcessing(canonicalDiskPath, canonicalAssetPath, status, contentHash, retryFailed)) return;

            status.schemaVersion = 2;
            status.assetKind = isExpression ? "Expression" : "Pose";
            status.canonicalImportPath = canonicalAssetPath;
            status.sourcePosePath = ReadSourcePosePath(canonicalDiskPath, status.sourcePosePath);
            status.destinationRelativeFolder = destinationFolder;
            status.expectedAnimPath = expectedAnimPath;
            status.expectedPerformerPosePath = isExpression ? string.Empty : expectedPerformerPosePath;
            status.expectedWrapperAssetPath = expectedPerformerPosePath;
            status.state = "Processing";
            status.timestamp = DateTime.UtcNow.ToString("O");
            status.errorMessage = string.Empty;
            status.canonicalContentHash = contentHash;
            WriteStatus(status);

            Scene previewScene = default(Scene);
            GameObject referenceInstance = null;
            try
            {
                var definition = DazPoseJsonLoader.Load(canonicalDiskPath, isExpression);
                if (!IsG8fPose(definition))
                    throw new InvalidOperationException("Only Genesis 8 Female canonical poses are supported by this pipeline. The source figure is '" + definition.source.figureAssetId + "'.");

                string referenceAssetPath;
                var referenceModel = DazPosePipelineSettings.LoadReferenceModel(out referenceAssetPath);
                if (referenceModel == null)
                    throw new InvalidOperationException("Configure a Genesis 8 Female reference FBX in Tools > DAZ Pose > Pipeline Settings.");
                ValidateReferenceImporter(referenceAssetPath);

                previewScene = EditorSceneManager.NewPreviewScene();
                referenceInstance = PrefabUtility.InstantiatePrefab(referenceModel, previewScene) as GameObject;
                if (referenceInstance == null)
                {
                    referenceInstance = UnityEngine.Object.Instantiate(referenceModel);
                    SceneManager.MoveGameObjectToScene(referenceInstance, previewScene);
                }
                referenceInstance.hideFlags = HideFlags.HideAndDontSave;

                ResolvedUnityPose resolvedPose;
                string resolutionFailure;
                if (!DazPoseUnityResolver.TryResolvePose(referenceInstance.transform, canonicalDiskPath,
                        out resolvedPose, out resolutionFailure, true, isExpression))
                    throw new InvalidOperationException(resolutionFailure ?? "The reference G8F rig could not resolve this pose.");

                var reportPath = ".dazposewizard/reports/" + StatusFileName(canonicalAssetPath) + ".report.json";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(expectedAnimPath);
                if (isExpression) DazPoseAnimationClipGenerator.GenerateExpression(resolvedPose, expectedAnimPath, reportPath, existing != null);
                else DazPoseAnimationClipGenerator.Generate(resolvedPose, expectedAnimPath, reportPath, existing != null);
                var generated = AssetDatabase.LoadAssetAtPath<AnimationClip>(expectedAnimPath);
                if (generated == null) throw new InvalidOperationException("Unity did not reload the generated AnimationClip at " + expectedAnimPath + ".");
                if (isExpression)
                {
                    var expression = AssetDatabase.LoadAssetAtPath<PerformerExpression>(expectedPerformerPosePath);
                    if (expression == null || expression.Clip != generated
                        || ((expression.Channels == null || expression.Channels.Length == 0)
                            && (expression.BoneChannels == null || expression.BoneChannels.Length == 0)))
                        throw new InvalidOperationException("Unity did not generate a valid PerformerExpression at " + expectedPerformerPosePath + ".");
                    if (AnimationUtility.GetCurveBindings(generated).Any(binding => !DazPoseAnimationClipGenerator.IsSupportedExpressionBinding(binding)))
                        throw new InvalidOperationException("Generated PerformerExpression clip contains a curve outside the sanitized blendshape and facial Transform properties.");
                    if (!DazPoseAnimationClipGenerator.ExpressionMetadataMatchesClip(expression, generated))
                        throw new InvalidOperationException("Generated PerformerExpression metadata is not in exact parity with the sanitized clip curves.");
                }
                else
                {
                    var performerPose = AssetDatabase.LoadAssetAtPath<PerformerPose>(expectedPerformerPosePath);
                    if (performerPose == null || performerPose.Clip != generated)
                        throw new InvalidOperationException("Unity did not generate a PerformerPose asset that references the expected AnimationClip at " + expectedPerformerPosePath + ".");
                }

                status.state = "Converted";
                status.timestamp = DateTime.UtcNow.ToString("O");
                status.errorMessage = string.Empty;
                WriteStatus(status);
            }
            catch (Exception exception)
            {
                status.state = "Failed";
                status.timestamp = DateTime.UtcNow.ToString("O");
                status.errorMessage = exception.Message;
                try { WriteStatus(status); }
                catch (Exception statusException) { Debug.LogError("Could not write DAZ Pose failure status: " + statusException.Message); }
                Debug.LogError("DAZ Pose browser import failed for " + canonicalAssetPath + ": " + exception.Message);
            }
            finally
            {
                try
                {
                    if (referenceInstance != null) UnityEngine.Object.DestroyImmediate(referenceInstance);
                }
                finally
                {
                    if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
                }
            }
        }

        private static bool NeedsProcessing(string canonicalDiskPath, string canonicalAssetPath,
            DazPoseBrowserJobStatus status, string contentHash, bool retryFailed)
        {
            if (!File.Exists(canonicalDiskPath)) return false;
            DazPoseProjectBridge bridge;
            string poseImportRoot, poseOutputRoot, expressionImportRoot, expressionOutputRoot;
            if (TryLoadBridge(out bridge, false)) ValidateBridge(bridge, out poseImportRoot, out poseOutputRoot, out expressionImportRoot, out expressionOutputRoot);
            else return false;
            var isExpression = IsWithinRoot(canonicalAssetPath, expressionImportRoot);
            var importRoot = isExpression ? expressionImportRoot : poseImportRoot;
            var outputRoot = isExpression ? expressionOutputRoot : poseOutputRoot;
            var expectedAnimPath = ResolveExpectedAnimPath(canonicalAssetPath, importRoot, outputRoot, status);
            var expectedPosePath = ResolveExpectedWrapperPath(expectedAnimPath, outputRoot, status);
            var outputIsUsable = HasUsableProductionOutput(expectedAnimPath, expectedPosePath, isExpression);
            var storedHash = status == null ? string.Empty : status.canonicalContentHash;
            var state = status == null ? string.Empty : status.state;
            if (string.Equals(state, "Converted", StringComparison.OrdinalIgnoreCase)
                && string.Equals(storedHash, contentHash, StringComparison.OrdinalIgnoreCase) && outputIsUsable) return false;
            if (string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase)
                && string.Equals(storedHash, contentHash, StringComparison.OrdinalIgnoreCase)
                && outputIsUsable && !retryFailed) return false;

            if (string.IsNullOrEmpty(storedHash) && outputIsUsable)
            {
                try
                {
                    var poseDiskPath = AssetToDiskPath(expectedPosePath);
                    if (File.GetLastWriteTimeUtc(poseDiskPath) >= File.GetLastWriteTimeUtc(canonicalDiskPath)
                        && (string.IsNullOrEmpty(state) || string.Equals(state, "Converted", StringComparison.OrdinalIgnoreCase))) return false;
                }
                catch (IOException) { }
            }
            return true;
        }

        private static string ResolveExpectedAnimPath(string canonicalAssetPath, string importRoot,
            string outputRoot, DazPoseBrowserJobStatus status)
        {
            if (status != null && !string.IsNullOrWhiteSpace(status.expectedAnimPath))
            {
                var recorded = NormalizeAssetPath(status.expectedAnimPath);
                if (recorded.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) && IsWithinRoot(recorded, outputRoot))
                    return recorded;
            }

            var relative = canonicalAssetPath.Substring(importRoot.Length).TrimStart('/');
            if (!relative.EndsWith(CanonicalSuffix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Canonical import filenames must end in .dazpose.json.");
            relative = relative.Substring(0, relative.Length - CanonicalSuffix.Length) + ".anim";
            return NormalizeAssetPath(outputRoot + "/" + relative);
        }

        private static string ResolveExpectedWrapperPath(string expectedAnimPath, string outputRoot,
            DazPoseBrowserJobStatus status)
        {
            if (status != null && !string.IsNullOrWhiteSpace(status.expectedWrapperAssetPath))
            {
                var recorded = NormalizeAssetPath(status.expectedWrapperAssetPath);
                if (recorded.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && IsWithinRoot(recorded, outputRoot)) return recorded;
            }
            if (status != null && !string.IsNullOrWhiteSpace(status.expectedPerformerPosePath))
            {
                var recorded = NormalizeAssetPath(status.expectedPerformerPosePath);
                if (recorded.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && IsWithinRoot(recorded, outputRoot))
                    return recorded;
            }
            return NormalizeAssetPath(Path.ChangeExtension(expectedAnimPath, ".asset"));
        }

        private static bool HasUsableProductionOutput(string expectedAnimPath, string expectedPerformerPosePath, bool isExpression)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(expectedAnimPath);
            if (clip == null) return false;
            if (isExpression)
            {
                var expression = AssetDatabase.LoadAssetAtPath<PerformerExpression>(expectedPerformerPosePath);
                return expression != null && expression.Clip == clip
                    && ((expression.Channels != null && expression.Channels.Length > 0)
                        || (expression.BoneChannels != null && expression.BoneChannels.Length > 0))
                    && !AnimationUtility.GetCurveBindings(clip).Any(binding => !DazPoseAnimationClipGenerator.IsSupportedExpressionBinding(binding))
                    && DazPoseAnimationClipGenerator.ExpressionMetadataMatchesClip(expression, clip);
            }
            var pose = AssetDatabase.LoadAssetAtPath<PerformerPose>(expectedPerformerPosePath);
            return pose != null && pose.Clip == clip;
        }

        private static string ResolveDestinationFolder(string canonicalAssetPath, string importRoot, DazPoseBrowserJobStatus status)
        {
            if (status != null && !string.IsNullOrWhiteSpace(status.destinationRelativeFolder))
                return NormalizeDestinationFolder(status.destinationRelativeFolder);
            var relative = canonicalAssetPath.Substring(importRoot.Length).TrimStart('/');
            var slash = relative.LastIndexOf('/');
            return slash < 0 ? string.Empty : NormalizeDestinationFolder(relative.Substring(0, slash));
        }

        private static string NormalizeDestinationFolder(string value)
        {
            var normalized = (value ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
            if (normalized.Length == 0) return string.Empty;
            if (normalized.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidDataException("The recorded destination folder contains an invalid path segment.");
            return normalized;
        }

        private static void ValidateReferenceImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Generic || importer.optimizeGameObjects || !importer.importBlendShapes)
                throw new InvalidOperationException("The G8F reference FBX must import as Generic, with Optimize Game Objects off and Import BlendShapes enabled. Reimport the model after changing its import settings.");
        }

        private static bool IsG8fPose(DazPoseDefinition definition)
        {
            if (definition == null || definition.source == null) return false;
            var figureId = definition.source.figureAssetId ?? string.Empty;
            var decoded = Uri.UnescapeDataString(figureId).Replace('\\', '/');
            if (decoded.EndsWith("/Genesis 8/Female/Genesis8Female.dsf", StringComparison.OrdinalIgnoreCase)) return true;
            var figureFile = definition.source.figureFile ?? string.Empty;
            return string.Equals(Path.GetFileName(figureFile.Replace('\\', '/')), "Genesis8Female.dsf", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadSourcePosePath(string canonicalDiskPath, string fallback)
        {
            try
            {
                var definition = JsonUtility.FromJson<DazPoseDefinition>(File.ReadAllText(canonicalDiskPath));
                if (definition != null && definition.source != null && !string.IsNullOrWhiteSpace(definition.source.poseFile))
                    return definition.source.poseFile;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException) { }
            return fallback ?? string.Empty;
        }

        private static DazPoseBrowserJobStatus ReadStatus(string canonicalAssetPath)
        {
            var path = GetStatusPath(canonicalAssetPath);
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<DazPoseBrowserJobStatus>(File.ReadAllText(path)); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Debug.LogWarning("Could not read DAZ Pose browser status " + path + ": " + exception.Message);
                return null;
            }
        }

        private static void WriteStatus(DazPoseBrowserJobStatus status)
        {
            var path = GetStatusPath(status.canonicalImportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(status, true), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static string GetStatusPath(string canonicalAssetPath)
        {
            var normalized = NormalizeAssetPath(canonicalAssetPath).ToUpperInvariant();
            using (var sha = SHA256.Create())
            {
                var hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(normalized))).Replace("-", string.Empty).ToLowerInvariant();
                return Path.Combine(ProjectRoot, ".dazposewizard", "status", hash + ".json");
            }
        }

        private static string StatusFileName(string canonicalAssetPath)
        {
            return Path.GetFileNameWithoutExtension(GetStatusPath(canonicalAssetPath));
        }

        private static string ContentHash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string[] EnumerateCanonicalFiles(string directory)
        {
            var result = new List<string>();
            string[] files;
            string[] subdirectories;
            try
            {
                files = Directory.GetFiles(directory, "*" + CanonicalSuffix, SearchOption.TopDirectoryOnly);
                subdirectories = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return result.ToArray();
            }

            result.AddRange(files);
            foreach (var subdirectory in subdirectories)
            {
                try
                {
                    if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) != 0) continue;
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException) { continue; }
                result.AddRange(EnumerateCanonicalFiles(subdirectory));
            }
            return result.ToArray();
        }

        private static string NormalizeAssetPath(string path)
        {
            var normalized = (path ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || normalized.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidDataException("Expected an Assets-relative path without traversal segments.");
            return normalized;
        }

        private static bool TryNormalizeImportedAssetPath(string path, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(path)) return false;

            var candidate = path.Trim().Replace('\\', '/');
            if (!candidate.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                normalized = NormalizeAssetPath(candidate);
                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        private static bool IsWithinRoot(string path, string root)
        {
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(root.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string AssetToDiskPath(string assetPath)
        {
            var normalized = NormalizeAssetPath(assetPath);
            var assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var diskPath = Path.GetFullPath(Path.Combine(assetsRoot, normalized.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar)));
            if (!diskPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Resolved asset path escaped the Unity Assets folder.");
            return diskPath;
        }

        private static string DiskToAssetPath(string diskPath)
        {
            var fullPath = Path.GetFullPath(diskPath);
            var assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!fullPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Canonical import resolved outside the Unity Assets folder.");
            return "Assets/" + fullPath.Substring(assetsRoot.Length + 1).Replace('\\', '/');
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    }

    internal sealed class DazPoseBrowserImportPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            DazPoseBrowserImportQueue.OnAssetsImported(importedAssets);
            if (movedAssets == null || movedAssets.Length == 0) return;
            DazPoseBrowserImportQueue.OnAssetsImported(movedAssets);
        }
    }
}
