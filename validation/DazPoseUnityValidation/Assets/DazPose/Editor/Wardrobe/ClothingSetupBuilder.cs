using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.UnityValidation
{
    public static class ClothingSetupBuilder
    {
        public const string ScenePath = "Assets/Scenes/ClothingSetup.unity";
        public const string CandidateScenePath = "Assets/TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity";
        public const string EnvironmentScenePath = DazPose.Editor.ProductionSceneEnvironment.TemplatePath;
        public const string CatalogPath = "Assets/Wardrobe/WardrobeCatalog.asset";
        public const string ConfigurationPath = "Assets/Wardrobe/WardrobeConfiguration.asset";

        [MenuItem("DazPose/Wardrobe/Open Clothing Setup")]
        public static void OpenFromMenu()
        {
            if (!TryOpenOrCreate(out _, out string error))
                EditorUtility.DisplayDialog("Clothing Setup", error, "OK");
            else ClothingSetupWindow.Open();
        }


        public static void PrepareAndOpenFromMenu()
        {
            if (!TryPrepareAndOpen(out _, out string error))
            {
                EditorUtility.DisplayDialog("Clothing Setup", error, "OK");
                return;
            }
            ClothingSetupWindow.Open();
        }

        public static bool TryPrepareAndOpen(out Scene scene, out string error)
        {
            scene = SceneManager.GetActiveScene();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                error = "Clothing Setup was not opened because the current scene changes were not saved.";
                return false;
            }

            try
            {
                WardrobeCatalogBuilder.BuildKnownPackages();
                return TryOpenOrCreate(out scene, out error);
            }
            catch (Exception exception)
            {
                error = "Could not prepare the wardrobe catalog: " + exception.Message;
                return false;
            }
        }

        public static bool TryOpenOrCreate(out Scene scene, out string error)
        {
            scene = SceneManager.GetActiveScene();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                if (scene.path == ScenePath) { error = null; return true; }
                if (Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isDirty))
                { error = "The active scene has unsaved changes. Save it or close it before opening Clothing Setup."; return false; }
                try { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); error = null; return true; }
                catch (Exception exception) { error = "Could not open Clothing Setup: " + exception.Message; return false; }
            }

            if (Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isDirty))
            { error = "The active scene has unsaved changes. Save it before creating Clothing Setup."; return false; }
            var catalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(CatalogPath);
            var configuration = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(ConfigurationPath);
            if (catalog == null || configuration == null)
            { error = "Wardrobe catalog/configuration assets are missing. Run the wardrobe migration first."; return false; }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentScenePath) == null)
            { error = "Authored production actor environment is missing: " + EnvironmentScenePath; return false; }

            try
            {
                scene = DazPose.Editor.ProductionSceneEnvironment.Copy(ScenePath);
                const string performerPath="Assets/Production/Performers/Lara.prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(performerPath);
                if(prefab==null) throw new InvalidOperationException("Prepare the supported Lara prefab before creating Clothing Setup: "+performerPath);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                var performer=instance.GetComponent<SuccubusPerformer>();
                var wardrobe=instance.GetComponent<PerformerWardrobe>();
                if(performer==null || wardrobe==null || wardrobe.Body==null)
                    throw new InvalidOperationException("The supported Lara prefab is missing its performer/wardrobe body binding.");
                wardrobe.ConfigureBinding(catalog,configuration,wardrobe.Body,performer,true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(wardrobe);
                var draft=instance.GetComponent<ClothingSetupDraftBinding>() ?? instance.AddComponent<ClothingSetupDraftBinding>();
                draft.Configure(catalog,configuration);
                var startup=instance.GetComponent<ClothingSetupStartup>() ?? instance.AddComponent<ClothingSetupStartup>();
                startup.Configure(performer);
                EnsureFolder("Assets/Scenes");
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                { error = "Unity could not save the new Clothing Setup scene."; return false; }
                scene = SceneManager.GetActiveScene();
                error = null;
                return true;
            }
            catch (Exception exception) { error = "Could not create Clothing Setup: " + exception; return false; }
        }

        // Explicit one-time workspace migration, never called by opening the tool.
        public static bool MigrateSavedStartup(Scene scene)
        {
            if(scene.path!=ScenePath) throw new InvalidOperationException("Migrate only the saved Clothing Setup workspace.");
            bool changed=false;
            var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c!=null).ToArray();
            foreach(var old in components.Where(c=>c.GetType().FullName=="DazPose.Validation.ClothingSetupPreviewRunner"))
            {
                foreach(var consumer in components.Where(c=>c!=old))
                {
                    using var serialized=new SerializedObject(consumer);
                    var field=serialized.GetIterator();
                    while(field.Next(true))
                        if(field.propertyType==SerializedPropertyType.ObjectReference && field.objectReferenceValue==old)
                            throw new InvalidOperationException("A component still references the old preview startup: "+consumer.name+"."+field.propertyPath);
                }
                using var data=new SerializedObject(old);
                var actor=data.FindProperty("actor").objectReferenceValue as SuccubusPerformer;
                string initial=data.FindProperty("initialOutfitId").stringValue;
                if(actor==null) throw new InvalidOperationException("Preserve the saved Clothing Setup actor binding before migrating startup.");
                var startup=old.GetComponent<ClothingSetupStartup>() ?? Undo.AddComponent<ClothingSetupStartup>(old.gameObject);
                startup.Configure(actor,initial);
                EditorUtility.SetDirty(startup);
                Undo.DestroyObjectImmediate(old);
                changed=true;
            }
            if(changed) EditorSceneManager.MarkSceneDirty(scene);
            return changed;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash <= 0) throw new InvalidDataException("Invalid asset folder: " + path);
            EnsureFolder(path.Substring(0, slash));
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
