#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DazPose.FirstPerformanceVoid;
using DazPose.Performer;
using DazPose.Player;
using DazPose.Validation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Remediation
{
    /// <summary>Creates and builds the two saved R12 scenes without changing the authored Void source.</summary>
    public static class RemediationBuild
    {
        public const string AuthoredSourceScene = "Assets/Scenes/FirstPerformanceVoid.unity";
        public const string ValidationScene = "Assets/Production/Scenes/RemediationValidation.unity";
        public const string ProductionScene = "Assets/Production/Scenes/ProductionRuntimeSmoke.unity";
        private const string OrdinaryContentRoot = "Assets/Content/Remediation/Folder With Spaces";

        [Serializable]
        public sealed class DependencyFile
        {
            public string path;
            public string sha256;
            public long bytes;
        }

        [Serializable]
        public sealed class BuildRecord
        {
            public string role;
            public string scene;
            public string result;
            public string executablePath;
            public string sceneSha256;
            public string sceneDependencySha256;
            public string sourceHash;
            public string inputAssetHash;
            public string defineSymbolsBefore;
            public string defineSymbolsAfter;
            public string[] extraScriptingDefines = Array.Empty<string>();
            public string[] managedFileHashes = Array.Empty<string>();
            public int errors;
            public int warnings;
            public long totalSizeBytes;
            public bool succeeded;
            public bool validationDefineOnly;
        }

        [Serializable]
        public sealed class Manifest
        {
            public int schemaVersion;
            public string capturedUtc;
            public string sourceHash;
            public string inputAssetHash;
            public string scriptingBackend;
            public string authoredSourceScene;
            public string authoredSourceSceneSha256Before;
            public string authoredSourceSceneSha256After;
            public string validationScene;
            public string productionScene;
            public string validationSceneCamera;
            public string[] validationSceneBehaviourTypes = Array.Empty<string>();
            public string[] productionSceneBehaviourTypes = Array.Empty<string>();
            public bool productionBoundaryPassed;
            public BuildRecord validation;
            public BuildRecord production;
        }

        public sealed class PreparedScenes
        {
            public string validationScene;
            public string productionScene;
            public string authoredSceneSha256Before;
            public string validationCamera;
            public string[] validationBehaviourTypes;
            public string[] productionBehaviourTypes;
            public bool productionBoundaryPassed;
        }

        public static PreparedScenes PrepareScenes(string sourceHash, string inputHash)
        {
            string sourceHashBefore = HashAsset(AuthoredSourceScene);
            if (sourceHashBefore == "missing") throw new FileNotFoundException("The accepted FirstPerformanceVoid scene is missing.", AuthoredSourceScene);
            EnsureAssetFolder("Assets/Production/Scenes");
            ResetCopy(AuthoredSourceScene, ValidationScene);
            Scene validation = EditorSceneManager.OpenScene(ValidationScene, OpenSceneMode.Single);
            SuccubusPerformer validationActor = FindActorInScene(validation);
            FirstContactPerformance firstContact = FindComponent<FirstContactPerformance>(validation);
            if (firstContact == null) throw new InvalidDataException("The accepted Void scene has no saved FirstContactPerformance speech bindings.");
            AudioClip firstSpeech = ReadAudioClip(firstContact, "speechClipA");
            AudioClip secondSpeech = ReadAudioClip(firstContact, "speechClipB");
            if (firstSpeech == null || secondSpeech == null)
                throw new InvalidDataException("FirstPerformanceVoid must retain both saved First Contact speech clips.");
            FindOrdinaryContent(out PerformerGesture gesture, out PerformerAction action);

            var bindingObject = new GameObject("R12 Remediation Scene Bindings");
            SceneManager.MoveGameObjectToScene(bindingObject, validation);
            var validationBindings = bindingObject.AddComponent<PerformanceSceneBindings>();
            PerformerCapability capabilities = PerformerCapability.Speech | PerformerCapability.Gesture | PerformerCapability.Action;
            validationBindings.Configure(validationActor, null, null, null, capabilities);
            validationBindings.DeclareAssets(null, null, new[] { firstSpeech, secondSpeech });
            validationBindings.DeclareContent(new[] { gesture }, new[] { action }, Array.Empty<PerformerSpell>(), Array.Empty<PerformerAura>());
            var validationRunnerObject = new GameObject("R12 Remediation Validation Runner");
            SceneManager.MoveGameObjectToScene(validationRunnerObject, validation);
            var validationRunner = validationRunnerObject.AddComponent<RemediationValidationPlayerRunner>();
            validationRunner.Configure(validationActor, validationBindings, gesture, action, firstSpeech, secondSpeech,
                sourceHash, inputHash);
            string cameraDescription = DescribeCameraFraming(validation, validationActor);
            EditorSceneManager.MarkSceneDirty(validation);
            if (!EditorSceneManager.SaveScene(validation)) throw new IOException("Could not save RemediationValidation.unity.");
            string[] validationBehaviourTypes = BehaviourTypes(validation);

            ResetCopy(AuthoredSourceScene, ProductionScene);
            Scene production = EditorSceneManager.OpenScene(ProductionScene, OpenSceneMode.Single);
            SuccubusPerformer productionActor = FindActorInScene(production);
            FirstContactPerformance productionFirstContact = FindComponent<FirstContactPerformance>(production);
            if (productionFirstContact == null) throw new InvalidDataException("The copied production smoke source has no saved speech reference to reuse.");
            AudioClip productionSpeech = ReadAudioClip(productionFirstContact, "speechClipA");
            if (productionSpeech == null) throw new InvalidDataException("Production smoke requires the authored First Contact speech clip.");
            StripValidationAndSceneRunnerComponents(production);
            productionActor = FindActorInScene(production);
            var productionBindingsObject = new GameObject("Production Runtime Smoke Bindings");
            SceneManager.MoveGameObjectToScene(productionBindingsObject, production);
            var productionBindings = productionBindingsObject.AddComponent<PerformanceSceneBindings>();
            productionBindings.Configure(productionActor, null, null, null, PerformerCapability.Speech);
            productionBindings.DeclareAssets(null, null, new[] { productionSpeech });
            var productionRunnerObject = new GameObject("Production Runtime Smoke Runner");
            SceneManager.MoveGameObjectToScene(productionRunnerObject, production);
            var productionRunner = productionRunnerObject.AddComponent<RemediationProductionSmokeRunner>();
            productionRunner.Configure(productionActor, productionBindings, productionSpeech);
            EditorSceneManager.MarkSceneDirty(production);
            if (!EditorSceneManager.SaveScene(production)) throw new IOException("Could not save ProductionRuntimeSmoke.unity.");
            string[] productionBehaviourTypes = BehaviourTypes(production);
            bool boundary = ProductionBoundaryIsClean(productionBehaviourTypes);

            return new PreparedScenes
            {
                validationScene = ValidationScene,
                productionScene = ProductionScene,
                authoredSceneSha256Before = sourceHashBefore,
                validationCamera = cameraDescription,
                validationBehaviourTypes = validationBehaviourTypes,
                productionBehaviourTypes = productionBehaviourTypes,
                productionBoundaryPassed = boundary
            };
        }

        public static BuildRecord BuildValidation(string outputDirectory, string sourceHash, string inputHash, string scenePath)
        {
            return Build("validation", outputDirectory, sourceHash, inputHash, scenePath, new[] { "DAZPOSE_VALIDATION" });
        }

        public static BuildRecord BuildProduction(string outputDirectory, string sourceHash, string inputHash, string scenePath,
            string[] productionBehaviourTypes)
        {
            if (!ProductionBoundaryIsClean(productionBehaviourTypes))
                throw new InvalidDataException("Production smoke scene contains a validation or acceptance component.");
            return Build("production", outputDirectory, sourceHash, inputHash, scenePath, Array.Empty<string>());
        }

        private static BuildRecord Build(string role, string outputDirectory, string sourceHash, string inputHash,
            string scenePath, string[] defines)
        {
            EnsureMonoBackend();
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (scene == null) throw new FileNotFoundException("Explicit R12 build scene is missing.", scenePath);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string executablePath = Path.Combine(outputDirectory, role == "validation" ? "RemediationValidation.exe" : "ProductionRuntimeSmoke.exe");
            Directory.CreateDirectory(outputDirectory);
            string symbolsBefore = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
                extraScriptingDefines = defines
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            string symbolsAfter = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone);
            var managedHashes = new List<string>();
            if (report != null && report.GetFiles() != null)
            {
                foreach (BuildFile buildFile in report.GetFiles())
                {
                    string filePath = Path.IsPathRooted(buildFile.path) ? buildFile.path : Path.Combine(outputDirectory, buildFile.path);
                    if (!File.Exists(filePath)) continue;
                    string extension = Path.GetExtension(filePath);
                    if (!string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    managedHashes.Add(Path.GetFileName(filePath) + ":" + HashFile(filePath));
                }
            }

            BuildSummary summary = report != null ? report.summary : default;
            return new BuildRecord
            {
                role = role,
                scene = scenePath,
                result = report != null ? summary.result.ToString() : "MissingBuildReport",
                executablePath = executablePath,
                sceneSha256 = HashAsset(scenePath),
                sceneDependencySha256 = HashAssetDependencies(scenePath, projectRoot),
                sourceHash = sourceHash ?? string.Empty,
                inputAssetHash = inputHash ?? string.Empty,
                defineSymbolsBefore = symbolsBefore,
                defineSymbolsAfter = symbolsAfter,
                extraScriptingDefines = defines ?? Array.Empty<string>(),
                managedFileHashes = managedHashes.ToArray(),
                errors = report != null ? (int)summary.totalErrors : -1,
                warnings = report != null ? (int)summary.totalWarnings : -1,
                totalSizeBytes = report != null ? (long)summary.totalSize : -1,
                succeeded = report != null && summary.result == BuildResult.Succeeded && summary.totalErrors == 0 && File.Exists(executablePath),
                validationDefineOnly = defines != null && defines.Length == 1 && string.Equals(defines[0], "DAZPOSE_VALIDATION", StringComparison.Ordinal)
            };
        }

        private static void EnsureMonoBackend()
        {
            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone);
            if (backend != ScriptingImplementation.Mono2x)
                throw new InvalidOperationException("R12 requires the existing Windows Standalone Mono2x backend; found " + backend
                    + ". Build configuration was not modified.");
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            int slash = assetPath.LastIndexOf('/');
            if (slash <= 0) throw new InvalidDataException("Invalid asset folder " + assetPath);
            EnsureAssetFolder(assetPath.Substring(0, slash));
            if (!AssetDatabase.IsValidFolder(assetPath)) AssetDatabase.CreateFolder(assetPath.Substring(0, slash), assetPath.Substring(slash + 1));
        }

        private static void ResetCopy(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(destinationPath) != null && !AssetDatabase.DeleteAsset(destinationPath))
                throw new IOException("Could not reset generated R12 scene " + destinationPath);
            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                throw new IOException("Could not copy authored scene " + sourcePath + " to " + destinationPath);
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static SuccubusPerformer FindActorInScene(Scene scene)
        {
            SuccubusPerformer[] actors = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SuccubusPerformer>(true)).ToArray();
            if (actors.Length != 1) throw new InvalidDataException("R12 scenes require exactly one authored Lara performer; found " + actors.Length + ".");
            return actors[0];
        }

        private static T FindComponent<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();

        private static AudioClip ReadAudioClip(Component source, string propertyName)
        {
            if (source == null) return null;
            var serialized = new SerializedObject(source);
            return serialized.FindProperty(propertyName)?.objectReferenceValue as AudioClip;
        }

        private static void FindOrdinaryContent(out PerformerGesture gesture, out PerformerAction action)
        {
            string[] gestureGuids = AssetDatabase.FindAssets("t:PerformerGesture", new[] { OrdinaryContentRoot });
            foreach (string gestureGuid in gestureGuids.OrderByDescending(guid => AssetDatabase.GUIDToAssetPath(guid), StringComparer.Ordinal))
            {
                string gesturePath = AssetDatabase.GUIDToAssetPath(gestureGuid);
                string folder = Path.GetDirectoryName(gesturePath).Replace('\\', '/');
                if (!folder.StartsWith(OrdinaryContentRoot + "/", StringComparison.Ordinal)) continue;
                string actionGuid = AssetDatabase.FindAssets("t:PerformerAction", new[] { folder })
                    .OrderBy(guid => AssetDatabase.GUIDToAssetPath(guid), StringComparer.Ordinal).FirstOrDefault();
                if (actionGuid == null) continue;
                PerformerGesture candidateGesture = AssetDatabase.LoadAssetAtPath<PerformerGesture>(gesturePath);
                PerformerAction candidateAction = AssetDatabase.LoadAssetAtPath<PerformerAction>(AssetDatabase.GUIDToAssetPath(actionGuid));
                if (candidateGesture == null || candidateAction == null) continue;
                if (!candidateGesture.IsReady(out _) || !candidateAction.IsReady(out _)) continue;
                gesture = candidateGesture;
                action = candidateAction;
                return;
            }
            throw new FileNotFoundException("R12 requires a validated prepared Gesture and Action in an ordinary Assets/Content folder. Run R09 first.");
        }

        private static void StripValidationAndSceneRunnerComponents(Scene scene)
        {
            foreach (MonoBehaviour behaviour in scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray())
            {
                if (behaviour == null) continue;
                string assembly = behaviour.GetType().Assembly.GetName().Name ?? string.Empty;
                string fullName = behaviour.GetType().FullName ?? string.Empty;
                bool validationAssembly = assembly.StartsWith("DazPose.Validation", StringComparison.Ordinal);
                bool firstContactRunner = fullName == typeof(FirstContactPerformance).FullName;
                if (validationAssembly || firstContactRunner) UnityEngine.Object.DestroyImmediate(behaviour, true);
            }
            foreach (MonoBehaviour remaining in scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (remaining == null) continue;
                string assembly = remaining.GetType().Assembly.GetName().Name ?? string.Empty;
                if (assembly.StartsWith("DazPose.Validation", StringComparison.Ordinal))
                    throw new InvalidDataException("Could not remove validation component " + remaining.GetType().FullName + " from production smoke scene.");
            }
        }

        private static string[] BehaviourTypes(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
            .Where(component => component != null)
            .Select(component => component.GetType().FullName + " [" + component.GetType().Assembly.GetName().Name + "]")
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();

        private static bool ProductionBoundaryIsClean(string[] types)
        {
            if (types == null) return false;
            return !types.Any(type => type.IndexOf("DazPose.Validation", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("Acceptance", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("TestController", StringComparison.OrdinalIgnoreCase) >= 0
                || type.IndexOf("ValidationRunner", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string DescribeCameraFraming(Scene scene, SuccubusPerformer actor)
        {
            Camera camera = FindComponent<PlayerController>(scene)?.View?.MainCamera;
            if (camera == null) camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(candidate => candidate != null && candidate.enabled && candidate.CompareTag("MainCamera"));
            if (camera == null || actor == null) return "camera-or-actor-missing";
            Renderer[] renderers = actor.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer != null).ToArray();
            if (renderers.Length == 0) return camera.name + "; actor renderers missing";
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 min = bounds.min, max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x,min.y,min.z), new Vector3(min.x,min.y,max.z), new Vector3(min.x,max.y,min.z), new Vector3(min.x,max.y,max.z),
                new Vector3(max.x,min.y,min.z), new Vector3(max.x,min.y,max.z), new Vector3(max.x,max.y,min.z), new Vector3(max.x,max.y,max.z)
            };
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            bool inFront = true;
            foreach (Vector3 corner in corners)
            {
                Vector3 view = camera.WorldToViewportPoint(corner);
                inFront &= view.z > 0f;
                minX = Mathf.Min(minX, view.x); minY = Mathf.Min(minY, view.y);
                maxX = Mathf.Max(maxX, view.x); maxY = Mathf.Max(maxY, view.y);
            }
            bool framed = inFront && minX >= 0.02f && maxX <= 0.98f && minY >= 0.02f && maxY <= 0.98f;
            return camera.name + "; target=standing/foot bounds; viewport=" + minX.ToString("F3") + "," + minY.ToString("F3")
                + "-" + maxX.ToString("F3") + "," + maxY.ToString("F3") + "; fullBoundsInView=" + framed;
        }

        public static string HashAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return "missing";
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return HashFile(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string HashAssetDependencies(string scenePath, string projectRoot)
        {
            var rows = new List<string>();
            foreach (string dependency in AssetDatabase.GetDependencies(scenePath, true).OrderBy(path => path, StringComparer.Ordinal))
            {
                string physical = Path.Combine(projectRoot, dependency.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(physical)) continue;
                var info = new FileInfo(physical);
                rows.Add(dependency.ToLowerInvariant() + ":" + HashFile(physical) + ":" + info.Length);
            }
            return HashText(string.Join("\n", rows));
        }

        private static string HashFile(string path)
        {
            if (!File.Exists(path)) return "missing";
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string HashText(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
#endif
