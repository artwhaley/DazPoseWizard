using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using DazPose.Story;
using DazPose.Validation.WardrobeProtectedCards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.UnityValidation
{
    /// <summary>Creates only the isolated, saved Gate A copy. Never applies scene overrides to source prefabs.</summary>
    public static class WardrobeProtectedCardsFixtureBuilder
    {
        public const string SourceScene = "Assets/Production/Scenes/FirstContact.unity";
        public const string GateAScene = "Assets/__WardrobeProtectedCards/Scenes/GateA_Appearance.unity";
        public static string CreateGateAScene(WardrobeCatalog catalog, WardrobeRigManifest manifest)
        {
            if (catalog == null || !catalog.IsReleased || manifest == null)
                throw new InvalidOperationException("Gate A requires the staged released wardrobe catalog and its generated rig manifest.");
            if (!manifest.Validate(catalog, out string manifestError)) throw new InvalidOperationException("Gate A rig manifest is invalid: " + manifestError);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GateAScene) != null)
                throw new IOException("Gate A scene already exists and will not be overwritten: " + GateAScene);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
                throw new FileNotFoundException("Saved First Contact composition is missing: " + SourceScene);
            EnsureFolder("Assets/__WardrobeProtectedCards/Scenes");
            if (!AssetDatabase.CopyAsset(SourceScene, GateAScene)) throw new IOException("Could not create the isolated Gate A scene copy.");
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(GateAScene, OpenSceneMode.Single);
                var host = UnityEngine.Object.FindAnyObjectByType<PerformanceHost>();
                if (host == null) throw new InvalidDataException("First Contact scene has no PerformanceHost.");
                host.PlayOnStart = false;
                EditorUtility.SetDirty(host);
                var actor = host.Bindings != null ? host.Bindings.Performer : null;
                if (actor == null) throw new InvalidDataException("First Contact PerformanceSceneBindings has no saved Lara performer reference.");
                var wardrobe = actor.GetComponent<PerformerWardrobe>();
                var body = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(x => x.sharedMesh != null && x.sharedMesh.GetBlendShapeIndex("CapturedOpening") >= 0);
                if (wardrobe == null || body == null) throw new InvalidDataException("Saved Lara performer/body/wardrobe is incomplete in the diagnostic scene.");
                wardrobe.ConfigureBinding(catalog, null, body, actor);
                var binding = actor.GetComponent<WardrobeRigBinding>() ?? actor.gameObject.AddComponent<WardrobeRigBinding>();
                var clothingRoot = Child(actor.transform, "Gate A Clothing Attachments");
                var hairRoot = Child(actor.transform, "Gate A Hair Attachments");
                binding.Configure(manifest, clothingRoot, hairRoot);
                var serialized = new SerializedObject(wardrobe);
                var rig = serialized.FindProperty("rigBinding");
                if (rig == null || rig.propertyType != SerializedPropertyType.ObjectReference)
                    throw new InvalidDataException("PerformerWardrobe.rigBinding serialized field is missing; diagnostic scene cannot bind generated assets.");
                rig.objectReferenceValue = binding;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(wardrobe);
                var runnerObject = new GameObject("Appearance Gate Controls");
                SceneManager.MoveGameObjectToScene(runnerObject, scene);
                var runner = runnerObject.AddComponent<AppearanceGateRunner>();
                string manifestPath = AssetDatabase.GetAssetPath(manifest);
                var rigRows = manifest.Clothing.Select(x => x.prefab).Concat(manifest.Hair.Select(x => x.prefab))
                    .Where(x => x != null).Select(x => AssetDatabase.GetAssetPath(x.gameObject) + "=" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x.gameObject))).ToArray();
                runner.Configure(actor, catalog, manifest, AssetDatabase.GetAssetPath(catalog), manifestPath,
                    AssetDatabase.AssetPathToGUID(manifestPath), string.Join(";", rigRows),
                    "first-outfit", "first-outfit/EmikoHair_59875");
                EditorUtility.SetDirty(runner);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Gate A scene: " + GateAScene);
                AssetDatabase.SaveAssets();
                return GateAScene;
            }
            catch
            {
                // Preserve the diagnostic composition for inspection if a post-copy setup step fails.
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.SaveScene(scene);
                throw;
            }
        }
        private static Transform Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) throw new InvalidDataException("Unexpected pre-existing diagnostic attachment root: " + name);
            var node = new GameObject(name); node.transform.SetParent(parent, false); return node.transform;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))) && !AssetDatabase.IsValidFolder(path))
                throw new IOException("Could not create diagnostic folder: " + path);
        }
    }
}
