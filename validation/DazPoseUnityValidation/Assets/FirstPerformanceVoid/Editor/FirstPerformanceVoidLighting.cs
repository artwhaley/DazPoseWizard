using System;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using RenderingLayerMask = UnityEngine.Rendering.HighDefinition.RenderingLayerMask;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Focused lighting corrections; preserves camera, chair, geometry and Bloom tuning.</summary>
    [InitializeOnLoad]
    public static class FirstPerformanceVoidLighting
    {
        private static readonly Color Magenta = new Color(1f, 0.015f, 1f);
        private const uint PerformerLayer = 2;
        private const int CurrentRevision = 5;

        static FirstPerformanceVoidLighting()
        {
            // External .unity writes cannot update a scene already loaded in the user's editor.
            // Upgrade that exact loaded scene once, preserving its current camera and manual edits.
            EditorApplication.delayCall += UpgradeLoadedLounge;
            EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += UpgradeLoadedLounge;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) UpgradeLoadedLounge();
                else if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += UpgradeLoadedLounge;
            };
        }

        private static void UpgradeLoadedLounge()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetSceneByPath(FirstPerformanceVoidBuilder.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return;
            var controls = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<FirstPerformanceVoidControls>(true)).SingleOrDefault();
            if (controls == null || controls.LightingRevision >= CurrentRevision) return;
            try
            {
                ApplyToScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the corrected lounge.");
                Debug.Log("P0C_LOADED_SCENE_UPGRADED: saved character lights and fog lights into the loaded lounge; existing camera/placement edits preserved.");
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Tools/DAZ Pose/First Performance Void/Apply Lara Lighting and Fog Corrections")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetSceneByPath(FirstPerformanceVoidBuilder.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(FirstPerformanceVoidBuilder.ScenePath);
            }
            ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("P0C_LIGHTING_COMPLETE: rising lit perimeter smoke, character lighting and softer skin. Camera and Bloom preserved.");
        }

        public static void ApplyFromCommandLine()
        {
            EditorSceneManager.OpenScene(FirstPerformanceVoidBuilder.ScenePath);
            Apply();
        }

        public static void ApplyToScene(Scene scene)
        {
            Transform room = scene.GetRootGameObjects().Single(go => go.name == "FirstPerformanceVoid").transform;
            var previousControls = room.GetComponent<FirstPerformanceVoidControls>();
            if (previousControls != null && previousControls.LightingRevision >= 3)
            {
                // Subsequent smoke revisions must not retune the accepted character/environment look.
                if (previousControls.LightingRevision >= 4) FirstPerformanceVoidSmoke.CopyFrontSettings(room);
                else FirstPerformanceVoidSmoke.Apply(room);
                var revision = new SerializedObject(previousControls);
                revision.FindProperty("lightingRevision").intValue = CurrentRevision;
                revision.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
            SuccubusPerformer performer = room.GetComponentInChildren<SuccubusPerformer>(true);
            if (performer == null) throw new InvalidOperationException("Lounge has no working performer.");

            // Camera flags cannot enable a feature disabled in the pipeline asset.
            var pipeline = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("Lounge requires the existing HDRP pipeline.");
            var settings = pipeline.currentPlatformRenderPipelineSettings;
            settings.supportLightLayers = true;
            settings.supportVolumetrics = true;
            pipeline.currentPlatformRenderPipelineSettings = settings;
            EditorUtility.SetDirty(pipeline);
            foreach (Camera camera in room.GetComponentsInChildren<Camera>(true))
            {
                var hd = camera.GetComponent<HDAdditionalCameraData>();
                if (hd == null) continue;
                hd.customRenderingSettings = true;
                foreach (FrameSettingsField field in new[] { FrameSettingsField.LightLayers,
                    FrameSettingsField.AtmosphericScattering, FrameSettingsField.Volumetrics })
                {
                    hd.renderingPathCustomFrameSettings.SetEnabled(field, true);
                    var mask = hd.renderingPathCustomFrameSettingsOverrideMask;
                    mask.mask[(uint)field] = true;
                    hd.renderingPathCustomFrameSettingsOverrideMask = mask;
                }
            }

            // Keep the default world lighting bit; add only the dedicated performer bit.
            foreach (Renderer renderer in performer.GetComponentsInChildren<Renderer>(true))
                renderer.renderingLayerMask |= PerformerLayer;
            Transform rig = Child(performer.transform, "Character Visibility Lights");
            rig.localPosition = Vector3.zero;
            rig.localRotation = Quaternion.identity;
            rig.localScale = Vector3.one;
            Point(rig, "Soft Character Key", new Vector3(-1.1f, 1.65f, 1.1f),
                new Color(1f, 0.91f, 0.87f), 1600f, true);
            Point(rig, "Soft Character Fill", new Vector3(1.1f, 1.1f, -0.8f),
                new Color(0.78f, 0.86f, 1f), 950f, true);

            Material neon = AssetDatabase.LoadAssetAtPath<Material>("Assets/FirstPerformanceVoid/Materials/M_NeonMagenta.mat");
            if (neon == null) throw new InvalidOperationException("Lounge neon material is missing.");
            HDMaterial.SetUseEmissiveIntensity(neon, true);
            HDMaterial.SetEmissiveColor(neon, Magenta);
            HDMaterial.SetEmissiveIntensity(neon, 3500f, EmissiveIntensityUnit.Nits);
            HDMaterial.ValidateMaterial(neon);
            EditorUtility.SetDirty(neon);

            Transform worldLights = room.Find("Environment/Lights");
            foreach (Light light in worldLights.GetComponentsInChildren<Light>(true))
            {
                if (!light.name.StartsWith("Magenta", StringComparison.Ordinal)) continue;
                light.color = Magenta;
                light.GetComponent<HDAdditionalLightData>().volumetricDimmer = 1f;
            }
            FirstPerformanceVoidSmoke.Apply(room);
            ApplySkinMaterials(performer);
            var controls = room.GetComponent<FirstPerformanceVoidControls>();
            if (controls != null)
            {
                var serialized = new SerializedObject(controls);
                serialized.FindProperty("lightingRevision").intValue = CurrentRevision;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ApplySkinMaterials(SuccubusPerformer performer)
        {
            const string folder = "Assets/FirstPerformanceVoid/Materials/LaraSkin";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/FirstPerformanceVoid/Materials", "LaraSkin");
            string[] skinNames = { "Face", "Ears", "Torso", "Arms", "Legs", "Lips", "EyeSocket" };
            foreach (Renderer renderer in performer.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) continue;
                    string skinName = source.name.Replace("M_LaraSkin_", "");
                    if (!skinNames.Contains(skinName) || !source.HasProperty("_Roughness")) continue;
                    string path = folder + "/M_LaraSkin_" + skinName + ".mat";
                    Material skin = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (skin == null)
                    {
                        skin = new Material(source) { name = "M_LaraSkin_" + skinName };
                        AssetDatabase.CreateAsset(skin, path);
                    }
                    // Keep imported shader and all maps. Reduce the oily second lobe and coat.
                    Set(skin, "_Roughness", 0.72f);
                    Set(skin, "_SpecularLobe1Roughness", 0.72f);
                    Set(skin, "_SpecularLobe2Roughness", 0.6f);
                    Set(skin, "_GlossyLayeredWeight", 0.5f);
                    Set(skin, "_DualLobeSpecularWeight", 0.2f);
                    Set(skin, "_TopCoatWeight", 0.03f);
                    HDMaterial.ValidateMaterial(skin);
                    EditorUtility.SetDirty(skin);
                    materials[i] = skin;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private static void Set(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Point(Transform parent, string name, Vector3 offset, Color color, float candela, bool characterOnly)
        {
            Transform item = Child(parent, name);
            item.localPosition = offset;
            Light light = item.GetComponent<Light>();
            if (light == null) light = item.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = characterOnly ? 5f : 12f;
            light.lightUnit = LightUnit.Candela;
            light.intensity = candela;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            var hd = item.GetComponent<HDAdditionalLightData>();
            if (hd == null) hd = item.gameObject.AddComponent<HDAdditionalLightData>();
            hd.SetLightLayer((RenderingLayerMask)(characterOnly ? PerformerLayer : 1u), (RenderingLayerMask)1u);
            hd.affectDiffuse = characterOnly;
            hd.affectSpecular = characterOnly;
            hd.volumetricDimmer = characterOnly ? 0f : 1f;
        }
    }
}
