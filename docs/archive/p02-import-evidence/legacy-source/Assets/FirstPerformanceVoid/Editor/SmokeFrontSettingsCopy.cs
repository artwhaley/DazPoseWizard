using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Copies the user's live Front settings once, independently of saved scene revisions.</summary>
    [InitializeOnLoad]
    public static class SmokeFrontSettingsCopy
    {
        private const string RequestKey = "DazPose.P0C.CopyLiveSmokeFront.20261001.2";

        static SmokeFrontSettingsCopy()
        {
            EditorApplication.delayCall += ApplyPendingCopy;
            EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += ApplyPendingCopy;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += ApplyPendingCopy;
                else if (state == PlayModeStateChange.ExitingEditMode) ApplyPendingCopy();
            };
        }

        private static void ApplyPendingCopy()
        {
            string key = RequestKey + Application.dataPath;
            if (EditorPrefs.GetBool(key, false) || EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetSceneByPath(FirstPerformanceVoidBuilder.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return;
            CopyLoadedFront();
            EditorPrefs.SetBool(key, true);
        }

        [MenuItem("Tools/DAZ Pose/First Performance Void/Copy Current Smoke Front to Other Edges")]
        public static void CopyLoadedFront()
        {
            Scene scene = SceneManager.GetSceneByPath(FirstPerformanceVoidBuilder.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlaying)
                throw new InvalidOperationException("Open FirstPerformanceVoid in Edit mode to copy the current Front settings.");
            Transform room = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "FirstPerformanceVoid") room = root.transform;
            if (room == null) throw new InvalidOperationException("FirstPerformanceVoid root is missing.");
            Transform front = room.Find("Environment/Perimeter Rising Smoke/Smoke Front");
            if (front == null) throw new InvalidOperationException("Smoke Front is missing.");
            string sourceSettings = EditorJsonUtility.ToJson(front.GetComponent<ParticleSystem>(), true);
            FirstPerformanceVoidSmoke.CopyFrontSettings(room);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save copied smoke settings.");
            string logs = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "SmokeFrontCopiedSettings.json"), sourceSettings);
            Debug.Log("SMOKE_FRONT_COPIED: copied every particle module and Renderer setting from the loaded Smoke Front to Left, Right and Back. Only each edge's shape dimensions and GameObject transform were preserved. Source record: Logs/SmokeFrontCopiedSettings.json");
        }
    }
}
