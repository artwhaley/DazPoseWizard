using System;
using System.Linq;
using DazPose.Performer;
using DazPose.Motion;
using DazPose.Toys;
using DazPose.FirstPerformanceVoid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.Editor.Performer
{
    /// <summary>Composes test actors into saved authored scene data, never builder lighting defaults.</summary>
    public static class ProductionTestEnvironmentBuilder
    {
        public const string AuthoredScene = "Assets/Scenes/FirstPerformanceVoid.unity";
        #if DAZPOSE_DEVELOPMENT_TOOLS
[MenuItem("DazPose/Development/Legacy/Production/Rebuild actor validation in authored void")]
#endif
        public static void Rebuild()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try { Compose(); }
            finally
            {
                if (previous.Any(s => s.isLoaded) && previous.Any(s => s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
        public static void Compose()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionPerformerBuilder.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Create the production performer first.");
            var scene = EditorSceneManager.OpenScene(AuthoredScene);
            var stage = scene.GetRootGameObjects().Single(x => x.name == "FirstPerformanceVoid");
            var actorContainer = stage.transform.Find("Performer");
            if (actorContainer == null) throw new InvalidOperationException("Authored performer container missing.");
            // These components run the old demonstration; retain all authored environment and view data.
            foreach (var component in stage.GetComponents<FirstPerformanceVoidControls>()) UnityEngine.Object.DestroyImmediate(component);
            foreach (var component in stage.GetComponents<FirstContactPerformance>()) UnityEngine.Object.DestroyImmediate(component);
            foreach (var component in stage.GetComponents<PerformerParticleBody>()) UnityEngine.Object.DestroyImmediate(component);
            foreach (var root in scene.GetRootGameObjects().Where(x => x.GetComponent<MotionDriver>() != null || x.GetComponent<ToyControlService>() != null))
                UnityEngine.Object.DestroyImmediate(root);
            foreach (var child in actorContainer.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            var first = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            first.name = "Performer A";
            // The production actor's documented origin stays stable; the authored camera is unchanged.
            first.transform.position = Vector3.zero;
            var second = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            second.name = "Performer B"; second.transform.position = new Vector3(3f, 0f, 0f);
            var mark = new GameObject("Bedside Mark"); mark.transform.position = new Vector3(0f, 0f, 1.5f);
            foreach (var instance in new[] { first, second })
            {
                var performer = instance.GetComponent<SuccubusPerformer>();
                var binding = instance.AddComponent<PerformanceSceneBindings>();
                binding.Configure(performer, null, performer.MotionSource, null,
                    PerformerCapability.Pose | PerformerCapability.Speech | PerformerCapability.LipSync |
                    PerformerCapability.Movement | PerformerCapability.Visibility | PerformerCapability.Wardrobe,
                    new[] { new PerformanceSceneBindings.Target { id = "bedside", transform = mark.transform } });
                var issues = binding.Validate();
                if (issues.Length != 0) throw new InvalidOperationException(string.Join("\n", issues.Select(x => x.ToString())));
            }
            if (!EditorSceneManager.SaveScene(scene, ProductionPerformerBuilder.ScenePath)) throw new InvalidOperationException("Scene save failed.");
        }
    }
}
