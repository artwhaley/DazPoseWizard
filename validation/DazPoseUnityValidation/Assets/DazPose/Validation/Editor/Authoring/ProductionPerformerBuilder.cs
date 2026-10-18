using System;
using System.IO;
using System.Linq;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using DazPose.UnityValidation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.Editor.Performer
{
    /// <summary>One-time migration of accepted Lara data. Playback never requires this builder.</summary>
    public static class ProductionPerformerBuilder
    {
        public const string PrefabPath = "Assets/Production/Performers/Lara.prefab";
        public const string ScenePath = "Assets/Production/Scenes/Day2ActorValidation.unity";
        const string SourceScene = "Assets/TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity";
        const string Root = "Assets/Production/Wardrobe";

        
        public static void Create()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before creating assets.");
            if (AssetDatabase.LoadMainAssetAtPath(PrefabPath) != null)
                throw new InvalidOperationException("Production prefab exists; this migration never overwrites it.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Folder("Assets/Production/Performers"); Folder("Assets/Production/Scenes"); Folder(Root);
                var scene = EditorSceneManager.OpenScene(SourceScene);
                var actor = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SuccubusPerformer>(true)).Single();
                var body = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.sharedMesh ==
                    AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Production/Performers/Lara/Rig/LaraBody.asset"));
                var container = actor.gameObject;
                actor.transform.SetParent(null, true);
                container.name = "Lara Performer";
                foreach (var harness in actor.GetComponents<PerformerPoseSmokeHarness>()) UnityEngine.Object.DestroyImmediate(harness);
                foreach (var harness in actor.GetComponents<PerformerPoseAcceptanceHarness>()) UnityEngine.Object.DestroyImmediate(harness);
                // The accepted particle component lived on the environment root. Copy only its configuration.
                var rig = actor.DissolveRig;
                if (rig == null || rig.ParticleBody == null) throw new InvalidDataException("Accepted Lara effect rig is missing.");
                var particles = new GameObject("Owned Particle Body").AddComponent<PerformerParticleBody>();
                particles.transform.SetParent(container.transform, false);
                EditorUtility.CopySerialized(rig.ParticleBody, particles);
                Set(particles, "debugVisibilityOwner", actor);
                Set(rig, "particleBody", particles);
                actor.transform.localPosition = Vector3.zero;
                actor.transform.localRotation = Quaternion.identity;
                var clock = new GameObject("Playback Clock").AddComponent<MotionDriver>();
                clock.transform.SetParent(container.transform, false);
                actor.ConfigureMotionSource(clock);
                Set(clock, "playOnEnable", false);
                Set(actor, "startHidden", false);
                var grip = actor.GetComponent<PerformerHandGripController>() ?? actor.gameObject.AddComponent<PerformerHandGripController>();
                Set(grip, "profile", AssetDatabase.LoadAssetAtPath<HandGripRigProfile>("Assets/Production/Performers/Lara/Contact/LaraRightHandGrip.asset"));
                Set(grip, "targetSource", null); Set(grip, "gripOnEnable", false); Set(grip, "drawDiagnostics", false);

                var sourceCatalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>("Assets/Wardrobe/WardrobeCatalog.asset");
                var catalog = UnityEngine.Object.Instantiate(sourceCatalog);
                var presets = sourceCatalog.Presets.Select(p => UnityEngine.Object.Instantiate(p)).ToArray();
                for (int i = 0; i < presets.Length; i++) AssetDatabase.CreateAsset(presets[i], Root + "/Candidate-" + presets[i].PresetId + ".asset");
                catalog.ConfigureCandidate(presets);
                AssetDatabase.CreateAsset(catalog, Root + "/LaraCatalog.asset");
                var configuration = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>("Assets/Wardrobe/WardrobeConfiguration.asset"));
                // Distinct identity ensures publication cannot collide with an existing accepted release.
                configuration.CommitRevision("production-lara-day2-v1", new[] { "cloned-accepted-inputs" });
                AssetDatabase.CreateAsset(configuration, Root + "/LaraConfiguration.asset");
                var release = WardrobeGenerationPublisher.PublishCandidate(catalog, configuration, body);
                if (!release.published) throw new InvalidDataException(string.Join("; ", release.issues));
                var wardrobe = actor.GetComponent<PerformerWardrobe>() ?? actor.gameObject.AddComponent<PerformerWardrobe>();
                wardrobe.ConfigureBinding(catalog, configuration, body, actor);
                // Unclothed is the explicit default. Outfit choice belongs to scene/content, not a draft installer.
                RejectExternalReferences(container);
                PrefabUtility.SaveAsPrefabAsset(container, PrefabPath);
                AssetDatabase.SaveAssets();
                ProductionTestEnvironmentBuilder.Compose();
                Debug.Log("Production Lara prefab and fresh scene created. Generation=" + release.generationId);
            }
            finally
            {
                if (previous.Any(s => s.isLoaded) && previous.Any(s => s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        public static void RejectExternalReferences(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidDataException("Missing component in " + root.name);
                if (component is PerformanceSceneBindings) continue; // Explicit sequence references may address scene marks.
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null || EditorUtility.IsPersistent(property.objectReferenceValue)) continue;
                        var value = property.objectReferenceValue;
                        var transform = value is Component c ? c.transform : value is GameObject go ? go.transform : null;
                        if (transform != null && transform != root.transform && !transform.IsChildOf(root.transform))
                            throw new InvalidDataException(component.GetType().Name + "/" + property.propertyPath + " references external scene object " + transform.name);
                    }
                }
            }
        }
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        static void Set(UnityEngine.Object owner, string path, object value)
        {
            using (var serialized = new SerializedObject(owner))
            {
                var p = serialized.FindProperty(path) ?? throw new InvalidDataException(owner.name + "/" + path);
                if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = (bool)value;
                else p.objectReferenceValue = value as UnityEngine.Object;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}

