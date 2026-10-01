using System;
using System.Linq;
using DazPose.AnimationAudit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.AnimationAudit
{
    /// <summary>
    /// Creates a stock Animator test using the user-configured Humanoid Lara FBX.
    /// Preserves both model importers; the full audit adds library playback controls.
    /// </summary>
    public static class LaraHumanoidKawaiiTestSetup
    {
        public const string ScenePath = "Assets/Scenes/LaraHumanoidKawaiiTest.unity";
        public const string ModelPath = "Assets/TestCharacter/laraHumanoid.fbx";
        public const string MotionPath = "Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Idle01_breathing.FBX";
        private const string AssetFolder = "Assets/DazPose/AnimationAudit/SimpleHumanoidTest";
        private const string ControllerPath = AssetFolder + "/LaraHumanoidBreathing.controller";
        private const string FloorMaterialPath = AssetFolder + "/Floor.mat";

        [MenuItem("Tools/DAZ Pose/Animation Audit/Create or Refresh Simple Humanoid Test")]
        public static void CreateOrRefresh()
        {
            Create(false);
        }

        public static void CreateAuditScene()
        {
            Create(true);
        }

        private static void Create(bool audit)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Create the animation test while out of Play Mode.");

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null) throw new InvalidOperationException("Model is missing: " + ModelPath);
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Human)
                throw new InvalidOperationException("Set laraHumanoid.fbx Rig > Animation Type to Humanoid and Apply before creating this scene.");

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("laraHumanoid.fbx has no valid Human Avatar. Open its Rig > Configure and correct its Avatar before creating this scene. This command does not rewrite the importer.");

            AnimationClip[] motions = AssetDatabase.LoadAllAssetsAtPath(MotionPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase) && clip.length > 0f)
                .ToArray();
            if (motions.Length != 1)
                throw new InvalidOperationException("Expected exactly one imported animation in " + MotionPath + "; found " + motions.Length + ".");
            AnimationClip motion = motions[0];
            if (!motion.isHumanMotion)
                throw new InvalidOperationException("The breathing idle was not imported as a Humanoid motion: " + MotionPath);

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(AssetFolder);
            EnsureFolder("Assets/Scenes");
            AnimationAuditCatalog catalog = audit ? AnimationAuditCatalogBuilder.BuildCatalog() : null;
            AnimatorController controller = audit ? PrepareAuditController(catalog, motion, false) : PrepareController(motion);
            AnimatorController footIKController = audit ? PrepareAuditController(catalog, motion, true) : null;
            Material floorMaterial = PrepareFloorMaterial();
            AssetDatabase.SaveAssets();

            // Single mode ensures that the audit/performer scenes cannot also run.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject model = PrefabUtility.InstantiatePrefab(modelAsset, scene) as GameObject;
            if (model == null) throw new InvalidOperationException("Could not instantiate " + ModelPath);
            model.name = audit ? "Lara Humanoid — Animation Audit" : "Lara Humanoid — Breathing Test";
            model.transform.position = Vector3.zero;
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);

            Animator[] animators = model.GetComponentsInChildren<Animator>(true);
            if (animators.Length > 1)
                throw new InvalidOperationException("The imported model contains multiple Animators; this test requires one.");
            Animator animator = animators.Length == 1 ? animators[0] : model.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 1f;
            animator.enabled = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = Vector3.one * (audit ? 2f : 0.5f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.4f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 1.1f, -3.4f);
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.95f, 0f) - camera.transform.position);
                camera.fieldOfView = 42f;
                camera.nearClipPlane = 0.05f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.18f, 0.21f);
            }
            Light light = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>()).FirstOrDefault();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                light.color = Color.white;
                light.intensity = GraphicsSettings.currentRenderPipeline != null &&
                                  GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("HDRenderPipeline") ? 85000f : 1.2f;
            }

            if (audit)
            {
                AnimationAuditHarness harness = new GameObject("Animation Browser").AddComponent<AnimationAuditHarness>();
                harness.catalog = catalog;
                harness.animator = animator;
                harness.noFootIKController = controller;
                harness.footIKController = footIKController;
                harness.previewCamera = camera;
                harness.floor = floor.transform;
                harness.initialEntryIndex = catalog.entries.FindIndex(entry => entry.clip == motion);
                harness.seatAnchor = CreateSeat(floorMaterial);
                CreateGrid(floorMaterial);
                if (camera != null)
                {
                    AnimationAuditOrbitCamera orbit = camera.gameObject.AddComponent<AnimationAuditOrbitCamera>();
                    orbit.target = animator.transform;
                    orbit.targetHeight = 0.95f;
                    orbit.minDistance = 2f;
                    orbit.maxDistance = 20f;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            string scenePath = audit ? AnimationAuditSceneSetup.ScenePath : ScenePath;
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new InvalidOperationException("Could not save " + scenePath);
            Selection.activeGameObject = model;
            Debug.Log((audit ? "Single-Lara animation audit" : "Simple Humanoid test") + " saved and opened: " + scenePath +
                      ". Model: " + ModelPath + "; Avatar: " + avatar.name +
                      " (isHuman=" + avatar.isHuman + ", isValid=" + avatar.isValid +
                      "); motion: " + motion.name + " from " + MotionPath +
                      ". Press Play: the stock Animator starts with the breathing idle at 1x and root motion off." +
                      (audit ? " Browser contains " + catalog.entries.Count + " clips from the installed libraries." : " No runtime scripts were added."));
        }

        private static AnimatorController PrepareAuditController(AnimationAuditCatalog catalog, AnimationClip initialMotion, bool footIK)
        {
            string path = footIK ? AnimationAuditSceneSetup.FootIKControllerPath : AnimationAuditSceneSetup.ControllerPath;
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorControllerLayer layer = controller.layers[0];
            layer.avatarMask = null;
            layer.iKPass = false;
            layer.syncedLayerIndex = -1;
            layer.defaultWeight = 1f;
            controller.layers = new[] { layer };
            if (!controller.parameters.Any(parameter => parameter.name == "Mirror"))
                controller.AddParameter("Mirror", AnimatorControllerParameterType.Bool);
            AnimatorStateMachine machine = layer.stateMachine;
            // Preserve existing nodes during refresh. Removing them invalidates open graph views.
            var states = machine.states.ToDictionary(child => child.state.name, child => child.state);
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                AnimationAuditEntry entry = catalog.entries[i];
                entry.animatorStateName = "";
                if (entry.clip == null || !entry.clip.isHumanMotion) continue;
                entry.animatorStateName = "Clip" + i.ToString("0000");
                if (!states.TryGetValue(entry.animatorStateName, out AnimatorState state))
                    state = machine.AddState(entry.animatorStateName, new Vector3(300f + (i % 8) * 280f, (i / 8) * 80f, 0f));
                state.motion = entry.clip;
                state.speed = 1f;
                state.iKOnFeet = footIK;
                state.mirrorParameterActive = true;
                state.mirrorParameter = "Mirror";
                if (entry.clip == initialMotion) machine.defaultState = state;
                EditorUtility.SetDirty(state);
            }
            if (machine.defaultState == null || machine.defaultState.motion != initialMotion)
                throw new InvalidOperationException("The catalog did not include the proven breathing idle.");
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static Transform CreateSeat(Material material)
        {
            GameObject seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seat.name = "Seat surface (edit height and position in Scene view)";
            seat.transform.position = new Vector3(2f, 0.46f, 2f);
            seat.transform.localScale = new Vector3(0.9f, 0.1f, 0.8f);
            seat.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(seat.GetComponent<Collider>());
            Transform anchor = new GameObject("SeatAnchor").transform;
            anchor.position = seat.transform.position;
            anchor.SetParent(seat.transform, true);
            return anchor;
        }

        private static void CreateGrid(Material fallbackMaterial)
        {
            const string gridMaterialPath = AssetFolder + "/Grid.mat";
            const string gridMeshPath = AssetFolder + "/Grid.asset";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(gridMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Color");
                material = shader != null ? new Material(shader) : new Material(fallbackMaterial);
                if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", new Color(0.18f, 0.18f, 0.18f));
                if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.18f, 0.18f, 0.18f));
                AssetDatabase.CreateAsset(material, gridMaterialPath);
            }
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(gridMeshPath);
            if (mesh == null)
            {
                var vertices = new System.Collections.Generic.List<Vector3>();
                var indices = new System.Collections.Generic.List<int>();
                for (int i = -10; i <= 10; i++)
                {
                    int start = vertices.Count;
                    vertices.Add(new Vector3(i, 0f, -10f)); vertices.Add(new Vector3(i, 0f, 10f));
                    vertices.Add(new Vector3(-10f, 0f, i)); vertices.Add(new Vector3(10f, 0f, i));
                    indices.AddRange(new[] { start, start + 1, start + 2, start + 3 });
                }
                mesh = new Mesh { name = "Audit 1m grid" };
                mesh.SetVertices(vertices);
                mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, gridMeshPath);
            }
            GameObject grid = new GameObject("Reference grid — 1 metre spacing");
            grid.transform.position = Vector3.up * 0.005f;
            grid.AddComponent<MeshFilter>().sharedMesh = mesh;
            grid.AddComponent<MeshRenderer>().sharedMaterial = material;
            AssetDatabase.SaveAssets();
        }

        private static AnimatorController PrepareController(AnimationClip motion)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            // This dedicated generated controller contains only the directly assigned idle.
            AnimatorControllerLayer layer = controller.layers[0];
            layer.avatarMask = null;
            layer.iKPass = false;
            layer.syncedLayerIndex = -1;
            layer.defaultWeight = 1f;
            controller.layers = new[] { layer };
            controller.parameters = new AnimatorControllerParameter[0];
            AnimatorStateMachine machine = layer.stateMachine;
            foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);
            foreach (ChildAnimatorStateMachine child in machine.stateMachines) machine.RemoveStateMachine(child.stateMachine);
            machine.anyStateTransitions = new AnimatorStateTransition[0];
            machine.entryTransitions = new AnimatorTransition[0];
            AnimatorState state = machine.AddState("KA_Idle01_breathing");
            state.motion = motion;
            state.speed = 1f;
            state.mirror = false;
            state.iKOnFeet = false;
            machine.defaultState = state;
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static Material PrepareFloorMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (material != null) return material;
            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No supported floor shader is available.");
            material = new Material(shader) { name = "Simple Humanoid Test Floor" };
            Color gray = new Color(0.38f, 0.38f, 0.38f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", gray);
            if (material.HasProperty("_Color")) material.SetColor("_Color", gray);
            AssetDatabase.CreateAsset(material, FloorMaterialPath);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
