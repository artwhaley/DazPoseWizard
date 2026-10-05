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
        public const string CatalogPath = "Assets/Wardrobe/WardrobeCatalog.asset";
        public const string ConfigurationPath = "Assets/Wardrobe/WardrobeConfiguration.asset";

        [MenuItem("Tools/DAZ Pose/Wardrobe Setup/Open Clothing Setup")]
        public static void OpenFromMenu()
        {
            if (!TryOpenOrCreate(out _, out string error))
                EditorUtility.DisplayDialog("Clothing Setup", error, "OK");
            else ClothingSetupWindow.Open();
        }

        [MenuItem("Tools/DAZ Pose/Wardrobe Setup/Prepare and Open Clothing Setup")]
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
                if (EditorSceneManager.GetActiveScene().isDirty)
                { error = "The active scene has unsaved changes. Save it or close it before opening Clothing Setup."; return false; }
                try { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); error = null; return true; }
                catch (Exception exception) { error = "Could not open Clothing Setup: " + exception.Message; return false; }
            }

            if (EditorSceneManager.GetActiveScene().isDirty)
            { error = "The active scene has unsaved changes. Save it before creating Clothing Setup."; return false; }
            var catalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(CatalogPath);
            var configuration = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(ConfigurationPath);
            if (catalog == null || configuration == null)
            { error = "Wardrobe catalog/configuration assets are missing. Run the wardrobe migration first."; return false; }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CandidateScenePath) == null)
            { error = "Canonical Lara candidate scene is missing: " + CandidateScenePath; return false; }

            try
            {
                scene = EditorSceneManager.OpenScene(CandidateScenePath, OpenSceneMode.Single);
                var performer = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
                var body = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include)
                    .FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("CapturedOpening") >= 0);
                if (performer == null || body == null)
                { error = "The candidate scene does not contain the expected Lara performer and canonical body."; return false; }
                var wardrobe = performer.GetComponent<PerformerWardrobe>() ?? Undo.AddComponent<PerformerWardrobe>(performer.gameObject);
                wardrobe.ConfigureBinding(catalog, configuration, body, performer, true);
                EnsureFolder("Assets/Scenes");
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                { error = "Unity could not save the new Clothing Setup scene."; return false; }
                scene = SceneManager.GetActiveScene();
                error = null;
                return true;
            }
            catch (Exception exception) { error = "Could not create Clothing Setup: " + exception; return false; }
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
