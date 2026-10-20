using System;
using System.Linq;
using DazPose.Content;
using DazPose.Game;
using DazPose.Performer;
using DazPose.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DazPose.Editor
{
    public static class AuthoringContentComposer
    {
        public const string ShowcaseScenePath = "Assets/Examples/Scenes/AuthoringShowcase.unity";
        public const string FirstContactScenePath = "Assets/Scenes/FirstContact.unity";
        public const string PublicCatalogPath = "Assets/Production/Application/SceneCatalog.asset";

        // Retained for existing validation callers. This method only inspects saved scenes;
        // it does not create assets, components, scene entries or build settings.
        public static void ValidateSavedContent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Inspect saved content in Edit Mode.");
            ValidateSavedScene(ShowcaseScenePath, typeof(AuthoringShowcase));
            ValidateSavedScene(FirstContactScenePath, typeof(StoryProgramSequence));

            var catalog = AssetDatabase.LoadAssetAtPath<SceneCatalog>(PublicCatalogPath);
            if (catalog == null || catalog.entries == null)
                throw new InvalidOperationException("The saved public scene catalog is missing.");

            var enabled = catalog.entries.Where(entry => entry != null && entry.enabled).ToArray();
            if (enabled.Length != 1 || enabled[0].scenePath != FirstContactScenePath)
                throw new InvalidOperationException("The public scene catalog must contain only the saved First Contact scene.");

            UnityEngine.Debug.Log("AUTHORING_SAVED_CONTENT_VALIDATED: saved Showcase and First Contact hosts/sequences exist; public catalog contains First Contact only.");
        }

        static void ValidateSavedScene(string path, Type sequenceType)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new InvalidOperationException("Missing saved authoring scene: " + path);

            var activeBefore = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path);
            bool opened=!scene.IsValid() || !scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var hosts = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PerformanceHost>(true))
                    .ToArray();
                if (hosts.Length != 1)
                    throw new InvalidOperationException(path + " must contain exactly one saved PerformanceHost.");
                if (hosts[0].Bindings == null || hosts[0].Sequence == null || hosts[0].Sequence.GetType() != sequenceType)
                    throw new InvalidOperationException(path + " has missing or unexpected saved session content.");
            }
            finally
            {
                if(opened)EditorSceneManager.CloseScene(scene, true);
                if (activeBefore.IsValid() && activeBefore.isLoaded)
                    SceneManager.SetActiveScene(activeBefore);
            }
        }
    }
}