using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DazPose.Editor.ParticleBody;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Builds the project-owned VFX graph and wires the isolated P0.G2 scene controls.</summary>
    public static class PerformerParticleBodyAcceptanceSetup
    {
        private const string ScenePath = "Assets/Scenes/FirstPerformanceVoid.unity";
        private const string EffectsFolder = "Assets/DazPose/Effects";
        private const string ParticleBodyFolder = EffectsFolder + "/ParticleBody";
        private const string BindingAssetPath = ParticleBodyFolder + "/PerformerSurfaceBindings.asset";
        private const string GraphAssetPath = ParticleBodyFolder + "/PerformerParticleBody.vfx";

        [MenuItem("Tools/DAZ Pose/First Performance Void/Install Particle Body Acceptance Harness")]
        public static void Install()
        {
            InstallInternal(false);
        }

        [MenuItem("Tools/DAZ Pose/First Performance Void/Rebuild Particle Body VFX Graph")]
        public static void RebuildGraph()
        {
            if (!EditorUtility.DisplayDialog("Rebuild Particle Body VFX Graph",
                    "Replace the project-owned PerformerParticleBody.vfx graph with a newly generated P0.G2 graph? Any manual edits made inside that graph will be lost.",
                    "Rebuild", "Cancel"))
                return;

            InstallInternal(true);
        }

        [MenuItem("Tools/DAZ Pose/First Performance Void/Validate Particle Body Acceptance Harness")]
        public static void ValidateInstalledSetup()
        {
            Scene scene = GetLoadedScene();
            FirstPerformanceVoidControls controls = FindControls(scene);
            SuccubusPerformer performer = Read<SuccubusPerformer>(new SerializedObject(controls), "performer");
            if (performer == null || performer.gameObject.scene != scene)
                throw new InvalidOperationException("The existing Lara performer reference is missing from FirstPerformanceVoidControls.");

            SkinnedMeshRenderer renderer = FindLaraRenderer(performer);
            PerformerParticleBody body = controls.GetComponent<PerformerParticleBody>();
            if (body == null)
                throw new InvalidOperationException("The scene controls GameObject has no PerformerParticleBody component. Run Install Particle Body Acceptance Harness in Edit Mode.");
            if (!body.ValidateConfiguration(out string reason))
                throw new InvalidOperationException(reason);
            PerformerSurfaceBindingAsset bindings = Read<PerformerSurfaceBindingAsset>(new SerializedObject(body), "surfaceBindings");
            PerformerSurfaceBindingBaker.ValidateBakedAsset(bindings, renderer);
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphAssetPath) == null)
                throw new InvalidOperationException("The project-owned VFX Graph is missing at " + GraphAssetPath + ".");
            if (body.BindingCount != PerformerSurfaceBindingAsset.RequiredBindingCount)
                throw new InvalidOperationException("The particle body must have exactly 32,768 surface bindings.");

            Debug.Log("PARTICLE_BODY_ACCEPTANCE_SETUP_VALID: Lara renderer, current-topology surface bindings, project-owned VFX graph, and isolated scene controls are assigned. No Play Mode or visual acceptance check was performed.", body);
        }

        private static void InstallInternal(bool rebuildGraph)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Install the particle body acceptance harness in Edit Mode.");

            Scene scene = GetLoadedScene();
            FirstPerformanceVoidControls controls = FindControls(scene);
            SerializedObject controlsData = new SerializedObject(controls);
            SuccubusPerformer performer = Read<SuccubusPerformer>(controlsData, "performer");
            if (performer == null || performer.gameObject.scene != scene)
                throw new InvalidOperationException("The existing Lara performer reference is required on FirstPerformanceVoidControls.");

            SkinnedMeshRenderer renderer = FindLaraRenderer(performer);
            if (!renderer.sharedMesh.isReadable)
                throw new InvalidOperationException("Lara's source mesh is not readable. P0.G2 needs Edit Mode mesh reads for the one-time area-weighted binding bake; enable Read/Write on the existing source mesh, then run this command again.");

            EnsureFolder(EffectsFolder);
            EnsureFolder(ParticleBodyFolder);

            PerformerSurfaceBindingAsset bindings = PerformerSurfaceBindingBaker.Bake(renderer,
                PerformerSurfaceBindingAsset.RequiredBindingCount, 0x504f3942, BindingAssetPath);
            PerformerSurfaceBindingBaker.ValidateBakedAsset(bindings, renderer);
            VisualEffectAsset graph = PerformerParticleBodyVfxGraphBuilder.GetOrCreate(GraphAssetPath, rebuildGraph);
            if (graph == null)
                throw new InvalidOperationException("The project-owned PerformerParticleBody VFX Graph could not be created.");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install Particle Body Acceptance Harness");
            try
            {
                PerformerParticleBody body = controls.GetComponent<PerformerParticleBody>();
                if (body == null) body = Undo.AddComponent<PerformerParticleBody>(controls.gameObject);

                Undo.RecordObjects(new UnityEngine.Object[] { controls, body }, "Assign Particle Body Acceptance Harness");
                SerializedObject bodyData = new SerializedObject(body);
                Require(bodyData, "targetRenderer").objectReferenceValue = renderer;
                Require(bodyData, "surfaceBindings").objectReferenceValue = bindings;
                Require(bodyData, "visualEffectAsset").objectReferenceValue = graph;
                bodyData.ApplyModifiedProperties();

                controlsData = new SerializedObject(controls);
                Require(controlsData, "particleBody").objectReferenceValue = body;
                controlsData.ApplyModifiedProperties();

                EditorUtility.SetDirty(body);
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

            Debug.Log("PARTICLE_BODY_ACCEPTANCE_INSTALLED: baked 32,768 deterministic area-weighted bindings, generated the project-owned live-skinned VFX Graph, and wired the existing FirstPerformanceVoid control panel. Camera, performer transform, lights, smoke, materials, and DissolveTo were not changed. Restart Play Mode before using the PARTICLE BODY buttons.", controls);
        }

        private static Scene GetLoadedScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the existing FirstPerformanceVoid scene before installing the particle body acceptance harness.");
            return scene;
        }

        private static FirstPerformanceVoidControls FindControls(Scene scene)
        {
            FirstPerformanceVoidControls[] found = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FirstPerformanceVoidControls>(true)).ToArray();
            if (found.Length != 1)
                throw new InvalidOperationException("Expected exactly one FirstPerformanceVoidControls component in the loaded scene. Found " + found.Length + ".");
            return found[0];
        }

        private static SkinnedMeshRenderer FindLaraRenderer(SuccubusPerformer performer)
        {
            SkinnedMeshRenderer[] renderers = performer.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(candidate => candidate.sharedMesh != null).ToArray();
            if (renderers.Length != 1)
                throw new InvalidOperationException("The FirstPerformanceVoid harness expects Lara's one SkinnedMeshRenderer. Found " + renderers.Length + ".");
            return renderers[0];
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

        private static SerializedProperty Require(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(serializedObject.targetObject.GetType().Name + " does not expose serialized field '" + propertyName + "'.");
            return property;
        }

        private static T Read<T>(SerializedObject serializedObject, string propertyName) where T : UnityEngine.Object
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }
    }

    /// <summary>
    /// Unity 6.5 keeps the VFX graph authoring model internal. This small adapter uses its installed
    /// editor API by reflection, fails with the missing API/member name, and keeps all graph content
    /// project-owned. It does not reference any vendor graph or package source.
    /// </summary>
    internal static class PerformerParticleBodyVfxGraphBuilder
    {
        private const string HlslPath = "Assets/DazPose/Effects/ParticleBody/PerformerParticleBody.hlsl";

        private sealed class ParameterNode
        {
            public object Model;
            public int NodeId;
        }

        private static readonly BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static VisualEffectAsset GetOrCreate(string path, bool replaceExisting)
        {
            VisualEffectAsset existing = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (existing != null && !replaceExisting) return existing;
            if (existing == null && File.Exists(path) && !replaceExisting)
                throw new InvalidOperationException("A VFX asset file exists but Unity could not load it: " + path + ". Use Rebuild Particle Body VFX Graph to regenerate the project-owned asset.");

            if (existing != null || File.Exists(path))
            {
                if (!AssetDatabase.DeleteAsset(path))
                    throw new InvalidOperationException("Could not replace the existing project-owned VFX Graph at " + path + ".");
            }

            if (!File.Exists(HlslPath))
                throw new InvalidOperationException("The P0.G2 HLSL source is missing at " + HlslPath + ".");

            Type assetEditorUtility = ResolveType("UnityEditor.VisualEffectAssetEditorUtility");
            MethodInfo createNewAsset = assetEditorUtility.GetMethod("CreateNewAsset", AllStatic, null, new[] { typeof(string) }, null);
            if (createNewAsset == null)
                throw new InvalidOperationException("Installed Visual Effect Graph package API is missing VisualEffectAssetEditorUtility.CreateNewAsset(string).");
            VisualEffectAsset asset = createNewAsset.Invoke(null, new object[] { path }) as VisualEffectAsset;
            if (asset == null)
                throw new InvalidOperationException("Unity's Visual Effect Graph editor did not create a VisualEffectAsset at " + path + ".");

            try
            {
                BuildGraph(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
                if (asset == null)
                    throw new InvalidOperationException("Unity did not reload the generated VFX Graph at " + path + ". Check the first VFX Graph import error in the Console.");
                return asset;
            }
            catch
            {
                AssetDatabase.DeleteAsset(path);
                throw;
            }
        }

        private static void BuildGraph(VisualEffectAsset asset)
        {
            object resource = InvokeExtension("UnityEditor.VFX.VisualEffectObjectExtensions", "GetOrCreateResource", asset);
            object graph = InvokeExtension("UnityEditor.VFX.VisualEffectResourceExtensions", "GetOrCreateGraph", resource);

            object spawner = CreateModel("UnityEditor.VFX.VFXBasicSpawner");
            object initialize = CreateModel("UnityEditor.VFX.VFXBasicInitialize");
            object update = CreateModel("UnityEditor.VFX.VFXBasicUpdate");
            object coreOutput = CreatePlanarQuadOutput();
            object glowOutput = CreatePlanarQuadOutput();

            SetPosition(spawner, new Vector2(100f, 350f));
            SetPosition(initialize, new Vector2(430f, 250f));
            SetPosition(update, new Vector2(770f, 250f));
            SetPosition(coreOutput, new Vector2(1110f, 150f));
            SetPosition(glowOutput, new Vector2(1110f, 430f));
            AddChild(graph, spawner);
            AddChild(graph, initialize);
            AddChild(graph, update);
            AddChild(graph, coreOutput);
            AddChild(graph, glowOutput);
            Call(spawner, "LinkTo", initialize, 0, 0);
            Call(initialize, "LinkTo", update, 0, 0);
            Call(update, "LinkTo", coreOutput, 0, 0);
            Call(update, "LinkTo", glowOutput, 0, 0);

            object data = Call(initialize, "GetData");
            if (data == null) throw new InvalidOperationException("VFX Graph did not create shared particle data for the initialization context.");
            Call(data, "SetSettingValue", "capacity", (uint)PerformerSurfaceBindingAsset.RequiredBindingCount);
            Type boundsModeType = FindField(data.GetType(), "boundsMode").FieldType;
            Call(data, "SetSettingValue", "boundsMode", Enum.Parse(boundsModeType, "Manual"));
            PropertyInfo dataSpace = FindProperty(data.GetType(), "space");
            dataSpace.SetValue(data, Enum.Parse(dataSpace.PropertyType, "World"), null);

            Type integrationType = FindField(update.GetType(), "integration").FieldType;
            Call(update, "SetSettingValue", "integration", Enum.Parse(integrationType, "None"));
            Type angularIntegrationType = FindField(update.GetType(), "angularIntegration").FieldType;
            Call(update, "SetSettingValue", "angularIntegration", Enum.Parse(angularIntegrationType, "None"));
            Call(update, "SetSettingValue", "ageParticles", false);
            Call(update, "SetSettingValue", "reapParticles", false);
            SetManualBounds(initialize);

            ParameterNode targetRenderer = AddParameter(graph, "TargetRenderer", typeof(SkinnedMeshRenderer), null, 20f, 20f);
            ParameterNode surfaceBindings = AddParameter(graph, "SurfaceBindings", typeof(GraphicsBuffer), null, 20f, 95f);
            ParameterNode bindingCount = AddParameter(graph, "BindingCount", typeof(uint), (uint)PerformerSurfaceBindingAsset.RequiredBindingCount, 20f, 170f);
            ParameterNode phase = AddParameter(graph, "Phase", typeof(int), 0, 20f, 245f);
            ParameterNode dissolveProgress = AddParameter(graph, "DissolveProgress", typeof(float), 0f, 20f, 320f);
            ParameterNode departureProgress = AddParameter(graph, "DepartureProgress", typeof(float), 0f, 20f, 395f);
            ParameterNode materializeProgress = AddParameter(graph, "MaterializeProgress", typeof(float), 0f, 20f, 470f);
            ParameterNode transitProgress = AddParameter(graph, "TransitProgress", typeof(float), 0f, 20f, 545f);
            ParameterNode sourceCenter = AddParameter(graph, "SourceCenter", typeof(Vector3), Vector3.zero, 20f, 620f);
            ParameterNode destinationCenter = AddParameter(graph, "DestinationCenter", typeof(Vector3), Vector3.zero, 20f, 695f);
            ParameterNode transitArcHeight = AddParameter(graph, "TransitArcHeight", typeof(float), 0.45f, 20f, 770f);
            ParameterNode cloudScale = AddParameter(graph, "CloudScale", typeof(float), 0.60f, 20f, 845f);
            ParameterNode swirlTurns = AddParameter(graph, "SwirlTurns", typeof(float), 1.25f, 20f, 920f);
            ParameterNode turbulenceStrength = AddParameter(graph, "TurbulenceStrength", typeof(float), 0.025f, 20f, 995f);
            ParameterNode fieldParams = AddParameter(graph, "DissolveFieldParams", typeof(Vector4), new Vector4(3.5f, 0.2f, 17f, 1.15f), 20f, 1070f);
            ParameterNode boundsMin = AddParameter(graph, "DissolveBoundsMin", typeof(Vector3), Vector3.zero, 20f, 1145f);
            ParameterNode boundsSize = AddParameter(graph, "DissolveBoundsSize", typeof(Vector3), Vector3.one, 20f, 1220f);
            ParameterNode coreColor = AddParameter(graph, "CoreColor", typeof(Vector4), new Vector4(1f, 0.86f, 1f, 1f), 20f, 1295f);
            ParameterNode glowColor = AddParameter(graph, "GlowColor", typeof(Vector4), new Vector4(0.72f, 0.12f, 1f, 0.75f), 20f, 1370f);
            ParameterNode coreSize = AddParameter(graph, "CoreSize", typeof(float), 0.006f, 20f, 1445f);
            ParameterNode glowSize = AddParameter(graph, "GlowSize", typeof(float), 0.015f, 20f, 1520f);
            ParameterNode worldToLocal = AddParameter(graph, "WorldToLocalMatrix", typeof(Matrix4x4), Matrix4x4.identity, 20f, 1595f);

            object burst = CreateModel("UnityEditor.VFX.VFXSpawnerBurst");
            AddChild(spawner, burst);
            SetBySettingName(burst, "repeat", "Single");
            SetBySettingName(burst, "spawnMode", "Constant");
            SetBySettingName(burst, "delayMode", "Constant");
            SetSlotValue(FindSlot(burst, "Delay"), 0f);
            LinkParameter(bindingCount, FindSlot(burst, "Count"));

            object sampleBuffer = CreateModel("UnityEditor.VFX.Operator.SampleBuffer");
            Call(sampleBuffer, "SetOperandType", typeof(PerformerSurfaceBinding));
            AddChild(graph, sampleBuffer);
            SetPosition(sampleBuffer, new Vector2(380f, 850f));
            Call(sampleBuffer, "ResyncSlots", true);

            object particleId = CreateModel("UnityEditor.VFX.VFXAttributeParameter");
            SetField(particleId, "attribute", "particleId");
            SetEnumField(particleId, "location", "Current");
            AddChild(graph, particleId);
            SetPosition(particleId, new Vector2(180f, 920f));
            Call(particleId, "ResyncSlots", true);

            LinkParameter(surfaceBindings, FindSlot(sampleBuffer, "buffer"));
            LinkInputToOutput(FindSlot(sampleBuffer, "index"), FirstSlot(particleId, "outputSlots"));

            object initializePosition = CreatePositionMesh(initialize);
            object updatePosition = CreatePositionMesh(update);
            LinkParameter(targetRenderer, FindSlot(initializePosition, "skinnedMesh"));
            LinkParameter(targetRenderer, FindSlot(updatePosition, "skinnedMesh"));
            LinkInputToOutput(FindSlot(initializePosition, "triangle"), FindSlot(sampleBuffer, "Triangle"));
            LinkInputToOutput(FindSlot(initializePosition, "square"), FindSlot(sampleBuffer, "Square"));
            LinkInputToOutput(FindSlot(updatePosition, "triangle"), FindSlot(sampleBuffer, "Triangle"));
            LinkInputToOutput(FindSlot(updatePosition, "square"), FindSlot(sampleBuffer, "Square"));

            AddHlslBlock(initialize, "PerformerParticleBodyInitialize");
            LinkParameter(sourceCenter, FindSlot(LastChildOfType(initialize, "CustomHLSL"), "SourceCenter"));
            LinkInputToOutput(FindSlot(LastChildOfType(initialize, "CustomHLSL"), "SurfaceSeed"), FindSlot(sampleBuffer, "Seed"));

            AddHlslBlock(update, "PerformerParticleBodyUpdate");
            object updateHlsl = LastChildOfType(update, "CustomHLSL");
            LinkParameter(phase, FindSlot(updateHlsl, "Phase"));
            LinkParameter(dissolveProgress, FindSlot(updateHlsl, "DissolveProgress"));
            LinkParameter(departureProgress, FindSlot(updateHlsl, "DepartureProgress"));
            LinkParameter(materializeProgress, FindSlot(updateHlsl, "MaterializeProgress"));
            LinkParameter(transitProgress, FindSlot(updateHlsl, "TransitProgress"));
            LinkParameter(sourceCenter, FindSlot(updateHlsl, "SourceCenter"));
            LinkParameter(destinationCenter, FindSlot(updateHlsl, "DestinationCenter"));
            LinkParameter(transitArcHeight, FindSlot(updateHlsl, "TransitArcHeight"));
            LinkParameter(cloudScale, FindSlot(updateHlsl, "CloudScale"));
            LinkParameter(swirlTurns, FindSlot(updateHlsl, "SwirlTurns"));
            LinkParameter(turbulenceStrength, FindSlot(updateHlsl, "TurbulenceStrength"));
            LinkParameter(fieldParams, FindSlot(updateHlsl, "DissolveFieldParams"));
            LinkParameter(boundsMin, FindSlot(updateHlsl, "DissolveBoundsMin"));
            LinkParameter(boundsSize, FindSlot(updateHlsl, "DissolveBoundsSize"));
            LinkParameter(worldToLocal, FindSlot(updateHlsl, "WorldToLocalMatrix"));

            AddHlslBlock(coreOutput, "PerformerParticleBodyCoreOutput");
            object coreHlsl = LastChildOfType(coreOutput, "CustomHLSL");
            LinkParameter(coreColor, FindSlot(coreHlsl, "CoreColor"));
            LinkParameter(coreSize, FindSlot(coreHlsl, "CoreSize"));

            AddHlslBlock(glowOutput, "PerformerParticleBodyGlowOutput");
            object glowHlsl = LastChildOfType(glowOutput, "CustomHLSL");
            LinkParameter(glowColor, FindSlot(glowHlsl, "GlowColor"));
            LinkParameter(glowSize, FindSlot(glowHlsl, "GlowSize"));

            InvokeExtension("UnityEditor.VFX.VisualEffectResourceExtensions", "WriteAssetWithSubAssets", resource);
        }

        private static object CreatePositionMesh(object context)
        {
            object positionMesh = CreateModel("UnityEditor.VFX.Block.PositionMesh");
            SetEnumField(positionMesh, "sourceMesh", "SkinnedMeshRenderer");
            SetEnumField(positionMesh, "placementMode", "Surface");
            SetEnumField(positionMesh, "surfaceCoordinates", "Uniform");
            SetEnumField(positionMesh, "spawnMode", "Custom");
            SetEnumField(positionMesh, "positionMode", "Surface");
            SetEnumField(positionMesh, "skinnedTransform", "ApplyWorldRootTransform");
            SetEnumField(positionMesh, "applyOrientation", "None");
            SetEnumField(positionMesh, "compositionPosition", "Overwrite");
            AddChild(context, positionMesh);
            Call(positionMesh, "ResyncSlots", true);
            return positionMesh;
        }

        private static object CreatePlanarQuadOutput()
        {
            object output = CreateModel("UnityEditor.VFX.VFXPlanarPrimitiveOutput");
            SetEnumField(output, "primitiveType", "Quad");
            SetEnumField(output, "useBaseColorMap", "ColorAndAlpha");
            SetEnumField(output, "uvMode", "Default");
            return output;
        }

        private static void SetManualBounds(object initialize)
        {
            object boundsSlot = FindSlot(initialize, "bounds");
            object boundsProperty = GetProperty(boundsSlot, "property");
            Type aaBoxType = (Type)GetProperty(boundsProperty, "type");
            object bounds = Activator.CreateInstance(aaBoxType);
            SetMember(bounds, "center", Vector3.zero);
            SetMember(bounds, "size", Vector3.one * 2000f);
            SetSlotValue(boundsSlot, bounds);
        }

        private static ParameterNode AddParameter(object graph, string name, Type type, object value, float x, float y)
        {
            Type parameterType = ResolveType("UnityEditor.VFX.VFXParameter");
            object parameter = ScriptableObject.CreateInstance(parameterType);
            SetField(parameter, "m_ExposedName", name);
            SetField(parameter, "m_Exposed", true);
            SetField(parameter, "m_Category", "Particle Body");
            Call(parameter, "Init", type);
            if (type != typeof(GraphicsBuffer)) SetProperty(parameter, "value", value);
            AddChild(graph, parameter);
            int nodeId = (int)Call(parameter, "AddNode", new Vector2(x, y));
            return new ParameterNode { Model = parameter, NodeId = nodeId };
        }

        private static void LinkParameter(ParameterNode parameter, object inputSlot)
        {
            LinkInputToOutput(inputSlot, FirstSlot(parameter.Model, "outputSlots"));
            RecordParameterNodeLink(parameter, inputSlot);
        }

        private static void LinkInputToOutput(object inputSlot, object outputSlot)
        {
            object linked = Call(inputSlot, "Link", outputSlot, true);
            if (linked is bool && !(bool)linked)
                throw new InvalidOperationException("VFX Graph refused to link input '" + GetProperty(inputSlot, "name") + "' to output '" + GetProperty(outputSlot, "name") + "'. Check the installed Visual Effect Graph package's slot types.");
        }

        private static void RecordParameterNodeLink(ParameterNode parameter, object inputSlot)
        {
            object node = Call(parameter.Model, "GetNode", parameter.NodeId);
            FieldInfo linksField = FindField(node.GetType(), "linkedSlots");
            IList links = linksField.GetValue(node) as IList;
            if (links == null)
            {
                Type elementType = linksField.FieldType.GetGenericArguments()[0];
                links = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                linksField.SetValue(node, links);
            }

            Type linkedSlotType = linksField.FieldType.GetGenericArguments()[0];
            object link = Activator.CreateInstance(linkedSlotType);
            SetField(link, "outputSlot", FirstSlot(parameter.Model, "outputSlots"));
            SetField(link, "inputSlot", inputSlot);
            links.Add(link);
        }

        private static void AddHlslBlock(object context, string functionName)
        {
            object block = CreateModel("UnityEditor.VFX.Block.CustomHLSL");
            SetField(block, "m_BlockName", functionName);
            SetField(block, "m_HLSLCode", File.ReadAllText(HlslPath));
            AddChild(context, block);
            Call(block, "ResyncSlots", true);

            FieldInfo selectionField = FindField(block.GetType(), "m_AvailableFunction");
            Type choiceType = selectionField.FieldType;
            object choice = Activator.CreateInstance(choiceType);
            PropertyInfo valuesProperty = choiceType.GetProperty("values", AllInstance);
            valuesProperty.SetValue(choice, new List<string>
            {
                "PerformerParticleBodyInitialize",
                "PerformerParticleBodyUpdate",
                "PerformerParticleBodyCoreOutput",
                "PerformerParticleBodyGlowOutput"
            }, null);
            Call(choice, "SetSelection", functionName);
            selectionField.SetValue(block, choice);
            Call(block, "ResyncSlots", true);
        }

        private static object LastChildOfType(object model, string simpleTypeName)
        {
            FieldInfo childrenField = FindField(model.GetType(), "m_Children", false);
            IList children = childrenField != null ? childrenField.GetValue(model) as IList : null;
            if (children != null)
            {
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    object child = children[i];
                    if (child != null && child.GetType().Name == simpleTypeName) return child;
                }
            }
            throw new InvalidOperationException(model.GetType().Name + " has no child block of type " + simpleTypeName + ".");
        }

        private static void SetBySettingName(object model, string settingName, string enumValue)
        {
            FieldInfo field = FindField(model.GetType(), settingName);
            Call(model, "SetSettingValue", settingName, Enum.Parse(field.FieldType, enumValue));
        }

        private static void SetEnumField(object model, string fieldName, string value)
        {
            FieldInfo field = FindField(model.GetType(), fieldName);
            object parsed = Enum.Parse(field.FieldType, value);
            field.SetValue(model, parsed);
        }

        private static object CreateModel(string fullTypeName)
        {
            return ScriptableObject.CreateInstance(ResolveType(fullTypeName));
        }

        private static Type ResolveType(string fullTypeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullTypeName, false);
                if (type != null) return type;
            }
            throw new InvalidOperationException("Installed Visual Effect Graph API is missing type '" + fullTypeName + "'.");
        }

        private static object InvokeExtension(string typeName, string methodName, object argument)
        {
            Type type = ResolveType(typeName);
            MethodInfo method = type.GetMethods(AllStatic).FirstOrDefault(candidate => candidate.Name == methodName
                && candidate.GetParameters().Length == 1
                && candidate.GetParameters()[0].ParameterType.IsInstanceOfType(argument));
            if (method == null)
                throw new InvalidOperationException("Installed Visual Effect Graph API is missing " + typeName + "." + methodName + "(resource).");
            return method.Invoke(null, new[] { argument });
        }

        private static object Call(object target, string methodName, params object[] args)
        {
            if (target == null) throw new InvalidOperationException("Cannot call " + methodName + " on a null VFX Graph model.");
            MethodInfo method = target.GetType().GetMethods(AllInstance)
                .Where(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length)
                .FirstOrDefault(candidate => ParametersAccept(candidate.GetParameters(), args));
            if (method == null)
                throw new InvalidOperationException("Installed Visual Effect Graph model " + target.GetType().FullName + " is missing a compatible '" + methodName + "' API.");
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException("Visual Effect Graph operation '" + target.GetType().Name + "." + methodName + "' failed: "
                    + (exception.InnerException != null ? exception.InnerException.Message : exception.Message), exception.InnerException ?? exception);
            }
        }

        private static bool ParametersAccept(ParameterInfo[] parameters, object[] args)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null)
                {
                    if (parameters[i].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[i].ParameterType) == null) return false;
                }
                else if (!parameters[i].ParameterType.IsInstanceOfType(args[i]))
                    return false;
            }
            return true;
        }

        private static void AddChild(object parent, object child)
        {
            Call(parent, "AddChild", child, -1, true);
        }

        private static void SetPosition(object model, Vector2 position)
        {
            SetProperty(model, "position", position);
        }

        private static object FirstSlot(object model, string propertyName)
        {
            IEnumerable slots = GetProperty(model, propertyName) as IEnumerable;
            if (slots == null) throw new InvalidOperationException(model.GetType().Name + " has no " + propertyName + " collection.");
            foreach (object slot in slots) return slot;
            throw new InvalidOperationException(model.GetType().Name + " has no slot in " + propertyName + ".");
        }

        private static object FindSlot(object model, string name)
        {
            foreach (object root in GetSlots(model, "inputSlots"))
            {
                object match = FindSlotRecursive(root, name);
                if (match != null) return match;
            }
            foreach (object root in GetSlots(model, "outputSlots"))
            {
                object match = FindSlotRecursive(root, name);
                if (match != null) return match;
            }
            throw new InvalidOperationException(model.GetType().Name + " has no VFX slot named '" + name + "'.");
        }

        private static IEnumerable<object> GetSlots(object model, string propertyName)
        {
            IEnumerable slots = GetProperty(model, propertyName) as IEnumerable;
            if (slots == null) yield break;
            foreach (object slot in slots) yield return slot;
        }

        private static object FindSlotRecursive(object slot, string name)
        {
            if (string.Equals(Convert.ToString(GetProperty(slot, "name")), name, StringComparison.OrdinalIgnoreCase)) return slot;
            FieldInfo childrenField = FindField(slot.GetType(), "m_Children", false);
            IList children = childrenField != null ? childrenField.GetValue(slot) as IList : null;
            if (children == null) return null;
            foreach (object child in children)
            {
                object match = FindSlotRecursive(child, name);
                if (match != null) return match;
            }
            return null;
        }

        private static void SetSlotValue(object slot, object value)
        {
            SetProperty(slot, "value", value);
        }

        private static object GetProperty(object target, string name)
        {
            PropertyInfo property = FindProperty(target.GetType(), name);
            return property.GetValue(target, null);
        }

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = FindProperty(target.GetType(), name);
            property.SetValue(target, value, null);
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, AllInstance | BindingFlags.DeclaredOnly);
                if (property != null) return property;
            }
            throw new InvalidOperationException("Installed Visual Effect Graph API is missing property '" + type.FullName + "." + name + "'.");
        }

        private static FieldInfo FindField(Type type, string name, bool required = true)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, AllInstance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            if (!required) return null;
            throw new InvalidOperationException("Installed Visual Effect Graph API is missing field '" + type.FullName + "." + name + "'.");
        }

        private static void SetMember(object target, string name, object value)
        {
            FieldInfo field = FindField(target.GetType(), name, false);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            SetProperty(target, name, value);
        }

        private static void SetField(object target, string name, object value)
        {
            FindField(target.GetType(), name).SetValue(target, value);
        }
    }
}
