using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DazPose.Editor.Importing;
using DazPose.FirstPerformanceVoid;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor
{
    /// <summary>Samples a source clip through Humanoid Lara and writes project-owned Generic Action assets.</summary>
    public static class PerformerActionBaker
    {
        private const string ProxyPath = "Assets/TestCharacter/laraHumanoid.fbx";
        private const string GeneratedFolder = "Assets/DazPose/Generated/Actions";
        private const string AcceptanceFolder = GeneratedFolder + "/Acceptance";
        private const string JumpForJoySourcePath = "Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Idle66_JumpForJoy.FBX";
        private const string ScenePath = "Assets/Scenes/FirstPerformanceVoid.unity";
        private const string ReportPath = "docs/animation-audit/PerformActionBakeReport.md";
        private const int SampleRate = 60;

        private sealed class SampleSet
        {
            public AnimationClip Source;
            public Transform ActorRoot;
            public Transform[] Bones;
            public Vector3[][] Positions;
            public Quaternion[][] Rotations;
            public Vector3[][] Scales;
            public Vector3[] RootPositions;
            public float[] RootYaw;
            public int Count => RootPositions.Length;
            public float Duration => Source.length;
        }

        [MenuItem("Tools/DAZ Pose/Action/Bake Selected Clip as Performer Action")]
        public static void BakeSelectedClip()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Bake Performer Actions outside Play Mode.");
            AnimationClip source = Selection.activeObject as AnimationClip;
            if (source == null)
                throw new InvalidOperationException("Select one Lara-compatible AnimationClip in the Project window before baking a Performer Action.");
            Bake(source, GeneratedFolder, Sanitize(source.name), "Selected source clip.", null);
        }

        [MenuItem("Tools/DAZ Pose/Action/Generate Jump for Joy Acceptance Assets")]
        public static void GenerateJumpForJoyAcceptance()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Generate Action acceptance assets in Edit Mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the existing FirstPerformanceVoid scene before generating Action acceptance assets.");

            FirstPerformanceVoidControls[] controlsMatches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FirstPerformanceVoidControls>(true)).ToArray();
            if (controlsMatches.Length != 1)
                throw new InvalidOperationException("Expected exactly one FirstPerformanceVoidControls in FirstPerformanceVoid. Found "
                    + controlsMatches.Length + ".");
            FirstPerformanceVoidControls controls = controlsMatches[0];
            SuccubusPerformer performer = controls.GetComponent<SuccubusPerformer>();
            var controlsData = new SerializedObject(controls);
            SerializedProperty performerProperty = controlsData.FindProperty("performer");
            if (performerProperty != null) performer = performerProperty.objectReferenceValue as SuccubusPerformer;
            if (performer == null || performer.gameObject.scene != scene)
                throw new InvalidOperationException("The existing Lara reference on FirstPerformanceVoidControls is required.");

            AnimationClip source = ResolveJumpForJoySource();
            (PerformerAction jumpForJoy, PerformerAction recoveryTest) = Bake(source, AcceptanceFolder,
                "JumpForJoy", "KAWAII Jump-for-Joy acceptance action.", performer);
            ConfigureAcceptanceScene(scene, controls, performer, jumpForJoy, recoveryTest);
        }

        private static (PerformerAction Action, PerformerAction RecoveryTest) Bake(AnimationClip source,
            string outputFolder, string outputName, string bakeNotes, SuccubusPerformer performer)
        {
            ValidateSource(source);
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath) || sourcePath.StartsWith(GeneratedFolder + "/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Select a source clip outside the generated Actions folder. Vendor/source clips are read-only inputs.");

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ProxyPath).OfType<Avatar>().FirstOrDefault();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ProxyPath);
            if (model == null || avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("The validated laraHumanoid.fbx and its valid Human Avatar are required. The baker does not modify either model importer.");
            if (performer != null && (performer.GetComponent<Animator>() == null
                || performer.GetComponent<Animator>().transform != performer.transform))
                throw new InvalidOperationException("The acceptance performer Animator must be on the SuccubusPerformer root.");

            EnsureFolder("Assets/DazPose/Generated");
            EnsureFolder(GeneratedFolder);
            EnsureFolder(outputFolder);

            string controllerPath = GeneratedFolder + "/__ActionBakeTemporary-" + Guid.NewGuid().ToString("N") + ".controller";
            AnimatorController controller = null;
            Scene preview = default;
            bool previewCreated = false;
            try
            {
                controller = CreateBakeController(controllerPath, source);
                AssetDatabase.SaveAssets();
                preview = EditorSceneManager.NewPreviewScene();
                previewCreated = true;

                GameObject instance = PrefabUtility.InstantiatePrefab(model, preview) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException("Could not instantiate the Humanoid Lara bake source.");
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    throw new InvalidOperationException("laraHumanoid.fbx contains no Animator.");
                if (animator.transform != instance.transform)
                    throw new InvalidOperationException("The Lara Humanoid retarget proxy Animator must be on the imported model root so baked root motion matches the production performer root.");
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 1f;
                animator.enabled = true;
                animator.Rebind();

                Transform[] bones = instance.GetComponentsInChildren<Transform>(true)
                    .Where(bone => bone != instance.transform
                        && !DazPoseExpressionBonePolicy.IsReservedFaceTransformForBodyBake(bone)).ToArray();
                if (bones.Length == 0)
                    throw new InvalidOperationException("The Lara proxy has no body bones available for Action sampling.");
                SampleSet sample = Sample(animator, instance.transform, bones, source);
                ValidateRootTrajectory(sample);
                AnimationClip bodyClip = SaveBodyClip(sample, outputFolder + "/" + outputName + "_Action.anim",
                    outputName + "_Action");
                PerformerAction action = GetOrCreateAction(outputFolder + "/" + outputName + ".asset", outputName);
                AnimationCurve x = BuildTrajectoryCurve(sample, value => value.x);
                AnimationCurve y = BuildTrajectoryCurve(sample, value => value.y);
                AnimationCurve z = BuildTrajectoryCurve(sample, value => value.z);
                AnimationCurve yaw = BuildTrajectoryCurve(sample.RootYaw);
                string notes = bakeNotes + " Sampled on the project-owned Lara Humanoid proxy at " + SampleRate
                    + " fps; skeletal Transform curves and actor-root X/Y/Z/yaw were saved separately. Production Animator root motion remains disabled.";
                action.ConfigureInEditor(bodyClip, x, y, z, yaw, sample.Duration, sourcePath, notes);
                if (!action.IsReady(out string reason))
                    throw new InvalidOperationException("The generated PerformerAction failed validation: " + reason);
                EditorUtility.SetDirty(action);

                PerformerAction recoveryTest = action;
                Vector2 nominalPlanarDisplacement = new Vector2(action.NominalDisplacement.x,
                    action.NominalDisplacement.z);
                bool requiresDeterministicRecovery = nominalPlanarDisplacement.magnitude < 0.25f;
                if (outputFolder == AcceptanceFolder && requiresDeterministicRecovery)
                    recoveryTest = CreateDisplacedRecoveryFixture(bodyClip, sourcePath, outputFolder, outputName);

                WriteBakeReport(source, sourcePath, action, sample, recoveryTest, requiresDeterministicRecovery);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("PERFORMER_ACTION_BAKED: source=" + sourcePath
                    + "; duration=" + action.DurationSeconds.ToString("0.000") + " s"
                    + "; displacement=" + action.NominalDisplacement.ToString("F3") + " m"
                    + "; yaw=" + action.NominalYawDegrees.ToString("0.0") + "°"
                    + "; peak root Y=" + sample.RootPositions.Max(position => position.y).ToString("0.000") + " m"
                    + "; body clip=" + AssetDatabase.GetAssetPath(bodyClip)
                    + "; action=" + AssetDatabase.GetAssetPath(action)
                    + "; recovery fixture=" + recoveryTest.name
                    + ". Source clip was sampled read-only. Review " + ReportPath + ".", action);
                return (action, recoveryTest);
            }
            finally
            {
                if (previewCreated && preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                    AssetDatabase.DeleteAsset(controllerPath);
                AssetDatabase.SaveAssets();
            }
        }

        private static AnimationClip ResolveJumpForJoySource()
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(JumpForJoySourcePath)
                .OfType<AnimationClip>().Where(clip => clip != null && clip.length > 0f && !clip.empty).ToArray();
            if (clips.Length == 0)
                throw new InvalidOperationException("The installed KAWAII JumpForJoy FBX contains no usable imported AnimationClip: " + JumpForJoySourcePath);
            AnimationClip[] named = clips.Where(clip => clip.name.IndexOf("JumpForJoy", StringComparison.OrdinalIgnoreCase) >= 0
                    || clip.name.IndexOf("Jump For Joy", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            if (named.Length == 1) return named[0];
            if (named.Length > 1)
                throw new InvalidOperationException("The KAWAII JumpForJoy FBX has multiple matching clips. Select the intended clip and use Bake Selected Clip as Performer Action.");
            if (clips.Length == 1) return clips[0];
            throw new InvalidOperationException("Could not identify one Jump-for-Joy clip in " + JumpForJoySourcePath
                + ". Imported clip names: " + string.Join(", ", clips.Select(clip => clip.name)) + ".");
        }

        private static void ValidateSource(AnimationClip source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.legacy)
                throw new InvalidOperationException("The selected source clip is Legacy. Use a non-Legacy Lara-compatible clip.");
            if (!IsFinite(source.length) || source.length <= 0f || !IsFinite(source.frameRate)
                || source.frameRate <= 0f || source.empty)
                throw new InvalidOperationException("The selected source clip must have finite, nonempty animation data and a positive duration/frame rate.");
            if (source.isLooping)
                throw new InvalidOperationException("The selected source clip is marked looping. PerformerAction v1 accepts finite, non-looping actions only.");
        }

        private static AnimatorController CreateBakeController(string path, AnimationClip source)
        {
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorControllerLayer layer = controller.layers[0];
            layer.avatarMask = null;
            layer.iKPass = false;
            layer.syncedLayerIndex = -1;
            controller.layers = new[] { layer };
            AnimatorStateMachine machine = layer.stateMachine;
            foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);
            machine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
            machine.entryTransitions = Array.Empty<AnimatorTransition>();
            AnimatorState state = machine.AddState("PerformerActionBake", Vector3.zero);
            state.motion = source;
            state.speed = 1f;
            state.mirror = false;
            state.mirrorParameterActive = false;
            state.iKOnFeet = false;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static SampleSet Sample(Animator animator, Transform actorRoot, Transform[] bones,
            AnimationClip source)
        {
            int frameCount = Mathf.Max(1, Mathf.CeilToInt(source.length * SampleRate));
            float deltaTime = source.length / frameCount;
            var sample = new SampleSet
            {
                Source = source,
                ActorRoot = actorRoot,
                Bones = bones,
                Positions = new Vector3[frameCount + 1][],
                Rotations = new Quaternion[frameCount + 1][],
                Scales = new Vector3[frameCount + 1][],
                RootPositions = new Vector3[frameCount + 1],
                RootYaw = new float[frameCount + 1]
            };

            actorRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator.Rebind();
            animator.Play("Base Layer.PerformerActionBake", 0, 0f);
            animator.Update(0f);
            Vector3 origin = actorRoot.position;
            float originYaw = actorRoot.eulerAngles.y;
            float previousYaw = originYaw;
            float accumulatedYaw = 0f;
            Quaternion originInverse = Quaternion.Euler(0f, -originYaw, 0f);
            for (int frame = 0; frame <= frameCount; frame++)
            {
                if (frame > 0) animator.Update(deltaTime);
                sample.Positions[frame] = new Vector3[bones.Length];
                sample.Rotations[frame] = new Quaternion[bones.Length];
                sample.Scales[frame] = new Vector3[bones.Length];
                for (int bone = 0; bone < bones.Length; bone++)
                {
                    sample.Positions[frame][bone] = bones[bone].localPosition;
                    sample.Rotations[frame][bone] = bones[bone].localRotation;
                    sample.Scales[frame][bone] = bones[bone].localScale;
                }
                sample.RootPositions[frame] = originInverse * (actorRoot.position - origin);
                if (frame > 0) accumulatedYaw += Mathf.DeltaAngle(previousYaw, actorRoot.eulerAngles.y);
                sample.RootYaw[frame] = accumulatedYaw;
                previousYaw = actorRoot.eulerAngles.y;
            }
            return sample;
        }

        private static void ValidateRootTrajectory(SampleSet sample)
        {
            const float startTolerance = 0.001f;
            Vector3 start = sample.RootPositions[0];
            if (Mathf.Abs(start.x) > startTolerance || Mathf.Abs(start.y) > startTolerance
                || Mathf.Abs(start.z) > startTolerance || Mathf.Abs(sample.RootYaw[0]) > startTolerance)
                throw new InvalidOperationException("The sampled PerformerAction root trajectory does not begin at zero.");
            for (int i = 0; i < sample.Count; i++)
            {
                Vector3 position = sample.RootPositions[i];
                if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z)
                    || !IsFinite(sample.RootYaw[i]))
                    throw new InvalidOperationException("The selected source produced a non-finite root trajectory at sample " + i + ". No Action assets were written.");
            }
            Vector3 end = sample.RootPositions[sample.Count - 1];
            if (Mathf.Abs(end.y) > PerformerAction.GroundReturnToleranceMeters)
                throw new InvalidOperationException("The selected action ends " + end.y.ToString("0.000")
                    + " m above/below its starting elevation. PerformerAction v1 accepts temporary actions that return within "
                    + PerformerAction.GroundReturnToleranceMeters.ToString("0.000")
                    + " m. No output clips or assets were written.");
        }

        private static AnimationClip SaveBodyClip(SampleSet sample, string path, string name)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name, frameRate = sample.Source.frameRate, wrapMode = WrapMode.Once };
                AssetDatabase.CreateAsset(clip, path);
            }
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());

            int retainedCurves = 0;
            for (int bone = 0; bone < sample.Bones.Length; bone++)
            {
                Transform target = sample.Bones[bone];
                if (target == sample.ActorRoot || DazPoseExpressionBonePolicy.IsReservedFaceTransformForBodyBake(target)) continue;
                string relativePath = AnimationUtility.CalculateTransformPath(target, sample.ActorRoot);
                if (string.IsNullOrEmpty(relativePath)) continue;
                var px = new List<Keyframe>(sample.Count); var py = new List<Keyframe>(sample.Count); var pz = new List<Keyframe>(sample.Count);
                var rx = new List<Keyframe>(sample.Count); var ry = new List<Keyframe>(sample.Count); var rz = new List<Keyframe>(sample.Count); var rw = new List<Keyframe>(sample.Count);
                var sx = new List<Keyframe>(sample.Count); var sy = new List<Keyframe>(sample.Count); var sz = new List<Keyframe>(sample.Count);
                for (int frame = 0; frame < sample.Count; frame++)
                {
                    float time = sample.Duration * frame / (sample.Count - 1);
                    Vector3 position = sample.Positions[frame][bone];
                    Quaternion rotation = sample.Rotations[frame][bone];
                    Vector3 scale = sample.Scales[frame][bone];
                    px.Add(new Keyframe(time, position.x)); py.Add(new Keyframe(time, position.y)); pz.Add(new Keyframe(time, position.z));
                    rx.Add(new Keyframe(time, rotation.x)); ry.Add(new Keyframe(time, rotation.y)); rz.Add(new Keyframe(time, rotation.z)); rw.Add(new Keyframe(time, rotation.w));
                    sx.Add(new Keyframe(time, scale.x)); sy.Add(new Keyframe(time, scale.y)); sz.Add(new Keyframe(time, scale.z));
                }
                SetCurve(clip, relativePath, "m_LocalPosition.x", px);
                SetCurve(clip, relativePath, "m_LocalPosition.y", py);
                SetCurve(clip, relativePath, "m_LocalPosition.z", pz);
                SetCurve(clip, relativePath, "m_LocalRotation.x", rx);
                SetCurve(clip, relativePath, "m_LocalRotation.y", ry);
                SetCurve(clip, relativePath, "m_LocalRotation.z", rz);
                SetCurve(clip, relativePath, "m_LocalRotation.w", rw);
                SetCurve(clip, relativePath, "m_LocalScale.x", sx);
                SetCurve(clip, relativePath, "m_LocalScale.y", sy);
                SetCurve(clip, relativePath, "m_LocalScale.z", sz);
                retainedCurves += 10;
            }
            if (retainedCurves == 0)
                throw new InvalidOperationException("No skeletal Transform curves were retained for the Generic Action body clip.");
            clip.frameRate = sample.Source.frameRate;
            clip.EnsureQuaternionContinuity();
            clip.wrapMode = WrapMode.Once;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void SetCurve(AnimationClip clip, string path, string property, List<Keyframe> keys)
        {
            var curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static AnimationCurve BuildTrajectoryCurve(SampleSet sample, Func<Vector3, float> selector)
        {
            var keys = new Keyframe[sample.Count];
            for (int i = 0; i < sample.Count; i++)
                keys[i] = new Keyframe((float)i / (sample.Count - 1), selector(sample.RootPositions[i]));
            return CreateLinearCurve(keys);
        }

        private static AnimationCurve BuildTrajectoryCurve(float[] values)
        {
            var keys = new Keyframe[values.Length];
            for (int i = 0; i < values.Length; i++)
                keys[i] = new Keyframe((float)i / (values.Length - 1), values[i]);
            return CreateLinearCurve(keys);
        }

        private static AnimationCurve CreateLinearCurve(Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        private static PerformerAction GetOrCreateAction(string path, string name)
        {
            PerformerAction action = AssetDatabase.LoadAssetAtPath<PerformerAction>(path);
            if (action != null) return action;
            action = ScriptableObject.CreateInstance<PerformerAction>();
            action.name = name;
            AssetDatabase.CreateAsset(action, path);
            return action;
        }

        private static PerformerAction CreateDisplacedRecoveryFixture(AnimationClip bodyClip,
            string sourcePath, string outputFolder, string outputName)
        {
            PerformerAction fixture = GetOrCreateAction(outputFolder + "/DisplacedRecoveryTest.asset", "DisplacedRecoveryTest");
            AnimationCurve zero = CreateLinearCurve(new[] { new Keyframe(0f, 0f), new Keyframe(1f, 0f) });
            AnimationCurve y = CreateLinearCurve(new[]
            {
                new Keyframe(0f, 0f), new Keyframe(0.18f, 0.16f), new Keyframe(0.43f, 0.62f),
                new Keyframe(0.68f, 0.58f), new Keyframe(0.9f, 0.12f), new Keyframe(1f, 0f)
            });
            AnimationCurve z = CreateLinearCurve(new[]
            {
                new Keyframe(0f, 0f), new Keyframe(0.25f, 0.12f), new Keyframe(0.65f, 0.48f), new Keyframe(1f, 0.75f)
            });
            AnimationCurve yaw = CreateLinearCurve(new[]
            {
                new Keyframe(0f, 0f), new Keyframe(0.3f, 4f), new Keyframe(0.7f, 18f), new Keyframe(1f, 25f)
            });
            fixture.ConfigureInEditor(bodyClip, zero, y, z, yaw, bodyClip.length, sourcePath,
                "Deterministic acceptance-only trajectory using the " + outputName
                + " full-body clip: 0.75 m forward, 25° yaw, 0.62 m peak root Y, and zero final elevation. Not a production action.");
            if (!fixture.IsReady(out string reason))
                throw new InvalidOperationException("The deterministic displaced recovery fixture failed validation: " + reason);
            EditorUtility.SetDirty(fixture);
            return fixture;
        }

        private static void ConfigureAcceptanceScene(Scene scene, FirstPerformanceVoidControls controls,
            SuccubusPerformer performer, PerformerAction jumpForJoy, PerformerAction recoveryTest)
        {
            PerformerPoseAcceptanceHarness harness = performer.GetComponent<PerformerPoseAcceptanceHarness>();
            if (harness == null)
                throw new InvalidOperationException("The Lara performer needs PerformerPoseAcceptanceHarness so the generated Action can be included in F5 acceptance checks.");

            Undo.RecordObjects(new UnityEngine.Object[] { controls, harness }, "Configure PerformerAction Acceptance");
            controls.ConfigureActionAcceptance(jumpForJoy, recoveryTest);
            EditorUtility.SetDirty(controls);
            var harnessData = new SerializedObject(harness);
            SerializedProperty jumpProperty = harnessData.FindProperty("actionAcceptanceJumpForJoy");
            SerializedProperty recoveryProperty = harnessData.FindProperty("actionAcceptanceDisplacedTest");
            if (jumpProperty == null || recoveryProperty == null)
                throw new InvalidOperationException("PerformerPoseAcceptanceHarness does not expose the P0.Perform acceptance action fields.");
            jumpProperty.objectReferenceValue = jumpForJoy;
            recoveryProperty.objectReferenceValue = recoveryTest;
            harnessData.ApplyModifiedProperties();
            EditorUtility.SetDirty(harness);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save PerformerAction acceptance references to FirstPerformanceVoid.");
        }

        private static void WriteBakeReport(AnimationClip source, string sourcePath,
            PerformerAction action, SampleSet sample, PerformerAction recoveryTest, bool generatedFixture)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string reportPath = Path.Combine(projectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            var report = new StringBuilder();
            report.AppendLine("# PerformerAction Bake Report");
            report.AppendLine();
            report.AppendLine("Generated by Tools > DAZ Pose > Action. Source assets were read-only inputs.");
            report.AppendLine();
            report.AppendLine("- Source clip: `" + source.name + "`");
            report.AppendLine("- Source asset: `" + sourcePath + "`");
            report.AppendLine("- Output body clip: `" + AssetDatabase.GetAssetPath(action.BodyClip) + "`");
            report.AppendLine("- Output Action asset: `" + AssetDatabase.GetAssetPath(action) + "`");
            report.AppendLine("- Duration: " + action.DurationSeconds.ToString("0.000") + " s");
            report.AppendLine("- Sample rate: " + SampleRate + " fps");
            report.AppendLine("- Nominal root displacement (X/Y/Z): `" + action.NominalDisplacement.ToString("F4") + " m`");
            report.AppendLine("- Nominal root yaw: " + action.NominalYawDegrees.ToString("0.00") + "°");
            report.AppendLine("- Peak sampled root Y: " + sample.RootPositions.Max(position => position.y).ToString("0.0000") + " m");
            report.AppendLine("- Final root Y: " + action.RootY.Evaluate(1f).ToString("0.0000") + " m (limit "
                + PerformerAction.GroundReturnToleranceMeters.ToString("0.000") + " m)");
            report.AppendLine("- Displaced recovery action: `" + recoveryTest.name + "`");
            report.AppendLine("- Synthetic recovery fixture generated: " + (generatedFixture ? "yes" : "no") + ".");
            report.AppendLine("- Production Animator root motion: disabled; trajectory is applied by PerformerActionRuntime.");
            File.WriteAllText(reportPath, report.ToString());
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Cannot create asset folder: " + path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string Sanitize(string value)
        {
            var chars = (value ?? string.Empty).Select(character => char.IsLetterOrDigit(character)
                || character == '_' || character == '-' ? character : '_').ToArray();
            string name = new string(chars).Trim('_');
            return string.IsNullOrEmpty(name) ? "PerformerAction" : name;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
