using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.UnityValidation
{
    [Serializable]
    internal sealed class DazPoseProjectBridge
    {
        public int schemaVersion;
        public string importRoot;
        public string outputRoot;
    }

    [Serializable]
    internal sealed class DazPoseBrowserJobStatus
    {
        public int schemaVersion = 1;
        public string canonicalImportPath;
        public string sourcePosePath;
        public string destinationRelativeFolder;
        public string expectedAnimPath;
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

        [MenuItem("Tools/DAZ Pose/Process Pending Browser Imports")]
        private static void ProcessPendingBrowserImports()
        {
            Reconcile(true, true);
        }

        internal static void OnAssetsImported(string[] importedAssets)
        {
            if (importedAssets == null || importedAssets.Length == 0) return;
            DazPoseProjectBridge bridge;
            if (!TryLoadBridge(out bridge, false)) return;
            string importRoot;
            string outputRoot;
            try { ValidateBridge(bridge, out importRoot, out outputRoot); }
            catch (Exception exception)
            {
                Debug.LogError("DAZ Pose browser import is paused because DazPoseWizard.project.json is invalid: " + exception.Message);
                return;
            }

            var referenceModel = DazPosePipelineSettings.LoadReferenceModel(out var referenceAssetPath);
            var referenceWasImported = referenceModel != null && importedAssets.Any(importedPath =>
                string.Equals(NormalizeAssetPath(importedPath), referenceAssetPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));

            foreach (var importedPath in importedAssets)
            {
                var assetPath = NormalizeAssetPath(importedPath);
                if (assetPath.EndsWith(CanonicalSuffix, StringComparison.OrdinalIgnoreCase)
                    && IsWithinRoot(assetPath, importRoot)) Enqueue(assetPath, false);
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
            string importRoot;
            string outputRoot;
            try { ValidateBridge(bridge, out importRoot, out outputRoot); }
            catch (Exception exception)
            {
                if (logMissingConfiguration) Debug.LogError("DAZ Pose browser import is paused because DazPoseWizard.project.json is invalid: " + exception.Message);
                return;
            }

            var importDiskPath = AssetToDiskPath(importRoot);
            if (!Directory.Exists(importDiskPath)) return;
            foreach (var diskPath in EnumerateCanonicalFiles(importDiskPath))
            {
                try
                {
                    var assetPath = DiskToAssetPath(diskPath);
                    var status = ReadStatus(assetPath);
                    var contentHash = ContentHash(diskPath);
                    if (NeedsProcessing(diskPath, assetPath, status, contentHash, retryFailed))
                        Enqueue(assetPath, retryFailed);
                }
                catch (Exception exception)
                {
                    Debug.LogError("DAZ Pose could not reconcile canonical import " + diskPath + ": " + exception.Message);
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
                if (logIfMissing) Debug.LogWarning("No browser bridge file was found. Configure this Unity project in DazPoseWizard, then retry Process Pending Browser Imports.");
                return false;
            }
            try
            {
                bridge = JsonUtility.FromJson<DazPoseProjectBridge>(File.ReadAllText(path));
                if (bridge == null || bridge.schemaVersion != 1) throw new InvalidDataException("Only schemaVersion 1 is supported.");
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is InvalidDataException)
            {
                if (logIfMissing) Debug.LogError("Could not load DazPoseWizard.project.json: " + exception.Message);
                return false;
            }
        }

        private static void ValidateBridge(DazPoseProjectBridge bridge, out string importRoot, out string outputRoot)
        {
            importRoot = NormalizeAssetRoot(bridge.importRoot);
            outputRoot = NormalizeAssetRoot(bridge.outputRoot);
            if (IsWithinRoot(importRoot, outputRoot) || IsWithinRoot(outputRoot, importRoot))
                throw new InvalidDataException("The canonical import root and final output root must be separate, non-overlapping Assets folders.");
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
            string importRoot;
            string outputRoot;
            ValidateBridge(bridge, out importRoot, out outputRoot);
            if (!canonicalAssetPath.EndsWith(CanonicalSuffix, StringComparison.OrdinalIgnoreCase)
                || !IsWithinRoot(canonicalAssetPath, importRoot)) return;

            var canonicalDiskPath = AssetToDiskPath(canonicalAssetPath);
            if (!File.Exists(canonicalDiskPath)) return;
            var contentHash = ContentHash(canonicalDiskPath);
            var status = ReadStatus(canonicalAssetPath) ?? new DazPoseBrowserJobStatus();
            var expectedAnimPath = ResolveExpectedAnimPath(canonicalAssetPath, importRoot, outputRoot, status);
            var destinationFolder = ResolveDestinationFolder(canonicalAssetPath, importRoot, status);
            if (!NeedsProcessing(canonicalDiskPath, canonicalAssetPath, status, contentHash, retryFailed)) return;

            status.schemaVersion = 1;
            status.canonicalImportPath = canonicalAssetPath;
            status.sourcePosePath = ReadSourcePosePath(canonicalDiskPath, status.sourcePosePath);
            status.destinationRelativeFolder = destinationFolder;
            status.expectedAnimPath = expectedAnimPath;
            status.state = "Processing";
            status.timestamp = DateTime.UtcNow.ToString("O");
            status.errorMessage = string.Empty;
            status.canonicalContentHash = contentHash;
            WriteStatus(status);

            Scene previewScene = default(Scene);
            GameObject referenceInstance = null;
            try
            {
                var definition = DazPoseJsonLoader.Load(canonicalDiskPath);
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
                if (!DazPoseEditorCommands.TryResolvePoseForPipeline(referenceInstance.transform, canonicalDiskPath,
                        out resolvedPose, out resolutionFailure))
                    throw new InvalidOperationException(resolutionFailure ?? "The reference G8F rig could not resolve this pose.");

                var reportPath = ".dazposewizard/reports/" + StatusFileName(canonicalAssetPath) + ".report.json";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(expectedAnimPath);
                DazPoseAnimationClipGenerator.Generate(resolvedPose, expectedAnimPath, reportPath, existing != null);
                var generated = AssetDatabase.LoadAssetAtPath<AnimationClip>(expectedAnimPath);
                if (generated == null) throw new InvalidOperationException("Unity did not reload the generated AnimationClip at " + expectedAnimPath + ".");

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
            string importRoot;
            string outputRoot;
            if (TryLoadBridge(out bridge, false)) ValidateBridge(bridge, out importRoot, out outputRoot);
            else return false;
            var expectedPath = ResolveExpectedAnimPath(canonicalAssetPath, importRoot, outputRoot, status);

            var outputDiskPath = AssetToDiskPath(expectedPath);
            var outputExists = File.Exists(outputDiskPath);
            var storedHash = status == null ? string.Empty : status.canonicalContentHash;
            var state = status == null ? string.Empty : status.state;
            if (string.Equals(state, "Converted", StringComparison.OrdinalIgnoreCase)
                && string.Equals(storedHash, contentHash, StringComparison.OrdinalIgnoreCase) && outputExists) return false;
            if (string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase)
                && string.Equals(storedHash, contentHash, StringComparison.OrdinalIgnoreCase) && !retryFailed) return false;

            if (string.IsNullOrEmpty(storedHash) && outputExists)
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(outputDiskPath) >= File.GetLastWriteTimeUtc(canonicalDiskPath)
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
