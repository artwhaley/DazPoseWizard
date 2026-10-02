using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using INab.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Installs only the P0.G Lara dissolve rig, profile, project-owned materials, and controls.</summary>
    public static class PerformerDissolveSetup
    {
        private const string ScenePath = "Assets/Scenes/FirstPerformanceVoid.unity";
        private const string Folder = "Assets/DazPose/Effects/Dissolve";
        private const string MaterialsFolder = Folder + "/Materials";
        private const string ProfilePath = Folder + "/FirstContactDissolveProfile.asset";
        private const string TransitPrefabPath = Folder + "/MagentaDissolveTransit.prefab";
        private const string LaraModelPath = "Assets/TestCharacter/lara.fbx";
        private const string ShaderPath = "Assets/INab Studio/Common HDRP/Shaders/Interactive Effect Burn.shadergraph";
        private const string VfxPath = "Assets/INab Studio/Interactive Dissolve/Effects/Attract To Mask Center/Attract Sparks Dissolve.vfx";
        private const string SoundPath = "Assets/teleportsound.mp3";
        private const string MagentaParticleMaterialPath = "Assets/DazPose/Effects/Teleport/M_TeleportMagenta.mat";
        private const string VioletParticleMaterialPath = "Assets/DazPose/Effects/Teleport/M_TeleportViolet.mat";

        [MenuItem("Tools/DAZ Pose/First Performance Void/Install Dissolve Acceptance Harness")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Install dissolve acceptance in Edit Mode.");

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the existing FirstPerformanceVoid scene before installing dissolve acceptance.");

            FirstPerformanceVoidControls controls = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FirstPerformanceVoidControls>(true)).SingleOrDefault();
            if (controls == null)
                throw new InvalidOperationException("The loaded FirstPerformanceVoid scene has no FirstPerformanceVoidControls.");

            SerializedObject controlsData = new SerializedObject(controls);
            SuccubusPerformer performer = Read<SuccubusPerformer>(controlsData, "performer");
            Transform markA = Read<Transform>(controlsData, "teleportMarkA");
            Transform markB = Read<Transform>(controlsData, "teleportMarkB");
            PerformerPose arrivalPose = Read<PerformerPose>(controlsData, "teleportArrivalPose");
            if (performer == null || performer.gameObject.scene != scene)
                throw new InvalidOperationException("The existing Lara performer reference is required.");
            if (markA == null || markB == null || arrivalPose == null || arrivalPose.Clip == null)
                throw new InvalidOperationException("Run Install Teleport Acceptance Harness first so Dissolve can reuse its A/B marks and arrival pose.");

            EnsureFolder("Assets/DazPose/Effects");
            EnsureFolder(Folder);
            EnsureFolder(MaterialsFolder);

            EnsureLaraMeshReadable();
            AssetDatabase.Refresh();
            Shader dissolveShader = LoadDissolveShader();
            VisualEffectAsset dissolveVfx = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(VfxPath);
            if (dissolveVfx == null)
                throw new InvalidOperationException("INAB Attract Sparks Dissolve VFX Graph is unavailable at " + VfxPath
                    + ". Resolve its existing Unity Console import/serialization errors before installing this harness.");

            SkinnedMeshRenderer targetRenderer = FindLaraRenderer(performer);
            Material[] dissolveMaterials = CreateMaterialVariants(targetRenderer, dissolveShader);
            Material magenta = AssetDatabase.LoadAssetAtPath<Material>(MagentaParticleMaterialPath);
            Material violet = AssetDatabase.LoadAssetAtPath<Material>(VioletParticleMaterialPath);
            if (magenta == null || violet == null)
                throw new InvalidOperationException("The project-owned magenta/violet particle materials from the Teleport harness are required.");

            GameObject transitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TransitPrefabPath);
            if (transitPrefab == null) transitPrefab = CreateTransitPrefab(magenta, violet);

            PerformerDissolveProfile profile = FindOrCreateProfile(performer);
            AudioClip teleportAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
            profile.ConfigureIfMissing(dissolveVfx, transitPrefab, dissolveMaterials, teleportAudio, teleportAudio);
            if (!profile.IsReady(out string profileReason)) throw new InvalidOperationException(profileReason);
            EditorUtility.SetDirty(profile);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install Dissolve Acceptance Harness");
            try
            {
                PerformerDissolveRig rig = ConfigureRig(performer, targetRenderer, dissolveVfx);
                SerializedObject performerData = new SerializedObject(performer);
                SerializedProperty profileProperty = performerData.FindProperty("dissolveProfile");
                SerializedProperty rigProperty = performerData.FindProperty("dissolveRig");
                if (profileProperty == null || rigProperty == null)
                    throw new InvalidOperationException("SuccubusPerformer does not expose the P0.G dissolve references.");
                profileProperty.objectReferenceValue = profile;
                rigProperty.objectReferenceValue = rig;
                performerData.ApplyModifiedProperties();

                EditorUtility.SetDirty(performer);
                EditorUtility.SetDirty(controls);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Could not save the loaded FirstPerformanceVoid scene.");
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }

            Debug.Log("DISSOLVE_ACCEPTANCE_INSTALLED: graph=" + VfxPath + "; profile=" + ProfilePath
                + "; transit=" + TransitPrefabPath + "; material slots=" + dissolveMaterials.Length
                + "; reused teleport marks A/B and arrival pose. No room, camera, lighting, smoke, Teleport, or INAB source graph was changed.", controls);
        }

        private static Shader LoadDissolveShader()
        {
            AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                throw new InvalidOperationException("Unity has not compiled the extracted INAB HDRP Interactive Effect Burn Shader Graph at "
                    + ShaderPath + ". Resolve its shader import messages, then rerun this installer.");
            return shader;
        }

        private static void EnsureLaraMeshReadable()
        {
            ModelImporter importer = AssetImporter.GetAtPath(LaraModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("The canonical Generic Lara FBX importer was not found at " + LaraModelPath + ".");
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        private static SkinnedMeshRenderer FindLaraRenderer(SuccubusPerformer performer)
        {
            SkinnedMeshRenderer[] renderers = performer.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.sharedMesh != null).ToArray();
            if (renderers.Length != 1)
                throw new InvalidOperationException("P0.G currently targets the single Lara SkinnedMeshRenderer. Found "
                    + renderers.Length + "; no clothing or multi-renderer dissolve was added.");
            if (!renderers[0].sharedMesh.isReadable)
                throw new InvalidOperationException("Lara's FBX was marked readable but has not finished reimporting. Wait for the targeted import, then rerun the installer.");
            if (renderers[0].sharedMaterials.Length == 0)
                throw new InvalidOperationException("Lara's SkinnedMeshRenderer has no material slots to create dissolve variants for.");
            return renderers[0];
        }

        private static Material[] CreateMaterialVariants(SkinnedMeshRenderer renderer, Shader shader)
        {
            Material[] sourceMaterials = renderer.sharedMaterials;
            var variants = new Material[sourceMaterials.Length];
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material source = sourceMaterials[i];
                if (source == null)
                    throw new InvalidOperationException("Lara's material slot " + i + " is empty; refusing to create an incomplete dissolve set.");

                string path = MaterialsFolder + "/M_Lara_Dissolve_" + i.ToString("00") + "_" + SafeAssetName(source.name) + ".mat";
                Material variant = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (variant != null)
                {
                    if (variant.shader != shader)
                        throw new InvalidOperationException("Existing project-owned dissolve material uses another shader: " + path);
                    variants[i] = variant;
                    continue;
                }

                variant = new Material(shader) { name = Path.GetFileNameWithoutExtension(path), enableInstancing = true };
                try
                {
                    variant.CopyPropertiesFromMaterial(source);
                    CopyTexture(source, variant, "_BaseMap", "_BaseMap", "_MainTex", "_DiffuseMap", "_BaseColorMap");
                    CopyTexture(source, variant, "_BumpMap", "_BumpMap", "_NormalMap", "_NormalTex");
                    CopyTexture(source, variant, "_MetallicGlossMap", "_MetallicGlossMap", "_MetallicMap");
                    CopyTexture(source, variant, "_OcclusionMap", "_OcclusionMap", "_AOMap");
                    CopyTexture(source, variant, "_EmissionMap", "_EmissionMap", "_EmissiveMap");
                    CopyColor(source, variant, "_BaseColor", "_BaseColor", "_Color", "_DiffuseColor", "_AlbedoColor");
                    CopyColor(source, variant, "_EmissionColor", "_EmissionColor", "_EmissiveColor");
                    CopyFloat(source, variant, "_Metallic", "_Metallic");
                    CopyFloat(source, variant, "_Smoothness", "_Smoothness", "_Glossiness");
                    CopyFloat(source, variant, "_OcclusionStrength", "_OcclusionStrength");
                    CopyFloat(source, variant, "_Cutoff", "_Cutoff", "_AlphaCutoff", "_Alpha_Cutoff_Threshold", "_Cutout");
                    CopyVector(source, variant, "_Tiling", "_Tiling");
                    if (variant.HasProperty("_Smoothness") && source.HasProperty("_Roughness"))
                        variant.SetFloat("_Smoothness", 1f - source.GetFloat("_Roughness"));
                    AssetDatabase.CreateAsset(variant, path);
                    variants[i] = variant;
                }
                catch
                {
                    UnityEngine.Object.DestroyImmediate(variant);
                    throw;
                }
            }
            return variants;
        }

        private static void CopyTexture(Material source, Material destination, string target, params string[] aliases)
        {
            if (!destination.HasProperty(target)) return;
            foreach (string alias in aliases)
            {
                if (!source.HasProperty(alias)) continue;
                Texture texture = source.GetTexture(alias);
                if (texture == null) continue;
                destination.SetTexture(target, texture);
                destination.SetTextureScale(target, source.GetTextureScale(alias));
                destination.SetTextureOffset(target, source.GetTextureOffset(alias));
                return;
            }
            if (target == "_BaseMap" && source.mainTexture != null)
                destination.SetTexture(target, source.mainTexture);
        }

        private static void CopyColor(Material source, Material destination, string target, params string[] aliases)
        {
            if (!destination.HasProperty(target)) return;
            foreach (string alias in aliases)
            {
                if (!source.HasProperty(alias)) continue;
                destination.SetColor(target, source.GetColor(alias));
                return;
            }
        }

        private static void CopyFloat(Material source, Material destination, string target, params string[] aliases)
        {
            if (!destination.HasProperty(target)) return;
            foreach (string alias in aliases)
            {
                if (!source.HasProperty(alias)) continue;
                destination.SetFloat(target, source.GetFloat(alias));
                return;
            }
        }

        private static void CopyVector(Material source, Material destination, string target, params string[] aliases)
        {
            if (!destination.HasProperty(target)) return;
            foreach (string alias in aliases)
            {
                if (!source.HasProperty(alias)) continue;
                destination.SetVector(target, source.GetVector(alias));
                return;
            }
        }

        private static PerformerDissolveProfile FindOrCreateProfile(SuccubusPerformer performer)
        {
            SerializedObject performerData = new SerializedObject(performer);
            SerializedProperty profileProperty = performerData.FindProperty("dissolveProfile");
            if (profileProperty == null) throw new InvalidOperationException("SuccubusPerformer has no dissolve profile field.");
            PerformerDissolveProfile profile = profileProperty.objectReferenceValue as PerformerDissolveProfile;
            if (profile != null) return profile;

            profile = AssetDatabase.LoadAssetAtPath<PerformerDissolveProfile>(ProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<PerformerDissolveProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static PerformerDissolveRig ConfigureRig(SuccubusPerformer performer,
            SkinnedMeshRenderer targetRenderer, VisualEffectAsset dissolveVfx)
        {
            PerformerDissolveRig rig = performer.GetComponent<PerformerDissolveRig>();
            if (rig == null) rig = Undo.AddComponent<PerformerDissolveRig>(performer.gameObject);

            Transform root = targetRenderer.transform.Find("Dissolve Effects");
            if (root == null)
            {
                var rootObject = new GameObject("Dissolve Effects");
                Undo.RegisterCreatedObjectUndo(rootObject, "Create Lara Dissolve Effects");
                rootObject.transform.SetParent(targetRenderer.transform, false);
                root = rootObject.transform;
            }

            Transform effectTransform = root.Find("INAB Interactive Dissolve");
            GameObject effectObject;
            InteractiveEffect effect;
            if (effectTransform == null)
            {
                effectObject = new GameObject("INAB Interactive Dissolve");
                Undo.RegisterCreatedObjectUndo(effectObject, "Create INAB Interactive Dissolve");
                effectObject.transform.SetParent(root, false);
                effectObject.SetActive(false);
                effect = Undo.AddComponent<InteractiveEffect>(effectObject);
            }
            else
            {
                effectObject = effectTransform.gameObject;
                effect = effectObject.GetComponent<InteractiveEffect>();
                if (effect == null) effect = Undo.AddComponent<InteractiveEffect>(effectObject);
                if (effectObject.activeSelf) Undo.RecordObject(effectObject, "Disable INAB Dissolve Effect Until Needed");
                effectObject.SetActive(false);
            }

            Transform vfxTransform = effectObject.transform.Find("VFX Graph");
            GameObject vfxObject;
            VisualEffect visualEffect;
            if (vfxTransform == null)
            {
                vfxObject = new GameObject("VFX Graph");
                Undo.RegisterCreatedObjectUndo(vfxObject, "Create INAB Dissolve VFX Graph");
                vfxObject.transform.SetParent(effectObject.transform, false);
                visualEffect = Undo.AddComponent<VisualEffect>(vfxObject);
                Undo.AddComponent<VFXPropertyBinder>(vfxObject);
            }
            else
            {
                vfxObject = vfxTransform.gameObject;
                visualEffect = vfxObject.GetComponent<VisualEffect>();
                if (visualEffect == null) visualEffect = Undo.AddComponent<VisualEffect>(vfxObject);
                if (vfxObject.GetComponent<VFXPropertyBinder>() == null)
                    Undo.AddComponent<VFXPropertyBinder>(vfxObject);
            }
            visualEffect.visualEffectAsset = dissolveVfx;

            Transform maskTransform = effectObject.transform.Find("Mask");
            GameObject maskObject;
            InteractiveEffectMask mask;
            if (maskTransform == null)
            {
                maskObject = new GameObject("Mask");
                Undo.RegisterCreatedObjectUndo(maskObject, "Create INAB Dissolve Mask");
                maskObject.transform.SetParent(effectObject.transform, false);
                mask = Undo.AddComponent<InteractiveEffectMask>(maskObject);
            }
            else
            {
                maskObject = maskTransform.gameObject;
                mask = maskObject.GetComponent<InteractiveEffectMask>();
                if (mask == null) mask = Undo.AddComponent<InteractiveEffectMask>(maskObject);
            }

            Undo.RecordObject(effect, "Wire INAB Dissolve Effect");
            effect.visualEffect = visualEffect;
            effect.propertyBinder = vfxObject.GetComponent<VFXPropertyBinder>();
            effect.meshTransform = targetRenderer.transform;
            effect.meshRenderer = targetRenderer;
            effect.mask = mask;
            effect.useVFXGraphEffect = true;
            effect.usePositionTransform = false;
            effect.useScaleTransform = true;
            effect.useRotationTransform = false;
            effect.useInstancedMaterials = false;
            effect.ChangeMaskType(InteractiveEffectMaskType.Ellipse);

            SerializedObject rigData = new SerializedObject(rig);
            SetObject(rigData, "targetRenderer", targetRenderer);
            SetObject(rigData, "interactiveEffect", effect);
            SetObject(rigData, "visualEffect", visualEffect);
            SetObject(rigData, "mask", mask.transform);
            rigData.ApplyModifiedProperties();
            EditorUtility.SetDirty(rig);
            EditorUtility.SetDirty(effect);
            EditorUtility.SetDirty(visualEffect);
            return rig;
        }

        private static GameObject CreateTransitPrefab(Material magenta, Material violet)
        {
            GameObject root = new GameObject("Magenta Dissolve Transit");
            try
            {
                CreateMoteEmitter(root.transform, "Magenta Motes", magenta,
                    rate: 82f, lifetimeMin: 0.55f, lifetimeMax: 1.15f,
                    sizeMin: 0.025f, sizeMax: 0.085f, speedMax: 0.36f, radius: 0.48f);
                CreateMoteEmitter(root.transform, "Violet Glints", violet,
                    rate: 39f, lifetimeMin: 0.45f, lifetimeMax: 0.95f,
                    sizeMin: 0.035f, sizeMax: 0.11f, speedMax: 0.24f, radius: 0.34f);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TransitPrefabPath);
                if (prefab == null) throw new InvalidOperationException("Unity could not save the project-owned dissolve transit prefab.");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateMoteEmitter(Transform parent, string name, Material material,
            float rate, float lifetimeMin, float lifetimeMax, float sizeMin, float sizeMax,
            float speedMax, float radius)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            ParticleSystem particles = item.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(2.3f, 0.08f, 3.2f, 0.96f), new Color(0.5f, 0.08f, 2.5f, 0.72f));
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 1f;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = CreateMoteFade();
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f)));
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.13f;
            noise.frequency = 0.8f;
            ParticleSystem.TrailModule trails = particles.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.lifetime = 0.18f;
            trails.ratio = 0.22f;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.38f);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.material = material;
            renderer.trailMaterial = material;
            renderer.sortingFudge = 4f;
        }

        private static Gradient CreateMoteFade()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.7f, 1f), 0f),
                    new GradientColorKey(new Color(0.9f, 0.12f, 1f), 0.55f),
                    new GradientColorKey(new Color(0.3f, 0.04f, 0.85f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.92f, 0.12f),
                    new GradientAlphaKey(0.65f, 0.58f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
                throw new InvalidOperationException("Required parent folder does not exist: " + parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static string SafeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Material";
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalid.Contains(character) || character == ' ' ? '_' : character).ToArray());
        }

        private static T Read<T>(SerializedObject source, string name) where T : UnityEngine.Object =>
            source.FindProperty(name)?.objectReferenceValue as T;

        private static void SetObject(SerializedObject source, string name, UnityEngine.Object value)
        {
            SerializedProperty property = source.FindProperty(name);
            if (property == null) throw new InvalidOperationException("Missing serialized dissolve rig field: " + name);
            property.objectReferenceValue = value;
        }
    }
}
