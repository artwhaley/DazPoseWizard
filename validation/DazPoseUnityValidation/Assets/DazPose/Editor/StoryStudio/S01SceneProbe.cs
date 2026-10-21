using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using DazPose.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.Editor.Studio
{
    // S01's optional read-only probe. This entry point never saves or plays the scene.
    public static class S01SceneProbe
    {
        [Serializable] sealed class Identity { public string sourceProject; public string sandboxProject; }
        [Serializable] sealed class Report
        {
            public string scene;
            public string[] locations, contentDiagnostics, performanceDiagnostics;
            public bool compiled, sceneDirtyAfterDiagnostics;
        }

        public static void Run()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd('\\', '/');
            var marker = Path.Combine(root, ".dazposewizard-sandbox.json");
            if (!Application.isBatchMode || !File.Exists(marker))
                throw new InvalidOperationException("S01 probe requires the identified batch-mode sandbox.");
            var identity = JsonUtility.FromJson<Identity>(File.ReadAllText(marker));
            if (!string.Equals(root, identity.sandboxProject, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(root, identity.sourceProject, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("S01 sandbox identity mismatch.");

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/FirstContact.unity", OpenSceneMode.Single);
            var hosts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StorySceneBindings>(true)).ToArray();
            if (hosts.Length != 1) throw new InvalidOperationException("Expected one FirstContact story binding host.");
            var host = hosts[0];
            var locations = new List<string>();
            foreach (var row in host.locations)
            {
                try { locations.Add(row.id + ": resolved to " + host.RequireLocation(row.id).placement.name); }
                catch (Exception error) { locations.Add((row?.id ?? "<null>") + ": " + error.Message); }
            }
            var story = AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>("Assets/Production/Story/Programs/FirstContact.dazstory");
            var content = story == null || story.content == null || story.program == null
                ? new[] { "FirstContact compiled content unavailable." }
                : story.content.Diagnostics(story.program, host);
            var bindings = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PerformanceSceneBindings>(true)).ToArray();
            var performance = bindings.Length == 0 ? new[] { "No PerformanceSceneBindings found." }
                : bindings.SelectMany(b => b.Validate().Select(issue => issue.ToString())).ToArray();
            var report = new Report { scene = scene.path, locations = locations.ToArray(), contentDiagnostics = content,
                performanceDiagnostics = performance, compiled = story != null && story.IsCompiled,
                sceneDirtyAfterDiagnostics = scene.isDirty };
            var json = JsonUtility.ToJson(report, true);
            Directory.CreateDirectory(Path.Combine(root, "TestOutput", "S01"));
            File.WriteAllText(Path.Combine(root, "TestOutput", "S01", "scene-probe.json"), json);
            Debug.Log("S01_SCENE_PROBE " + json);
        }
    }
}
