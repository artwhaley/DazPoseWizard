using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Installs permanent project-owned dissolve shaders and Lara materials.</summary>
    public static class PerformerDissolveSetup
    {
        private const string ScenePath = "Assets/Scenes/FirstPerformanceVoid.unity";
        private const string Folder = "Assets/DazPose/Effects/Dissolve";
        private const string RuntimeMaterialsFolder = Folder + "/LaraRuntimeMaterials";
        private const string ProfilePath = Folder + "/FirstContactDissolveProfile.asset";
        private const string SssShaderPath = Folder + "/Shaders/uDTU HDRP SSS Dissolve.shadergraph";
        private const string WetShaderPath = Folder + "/Shaders/Wet Dissolve.shadergraph";

        private static readonly string[] SourceMaterialPaths =
        {
            "Assets/FirstPerformanceVoid/Materials/LaraSkin/M_LaraSkin_Torso.mat",
            "Assets/FirstPerformanceVoid/Materials/LaraSkin/M_LaraSkin_Face.mat",
            "Assets/FirstPerformanceVoid/Materials/LaraSkin/M_LaraSkin_Lips.mat",
            "Assets/Daz3D/larabridge/Genesis8Female/Teeth.mat",
            "Assets/FirstPerformanceVoid/Materials/LaraSkin/M_LaraSkin_Ears.mat",
            "Assets/FirstPerformanceVoid/Materials/LaraSkin/M_LaraSkin_Legs.mat",
            "Assets/FirstPerformanceVoid/Materials/LaraSkin/M_LaraSkin_EyeSocket.mat",
            "Assets/Daz3D/larabridge/Genesis8Female/Mouth.mat",
            "Assets/FirstPerformanceVoid/Materials/LaraSkin/M_LaraSkin_Arms.mat",
            "Assets/Daz3D/larabridge/Genesis8Female/Pupils.mat",
            "Assets/Daz3D/larabridge/Genesis8Female/EyeMoisture.mat",
            "Assets/Daz3D/larabridge/Genesis8Female/Fingernails.mat",
            "Assets/Daz3D/larabridge/Genesis8Female/Irises.mat",
            "Assets/Daz3D/larabridge/Genesis8Female/Sclera.mat",
            "Assets/Daz3D/larabridge/Genesis8Female/Cornea.mat",
            "Assets/Daz3D/larabridge/Genesis8Female/Toenails.mat"
        };

        [MenuItem("Tools/DAZ Pose/First Performance Void/Install Dissolve Acceptance Harness")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Install dissolve shader acceptance in Edit Mode.");

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the existing FirstPerformanceVoid scene before installing dissolve shader acceptance.");

            FirstPerformanceVoidControls controls = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FirstPerformanceVoidControls>(true)).SingleOrDefault();
            if (controls == null)
                throw new InvalidOperationException("The loaded FirstPerformanceVoid scene has no FirstPerformanceVoidControls.");

            SerializedObject controlsData = new SerializedObject(controls);
            SuccubusPerformer performer = Read<SuccubusPerformer>(controlsData, "performer");
            if (performer == null || performer.gameObject.scene != scene)
                throw new InvalidOperationException("The existing Lara performer reference is required.");

            EnsureFolder("Assets/DazPose/Effects");
            EnsureFolder(Folder);
            EnsureFolder(RuntimeMaterialsFolder);
            AssetDatabase.Refresh();

            Shader sssShader = LoadShader(SssShaderPath);
            Shader wetShader = LoadShader(WetShaderPath);
            SkinnedMeshRenderer targetRenderer = FindLaraRenderer(performer);
            Material[] runtimeMaterials = CreateRuntimeMaterials(sssShader, wetShader);
            PerformerDissolveProfile profile = FindProfile(performer);
            profile.ConfigureNativeMaterials(sssShader, wetShader, runtimeMaterials);
            EditorUtility.SetDirty(profile);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install Native Dissolve Shader Acceptance");
            try
            {
                Undo.RecordObject(targetRenderer, "Assign Permanent Lara Dissolve Materials");
                targetRenderer.sharedMaterials = runtimeMaterials;

                PerformerDissolveRig rig = performer.GetComponent<PerformerDissolveRig>();
                if (rig == null) rig = Undo.AddComponent<PerformerDissolveRig>(performer.gameObject);
                VisualEffect visualEffect = performer.GetComponentInChildren<VisualEffect>(true);
                SerializedObject rigData = new SerializedObject(rig);
                rigData.FindProperty("targetRenderer").objectReferenceValue = targetRenderer;
                rigData.FindProperty("visualEffect").objectReferenceValue = visualEffect;
                rigData.ApplyModifiedProperties();

                SerializedObject performerData = new SerializedObject(performer);
                SerializedProperty profileProperty = performerData.FindProperty("dissolveProfile");
                SerializedProperty rigProperty = performerData.FindProperty("dissolveRig");
                if (profileProperty == null || rigProperty == null)
                    throw new InvalidOperationException("SuccubusPerformer does not expose its dissolve profile and rig references.");
                profileProperty.objectReferenceValue = profile;
                rigProperty.objectReferenceValue = rig;
                performerData.ApplyModifiedProperties();

                EditorUtility.SetDirty(targetRenderer);
                EditorUtility.SetDirty(rig);
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

            Debug.Log("NATIVE_DISSOLVE_SHADER_ACCEPTANCE_INSTALLED: permanently assigned 16 project-owned Lara materials (14 uDTU SSS, 2 Wet); runtime control uses one MaterialPropertyBlock. Existing INAB variants were left untouched for rollback. No camera, lighting, smoke, environment, or vendor source material was changed.", controls);
        }

        [MenuItem("Tools/DAZ Pose/First Performance Void/Validate Native Dissolve Shader Setup")]
        public static void ValidateInstalledSetup()
        {
            PerformerDissolveProfile profile = AssetDatabase.LoadAssetAtPath<PerformerDissolveProfile>(ProfilePath);
            if (profile == null)
                throw new InvalidOperationException("The native dissolve profile asset is missing.");
            if (!profile.IsShaderReady(out string profileReason))
                throw new InvalidOperationException(profileReason);

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open FirstPerformanceVoid before validating its renderer and rig assignments.");
            SuccubusPerformer performer = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SuccubusPerformer>(true)).SingleOrDefault();
            PerformerDissolveRig rig = performer != null ? performer.GetComponent<PerformerDissolveRig>() : null;
            if (rig == null)
                throw new InvalidOperationException("The FirstPerformanceVoid scene has no Lara dissolve rig.");
            if (!rig.IsShaderReady(profile, out string rigReason))
                throw new InvalidOperationException(rigReason);

            ValidateIncompatibleAssignmentsRejected(profile, rig);
            ValidateSourceMaterialsRemainSeparate(profile);
            Debug.Log("NATIVE_DISSOLVE_SHADER_SETUP_VALID: 16 materials, 14 uDTU SSS, 2 Wet; required properties and exact assignments are valid; profile and rig reject incompatible material assignments; source DAZ materials remain separate.", performer);
        }

        [MenuItem("Tools/DAZ Pose/First Performance Void/Run Dissolve Material Stability Check")]
        public static void RunMaterialStabilityCheck()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Run the material stability check in Play Mode.");

            SuccubusPerformer performer = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            if (performer == null || !performer.DissolveShaderAcceptanceAvailable)
                throw new InvalidOperationException("The active scene has no ready native dissolve shader performer.");
            SkinnedMeshRenderer renderer = performer.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null) throw new InvalidOperationException("The performer has no SkinnedMeshRenderer.");

            Material[] before = renderer.sharedMaterials;
            try
            {
                performer.SetDissolveShaderAcceptanceState(false, 0f);
                performer.SetDissolveShaderAcceptanceState(true, 0.25f);
                performer.SetDissolveShaderAcceptanceState(true, 0.5f);
                performer.SetDissolveShaderAcceptanceState(true, 0.75f);
                performer.SetDissolveShaderAcceptanceState(true, 1f);
                performer.SetDissolveShaderAcceptanceState(true, 0.5f);
            }
            finally
            {
                performer.SetDissolveShaderAcceptanceState(false, 0f);
            }

            Material[] after = renderer.sharedMaterials;
            if (before.Length != after.Length)
                throw new InvalidOperationException("The renderer material slot count changed during the dissolve progress cycle.");
            for (int i = 0; i < before.Length; i++)
                if (before[i] != after[i])
                    throw new InvalidOperationException("Renderer material reference changed at slot " + i + " during the dissolve progress cycle.");

            Debug.Log("DISSOLVE_MATERIAL_STABILITY_CHECK_PASSED: renderer material references did not change during progress 0→25→50→75→100→50→0; dissolve was disabled at the end.", performer);
        }

        private static Shader LoadShader(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
                throw new InvalidOperationException("Unity has not imported the project-owned Shader Graph at " + path + ". Resolve its import messages before installing the materials.");
            return shader;
        }

        private static SkinnedMeshRenderer FindLaraRenderer(SuccubusPerformer performer)
        {
            SkinnedMeshRenderer[] renderers = performer.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.sharedMesh != null).ToArray();
            if (renderers.Length != 1)
                throw new InvalidOperationException("The FirstPerformanceVoid setup expects Lara's one SkinnedMeshRenderer. Found " + renderers.Length + ".");
            if (renderers[0].sharedMaterials.Length != SourceMaterialPaths.Length)
                throw new InvalidOperationException("Lara's renderer no longer has the expected 16 material slots.");
            return renderers[0];
        }

        private static Material[] CreateRuntimeMaterials(Shader sssShader, Shader wetShader)
        {
            var result = new Material[SourceMaterialPaths.Length];
            for (int i = 0; i < SourceMaterialPaths.Length; i++)
            {
                string sourcePath = SourceMaterialPaths[i];
                Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                if (source == null)
                    throw new InvalidOperationException("Accepted source material is missing at " + sourcePath + ".");

                Shader targetShader = IsWetSlot(i) ? wetShader : sssShader;
                string fileName = "M_Lara_Runtime_" + i.ToString("00") + "_" + SafeAssetName(source.name) + ".mat";
                string destinationPath = RuntimeMaterialsFolder + "/" + fileName;
                Material destination = AssetDatabase.LoadAssetAtPath<Material>(destinationPath);
                if (destination == null)
                {
                    if (File.Exists(destinationPath))
                        throw new InvalidOperationException("A runtime material file exists but Unity could not import it: " + destinationPath);
                    if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                        throw new InvalidOperationException("Could not make the permanent runtime material copy: " + destinationPath);
                    AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
                    destination = AssetDatabase.LoadAssetAtPath<Material>(destinationPath);
                }

                if (destination == null)
                    throw new InvalidOperationException("Could not load the permanent runtime material copy: " + destinationPath);
                if (destination.shader != targetShader)
                {
                    Undo.RecordObject(destination, "Assign Native Dissolve Shader");
                    EditorUtility.CopySerialized(source, destination);
                    destination.shader = targetShader;
                }
                destination.name = Path.GetFileNameWithoutExtension(destinationPath);
                EditorUtility.SetDirty(destination);
                result[i] = destination;
            }
            return result;
        }

        private static PerformerDissolveProfile FindProfile(SuccubusPerformer performer)
        {
            SerializedObject performerData = new SerializedObject(performer);
            SerializedProperty property = performerData.FindProperty("dissolveProfile");
            if (property == null) throw new InvalidOperationException("SuccubusPerformer has no dissolve profile field.");
            PerformerDissolveProfile profile = property.objectReferenceValue as PerformerDissolveProfile;
            if (profile != null) return profile;

            profile = AssetDatabase.LoadAssetAtPath<PerformerDissolveProfile>(ProfilePath);
            if (profile != null) return profile;
            throw new InvalidOperationException("The existing P0.G dissolve profile is missing at " + ProfilePath + ". Run the earlier P0.G setup before installing G1.");
        }

        private static void ValidateSourceMaterialsRemainSeparate(PerformerDissolveProfile profile)
        {
            for (int i = 0; i < SourceMaterialPaths.Length; i++)
            {
                Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPaths[i]);
                if (source == null)
                    throw new InvalidOperationException("Original DAZ/accepted source material is missing: " + SourceMaterialPaths[i]);
                if (source.shader == profile.SssDissolveShader || source.shader == profile.WetDissolveShader)
                    throw new InvalidOperationException("A source material was assigned a project-owned dissolve shader: " + SourceMaterialPaths[i]);
            }
        }

        private static void ValidateIncompatibleAssignmentsRejected(PerformerDissolveProfile profile, PerformerDissolveRig sceneRig)
        {
            Material wrongMaterial = new Material(profile.WetDissolveShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            PerformerDissolveProfile incompatibleProfile = ScriptableObject.CreateInstance<PerformerDissolveProfile>();
            incompatibleProfile.hideFlags = HideFlags.HideAndDontSave;
            GameObject probeObject = new GameObject("Native Dissolve Validation Probe")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                Material[] profileMaterials = profile.LaraRuntimeMaterials.ToArray();
                profileMaterials[0] = wrongMaterial;
                incompatibleProfile.ConfigureNativeMaterials(profile.SssDissolveShader,
                    profile.WetDissolveShader, profileMaterials);
                if (incompatibleProfile.IsShaderReady(out _))
                    throw new InvalidOperationException("The dissolve profile accepted a Wet material in Lara's SSS slot 0.");

                SkinnedMeshRenderer probeRenderer = probeObject.AddComponent<SkinnedMeshRenderer>();
                SkinnedMeshRenderer sourceRenderer = sceneRig.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .SingleOrDefault(renderer => renderer.sharedMesh != null);
                if (sourceRenderer == null)
                    throw new InvalidOperationException("The dissolve validation probe could not find the scene rig's target renderer.");
                probeRenderer.sharedMesh = sourceRenderer.sharedMesh;
                Material[] rendererMaterials = profile.LaraRuntimeMaterials.ToArray();
                rendererMaterials[0] = wrongMaterial;
                probeRenderer.sharedMaterials = rendererMaterials;

                PerformerDissolveRig probeRig = probeObject.AddComponent<PerformerDissolveRig>();
                SerializedObject probeRigData = new SerializedObject(probeRig);
                probeRigData.FindProperty("targetRenderer").objectReferenceValue = probeRenderer;
                probeRigData.ApplyModifiedPropertiesWithoutUndo();
                if (probeRig.IsShaderReady(profile, out _))
                    throw new InvalidOperationException("The dissolve rig accepted renderer material references that differ from the profile.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probeObject);
                UnityEngine.Object.DestroyImmediate(incompatibleProfile);
                UnityEngine.Object.DestroyImmediate(wrongMaterial);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
                throw new InvalidOperationException("Cannot create asset folder without a valid parent: " + path);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static T Read<T>(SerializedObject serializedObject, string propertyName) where T : UnityEngine.Object
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static bool IsWetSlot(int index) => index == 10 || index == 14;

        private static string SafeAssetName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value.Replace(' ', '_');
        }
    }
}
