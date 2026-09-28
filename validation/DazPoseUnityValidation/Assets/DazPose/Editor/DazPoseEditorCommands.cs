using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            if (importer == null || importer.animationType != ModelImporterAnimationType.Generic || importer.optimizeGameObjects)
                throw new InvalidOperationException("Lara FBX must import as Generic with Optimize Game Objects off. The DAZ Pose AssetPostprocessor should set this; reimport " + CharacterAssetPath + " and retry.");

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
                transforms = transforms.Select(item => DescribeTransform(root, item)).ToArray(),
                expectedBones = expected,
                warnings = expected.Where(item => !item.found).Select(item => "Expected bone '" + item.name + "' is missing from the primary G8F hierarchy.")
                    .Concat(expected.Where(item => item.matchCount > 1).Select(item => "Expected bone '" + item.name + "' appears " + item.matchCount + " times; the pose resolver disambiguates using exact DAZ parent IDs.")).ToArray()
            };
            var output = Path.Combine(ProjectRoot, "TestOutput", "lara-unity-skeleton.json");
            WriteReport(output, report);

            var found = expected.Count(item => item.found);
            var missing = expected.Where(item => !item.found).Select(item => item.name).ToArray();
            var summary = "Unity skeleton inspected: " + root.name + " | transform count " + transforms.Length + " | primary figure hierarchy " + figureHierarchy.name + " | expected Genesis bones " + found + "/" + expected.Length + " | root scale " + root.lossyScale;
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

        [MenuItem("Tools/DAZ Pose/Restore Captured Rest Pose")]
        public static void RestoreCapturedRestPose()
        {
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
            OpenValidationScene();
            var character = GameObject.Find("Lara");
            if (character == null) throw new InvalidOperationException("Validation scene does not contain the Lara root. Run Setup Validation Scene first.");
            InspectSelectedCharacter();
            RunAdapterSelfTests();
            var posePath = Path.Combine(ProjectRoot, PoseAssetFolder.Replace('/', Path.DirectorySeparatorChar), "Cherish Genesis 8 Female 16.dazpose.json");
            if (!ApplyPose(character.transform, posePath))
                throw new InvalidOperationException("Batch validation stopped; inspect TestOutput/pose-application-report.json and the Unity Console log.");
        }

        private static bool ApplyPose(Transform root, string jsonPath)
        {
            var pose = DazPoseJsonLoader.Load(jsonPath);
            var state = CaptureRestPose(root, false);
            RestoreSnapshot(root, state);
            var resolutions = DazPoseSkeletonResolver.Resolve(root, pose);
            var poseTargetIds = new HashSet<string>((pose.poseChannels ?? Array.Empty<DazPoseChannel>())
                .Where(channel => channel.supported && !string.IsNullOrEmpty(channel.targetId))
                .Select(channel => channel.targetId), StringComparer.Ordinal);
            var missingTargets = resolutions.Where(item => poseTargetIds.Contains(item.Definition.id) && item.Status != "resolved").ToArray();
            var missingCalibration = resolutions.Where(item => RestCalibrationBoneIds.Contains(item.Definition.id) && item.Status != "resolved").ToArray();
            var resolved = resolutions.Where(item => item.Status == "resolved").ToArray();

            var warnings = new List<string>();
            if (root.lossyScale.x <= 0 || root.lossyScale.y <= 0 || root.lossyScale.z <= 0)
                warnings.Add("Character root has a non-positive scale; the pose was not applied.");
            if (!ApproximatelyUniformOne(root.lossyScale))
                warnings.Add("Character root world scale is not approximately (1,1,1); DAZ centimeters to Unity meters may not match.");
            foreach (var item in resolutions.Where(item => item.Status != "resolved"))
                warnings.Add(item.Status + " bone mapping: DAZ id='" + item.Definition.id + "', name='" + item.Definition.name + "', expected parent id='" + item.Definition.parentId + "'.");

            DazPoseRestBasisFit fit = null;
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

            var report = BuildReport(root, pose, resolutions, poseTargetIds, RestCalibrationBoneIds, warnings, fit);
            var reportPath = Path.Combine(ProjectRoot, "TestOutput", "pose-application-report.json");
            WriteReport(reportPath, report);
            var missingCount = resolutions.Count(item => item.Status == "missing");
            var ambiguousCount = resolutions.Count(item => item.Status == "ambiguous");
            var summary = "Pose: " + DazPoseJsonLoader.PoseName(pose.source.poseFile, pose.source.poseAssetId)
                + " | character: " + root.name + " | pose targets: " + poseTargetIds.Count
                + " | resolved bones: " + resolved.Length + "/" + pose.bones.Length
                + " | missing: " + missingCount + " | ambiguous: " + ambiguousCount
                + " | rest-fit RMS: " + (fit == null ? "unavailable" : (fit.RmsErrorMeters * 1000).ToString("F1") + " mm")
                + " | report: " + reportPath;

            if (missingTargets.Length > 0 || missingCalibration.Length > 0 || fit == null || fit.RmsErrorMeters > RestFitWarningThresholdMeters || warnings.Any(value => value.StartsWith("Could not derive", StringComparison.Ordinal)))
            {
                Debug.LogError(summary + " | Application stopped: target mapping or rest calibration failed. Review the report; no axis settings were changed.");
                return false;
            }

            var targets = new List<PoseTarget>();
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

            Undo.RecordObjects(targets.Select(item => item.Transform).Cast<UnityEngine.Object>().ToArray(), "Apply DAZ Pose");
            foreach (var target in targets.OrderBy(item => DazPoseTransformPath.DepthFrom(root, item.Transform)))
                target.Transform.SetPositionAndRotation(target.Position, target.Rotation);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            var activeWarnings = warnings.Where(value => !value.StartsWith("missing bone mapping", StringComparison.Ordinal)).ToArray();
            if (activeWarnings.Length == 0) Debug.Log(summary + " | DAZ translation cm → Unity meters at ×0.01.");
            else Debug.LogWarning(summary + " | warnings: " + string.Join(" ", activeWarnings));
            return true;
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

        private static DazPoseCharacterState CaptureRestPose(Transform root, bool force)
        {
            var state = root.GetComponent<DazPoseCharacterState>();
            if (state == null) state = Undo.AddComponent<DazPoseCharacterState>(root.gameObject);
            if (state.hasCapturedRestPose && !force) return state;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            state.transforms = transforms.Select(item => new DazPoseRestTransform
            {
                path = DazPoseTransformPath.Get(root, item),
                localPosition = item.localPosition,
                localRotation = item.localRotation,
                localScale = item.localScale
            }).ToArray();
            state.hasCapturedRestPose = true;
            EditorUtility.SetDirty(state);
            return state;
        }

        private static void RestoreSnapshot(Transform root, DazPoseCharacterState state)
        {
            var byPath = state.transforms.ToDictionary(item => item.path, StringComparer.Ordinal);
            foreach (var item in root.GetComponentsInChildren<Transform>(true).OrderBy(value => DazPoseTransformPath.DepthFrom(root, value)))
            {
                if (!byPath.TryGetValue(DazPoseTransformPath.Get(root, item), out var snapshot)) continue;
                item.localPosition = snapshot.localPosition;
                item.localRotation = snapshot.localRotation;
                item.localScale = snapshot.localScale;
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
