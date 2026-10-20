using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using DazPose.Validation.WardrobeProtectedCards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.UnityValidation
{
    /// <summary>Creates or reuses the saved diagnostic Gate A composition without changing production assets.</summary>
    public static class WardrobeProtectedCardsFixtureBuilder
    {
        public const string SourceScene = "Assets/Production/Scenes/FirstContact.unity";
        public const string GateAScene = "Assets/__WardrobeProtectedCards/Scenes/GateA_Appearance.unity";
        public static string CreateGateAScene(WardrobeCatalog catalog, WardrobeRigManifest manifest, string previousGeneration)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play before preparing the saved diagnostic scene.");
            if (catalog == null || !catalog.IsReleased || manifest == null || !manifest.Validate(catalog, out _))
                throw new InvalidOperationException("Gate A requires the corrected released catalog and its valid rig manifest.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Scene change cancelled. Prepared release and prefabs are retained.");
            const string sceneFolder = "Assets/__WardrobeProtectedCards/Scenes";
            if (AssetDatabase.IsValidFolder(sceneFolder))
            {
                foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { sceneFolder }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => Path.GetFileName(path).StartsWith("GateA_Appearance", StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal))
                {
                    var saved = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var runners = saved.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AppearanceGateRunner>(true)).ToArray();
                    var hosts = saved.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PerformanceHost>(true)).ToArray();
                    if (runners.Length == 1 && runners[0].MatchesSavedSetup(catalog, manifest) && hosts.All(host => !host.PlayOnStart)) return path;
                }
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
                throw new FileNotFoundException("Saved First Contact composition is missing: " + SourceScene);
            EnsureFolder(sceneFolder);
            string output = AssetDatabase.GenerateUniqueAssetPath(GateAScene);
            if (!AssetDatabase.CopyAsset(SourceScene, output)) throw new IOException("Could not copy diagnostic scene: " + output);
            Scene scene = default;
            bool autoStartDisabled = false;
            try
            {
                scene = EditorSceneManager.OpenScene(output, OpenSceneMode.Single);
                var hosts = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PerformanceHost>(true)).ToArray();
                if (hosts.Length != 1) throw new InvalidDataException("First Contact must have exactly one saved PerformanceHost; found " + hosts.Length);
                var host = hosts[0];
                host.PlayOnStart = false;
                EditorUtility.SetDirty(host);
                autoStartDisabled = true;
                var actor = host.Bindings != null ? host.Bindings.Performer : null;
                if (actor == null) throw new InvalidDataException("First Contact host has no saved Lara reference.");
                var wardrobe = actor.GetComponent<PerformerWardrobe>();
                var body = wardrobe != null ? wardrobe.Body : null;
                if (body == null) throw new InvalidDataException("Saved Lara PerformerWardrobe.Body reference is missing.");
                actor.ConfigureInitialState(actor.InitialPose, actor.InitialExpression, actor.InitialExpressionIntensity, false);
                actor.gameObject.SetActive(true);
                wardrobe.ConfigureBinding(catalog, null, body, actor);
                var binding = actor.GetComponent<WardrobeRigBinding>() ?? actor.gameObject.AddComponent<WardrobeRigBinding>();
                binding.Configure(manifest, Child(body.transform, "Gate A Clothing Attachments"), Child(body.transform, "Gate A Hair Attachments"));
                var serialized = new SerializedObject(wardrobe);
                var rig = serialized.FindProperty("rigBinding");
                if (rig == null) throw new InvalidDataException("PerformerWardrobe.rigBinding serialized field is missing.");
                rig.objectReferenceValue = binding;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
                PrefabUtility.RecordPrefabInstancePropertyModifications(wardrobe);
                EditorUtility.SetDirty(actor); EditorUtility.SetDirty(wardrobe); EditorUtility.SetDirty(binding);

                // Save a clean inactive template before Play allocates any attachment instances/local bones.
                var templateObject = UnityEngine.Object.Instantiate(actor.gameObject);
                templateObject.name = "Gate A second actor template (inactive)";
                templateObject.SetActive(false);
                SceneManager.MoveGameObjectToScene(templateObject, scene);
                var template = templateObject.GetComponent<SuccubusPerformer>();
                var runnerObject = new GameObject("Appearance Gate Controls");
                SceneManager.MoveGameObjectToScene(runnerObject, scene);
                var runner = runnerObject.AddComponent<AppearanceGateRunner>();
                string manifestPath = AssetDatabase.GetAssetPath(manifest);
                var rigRows = manifest.Clothing.Select(x => x.prefab).Concat(manifest.Hair.Select(x => x.prefab))
                    .Select(x => AssetDatabase.GetAssetPath(x.gameObject) + "=" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x.gameObject))).ToArray();
                runner.Configure(actor, catalog, manifest, AssetDatabase.GetAssetPath(catalog), manifestPath,
                    AssetDatabase.AssetPathToGUID(manifestPath), string.Join(";", rigRows), previousGeneration,
                    "first-outfit", "first-outfit/EmikoHair_59875", template);
                if (!runner.MatchesSavedSetup(catalog, manifest)) throw new InvalidDataException("Gate A saved runner/actor/template references did not validate.");
                EditorUtility.SetDirty(runner);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Gate A scene: " + output);
                AssetDatabase.SaveAssets();
                // Reopen the actual saved file to verify serialization of prefab overrides and template refs.
                scene = EditorSceneManager.OpenScene(output, OpenSceneMode.Single);
                var reopened = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AppearanceGateRunner>(true)).Single();
                if (!reopened.MatchesSavedSetup(catalog, manifest)) throw new InvalidDataException("Reopened Gate A scene lost its saved actor/catalog/rig/template references.");
                return output;
            }
            catch
            {
                // Preserve a failed diagnostic copy with automatic story playback disabled.
                if (autoStartDisabled && scene.IsValid() && scene.isLoaded) EditorSceneManager.SaveScene(scene);
                throw;
            }
        }
        private static Transform Child(Transform parent, string name)
        {
            if (parent.Find(name) != null) throw new InvalidDataException("Unexpected existing diagnostic attachment root: " + name);
            var node = new GameObject(name); node.transform.SetParent(parent, false); return node.transform;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path))))
                throw new IOException("Could not create diagnostic folder: " + path);
        }
    }
}
