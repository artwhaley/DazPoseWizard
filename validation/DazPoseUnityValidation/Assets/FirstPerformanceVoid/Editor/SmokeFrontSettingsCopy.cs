using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Explicitly copies the loaded Front settings when requested by an editor operation.</summary>
    public static class SmokeFrontSettingsCopy
    {

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
