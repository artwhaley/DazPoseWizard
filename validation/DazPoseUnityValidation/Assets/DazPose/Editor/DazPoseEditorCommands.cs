using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DazPose.Performer;
using DazPose.Editor.Importing;
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
        private static readonly string[] ExpectedNames =
        {
            "hip", "pelvis", "abdomenLower", "abdomenUpper", "lThighBend", "lThighTwist", "lShin", "lFoot",
            "rThighBend", "rThighTwist", "rShin", "rFoot", "head"
        };

        [MenuItem("Tools/DAZ Pose/Development/Setup or Refresh Validation Scene")]
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
            DazPoseRestPoseService.Capture(character.transform, true);

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

        [MenuItem("Tools/DAZ Pose/Development/Open Pose Validation Scene")]
        public static void OpenValidationScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PoseValidation.unity", OpenSceneMode.Single);
            var character = GameObject.Find("Lara");
            if (character == null) throw new InvalidOperationException("PoseValidation.unity does not contain Lara. Run Tools > DAZ Pose > Setup Validation Scene first.");
            Selection.activeGameObject = character;
            EditorGUIUtility.PingObject(character);
            Debug.Log("Opened PoseValidation.unity and selected Lara. Use the development diagnostics or performer acceptance harness as needed.");
        }

        [MenuItem("Tools/DAZ Pose/Development/Diagnostics/Inspect Selected Character")]
        public static void InspectSelectedCharacter()
        {
            var root = RequireSelectedRoot();
            var state = DazPoseRestPoseService.Capture(root, false);
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

        [MenuItem("Tools/DAZ Pose/Development/Setup or Refresh Performer Pose Acceptance Harness")]
        public static void SetupPerformerPoseAcceptanceHarness()
        {
            var characterRoot = RequireSelectedRoot();
            var animationRoot = FindBindingRoot(characterRoot);
            var animator = animationRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = Undo.AddComponent<Animator>(animationRoot.gameObject);
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            ClearAnimatorControllerForPoseSmokeTest(animator);
            var performer = animationRoot.GetComponent<SuccubusPerformer>();
            if (performer == null) performer = Undo.AddComponent<SuccubusPerformer>(animationRoot.gameObject);
            var smoke = animationRoot.GetComponent<PerformerPoseSmokeHarness>();
            if (smoke == null) smoke = Undo.AddComponent<PerformerPoseSmokeHarness>(animationRoot.gameObject);
            var acceptance = animationRoot.GetComponent<PerformerPoseAcceptanceHarness>();
            if (acceptance == null) acceptance = Undo.AddComponent<PerformerPoseAcceptanceHarness>(animationRoot.gameObject);

            AssignSmokeHarnessOwners(performer, smoke, acceptance);

            EditorSceneManager.MarkSceneDirty(characterRoot.gameObject.scene);
            Selection.activeGameObject = animationRoot.gameObject;
            Debug.Log("Performer pose acceptance harness is ready on " + animationRoot.name
                + ". Assign three PerformerPose assets on the harness if they are not already configured.");
        }

        private static void ClearAnimatorControllerForPoseSmokeTest(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            Undo.RecordObject(animator, "Clear Animator Controller for Performer Pose Test");
            animator.runtimeAnimatorController = null;
            EditorUtility.SetDirty(animator);
        }

        private static void AssignSmokeHarnessOwners(SuccubusPerformer performer,
            PerformerPoseSmokeHarness smoke, PerformerPoseAcceptanceHarness acceptance)
        {
            AssignReference(smoke, "performer", performer);
            AssignReference(smoke, "acceptanceHarness", acceptance);
            AssignReference(acceptance, "performer", performer);
        }

        private static void AssignReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
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
            var performerPosePath = Path.ChangeExtension(assetPath, ".asset").Replace('\\', '/');
            var performerPose = AssetDatabase.LoadAssetAtPath<PerformerPose>(performerPosePath);
            if (performerPose == null || performerPose.Clip != clip)
                throw new InvalidOperationException("The generated PerformerPose wrapper is missing or does not reference " + assetPath + ".");
            var performerPoseGuidBefore = AssetDatabase.AssetPathToGUID(performerPosePath);
            var sentinelPath = "__DazPoseP3RegenerationProbe_" + Guid.NewGuid().ToString("N");
            var sentinelBinding = EditorCurveBinding.FloatCurve(sentinelPath, typeof(Transform), "m_LocalPosition.x");
            AnimationUtility.SetEditorCurve(clip, sentinelBinding, AnimationCurve.Constant(0f, 1f, 0f));
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            try
            {
                var report = DazPoseAnimationClipGenerator.Generate(pose, true);
                var regenerated = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                var regeneratedPose = AssetDatabase.LoadAssetAtPath<PerformerPose>(performerPosePath);
                var guidAfter = AssetDatabase.AssetPathToGUID(assetPath);
                var performerPoseGuidAfter = AssetDatabase.AssetPathToGUID(performerPosePath);
                if (!string.Equals(guidBefore, guidAfter, StringComparison.Ordinal))
                    throw new InvalidOperationException("Regenerating " + assetPath + " changed its asset GUID from " + guidBefore + " to " + guidAfter + ".");
                if (!string.Equals(performerPoseGuidBefore, performerPoseGuidAfter, StringComparison.Ordinal))
                    throw new InvalidOperationException("Regenerating " + performerPosePath + " changed its asset GUID from "
                        + performerPoseGuidBefore + " to " + performerPoseGuidAfter + ".");
                if (regeneratedPose == null || regeneratedPose.Clip != regenerated)
                    throw new InvalidOperationException("Regenerated PerformerPose does not reference the exact clip at " + assetPath + ".");
                if (regenerated == null || AnimationUtility.GetCurveBindings(regenerated).Any(binding => binding.path == sentinelPath))
                    throw new InvalidOperationException("Regeneration left an obsolete test curve in " + assetPath + ".");
                Debug.Log("PASS DAZ Pose regeneration regression: AnimationClip GUID " + guidBefore + " and PerformerPose GUID "
                    + performerPoseGuidBefore + " stayed stable; the wrapper references the regenerated clip and obsolete curves were removed.");
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

        private static bool TryResolvePose(Transform root, string jsonPath, out ResolvedUnityPose resolvedPose)
            => DazPoseUnityResolver.TryResolvePose(root, jsonPath, out resolvedPose, out _, false);


        private static void ApplyResolvedPose(ResolvedUnityPose pose, bool recordUndoAndDirty = true)
        {
            DazPoseRestPoseService.RestoreSnapshot(pose.CharacterRoot, pose.RestState);
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

        [MenuItem("Tools/DAZ Pose/Development/Diagnostics/Inspect Pose Bone Mapping")]
        public static void InspectPoseBoneMapping()
        {
            var root = RequireSelectedRoot();
            var path = EditorUtility.OpenFilePanel("Select canonical DAZ pose JSON", Path.Combine(ProjectRoot, PoseAssetFolder), "json");
            if (string.IsNullOrEmpty(path)) return;
            var pose = DazPoseJsonLoader.Load(path);
            DazPoseRestPoseService.Capture(root, false);
            var resolutions = DazPoseSkeletonResolver.Resolve(root, pose);
            var report = BuildSkeletonReport(root, pose, resolutions);
            var output = Path.Combine(ProjectRoot, "TestOutput", "pose-skeleton-mapping.json");
            WriteReport(output, report);
            Debug.Log("Pose bone mapping: " + report.resolvedCount + " resolved / " + pose.bones.Length + " | missing " + report.missingCount + " | ambiguous " + report.ambiguousCount + " | report: " + output);
        }

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

                var rest = DazPoseRestPoseService.Capture(character.transform, true, false);
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
                DazPoseRestPoseService.RestoreSnapshot(character.transform, rest);

                var skeletonRoot = new GameObject("Genesis8Female");
                skeletonRoot.transform.SetParent(character.transform, false);
                var hip = new GameObject("hip");
                hip.transform.SetParent(skeletonRoot.transform, false);
                var mixedRest = DazPoseRestPoseService.Capture(character.transform, true, false);
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

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    }
}
