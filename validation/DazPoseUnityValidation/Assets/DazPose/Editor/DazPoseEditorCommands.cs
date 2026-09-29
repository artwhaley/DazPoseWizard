using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.UnityValidation
{
    public static class DazPoseEditorCommands
    {
        private const string CharacterAssetPath = "Assets/TestCharacter/lara.fbx";
        private const string PoseAssetFolder = "Assets/TestData";
        private const float RestFitWarningThresholdMeters = 0.02f;
        private static bool _ownsPreviewAnimationMode;
        private static Transform _previewBindingRoot;
        private static Transform _previewCharacterRoot;
        private static DazPoseCharacterState _previewRestState;
        private static AnimationClip _previewClip;
        private static readonly HashSet<string> RestCalibrationBoneIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "hip", "pelvis", "abdomenLower", "abdomen2", "chest", "chest_2", "neck", "neck_2", "head",
            "lCollar", "rCollar", "lShldr", "rShldr", "lForeArm", "rForeArm", "lHand", "rHand",
            "lThigh", "rThigh", "lShin", "rShin", "lFoot", "rFoot"
        };
        private static readonly string[] ExpectedNames =
        {
            "hip", "pelvis", "abdomenLower", "abdomenUpper", "lThighBend", "lThighTwist", "lShin", "lFoot",
            "rThighBend", "rThighTwist", "rShin", "rFoot", "head"
        };

        [MenuItem("Tools/DAZ Pose/Setup Validation Scene")]
        public static void SetupValidationScene()
        {
            AssetDatabase.Refresh();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetPath);
            if (model == null) throw new InvalidOperationException("Could not load " + CharacterAssetPath + ". Copy the local lara.fbx into Assets/TestCharacter first.");

            var importer = AssetImporter.GetAtPath(CharacterAssetPath) as ModelImporter;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Generic || importer.optimizeGameObjects || !importer.importBlendShapes)
                throw new InvalidOperationException("Lara FBX must import as Generic, Optimize Game Objects off, with Import BlendShapes enabled. Reimport " + CharacterAssetPath + " and retry.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var character = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
            if (character == null) throw new InvalidOperationException("Unity could not instantiate the imported Lara FBX.");
            character.name = "Lara";
            character.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            character.transform.localScale = Vector3.one;
            CaptureRestPose(character.transform, true);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Validation Floor";
            floor.transform.position = new Vector3(0, -0.015f, 0);
            floor.transform.localScale = new Vector3(0.3f, 1, 0.3f);
            floor.GetComponent<Collider>().enabled = false;

            var lightObject = new GameObject("Validation Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(35, -25, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.48f, 0.48f, 0.48f);

            var cameraObject = new GameObject("Validation Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.19f, 0.21f, 0.24f);
            camera.fieldOfView = 36;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 100;
            cameraObject.transform.position = new Vector3(0, 1.05f, 3.25f);
            cameraObject.transform.LookAt(new Vector3(0, 0.95f, 0));

            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Assets", "Scenes"));
            var scenePath = "Assets/Scenes/PoseValidation.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = character;
            EditorGUIUtility.PingObject(character);
            Debug.Log("DAZ Pose validation scene ready. Lara uses Generic import; Optimize Game Objects is off. Selected Lara and saved " + scenePath + ".");
        }

        [MenuItem("Tools/DAZ Pose/Open Validation Scene")]
        public static void OpenValidationScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PoseValidation.unity", OpenSceneMode.Single);
            var character = GameObject.Find("Lara");
            if (character == null) throw new InvalidOperationException("PoseValidation.unity does not contain Lara. Run Tools > DAZ Pose > Setup Validation Scene first.");
            Selection.activeGameObject = character;
            EditorGUIUtility.PingObject(character);
            Debug.Log("Opened PoseValidation.unity and selected Lara. Run Inspect Selected Character, then Apply Pose to Selected Character.");
        }

        [MenuItem("Tools/DAZ Pose/Inspect Selected Character")]
        public static void InspectSelectedCharacter()
        {
            var root = RequireSelectedRoot();
            var state = CaptureRestPose(root, false);
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var figureHierarchy = transforms.FirstOrDefault(item => item.name == "Genesis8Female" && item.parent == root) ?? root;
            var figureTransforms = figureHierarchy.GetComponentsInChildren<Transform>(true);
            var byName = figureTransforms.GroupBy(item => item.name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .OrderBy(renderer => AnimationUtility.CalculateTransformPath(renderer.transform, root), StringComparer.Ordinal)
                .Select(renderer =>
                {
                    var mesh = renderer.sharedMesh;
                    var shapes = Enumerable.Range(0, mesh.blendShapeCount).Select(index =>
                    {
                        var frameCount = mesh.GetBlendShapeFrameCount(index);
                        return new DazPoseBlendShapeDiagnostic
                        {
                            index = index,
                            name = mesh.GetBlendShapeName(index),
                            frameCount = frameCount,
                            frameWeights = Enumerable.Range(0, frameCount).Select(frame => mesh.GetBlendShapeFrameWeight(index, frame)).ToArray()
                        };
                    }).ToArray();
                    return new DazPoseRendererDiagnostic
                    {
                        rendererPath = AnimationUtility.CalculateTransformPath(renderer.transform, root),
                        meshName = mesh.name,
                        blendShapeCount = mesh.blendShapeCount,
                        blendShapes = shapes
                    };
                }).ToArray();
            var expected = ExpectedNames.Select(name =>
            {
                var matches = byName.TryGetValue(name, out var entries) ? entries : Array.Empty<Transform>();
                return new DazPoseExpectedBoneDiagnostic
                {
                    name = name,
                    found = matches.Length > 0,
                    matchCount = matches.Length,
                    path = string.Join(" | ", matches.Select(match => DazPoseTransformPath.Get(root, match)))
                };
            }).ToArray();

            var report = new DazPoseValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                character = root.name,
                rootLocalScale = root.localScale,
                rootLossyScale = root.lossyScale,
                skinnedRendererCount = renderers.Length,
                importedBlendShapeCount = renderers.Sum(item => item.blendShapeCount),
                renderers = renderers,
                transforms = transforms.Select(item => DescribeTransform(root, item)).ToArray(),
                expectedBones = expected,
                warnings = expected.Where(item => !item.found).Select(item => "Expected bone '" + item.name + "' is missing from the primary G8F hierarchy.")
                    .Concat(expected.Where(item => item.matchCount > 1).Select(item => "Expected bone '" + item.name + "' appears " + item.matchCount + " times; the pose resolver disambiguates using exact DAZ parent IDs.")).ToArray()
            };
            var output = Path.Combine(ProjectRoot, "TestOutput", "lara-unity-skeleton.json");
            WriteReport(output, report);

            var found = expected.Count(item => item.found);
            var missing = expected.Where(item => !item.found).Select(item => item.name).ToArray();
            var summary = "Unity character inspected: " + root.name + " | transform count " + transforms.Length
                + " | primary skeleton " + figureHierarchy.name + " | expected Genesis bones " + found + "/" + expected.Length
                + " | SkinnedMeshRenderers " + renderers.Length + " | imported blendshapes " + report.importedBlendShapeCount
                + " | root scale " + root.lossyScale;
            var duplicates = expected.Where(item => item.matchCount > 1).Select(item => item.name + "×" + item.matchCount).ToArray();
            if (missing.Length == 0 && duplicates.Length == 0) Debug.Log(summary + " | report: " + output);
            else if (missing.Length == 0) Debug.LogWarning(summary + " | duplicate imported names (parent-chain mapping disambiguates): " + string.Join(", ", duplicates) + " | report: " + output);
            else Debug.LogError(summary + " | MISSING/AMBIGUOUS: " + string.Join(", ", missing) + " | report: " + output);
        }

        [MenuItem("Tools/DAZ Pose/Apply Pose to Selected Character")]
        public static void ApplyPoseToSelectedCharacter()
        {
            var root = RequireSelectedRoot();
            var startFolder = Path.Combine(ProjectRoot, PoseAssetFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(startFolder)) startFolder = Path.Combine(ProjectRoot, "output");
            var jsonPath = EditorUtility.OpenFilePanel("Select canonical DAZ pose JSON", startFolder, "json");
            if (string.IsNullOrEmpty(jsonPath)) return;
            ApplyPose(root, jsonPath);
        }

        [MenuItem("Tools/DAZ Pose/Generate AnimationClip from Pose")]
        public static void GenerateAnimationClipFromPose()
        {
            var root = RequireSelectedRoot();
            var startFolder = Path.Combine(ProjectRoot, PoseAssetFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(startFolder)) startFolder = Path.Combine(ProjectRoot, "output");
            var jsonPath = EditorUtility.OpenFilePanel("Select canonical DAZ pose JSON", startFolder, "json");
            if (string.IsNullOrEmpty(jsonPath)) return;
            if (!TryResolvePose(root, jsonPath, out var resolved)) return;

            var assetPath = DazPoseAnimationClipGenerator.AssetPathFor(resolved);
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            var replace = existing != null;
            if (replace && !EditorUtility.DisplayDialog("Regenerate DAZ Pose AnimationClip?",
                    "The existing asset will be replaced with a deterministic regeneration:\n\n" + assetPath,
                    "Regenerate", "Cancel")) return;

            DazPoseAnimationClipGenerator.Generate(resolved, replace);
            var generated = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            Selection.activeObject = generated;
            EditorGUIUtility.PingObject(generated);
        }

        [MenuItem("Tools/DAZ Pose/Preview AnimationClip on Selected Character")]
        public static void PreviewAnimationClipOnSelectedCharacter()
        {
            if (_ownsPreviewAnimationMode) StopAnimationClipPreview();
            if (AnimationMode.InAnimationMode())
                throw new InvalidOperationException("Unity is already previewing another animation. Stop that preview before starting the DAZ Pose preview.");

            var root = RequireSelectedRoot();
            var startFolder = Path.Combine(ProjectRoot, DazPoseAnimationClipGenerator.OutputFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(startFolder)) startFolder = Path.Combine(ProjectRoot, "Assets");
            var absolutePath = EditorUtility.OpenFilePanel("Select generated Unity AnimationClip", startFolder, "anim");
            if (string.IsNullOrEmpty(absolutePath)) return;
            var clip = DazPoseAnimationClipGenerator.LoadClipFromAbsolutePath(absolutePath);
            if (clip == null) throw new InvalidOperationException("Unity could not load the selected .anim. Select a generated clip inside this project's Assets folder.");

            var animationRoot = FindBindingRoot(root);
            var state = CaptureRestPose(root, false);
            RestoreSnapshot(root, state);
            AnimationMode.StartAnimationMode();
            _ownsPreviewAnimationMode = true;
            _previewBindingRoot = animationRoot;
            _previewCharacterRoot = root;
            _previewRestState = state;
            _previewClip = clip;
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animationRoot.gameObject, clip, 0.5f);
                AnimationMode.EndSampling();
            }
            catch
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                _ownsPreviewAnimationMode = false;
                _previewBindingRoot = null;
                _previewCharacterRoot = null;
                _previewRestState = null;
                _previewClip = null;
                throw;
            }
            Debug.Log("Previewing '" + clip.name + "' on " + root.name + " using stable animation root '" + animationRoot.name
                + "' at 0.5 seconds. Choose Tools > DAZ Pose > Stop Preview / Restore Pose to return Lara to the captured import rest pose.");
        }

        [MenuItem("Tools/DAZ Pose/Stop Preview / Restore Pose")]
        public static void StopAnimationClipPreview()
        {
            if (_ownsPreviewAnimationMode && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            _ownsPreviewAnimationMode = false;
            var clipName = _previewClip == null ? "animation" : "'" + _previewClip.name + "'";
            var rootName = _previewBindingRoot == null ? "selected character" : "'" + _previewBindingRoot.name + "'";
            if (_previewCharacterRoot != null && _previewRestState != null)
                RestoreSnapshot(_previewCharacterRoot, _previewRestState);
            _previewBindingRoot = null;
            _previewCharacterRoot = null;
            _previewRestState = null;
            _previewClip = null;
            Debug.Log("Stopped DAZ Pose preview for " + clipName + " on " + rootName + ". The captured Transform and blendshape state was restored.");
        }

        [MenuItem("Tools/DAZ Pose/Run Play Mode AnimationClip Smoke Test")]
        public static void RunPlayModeAnimationClipSmokeTest()
        {
            var root = RequireSelectedRoot();
            var startFolder = Path.Combine(ProjectRoot, DazPoseAnimationClipGenerator.OutputFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(startFolder)) startFolder = Path.Combine(ProjectRoot, "Assets");
            var absolutePath = EditorUtility.OpenFilePanel("Select generated Unity AnimationClip", startFolder, "anim");
            if (string.IsNullOrEmpty(absolutePath)) return;
            var clip = DazPoseAnimationClipGenerator.LoadClipFromAbsolutePath(absolutePath);
            if (clip == null) throw new InvalidOperationException("Unity could not load the selected .anim asset.");
            var ownerId = DazPosePlayableBatchMonitor.CreateOwnerId();
            ConfigurePlayableSmokeTest(root, clip, ownerId);
            DazPosePlayableBatchMonitor.RegisterOwner(ownerId);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/DAZ Pose/Setup Performer Pose Smoke Test")]
        public static void SetupRuntimeBlendDemo()
        {
            var characterRoot = RequireSelectedRoot();
            var animationRoot = FindBindingRoot(characterRoot);
            RemovePhase2ValidationDriverForBlendDemo(animationRoot, characterRoot.gameObject.scene);
            var animator = animationRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = Undo.AddComponent<Animator>(animationRoot.gameObject);
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            RemoveLegacyBlendDemo(animationRoot);
            var performer = animationRoot.GetComponent<SuccubusPerformer>();
            if (performer == null) performer = Undo.AddComponent<SuccubusPerformer>(animationRoot.gameObject);
            var smoke = animationRoot.GetComponent<PerformerPoseSmokeHarness>();
            if (smoke == null) smoke = Undo.AddComponent<PerformerPoseSmokeHarness>(animationRoot.gameObject);
            var acceptance = animationRoot.GetComponent<PerformerPoseAcceptanceHarness>();
            if (acceptance == null) acceptance = Undo.AddComponent<PerformerPoseAcceptanceHarness>(animationRoot.gameObject);

            var wrappers = Stack3PoseAssetPaths().Select(path => AssetDatabase.LoadAssetAtPath<PerformerPose>(path)).ToArray();
            if (wrappers.All(pose => pose != null))
                AssignPerformerSmokePoses(performer, smoke, acceptance, wrappers);
            else
                AssignSmokeHarnessOwners(performer, smoke, acceptance);

            EditorSceneManager.MarkSceneDirty(characterRoot.gameObject.scene);
            Selection.activeGameObject = animationRoot.gameObject;
            Debug.Log("Production performer pose smoke test is set up on " + animationRoot.name
                + ". The controller uses SuccubusPerformer and the three generated PerformerPose assets when available.");
        }

        [MenuItem("Tools/DAZ Pose/Import Three Stack3 Poses and Setup Performer Pose Test")]
        public static void ImportThreeStack3PosesAndSetupBlendTest()
        {
            const string validationScenePath = "Assets/Scenes/PoseValidation.unity";
            var scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.path, validationScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("Open " + validationScenePath + " before importing the three local Stack3 poses.");

            var character = GameObject.Find("Lara");
            if (character == null) throw new InvalidOperationException("The validation scene does not contain Lara. Run Setup Validation Scene first.");

            var characterRoot = character.transform;
            var animationRoot = FindBindingRoot(characterRoot);
            RemovePhase2ValidationDriverForBlendDemo(animationRoot, scene);

            var sourceDirectory = Path.GetFullPath(Path.Combine(ProjectRoot, "..", "..", "stack3 poses"));
            var poseFileNames = new[]
            {
                "Vintage Glamour Genesis 8 Female 03.dazpose.json",
                "Vintage Glamour Genesis 8 Female 22.dazpose.json",
                "Vintage Glamour Genesis 8 Female 25.dazpose.json"
            };
            var testDataDirectory = Path.Combine(ProjectRoot, PoseAssetFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(testDataDirectory);

            var posePaths = new string[poseFileNames.Length];
            for (var index = 0; index < poseFileNames.Length; index++)
            {
                var sourcePath = Path.Combine(sourceDirectory, poseFileNames[index]);
                posePaths[index] = Path.Combine(testDataDirectory, poseFileNames[index]);
                if (File.Exists(sourcePath)) File.Copy(sourcePath, posePaths[index], true);
                else if (!File.Exists(posePaths[index]))
                    throw new FileNotFoundException("The Stack3 source pose was not found in the local stack3 poses folder or Assets/TestData.", sourcePath);
            }
            AssetDatabase.Refresh();

            var clips = new AnimationClip[posePaths.Length];
            var poses = new PerformerPose[posePaths.Length];
            for (var index = 0; index < posePaths.Length; index++)
            {
                if (!TryResolvePose(characterRoot, posePaths[index], out var resolved))
                    throw new InvalidOperationException("Pose resolution failed for " + poseFileNames[index] + ". Inspect TestOutput/pose-application-report.json and the Unity Console.");

                var assetPath = DazPoseAnimationClipGenerator.AssetPathFor(resolved);
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                DazPoseAnimationClipGenerator.Generate(resolved, existing != null);
                clips[index] = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clips[index] == null)
                    throw new InvalidOperationException("AnimationClip generation did not create " + assetPath + ".");
                var poseAssetPath = Path.ChangeExtension(assetPath, ".asset").Replace('\\', '/');
                poses[index] = AssetDatabase.LoadAssetAtPath<PerformerPose>(poseAssetPath);
                if (poses[index] == null || poses[index].Clip != clips[index])
                    throw new InvalidOperationException("PerformerPose generation did not create a wrapper for " + assetPath + ".");
            }

            Selection.activeGameObject = character;
            SetupRuntimeBlendDemo();
            animationRoot = FindBindingRoot(characterRoot);
            var performer = animationRoot.GetComponent<SuccubusPerformer>();
            var smoke = animationRoot.GetComponent<PerformerPoseSmokeHarness>();
            var acceptance = animationRoot.GetComponent<PerformerPoseAcceptanceHarness>();
            if (performer == null || smoke == null || acceptance == null)
                throw new InvalidOperationException("The production pose smoke components were not attached to " + animationRoot.name + ".");

            AssignPerformerSmokePoses(performer, smoke, acceptance, poses);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity could not save the three-pose runtime blend setup to " + validationScenePath + ".");

            Debug.Log("Three converted Stack3 PerformerPose assets are ready on " + animationRoot.name + ": Pose A=" + poses[0].name
                + ", Pose B=" + poses[1].name + ", Pose C=" + poses[2].name
                + ". Enter Play Mode, use 1/2/3 or the on-screen buttons to switch poses, and F5 runs runtime acceptance checks.");
        }

        private static void RemoveLegacyBlendDemo(Transform animationRoot)
        {
            var demo = animationRoot.GetComponent<DazPoseBlendDemo>();
            if (demo != null) Undo.DestroyObjectImmediate(demo);
            var player = animationRoot.GetComponent<DazPoseBlendPlayer>();
            if (player != null) Undo.DestroyObjectImmediate(player);
        }

        private static string[] Stack3PoseAssetPaths()
        {
            return new[]
            {
                DazPoseAnimationClipGenerator.OutputFolder + "/Vintage Glamour Genesis 8 Female 03.asset",
                DazPoseAnimationClipGenerator.OutputFolder + "/Vintage Glamour Genesis 8 Female 22.asset",
                DazPoseAnimationClipGenerator.OutputFolder + "/Vintage Glamour Genesis 8 Female 25.asset"
            };
        }

        private static void AssignPerformerSmokePoses(SuccubusPerformer performer,
            PerformerPoseSmokeHarness smoke, PerformerPoseAcceptanceHarness acceptance, PerformerPose[] poses)
        {
            var performerProperties = new SerializedObject(performer);
            performerProperties.FindProperty("initialPose").objectReferenceValue = poses[0];
            performerProperties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(performer);

            AssignSmokeHarnessOwners(performer, smoke, acceptance);
            AssignPoseReferences(smoke, poses);
            AssignPoseReferences(acceptance, poses);
        }

        private static void AssignSmokeHarnessOwners(SuccubusPerformer performer,
            PerformerPoseSmokeHarness smoke, PerformerPoseAcceptanceHarness acceptance)
        {
            AssignReference(smoke, "performer", performer);
            AssignReference(smoke, "acceptanceHarness", acceptance);
            AssignReference(acceptance, "performer", performer);
        }

        private static void AssignPoseReferences(UnityEngine.Object target, PerformerPose[] poses)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty("poseA").objectReferenceValue = poses[0];
            serialized.FindProperty("poseB").objectReferenceValue = poses[1];
            serialized.FindProperty("poseC").objectReferenceValue = poses[2];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void RemovePhase2ValidationDriverForBlendDemo(Transform animationRoot, Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before switching " + animationRoot.name + " from the Phase 2 smoke test to the runtime blend demo.");

            var phase2Driver = animationRoot.GetComponent<DazPosePlayableValidationDriver>();
            if (phase2Driver == null) return;

            Undo.DestroyObjectImmediate(phase2Driver);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Removed the Phase 2 validation-only Playables driver from " + animationRoot.name
                + "; its Animator remains available for runtime blending.");
        }

        [MenuItem("Tools/DAZ Pose/Restore Captured Rest Pose")]
        public static void RestoreCapturedRestPose()
        {
            if (_ownsPreviewAnimationMode) StopAnimationClipPreview();
            var root = RequireSelectedRoot();
            var state = root.GetComponent<DazPoseCharacterState>();
            if (state == null || !state.hasCapturedRestPose || state.transforms == null || state.transforms.Length == 0)
            {
                Debug.LogError("No captured import rest pose is saved on " + root.name + ". Run Inspect Selected Character first.");
                return;
            }
            var transforms = root.GetComponentsInChildren<Transform>(true);
            Undo.RecordObjects(transforms, "Restore DAZ Character Rest Pose");
            RestoreSnapshot(root, state);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log("Restored captured import pose for " + root.name + ".");
        }

        public static void RunBatchValidation()
        {
            RunBatchValidationCore();
        }

        private static AnimationClip RunBatchValidationCore()
        {
            OpenValidationScene();
            var character = GameObject.Find("Lara");
            if (character == null) throw new InvalidOperationException("Validation scene does not contain the Lara root. Run Setup Validation Scene first.");
            InspectSelectedCharacter();
            RunAdapterSelfTests();
            RunStage5MorphSelfTests();
            var testFolder = Path.Combine(ProjectRoot, PoseAssetFolder.Replace('/', Path.DirectorySeparatorChar));
            var posePaths = Directory.Exists(testFolder) ? Directory.GetFiles(testFolder, "*.dazpose.json").OrderBy(path => path, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
            if (posePaths.Length == 0) throw new FileNotFoundException("No local G8F .dazpose.json fixture is available under " + testFolder + ".");

            AnimationClip firstGeneratedClip = null;
            foreach (var posePath in posePaths)
            {
                if (!TryResolvePose(character.transform, posePath, out var resolved))
                    throw new InvalidOperationException("Batch validation stopped during pose resolution; inspect TestOutput/pose-application-report.json and the Unity Console log.");
                ApplyResolvedPose(resolved);
                var directParity = ValidateDirectApply(resolved);
                if (!directParity.Passed)
                    throw new InvalidOperationException("The refactored direct Apply result differs from the captured proven adapter output. " + directParity.Summary);

                var assetPath = DazPoseAnimationClipGenerator.AssetPathFor(resolved);
                var clipReport = RegenerateAndVerifyAssetIdentity(resolved, assetPath);
                if (!clipReport.generationPassed || !clipReport.directApplyParityPassed)
                    throw new InvalidOperationException("Generated clip parity failed for " + Path.GetFileName(posePath) + ".");
                if (firstGeneratedClip == null) firstGeneratedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                Debug.Log("PASS DAZ Pose Phase 2 regression: direct local transforms match the preserved adapter output; clip parity passed for " + Path.GetFileName(posePath) + ".");
            }
            Debug.Log("DAZ Pose Phase 2 batch validation passed for " + posePaths.Length + " available G8F pose fixture(s).");
            return firstGeneratedClip;
        }

        public static void RunBatchPlayableSmokeTest()
        {
            var clip = RunBatchValidationCore();
            var character = GameObject.Find("Lara");
            if (clip == null || character == null) throw new InvalidOperationException("Could not load Lara or the generated .anim for the Play Mode smoke test.");
            var ownerId = DazPosePlayableBatchMonitor.CreateOwnerId();
            ConfigurePlayableSmokeTest(character.transform, clip, ownerId);
            DazPosePlayableBatchMonitor.Arm(ownerId);
            EditorApplication.isPlaying = true;
            Debug.Log("DAZ Pose batch Play Mode smoke test entered Play Mode with validation-only Playables driver.");
        }

        private static DazPoseAnimationClipReport RegenerateAndVerifyAssetIdentity(ResolvedUnityPose pose, string assetPath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip == null)
            {
                DazPoseAnimationClipGenerator.Generate(pose, false);
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            }
            if (clip == null) throw new InvalidOperationException("AnimationClip generation did not create " + assetPath + ".");

            var guidBefore = AssetDatabase.AssetPathToGUID(assetPath);
            var sentinelPath = "__DazPoseP3RegenerationProbe_" + Guid.NewGuid().ToString("N");
            var sentinelBinding = EditorCurveBinding.FloatCurve(sentinelPath, typeof(Transform), "m_LocalPosition.x");
            AnimationUtility.SetEditorCurve(clip, sentinelBinding, AnimationCurve.Constant(0f, 1f, 0f));
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            try
            {
                var report = DazPoseAnimationClipGenerator.Generate(pose, true);
                var regenerated = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                var guidAfter = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.Equals(guidBefore, guidAfter, StringComparison.Ordinal))
                    throw new InvalidOperationException("Regenerating " + assetPath + " changed its asset GUID from " + guidBefore + " to " + guidAfter + ".");
                if (regenerated == null || AnimationUtility.GetCurveBindings(regenerated).Any(binding => binding.path == sentinelPath))
                    throw new InvalidOperationException("Regeneration left an obsolete test curve in " + assetPath + ".");
                Debug.Log("PASS DAZ Pose regeneration regression: asset GUID stayed " + guidBefore + " and obsolete curves were removed from " + assetPath + ".");
                return report;
            }
            finally
            {
                var regenerated = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (regenerated != null && AnimationUtility.GetCurveBindings(regenerated).Any(binding => binding.path == sentinelPath))
                {
                    AnimationUtility.SetEditorCurve(regenerated, sentinelBinding, null);
                    EditorUtility.SetDirty(regenerated);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private static bool ApplyPose(Transform root, string jsonPath)
        {
            if (!TryResolvePose(root, jsonPath, out var resolved)) return false;
            ApplyResolvedPose(resolved);
            var activeWarnings = resolved.Warnings;
            if (activeWarnings.Length == 0)
                Debug.Log("Applied the resolved DAZ pose to " + root.name + " using the same local transform result used by AnimationClip generation.");
            else Debug.LogWarning("Applied the resolved DAZ pose to " + root.name + " with warnings: " + string.Join(" ", activeWarnings));
            return true;
        }

        private static bool TryResolvePose(Transform root, string jsonPath, out ResolvedUnityPose resolvedPose)
            => TryResolvePose(root, jsonPath, out resolvedPose, out _, false);

        internal static bool TryResolvePoseForPipeline(Transform root, string jsonPath,
            out ResolvedUnityPose resolvedPose, out string failure)
            => TryResolvePose(root, jsonPath, out resolvedPose, out failure, true);

        private static bool TryResolvePose(Transform root, string jsonPath, out ResolvedUnityPose resolvedPose,
            out string failure, bool temporaryReference)
        {
            resolvedPose = null;
            failure = null;
            var pose = DazPoseJsonLoader.Load(jsonPath);
            var state = CaptureRestPose(root, false, !temporaryReference);
            var originalTransformsByPath = root.GetComponentsInChildren<Transform>(true)
                .ToDictionary(item => DazPoseTransformPath.Get(root, item), StringComparer.Ordinal);
            GameObject evaluationRootObject = null;
            try
            {
                evaluationRootObject = UnityEngine.Object.Instantiate(root.gameObject, root.parent, false);
                evaluationRootObject.name = root.name;
                evaluationRootObject.hideFlags = HideFlags.HideAndDontSave;
                var evaluationRoot = evaluationRootObject.transform;
                RestoreSnapshot(evaluationRoot, state);
                var bindingRoot = FindBindingRoot(evaluationRoot);
                var poseTargetIds = new HashSet<string>((pose.poseChannels ?? Array.Empty<DazPoseChannel>())
                    .Where(channel => channel.supported && !string.IsNullOrEmpty(channel.targetId))
                    .Select(channel => channel.targetId), StringComparer.Ordinal);
                var hasActiveMorphs = (pose.figureControls ?? Array.Empty<DazPoseFigureControl>())
                    .Any(control => control != null && Mathf.Abs(control.value) > 1e-7f);
                if (poseTargetIds.Count == 0 && !hasActiveMorphs)
                    throw new InvalidDataException("The canonical pose contains no active skeletal targets or figure controls.");

                var warnings = new List<string>();
                if (evaluationRoot.lossyScale.x <= 0 || evaluationRoot.lossyScale.y <= 0 || evaluationRoot.lossyScale.z <= 0)
                    warnings.Add("Character root has a non-positive scale; the pose was not applied.");
                if (!ApproximatelyUniformOne(evaluationRoot.lossyScale))
                    warnings.Add("Character root world scale is not approximately (1,1,1); DAZ centimeters to Unity meters may not match.");

                var resolutions = Array.Empty<DazPoseBoneResolution>();
                var missingTargets = Array.Empty<DazPoseBoneResolution>();
                var missingCalibration = Array.Empty<DazPoseBoneResolution>();
                var resolved = Array.Empty<DazPoseBoneResolution>();
                var resolvedBones = new List<ResolvedBonePose>();
                Transform skeletonRootOriginal = null;
                DazPoseRestBasisFit fit = null;
                var skeletalPose = poseTargetIds.Count > 0;
                if (skeletalPose)
                {
                    var skeletonRootEvaluation = FindSkeletonRoot(evaluationRoot);
                    var skeletonRootPath = DazPoseTransformPath.Get(evaluationRoot, skeletonRootEvaluation);
                    if (!originalTransformsByPath.TryGetValue(skeletonRootPath, out skeletonRootOriginal))
                        throw new InvalidOperationException("The Genesis 8 Female skeleton root could not be mapped back to the selected character.");
                    resolutions = DazPoseSkeletonResolver.Resolve(skeletonRootEvaluation, pose);
                    missingTargets = resolutions.Where(item => poseTargetIds.Contains(item.Definition.id) && item.Status != "resolved").ToArray();
                    missingCalibration = resolutions.Where(item => RestCalibrationBoneIds.Contains(item.Definition.id) && item.Status != "resolved").ToArray();
                    resolved = resolutions.Where(item => item.Status == "resolved").ToArray();
                    foreach (var item in resolutions.Where(item => item.Status != "resolved"))
                        warnings.Add(item.Status + " bone mapping: DAZ id='" + item.Definition.id + "', name='" + item.Definition.name + "', expected parent id='" + item.Definition.parentId + "'.");

                    try
                    {
                        var dazPoints = new List<Vector3>();
                        var unityPoints = new List<Vector3>();
                        foreach (var item in resolved.Where(item => RestCalibrationBoneIds.Contains(item.Definition.id)))
                        {
                            dazPoints.Add(DazPoseJsonLoader.Vector(item.Definition.restWorldPositionCm));
                            unityPoints.Add(item.Transform.position);
                        }
                        fit = DazPoseRestBasisCalibration.Fit(dazPoints, unityPoints);
                        if (fit.RmsErrorMeters > RestFitWarningThresholdMeters)
                            warnings.Add("DAZ-to-Unity rest landmark fit RMS is " + (fit.RmsErrorMeters * 1000).ToString("F1") + " mm (over 20 mm). Pose application was stopped; inspect the rest matrices and verify this is the matching neutral FBX.");
                    }
                    catch (Exception exception)
                    {
                        warnings.Add("Could not derive DAZ-to-Unity rest basis: " + exception.Message);
                    }

                    foreach (var item in missingCalibration)
                        warnings.Add("Rest calibration anchor did not resolve: DAZ id='" + item.Definition.id + "', name='" + item.Definition.name + "', parent id='" + item.Definition.parentId + "'.");
                    if (fit != null)
                    {
                        foreach (var item in resolved.Where(item => poseTargetIds.Contains(item.Definition.id)))
                        {
                            var dazRest = DazPoseJsonLoader.Vector(item.Definition.restWorldPositionCm) * 0.01f;
                            var predicted = fit.Basis.MultiplyVector(dazRest) + fit.Translation;
                            var errorMm = Vector3.Distance(predicted, item.Transform.position) * 1000f;
                            if (errorMm > RestFitWarningThresholdMeters * 1000f)
                                warnings.Add("Active pose target rest position differs by " + errorMm.ToString("F1") + " mm after the shared basis fit: DAZ id='" + item.Definition.id + "', name='" + item.Definition.name + "'. No per-bone correction was added.");
                        }
                    }

                    var report = BuildReport(evaluationRoot, pose, resolutions, poseTargetIds, RestCalibrationBoneIds, warnings, fit);
                    var reportPath = Path.Combine(ProjectRoot, "TestOutput", "pose-application-report.json");
                    if (!temporaryReference) WriteReport(reportPath, report);
                    if (missingTargets.Length > 0 || missingCalibration.Length > 0 || fit == null
                        || fit.RmsErrorMeters > RestFitWarningThresholdMeters
                        || warnings.Any(value => value.StartsWith("Could not derive", StringComparison.Ordinal)))
                    {
                        failure = "Pose resolution stopped: target mapping or rest calibration failed. No scene object was changed."
                            + (temporaryReference ? string.Empty : " Review the report at " + reportPath + ".");
                        if (!temporaryReference) Debug.LogError(failure);
                        return false;
                    }

                    var targets = new List<PoseTarget>(resolved.Length);
                    foreach (var item in resolved)
                    {
                        var bone = item.Definition;
                        var restRotationDaz = DazPoseJsonLoader.Quaternion(bone.restWorldRotation);
                        var poseRotationDaz = DazPoseJsonLoader.Quaternion(bone.evaluatedWorldRotation);
                        var deltaDaz = Quaternion.Normalize(poseRotationDaz * Quaternion.Inverse(restRotationDaz));
                        var deltaUnity = DazPoseRestBasisCalibration.ConvertWorldRotationDelta(fit.Basis, deltaDaz);
                        var targetRotation = Quaternion.Normalize(deltaUnity * item.Transform.rotation);
                        var restPositionDaz = DazPoseJsonLoader.Vector(bone.restWorldPositionCm);
                        var posePositionDaz = DazPoseJsonLoader.Vector(bone.evaluatedWorldPositionCm);
                        var targetPosition = item.Transform.position + DazPoseRestBasisCalibration.ConvertDazCentimeterDelta(fit.Basis, posePositionDaz - restPositionDaz);
                        targets.Add(new PoseTarget { Transform = item.Transform, Position = targetPosition, Rotation = targetRotation });
                    }
                    foreach (var target in targets.OrderBy(item => DazPoseTransformPath.DepthFrom(evaluationRoot, item.Transform)))
                        target.Transform.SetPositionAndRotation(target.Position, target.Rotation);

                    var channelTargets = (pose.poseChannels ?? Array.Empty<DazPoseChannel>())
                        .Where(channel => channel.supported && !string.IsNullOrEmpty(channel.targetId))
                        .GroupBy(channel => channel.targetId, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => new
                        {
                            Rotation = group.Any(channel => channel.property == "rotation"),
                            Translation = group.Any(channel => channel.property == "translation")
                        }, StringComparer.Ordinal);
                    var restByPath = (state.transforms ?? Array.Empty<DazPoseRestTransform>()).ToDictionary(item => item.path, StringComparer.Ordinal);
                    foreach (var item in resolved)
                    {
                        var instancePath = DazPoseTransformPath.Get(evaluationRoot, item.Transform);
                        if (!originalTransformsByPath.TryGetValue(instancePath, out var originalTransform))
                            throw new InvalidOperationException("Could not map resolved DAZ bone '" + item.Definition.id + "' back to the selected character at " + instancePath + ".");
                        if (!item.Transform.IsChildOf(skeletonRootEvaluation) && item.Transform != skeletonRootEvaluation)
                            throw new InvalidOperationException("Resolved DAZ bone '" + item.Definition.id + "' is outside the configured skeleton root: " + instancePath + ".");
                        if (!restByPath.TryGetValue(instancePath, out var restTransform))
                            throw new InvalidOperationException("The captured import rest pose has no entry for resolved DAZ bone '" + item.Definition.id + "'.");
                        channelTargets.TryGetValue(item.Definition.id, out var channels);
                        var localRotation = item.Transform.localRotation;
                        var localPosition = item.Transform.localPosition;
                        var localScale = item.Transform.localScale;
                        resolvedBones.Add(new ResolvedBonePose
                        {
                            DazBoneId = item.Definition.id,
                            DazBoneName = item.Definition.name,
                            InstancePath = instancePath,
                            AnimationPath = AnimationUtility.CalculateTransformPath(item.Transform, bindingRoot),
                            MappingMethod = item.Method,
                            Transform = originalTransform,
                            HasPosition = Vector3.Distance(restTransform.localPosition, localPosition) > 1e-5f,
                            HasRotation = Quaternion.Angle(restTransform.localRotation, localRotation) > 0.001f,
                            HasScale = false,
                            HasDazTranslationChannel = channels != null && channels.Translation,
                            HasDazRotationChannel = channels != null && channels.Rotation,
                            LocalPosition = localPosition,
                            LocalRotation = localRotation,
                            LocalScale = localScale
                        });
                    }
                }

                var morphControls = DazPoseMorphResolver.Resolve(root, pose.figureControls);
                if (morphControls.Count > 0)
                {
                    try
                    {
                        var confirmed = DazPoseRequiredMorphManifestStore.MarkConfirmed(ProjectRoot, morphControls);
                        if (confirmed > 0) Debug.Log("Confirmed " + confirmed + " direct morph(s) in the project Required Morph Manifest.");
                    }
                    catch (Exception exception)
                    {
                        warnings.Add("Could not update the Required Morph Manifest after direct blendshape resolution: " + exception.Message);
                    }
                }

                resolvedPose = new ResolvedUnityPose
                {
                    CharacterRoot = root,
                    SkeletonRoot = skeletonRootOriginal,
                    BindingRoot = root,
                    Definition = pose,
                    RestState = state,
                    SourcePoseJsonPath = jsonPath,
                    CharacterName = root.name,
                    PoseTargetBoneCount = poseTargetIds.Count,
                    UnresolvedRequiredBoneCount = missingTargets.Length + missingCalibration.Length,
                    AmbiguousBoneCount = resolutions.Count(item => item.Status == "ambiguous"),
                    Bones = resolvedBones,
                    MorphControls = morphControls,
                    Warnings = warnings.ToArray()
                };
                var resolvedMorphBindingCount = morphControls.Sum(item => item.Bindings.Count);
                Debug.Log("Resolved DAZ pose on " + root.name + ": driven bones " + resolvedBones.Count(item => item.HasRotation || item.HasPosition || item.HasScale)
                    + " | active figure controls " + morphControls.Count + " | renderer bindings " + resolvedMorphBindingCount
                    + (fit == null ? string.Empty : " | rest-fit RMS " + (fit.RmsErrorMeters * 1000).ToString("F1") + " mm"));
                return true;
            }
            finally
            {
                if (evaluationRootObject != null) UnityEngine.Object.DestroyImmediate(evaluationRootObject);
            }
        }

        private static void ApplyResolvedPose(ResolvedUnityPose pose, bool recordUndoAndDirty = true)
        {
            RestoreSnapshot(pose.CharacterRoot, pose.RestState);
            var changedObjects = pose.Bones.Select(item => item.Transform).Where(item => item != null).Cast<UnityEngine.Object>()
                .Concat(pose.MorphControls.SelectMany(item => item.Bindings).Select(item => item.Renderer)
                    .Where(item => item != null).Cast<UnityEngine.Object>())
                .Distinct().ToArray();
            if (recordUndoAndDirty && changedObjects.Length > 0) Undo.RecordObjects(changedObjects, "Apply DAZ Pose");
            foreach (var bone in pose.Bones.OrderBy(item => DazPoseTransformPath.DepthFrom(pose.CharacterRoot, item.Transform)))
            {
                bone.Transform.localPosition = bone.LocalPosition;
                bone.Transform.localRotation = bone.LocalRotation;
                bone.Transform.localScale = bone.LocalScale;
            }
            foreach (var binding in pose.MorphControls.SelectMany(item => item.Bindings))
                binding.Renderer.SetBlendShapeWeight(binding.BlendShapeIndex, binding.UnityWeight);
            if (recordUndoAndDirty) EditorSceneManager.MarkSceneDirty(pose.CharacterRoot.gameObject.scene);
        }

        private static DazPoseClipParityResult ValidateDirectApply(ResolvedUnityPose pose)
        {
            var maxPosition = 0f;
            var maxRotation = 0f;
            var maxScale = 0f;
            var maxBlendShapeWeight = 0f;
            foreach (var bone in pose.Bones)
            {
                maxPosition = Mathf.Max(maxPosition, Vector3.Distance(bone.LocalPosition, bone.Transform.localPosition));
                maxRotation = Mathf.Max(maxRotation, Quaternion.Angle(bone.LocalRotation, bone.Transform.localRotation));
                maxScale = Mathf.Max(maxScale, Vector3.Distance(bone.LocalScale, bone.Transform.localScale));
            }
            foreach (var morphBinding in pose.MorphControls.SelectMany(item => item.Bindings))
                maxBlendShapeWeight = Mathf.Max(maxBlendShapeWeight,
                    Mathf.Abs(morphBinding.UnityWeight - morphBinding.Renderer.GetBlendShapeWeight(morphBinding.BlendShapeIndex)));
            var result = new DazPoseClipParityResult
            {
                MaximumPositionErrorMeters = maxPosition,
                MaximumRotationErrorDegrees = maxRotation,
                MaximumScaleError = maxScale,
                MaximumBlendShapeWeightError = maxBlendShapeWeight,
                Passed = maxPosition <= 1e-5f && maxRotation <= 0.001f && maxScale <= 1e-5f && maxBlendShapeWeight <= 1e-3f,
                Summary = "resolved direct-output comparison: max local position " + maxPosition.ToString("G6") + " m, rotation "
                    + maxRotation.ToString("G6") + " deg, scale " + maxScale.ToString("G6")
                    + ", blendshape weight " + maxBlendShapeWeight.ToString("G6") + "."
            };
            Debug.Log((result.Passed ? "PASS" : "FAIL") + " DAZ Pose direct Apply regression: " + result.Summary);
            return result;
        }

        private static Transform FindSkeletonRoot(Transform characterRoot)
        {
            var candidates = characterRoot.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name == "Genesis8Female" && item.parent == characterRoot).ToArray();
            if (candidates.Length != 1)
                throw new InvalidOperationException("Expected exactly one direct child named Genesis8Female under selected character '" + characterRoot.name
                + "', found " + candidates.Length + ". The G8F skeleton root must be a unique direct child.");
            return candidates[0];
        }

        private static Transform FindBindingRoot(Transform characterRoot)
        {
            if (characterRoot == null) throw new ArgumentNullException(nameof(characterRoot));
            FindSkeletonRoot(characterRoot);
            return characterRoot;
        }

        private static void ConfigurePlayableSmokeTest(Transform characterRoot, AnimationClip clip, string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId)) throw new ArgumentException("A smoke-test owner id is required.", nameof(ownerId));
            var animationRoot = FindBindingRoot(characterRoot);
            if (animationRoot.GetComponent<DazPosePlayableValidationDriver>() != null)
                throw new InvalidOperationException("A DAZ Pose smoke-test driver already exists on " + animationRoot.name + ". Remove or inspect it before starting another test; the command will not commandeer it.");
            if (animationRoot.GetComponent<DazPoseBlendPlayer>() != null)
                throw new InvalidOperationException("A runtime DazPoseBlendPlayer is attached to " + animationRoot.name + ". Stop or remove it before running the separate Phase 2 smoke test.");

            var animator = animationRoot.GetComponent<Animator>();
            var addedAnimator = animator == null;
            if (addedAnimator) animator = Undo.AddComponent<Animator>(animationRoot.gameObject);
            if (addedAnimator)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            DazPosePlayableValidationDriver driver = null;
            try
            {
                driver = Undo.AddComponent<DazPosePlayableValidationDriver>(animationRoot.gameObject);
                driver.clip = clip;
                driver.validationAddedAnimator = addedAnimator;
                driver.validationOwnedAnimator = addedAnimator ? animator : null;
                driver.validationSmokeTestOwner = ownerId;
                driver.expectedTransforms = BuildRuntimeExpectations(clip);
                driver.expectedBlendShapes = BuildRuntimeBlendShapeExpectations(clip, characterRoot);
                EditorUtility.SetDirty(driver);
            }
            catch
            {
                if (driver != null) Undo.DestroyObjectImmediate(driver);
                if (addedAnimator && animator != null) Undo.DestroyObjectImmediate(animator);
                throw;
            }
            EditorSceneManager.MarkSceneDirty(characterRoot.gameObject.scene);
            Selection.activeGameObject = characterRoot.gameObject;
            Debug.Log("Prepared validation-only Animator/Playables smoke test on stable root '" + animationRoot.name + "' using '" + clip.name
                + "'. It will hold the static pose during Play Mode and compare every generated animated local transform.");
        }

        private static DazPoseRuntimeExpectedTransform[] BuildRuntimeExpectations(AnimationClip clip)
        {
            var groups = AnimationUtility.GetCurveBindings(clip).Where(binding => binding.type == typeof(Transform))
                .GroupBy(binding => binding.path, StringComparer.Ordinal);
            var expectations = new List<DazPoseRuntimeExpectedTransform>();
            foreach (var group in groups)
            {
                var curves = group.ToDictionary(binding => binding.propertyName, binding => AnimationUtility.GetEditorCurve(clip, binding), StringComparer.Ordinal);
                var expectation = new DazPoseRuntimeExpectedTransform { path = group.Key, localRotation = Quaternion.identity, localScale = Vector3.one };
                if (curves.TryGetValue("m_LocalPosition.x", out var positionX)
                    && curves.TryGetValue("m_LocalPosition.y", out var positionY)
                    && curves.TryGetValue("m_LocalPosition.z", out var positionZ))
                {
                    expectation.hasPosition = true;
                    expectation.localPosition = new Vector3(positionX.Evaluate(0.5f), positionY.Evaluate(0.5f), positionZ.Evaluate(0.5f));
                }
                if (curves.TryGetValue("m_LocalRotation.x", out var rotationX)
                    && curves.TryGetValue("m_LocalRotation.y", out var rotationY)
                    && curves.TryGetValue("m_LocalRotation.z", out var rotationZ)
                    && curves.TryGetValue("m_LocalRotation.w", out var rotationW))
                {
                    expectation.hasRotation = true;
                    expectation.localRotation = Quaternion.Normalize(new Quaternion(rotationX.Evaluate(0.5f), rotationY.Evaluate(0.5f), rotationZ.Evaluate(0.5f), rotationW.Evaluate(0.5f)));
                }
                if (curves.TryGetValue("m_LocalScale.x", out var scaleX)
                    && curves.TryGetValue("m_LocalScale.y", out var scaleY)
                    && curves.TryGetValue("m_LocalScale.z", out var scaleZ))
                {
                    expectation.hasScale = true;
                    expectation.localScale = new Vector3(scaleX.Evaluate(0.5f), scaleY.Evaluate(0.5f), scaleZ.Evaluate(0.5f));
                }
                if (expectation.hasPosition || expectation.hasRotation || expectation.hasScale) expectations.Add(expectation);
            }
            return expectations.ToArray();
        }

        private static DazPoseRuntimeExpectedBlendShape[] BuildRuntimeBlendShapeExpectations(AnimationClip clip, Transform bindingRoot)
        {
            return AnimationUtility.GetCurveBindings(clip)
                .Where(binding => binding.type == typeof(SkinnedMeshRenderer)
                    && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                .Select(binding =>
                {
                    var path = binding.path;
                    var blendShapeName = binding.propertyName.Substring("blendShape.".Length);
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null || curve.length == 0)
                        throw new InvalidOperationException("Runtime validation found an empty blendshape curve at '" + path + "/" + binding.propertyName + "'.");
                    var rendererTransform = string.IsNullOrEmpty(path) ? bindingRoot : bindingRoot.Find(path);
                    if (rendererTransform == null)
                        throw new InvalidOperationException("Runtime validation could not resolve blendshape renderer path '" + path + "'.");
                    var renderers = rendererTransform.GetComponents<SkinnedMeshRenderer>();
                    var matches = renderers.SelectMany((renderer, componentIndex) => renderer == null || renderer.sharedMesh == null
                            ? Enumerable.Empty<DazPoseRuntimeExpectedBlendShape>()
                            : Enumerable.Range(0, renderer.sharedMesh.blendShapeCount)
                                .Where(index => string.Equals(renderer.sharedMesh.GetBlendShapeName(index), blendShapeName, StringComparison.Ordinal))
                                .Select(index => new DazPoseRuntimeExpectedBlendShape
                                {
                                    path = path,
                                    blendShapeName = blendShapeName,
                                    blendShapeIndex = index,
                                    rendererComponentIndex = componentIndex,
                                    weight = curve.Evaluate(0.5f)
                                }))
                        .ToArray();
                    if (matches.Length != 1)
                        throw new InvalidOperationException("Runtime validation expected one imported blendshape '" + blendShapeName + "' at '" + path + "', found " + matches.Length + ".");
                    return matches[0];
                }).ToArray();
        }

        private static DazPoseValidationReport BuildReport(Transform root, DazPoseDefinition pose,
            DazPoseBoneResolution[] resolutions, HashSet<string> poseTargetIds, HashSet<string> calibrationBoneIds,
            List<string> warnings, DazPoseRestBasisFit fit)
        {
            var mappings = resolutions.Select(item => new DazPoseMappingDiagnostic
            {
                id = item.Definition.id,
                name = item.Definition.name,
                expectedParentId = item.Definition.parentId,
                transformPath = item.Transform == null ? string.Empty : DazPoseTransformPath.Get(root, item.Transform),
                method = item.Method,
                status = item.Status,
                detail = item.Detail,
                usedForRestCalibration = calibrationBoneIds.Contains(item.Definition.id),
                activePoseTarget = poseTargetIds.Contains(item.Definition.id),
                restFitErrorMm = fit == null || item.Transform == null ? -1 : (Vector3.Distance(
                    fit.Basis.MultiplyVector(DazPoseJsonLoader.Vector(item.Definition.restWorldPositionCm) * 0.01f) + fit.Translation,
                    item.Transform.position) * 1000f)
            }).ToArray();
            return new DazPoseValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                character = root.name,
                pose = DazPoseJsonLoader.PoseName(pose.source.poseFile, pose.source.poseAssetId),
                figureAssetId = pose.source.figureAssetId,
                poseAssetId = pose.source.poseAssetId,
                boneCount = pose.bones.Length,
                poseChannelCount = pose.poseChannels == null ? 0 : pose.poseChannels.Length,
                poseTargetCount = poseTargetIds.Count,
                resolvedCount = resolutions.Count(item => item.Status == "resolved"),
                missingCount = resolutions.Count(item => item.Status == "missing"),
                ambiguousCount = resolutions.Count(item => item.Status == "ambiguous"),
                rootLocalScale = root.localScale,
                rootLossyScale = root.lossyScale,
                dazUnits = pose.coordinateSystem == null ? "centimeter" : pose.coordinateSystem.lengthUnit,
                unityUnits = "meter",
                centimetersToMeters = 0.01f,
                restFitRmsMm = fit == null ? -1 : fit.RmsErrorMeters * 1000f,
                restFitMaxMm = fit == null ? -1 : fit.MaxErrorMeters * 1000f,
                restFitSampleCount = fit == null ? 0 : fit.SampleCount,
                restCalibrationBoneIds = calibrationBoneIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                dazToUnityWorldBasis = fit == null ? DazPoseRestBasisCalibration.MatrixValues(Matrix4x4.identity) : DazPoseRestBasisCalibration.MatrixValues(fit.Basis),
                dazToUnityBasisDeterminant = fit == null ? 1 : fit.BasisDeterminant,
                dazToUnityWorldTranslation = fit == null ? Vector3.zero : fit.Translation,
                warnings = warnings.ToArray(),
                mappings = mappings,
                transforms = root.GetComponentsInChildren<Transform>(true).Select(item => DescribeTransform(root, item)).ToArray()
            };
        }

        private static DazPoseValidationReport BuildSkeletonReport(Transform root, DazPoseDefinition pose, DazPoseBoneResolution[] resolutions)
        {
            var mappings = resolutions.Select(item => new DazPoseMappingDiagnostic
            {
                id = item.Definition.id, name = item.Definition.name, expectedParentId = item.Definition.parentId,
                transformPath = item.Transform == null ? string.Empty : DazPoseTransformPath.Get(root, item.Transform),
                method = item.Method, status = item.Status, detail = item.Detail, restFitErrorMm = -1
            }).ToArray();
            return new DazPoseValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"), character = root.name,
                pose = DazPoseJsonLoader.PoseName(pose.source.poseFile, pose.source.poseAssetId),
                figureAssetId = pose.source.figureAssetId, poseAssetId = pose.source.poseAssetId,
                boneCount = pose.bones.Length, poseChannelCount = pose.poseChannels == null ? 0 : pose.poseChannels.Length,
                poseTargetCount = (pose.poseChannels ?? Array.Empty<DazPoseChannel>()).Where(item => item.supported && !string.IsNullOrEmpty(item.targetId)).Select(item => item.targetId).Distinct().Count(),
                resolvedCount = resolutions.Count(item => item.Status == "resolved"),
                missingCount = resolutions.Count(item => item.Status == "missing"),
                ambiguousCount = resolutions.Count(item => item.Status == "ambiguous"),
                rootLocalScale = root.localScale, rootLossyScale = root.lossyScale,
                dazUnits = pose.coordinateSystem == null ? "centimeter" : pose.coordinateSystem.lengthUnit,
                unityUnits = "meter", centimetersToMeters = 0.01f,
                warnings = resolutions.Where(item => item.Status != "resolved").Select(item => item.Status + " DAZ bone id='" + item.Definition.id + "', name='" + item.Definition.name + "', parent id='" + item.Definition.parentId + "'.").ToArray(),
                mappings = mappings,
                transforms = root.GetComponentsInChildren<Transform>(true).Select(item => DescribeTransform(root, item)).ToArray()
            };
        }

        [MenuItem("Tools/DAZ Pose/Inspect Pose Bone Mapping")]
        public static void InspectPoseBoneMapping()
        {
            var root = RequireSelectedRoot();
            var path = EditorUtility.OpenFilePanel("Select canonical DAZ pose JSON", Path.Combine(ProjectRoot, PoseAssetFolder), "json");
            if (string.IsNullOrEmpty(path)) return;
            var pose = DazPoseJsonLoader.Load(path);
            CaptureRestPose(root, false);
            var resolutions = DazPoseSkeletonResolver.Resolve(root, pose);
            var report = BuildSkeletonReport(root, pose, resolutions);
            var output = Path.Combine(ProjectRoot, "TestOutput", "pose-skeleton-mapping.json");
            WriteReport(output, report);
            Debug.Log("Pose bone mapping: " + report.resolvedCount + " resolved / " + pose.bones.Length + " | missing " + report.missingCount + " | ambiguous " + report.ambiguousCount + " | report: " + output);
        }

        [MenuItem("Tools/DAZ Pose/Run Adapter Self Tests")]
        public static void RunAdapterSelfTests()
        {
            var passed = 0;
            void Check(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException("Adapter self-test failed: " + name);
                passed++;
                Debug.Log("PASS DAZ Pose self-test: " + name);
            }

            var angle = Quaternion.AngleAxis(73, new Vector3(0.3f, 0.8f, -0.2f).normalized);
            var source = new[] { new Vector3(0, 0, 0), Vector3.right, Vector3.up, Vector3.forward, new Vector3(1, 2, 3), new Vector3(-2, 1, 0.5f) };
            var translation = new Vector3(2.5f, -0.3f, 7);
            var target = source.Select(point => angle * point + translation).ToArray();
            var fit = DazPoseRestBasisCalibration.Fit(source.Select(point => point * 100f).ToArray(), target);
            var expectedBasis = Matrix4x4.Rotate(angle);
            Check(MatrixMaxDifference(expectedBasis, fit.Basis) < 1e-4f && fit.RmsErrorMeters < 1e-5f, "rest calibration recovers a known rotation, translation, and cm-to-m scale");
            Check(Mathf.Abs(DazPoseRestBasisCalibration.ConvertDazCentimeterDelta(Matrix4x4.identity, new Vector3(100, 0, 0)).x - 1f) < 1e-6f, "100 DAZ centimeters equal one Unity meter");
            var reflectedTargets = source.Select(point => new Vector3(-point.x, point.y, point.z) + translation).ToArray();
            var reflectedFit = DazPoseRestBasisCalibration.Fit(source.Select(point => point * 100f).ToArray(), reflectedTargets);
            Check(reflectedFit.BasisDeterminant < -0.999f && reflectedFit.RmsErrorMeters < 1e-5f, "rest calibration preserves a data-derived reflected coordinate basis");
            var identityDelta = DazPoseRestBasisCalibration.ConvertWorldRotationDelta(fit.Basis, Quaternion.identity);
            Check(Quaternion.Angle(Quaternion.identity, identityDelta) < 0.001f, "neutral/rest rotation delta preserves imported Unity rest orientation");
            var unityRestRotation = Quaternion.Euler(18, -37, 91);
            Check(Quaternion.Angle(unityRestRotation, identityDelta * unityRestRotation) < 0.001f, "neutral/rest application leaves an arbitrary imported Unity rest rotation unchanged");
            var reflectedYRotation = DazPoseRestBasisCalibration.ConvertWorldRotationDelta(reflectedFit.Basis, Quaternion.AngleAxis(25, Vector3.up));
            Check(Quaternion.Angle(reflectedYRotation, Quaternion.AngleAxis(-25, Vector3.up)) < 0.01f, "matrix conjugation converts a rotation correctly through the measured reflection");
            var finiteInput = Quaternion.Normalize(new Quaternion(0.1f, -0.3f, 0.5f, 0.8f));
            var finite = DazPoseRestBasisCalibration.ConvertWorldRotationDelta(reflectedFit.Basis, finiteInput);
            var finiteNormSquared = finite.x * finite.x + finite.y * finite.y + finite.z * finite.z + finite.w * finite.w;
            Check(!float.IsNaN(finite.x) && !float.IsInfinity(finite.x) && Mathf.Abs(finiteNormSquared - 1f) < 1e-5f, "converted quaternion is finite and normalized");

            var holder = new GameObject("ResolverSelfTestRoot");
            try
            {
                var child = new GameObject("lThighBend");
                child.transform.SetParent(holder.transform, false);
                var thigh = new DazPoseBone { id = "lThigh", name = "lThighBend", parentId = "ResolverSelfTestRoot" };
                var definition = new DazPoseDefinition { bones = new[] { thigh, new DazPoseBone { id = "missingBone", name = "missingBone" } } };
                var resolution = DazPoseSkeletonResolver.Resolve(holder.transform, definition);
                Debug.Log("DAZ Pose resolver self-test detail: " + resolution[0].Status + ", " + resolution[0].Method + ", " + resolution[0].Detail + ", path " + (resolution[0].Transform == null ? "<none>" : DazPoseTransformPath.Get(holder.transform, resolution[0].Transform)));
                Check(resolution[0].Status == "resolved" && resolution[0].Method.StartsWith("exact name", StringComparison.Ordinal) && resolution[0].Transform == child.transform, "DAZ id lThigh resolves to the exact imported name lThighBend");
                Check(resolution[1].Status == "missing", "missing bone produces an explicit diagnostic status");
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }

            Debug.Log("DAZ Pose adapter self-tests passed: " + passed + "/9.");
        }

        [MenuItem("Tools/DAZ Pose/Run Stage 5 Morph Self Tests")]
        public static void RunStage5MorphSelfTests()
        {
            var checks = 0;
            void Check(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException("Stage 5 morph self-test failed: " + name);
                checks++;
                Debug.Log("PASS DAZ Pose Stage 5 self-test: " + name);
            }

            var character = new GameObject("Stage5MorphSelfTestCharacter");
            Mesh bodyMesh = null;
            Mesh shirtMesh = null;
            Mesh untouchedMesh = null;
            try
            {
                var body = CreateTestSkinnedRenderer(character.transform, "BodyRenderer", out bodyMesh, true);
                var shirt = CreateTestSkinnedRenderer(character.transform, "ShirtRenderer", out shirtMesh, true);
                var accessory = CreateTestSkinnedRenderer(character.transform, "RendererWithoutSmile", out untouchedMesh, false);
                untouchedMesh.AddBlendShapeFrame("TestBlink", 100f,
                    new[] { Vector3.zero, Vector3.zero, Vector3.forward * 0.05f },
                    new[] { Vector3.zero, Vector3.zero, Vector3.zero },
                    new[] { Vector3.zero, Vector3.zero, Vector3.zero });
                accessory.SetBlendShapeWeight(0, 37f);
                var control = new DazPoseFigureControl
                {
                    sourceUrl = "name://@selection#TestSmile:?value/value",
                    rawControlId = "TestSmile",
                    name = "TestSmile",
                    value = 1f
                };
                var morphs = DazPoseMorphResolver.Resolve(character.transform, new[] { control });
                Check(morphs.Count == 1 && morphs[0].Bindings.Count == 2,
                    "one canonical control resolves to both body and shirt renderers");
                Check(morphs[0].Bindings.All(binding => Mathf.Abs(binding.UnityWeight - 100f) < 1e-4f),
                    "DAZ unit value maps to each imported mesh's full-value frame weight");

                var rest = CaptureRestPose(character.transform, true, false);
                var shapeOnly = new ResolvedUnityPose
                {
                    CharacterRoot = character.transform,
                    BindingRoot = character.transform,
                    Definition = new DazPoseDefinition
                    {
                        format = "DazPoseTool", version = 2,
                        source = new DazPoseSource { poseFile = "TestSmile.dazpose.json", poseAssetId = "TestSmile" },
                        figureControls = new[] { control }
                    },
                    RestState = rest,
                    CharacterName = character.name,
                    MorphControls = morphs
                };
                var shapeClip = DazPoseAnimationClipGenerator.BuildCandidateClip(shapeOnly);
                try
                {
                    var shapeBindings = AnimationUtility.GetCurveBindings(shapeClip);
                    Check(shapeBindings.Length == 2 && shapeBindings.All(binding => binding.type == typeof(SkinnedMeshRenderer)
                        && binding.propertyName == "blendShape.TestSmile"),
                        "shape-only clip emits one exact blendshape curve per renderer without Transform curves");
                    var shapeParity = DazPoseClipParityValidator.Validate(shapeOnly, shapeClip);
                    Check(shapeParity.Passed && shapeParity.MaximumBlendShapeWeightError <= 1e-3f,
                        "shape-only clip passes direct/clip parity at 0, 0.5, and 1 seconds");
                }
                finally { UnityEngine.Object.DestroyImmediate(shapeClip); }

                ApplyResolvedPose(shapeOnly, false);
                Check(Mathf.Abs(body.GetBlendShapeWeight(0) - 100f) < 1e-3f
                    && Mathf.Abs(shirt.GetBlendShapeWeight(0) - 100f) < 1e-3f,
                    "direct application writes the same resolved value to every matching renderer");
                Check(accessory.sharedMesh.blendShapeCount == 1 && Mathf.Abs(accessory.GetBlendShapeWeight(0) - 37f) < 1e-3f,
                    "renderer without the required blendshape remains untouched");
                RestoreSnapshot(character.transform, rest);

                var skeletonRoot = new GameObject("Genesis8Female");
                skeletonRoot.transform.SetParent(character.transform, false);
                var hip = new GameObject("hip");
                hip.transform.SetParent(skeletonRoot.transform, false);
                var mixedRest = CaptureRestPose(character.transform, true, false);
                var mixed = new ResolvedUnityPose
                {
                    CharacterRoot = character.transform,
                    SkeletonRoot = skeletonRoot.transform,
                    BindingRoot = character.transform,
                    Definition = shapeOnly.Definition,
                    RestState = mixedRest,
                    CharacterName = character.name,
                    PoseTargetBoneCount = 1,
                    Bones = new List<ResolvedBonePose>
                    {
                        new ResolvedBonePose
                        {
                            DazBoneId = "hip", DazBoneName = "hip", InstancePath = "Genesis8Female/hip",
                            AnimationPath = "Genesis8Female/hip", Transform = hip.transform,
                            HasPosition = true, LocalPosition = new Vector3(0.125f, 0.25f, -0.05f),
                            LocalRotation = Quaternion.identity, LocalScale = Vector3.one
                        }
                    },
                    MorphControls = morphs
                };
                var mixedClip = DazPoseAnimationClipGenerator.BuildCandidateClip(mixed);
                try
                {
                    var mixedBindings = AnimationUtility.GetCurveBindings(mixedClip);
                    Check(mixedBindings.Any(binding => binding.type == typeof(Transform) && binding.path == "Genesis8Female/hip")
                        && mixedBindings.Any(binding => binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName == "blendShape.TestSmile"),
                        "mixed skeletal and morph clip contains both binding families");
                    Check(DazPoseClipParityValidator.Validate(mixed, mixedClip).Passed,
                        "mixed skeletal and morph clip passes direct/clip parity");
                }
                finally { UnityEngine.Object.DestroyImmediate(mixedClip); }

                var missingControl = new DazPoseFigureControl { rawControlId = "MissingFace", name = "MissingFace", value = 1f };
                var missingFailedClearly = false;
                try { DazPoseMorphResolver.Resolve(character.transform, new[] { missingControl }); }
                catch (InvalidOperationException exception)
                {
                    missingFailedClearly = exception.Message.Contains("not present as a direct blendshape");
                }
                Check(missingFailedClearly, "missing direct morph fails with a reference-refresh diagnostic");
                Debug.Log("DAZ Pose Stage 5 morph self-tests passed: " + checks + "/9.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(character);
                if (bodyMesh != null) UnityEngine.Object.DestroyImmediate(bodyMesh);
                if (shirtMesh != null) UnityEngine.Object.DestroyImmediate(shirtMesh);
                if (untouchedMesh != null) UnityEngine.Object.DestroyImmediate(untouchedMesh);
            }
        }

        private static SkinnedMeshRenderer CreateTestSkinnedRenderer(Transform root, string name, out Mesh mesh, bool withSmile)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(root, false);
            var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            mesh = new Mesh { name = name + "Mesh" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            if (withSmile)
                mesh.AddBlendShapeFrame("TestSmile", 100f,
                    new[] { Vector3.zero, Vector3.forward * 0.1f, Vector3.forward * 0.1f },
                    new[] { Vector3.zero, Vector3.zero, Vector3.zero },
                    new[] { Vector3.zero, Vector3.zero, Vector3.zero });
            renderer.sharedMesh = mesh;
            return renderer;
        }

        private static DazPoseCharacterState CaptureRestPose(Transform root, bool force)
            => CaptureRestPose(root, force, true);

        private static DazPoseCharacterState CaptureRestPose(Transform root, bool force, bool recordUndo)
        {
            var state = root.GetComponent<DazPoseCharacterState>();
            if (state == null)
                state = recordUndo ? Undo.AddComponent<DazPoseCharacterState>(root.gameObject) : root.gameObject.AddComponent<DazPoseCharacterState>();
            if (state.hasCapturedRestPose && !force)
            {
                if (!state.hasCapturedBlendShapes || !RestBlendShapeStructureMatches(root, state))
                    CaptureRestBlendShapes(root, state);
                return state;
            }
            var transforms = root.GetComponentsInChildren<Transform>(true);
            state.transforms = transforms.Select(item => new DazPoseRestTransform
                {
                    path = DazPoseTransformPath.Get(root, item),
                    localPosition = item.localPosition,
                    localRotation = item.localRotation,
                    localScale = item.localScale
                }).ToArray();
            CaptureRestBlendShapes(root, state);
            state.hasCapturedRestPose = true;
            if (recordUndo) EditorUtility.SetDirty(state);
            return state;
        }

        private static void CaptureRestBlendShapes(Transform root, DazPoseCharacterState state)
        {
            state.blendShapes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .Select(renderer =>
                {
                    var components = renderer.transform.GetComponents<SkinnedMeshRenderer>();
                    var componentIndex = Array.IndexOf(components, renderer);
                    var shapeCount = renderer.sharedMesh.blendShapeCount;
                    var weights = new float[shapeCount];
                    var shapeNames = new string[shapeCount];
                    for (var index = 0; index < shapeCount; index++)
                    {
                        weights[index] = renderer.GetBlendShapeWeight(index);
                        shapeNames[index] = renderer.sharedMesh.GetBlendShapeName(index);
                    }
                    return new DazPoseRestBlendShape
                    {
                        rendererPath = DazPoseTransformPath.Get(root, renderer.transform),
                        rendererComponentIndex = componentIndex,
                        blendShapeCount = shapeCount,
                        blendShapeNames = shapeNames,
                        weights = weights
                    };
                }).ToArray();
            state.hasCapturedBlendShapes = true;
        }

        private static bool RestBlendShapeStructureMatches(Transform root, DazPoseCharacterState state)
        {
            var saved = state.blendShapes ?? Array.Empty<DazPoseRestBlendShape>();
            var current = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMesh != null).ToArray();
            if (saved.Length != current.Length) return false;
            for (var index = 0; index < current.Length; index++)
            {
                var renderer = current[index];
                var savedItem = saved.FirstOrDefault(item => item != null
                    && item.rendererPath == DazPoseTransformPath.Get(root, renderer.transform)
                    && item.rendererComponentIndex == Array.IndexOf(renderer.transform.GetComponents<SkinnedMeshRenderer>(), renderer));
                if (savedItem == null || savedItem.blendShapeCount != renderer.sharedMesh.blendShapeCount
                    || savedItem.blendShapeNames == null || savedItem.blendShapeNames.Length != renderer.sharedMesh.blendShapeCount)
                    return false;
                for (var shapeIndex = 0; shapeIndex < renderer.sharedMesh.blendShapeCount; shapeIndex++)
                    if (!string.Equals(savedItem.blendShapeNames[shapeIndex], renderer.sharedMesh.GetBlendShapeName(shapeIndex), StringComparison.Ordinal))
                        return false;
            }
            return true;
        }

        internal static void RestoreSnapshot(Transform root, DazPoseCharacterState state)
        {
            if (root == null || state == null) return;
            var byPath = state.transforms.ToDictionary(item => item.path, StringComparer.Ordinal);
            foreach (var item in root.GetComponentsInChildren<Transform>(true).OrderBy(value => DazPoseTransformPath.DepthFrom(root, value)))
            {
                if (!byPath.TryGetValue(DazPoseTransformPath.Get(root, item), out var snapshot)) continue;
                item.localPosition = snapshot.localPosition;
                item.localRotation = snapshot.localRotation;
                item.localScale = snapshot.localScale;
            }

            var transformByPath = root.GetComponentsInChildren<Transform>(true)
                .ToDictionary(item => DazPoseTransformPath.Get(root, item), StringComparer.Ordinal);
            foreach (var snapshot in state.blendShapes ?? Array.Empty<DazPoseRestBlendShape>())
            {
                if (snapshot == null || !transformByPath.TryGetValue(snapshot.rendererPath, out var rendererTransform)) continue;
                var renderers = rendererTransform.GetComponents<SkinnedMeshRenderer>();
                if (snapshot.rendererComponentIndex < 0 || snapshot.rendererComponentIndex >= renderers.Length) continue;
                var renderer = renderers[snapshot.rendererComponentIndex];
                if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount != snapshot.blendShapeCount
                    || snapshot.weights == null || snapshot.weights.Length != snapshot.blendShapeCount
                    || snapshot.blendShapeNames == null || snapshot.blendShapeNames.Length != snapshot.blendShapeCount) continue;
                var namesMatch = true;
                for (var shapeIndex = 0; shapeIndex < snapshot.blendShapeCount; shapeIndex++)
                    if (!string.Equals(snapshot.blendShapeNames[shapeIndex], renderer.sharedMesh.GetBlendShapeName(shapeIndex), StringComparison.Ordinal))
                    {
                        namesMatch = false;
                        break;
                    }
                if (!namesMatch) continue;
                for (var index = 0; index < snapshot.weights.Length; index++) renderer.SetBlendShapeWeight(index, snapshot.weights[index]);
            }
        }

        private static DazPoseTransformDiagnostic DescribeTransform(Transform root, Transform item)
        {
            var parent = item.parent;
            return new DazPoseTransformDiagnostic
            {
                name = item.name,
                path = DazPoseTransformPath.Get(root, item),
                parentName = parent == null ? string.Empty : parent.name,
                parentPath = parent == null || parent == root.parent ? string.Empty : DazPoseTransformPath.Get(root, parent),
                localPosition = item.localPosition,
                localRotation = item.localRotation,
                localScale = item.localScale,
                worldPosition = item.position,
                worldRotation = item.rotation,
                localToWorldMatrix = MatrixValues(item.localToWorldMatrix)
            };
        }

        private static float[] MatrixValues(Matrix4x4 m) => new[]
        {
            m.m00, m.m01, m.m02, m.m03, m.m10, m.m11, m.m12, m.m13,
            m.m20, m.m21, m.m22, m.m23, m.m30, m.m31, m.m32, m.m33
        };

        private static float MatrixMaxDifference(Matrix4x4 first, Matrix4x4 second)
        {
            var valuesA = DazPoseRestBasisCalibration.MatrixValues(first);
            var valuesB = DazPoseRestBasisCalibration.MatrixValues(second);
            var maximum = 0f;
            for (var index = 0; index < valuesA.Length; index++) maximum = Mathf.Max(maximum, Mathf.Abs(valuesA[index] - valuesB[index]));
            return maximum;
        }

        private static void WriteReport(string path, DazPoseValidationReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }

        private static Transform RequireSelectedRoot()
        {
            var selected = Selection.activeTransform;
            if (selected == null) throw new InvalidOperationException("Select the instantiated Lara root object in the Hierarchy first.");
            return selected;
        }

        private static bool ApproximatelyUniformOne(Vector3 scale)
            => Mathf.Abs(scale.x - 1) < 0.001f && Mathf.Abs(scale.y - 1) < 0.001f && Mathf.Abs(scale.z - 1) < 0.001f;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private sealed class PoseTarget
        {
            public Transform Transform;
            public Vector3 Position;
            public Quaternion Rotation;
        }
    }
}
