using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.AnimationAudit
{
    /// <summary>
    /// Samples the installed Humanoid clips through the validated Lara Avatar, then writes
    /// project-owned Generic transform clips and actor-root trajectories. Vendor assets are read-only.
    /// </summary>
    public static class PerformerLocomotionBaker
    {
        private const string ProxyPath = "Assets/TestCharacter/laraHumanoid.fbx";
        private const string OutputFolder = "Assets/Animations/Performer/Locomotion";
        private const string BakeControllerPath = OutputFolder + "/LocomotionBake.controller";
        private const string ProfilePath = OutputFolder + "/KawaiiWalk01Profile.asset";
        private const string ReportPath = "docs/animation-audit/Walk01BakeReport.md";
        private const int SampleRate = 60;

        private sealed class MotionSpec
        {
            public string Name;
            public string AssetPath;
            public bool Mirror;
            public bool Loop;
            public int StateHash;
        }

        private sealed class SampleSet
        {
            public string Name;
            public AnimationClip BodyClip;
            public PerformerLocomotionMotion Motion;
            public string SourcePath;
            public float Duration;
            public float FrameRate;
            public Transform[] Bones;
            public Vector3[][] Positions;
            public Quaternion[][] Rotations;
            public Vector3[][] Scales;
            public Vector3[] RootPositions;
            public float[] RootYaw;
            public Vector3[] LeftFootRelative;
            public Vector3[] RightFootRelative;
            public int Count => RootPositions.Length;
        }

        [MenuItem("Tools/DAZ Pose/Locomotion/Bake KAWAII Walk01 for Generic Lara")]
        public static void BakeWalk01()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Bake locomotion outside Play Mode.");

            var sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(ProxyPath).OfType<Avatar>().FirstOrDefault();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ProxyPath);
            if (model == null || sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
                throw new InvalidOperationException("The validated laraHumanoid.fbx and its valid Human Avatar are required. The baker does not modify the model importer.");

            EnsureFolder("Assets/Animations");
            EnsureFolder("Assets/Animations/Performer");
            EnsureFolder(OutputFolder);
            Directory.CreateDirectory(ProjectPath("docs/animation-audit"));
            MotionSpec[] specs = CreateSpecs();
            var controller = PrepareBakeController(specs);
            AssetDatabase.SaveAssets();

            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(model, preview) as GameObject;
                if (instance == null) throw new InvalidOperationException("Could not instantiate the Humanoid Lara bake source.");
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new InvalidOperationException("laraHumanoid.fbx contains no Animator.");
                if (animator.transform != instance.transform)
                    throw new InvalidOperationException("The laraHumanoid retarget proxy Animator must be on the imported model root so its measured root trajectory matches the production SuccubusPerformer root.");
                animator.avatar = sourceAvatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 1f;
                animator.enabled = true;
                animator.Rebind();

                Transform[] bones = instance.GetComponentsInChildren<Transform>(true)
                    .Where(bone => bone != instance.transform).ToArray();
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (leftFoot == null || rightFoot == null)
                    throw new InvalidOperationException("The validated Humanoid Avatar does not expose both feet.");

                var samples = new SampleSet[specs.Length];
                for (int i = 0; i < specs.Length; i++)
                {
                    samples[i] = Sample(animator, instance.transform, bones, leftFoot, rightFoot, specs[i]);
                    Debug.Log("Baked source sample " + specs[i].Name + ": " +
                              samples[i].Duration.ToString("0.000") + " s, " +
                              samples[i].RootPositions.Last().ToString("F3") + " m root displacement, " +
                              samples[i].RootYaw.Last().ToString("F1") + "° root yaw.");
                }
                ValidateRootTrajectory(samples);
                SaveProjectOwnedOutputs(samples, specs);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
            Debug.Log("KAWAII Walk01 was sampled through laraHumanoid and baked to Generic transform clips with separate root trajectories. Review " + ReportPath + " and run the visual gate before production use.");
        }

        private static MotionSpec[] CreateSpecs()
        {
            const string root = "Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/";
            return new[]
            {
                Spec("Walk01_Start_A", root + "@KA_Walk01_Start.FBX", false, false),
                Spec("Walk01_Start_B_Mirrored", root + "@KA_Walk01_Start.FBX", true, false),
                Spec("Walk01_Loop", root + "@KA_Walk01.FBX", false, true),
                Spec("Walk01_Stop_A", root + "@KA_Walk01_Stop.FBX", false, false),
                Spec("Walk01_Stop_B_Mirrored", root + "@KA_Walk01_Stop.FBX", true, false),
                Spec("TurnLeft90", root + "@KA_TurnLeft_90.FBX", false, false),
                Spec("TurnRight90", root + "@KA_TurnRight_90.FBX", false, false),
                Spec("TurnLeft180", root + "@KA_TurnLeft_180.FBX", false, false),
                Spec("TurnRight180", root + "@KA_TurnRight_180.FBX", false, false)
            };
        }

        private static MotionSpec Spec(string name, string path, bool mirror, bool loop)
        {
            if (!AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Any(AnimationAuditCatalogBuilder.IsAuditableClip))
                throw new InvalidOperationException("Required installed source clip is missing: " + path);
            return new MotionSpec { Name = name, AssetPath = path, Mirror = mirror, Loop = loop,
                StateHash = Animator.StringToHash("Base Layer." + name) };
        }

        private static AnimatorController PrepareBakeController(MotionSpec[] specs)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BakeControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(BakeControllerPath);
            AnimatorControllerLayer layer = controller.layers[0];
            layer.avatarMask = null;
            layer.iKPass = false;
            layer.syncedLayerIndex = -1;
            controller.layers = new[] { layer };
            AnimatorStateMachine machine = layer.stateMachine;
            foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);
            machine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
            machine.entryTransitions = Array.Empty<AnimatorTransition>();

            for (int i = 0; i < specs.Length; i++)
            {
                MotionSpec spec = specs[i];
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(spec.AssetPath).OfType<AnimationClip>()
                    .Where(AnimationAuditCatalogBuilder.IsAuditableClip).OrderBy(candidate => candidate.name, StringComparer.OrdinalIgnoreCase).First();
                AnimatorState state = machine.AddState(spec.Name, new Vector3((i % 4) * 240f, (i / 4) * 90f, 0f));
                state.motion = clip;
                state.speed = 1f;
                state.mirror = spec.Mirror;
                state.mirrorParameterActive = false;
                // This matches the visually accepted Humanoid audit setting. Foot placement is
                // resolved here and baked into Generic transforms; production has no runtime IK.
                state.iKOnFeet = true;
                if (i == 0) machine.defaultState = state;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static SampleSet Sample(Animator animator, Transform actorRoot, Transform[] bones,
            Transform leftFoot, Transform rightFoot, MotionSpec spec)
        {
            AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(spec.AssetPath).OfType<AnimationClip>()
                .Where(AnimationAuditCatalogBuilder.IsAuditableClip).OrderBy(candidate => candidate.name, StringComparer.OrdinalIgnoreCase).First();
            int frameCount = Mathf.Max(1, Mathf.CeilToInt(source.length * SampleRate));
            float dt = source.length / frameCount;
            var set = new SampleSet
            {
                Name = spec.Name,
                SourcePath = spec.AssetPath,
                Duration = source.length,
                FrameRate = source.frameRate,
                Bones = bones,
                Positions = new Vector3[frameCount + 1][],
                Rotations = new Quaternion[frameCount + 1][],
                Scales = new Vector3[frameCount + 1][],
                RootPositions = new Vector3[frameCount + 1],
                RootYaw = new float[frameCount + 1],
                LeftFootRelative = new Vector3[frameCount + 1],
                RightFootRelative = new Vector3[frameCount + 1]
            };

            actorRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator.Rebind();
            animator.Play(spec.StateHash, 0, 0f);
            animator.Update(0f);
            Vector3 rootOrigin = actorRoot.position;
            float yawOrigin = actorRoot.eulerAngles.y;
            float previousRootYaw = yawOrigin;
            float accumulatedRootYaw = 0f;
            for (int frame = 0; frame <= frameCount; frame++)
            {
                if (frame > 0) animator.Update(dt);
                set.Positions[frame] = new Vector3[bones.Length];
                set.Rotations[frame] = new Quaternion[bones.Length];
                set.Scales[frame] = new Vector3[bones.Length];
                for (int bone = 0; bone < bones.Length; bone++)
                {
                    set.Positions[frame][bone] = bones[bone].localPosition;
                    set.Rotations[frame][bone] = bones[bone].localRotation;
                    set.Scales[frame][bone] = bones[bone].localScale;
                }
                Quaternion rootInverse = Quaternion.Euler(0f, -yawOrigin, 0f);
                set.RootPositions[frame] = rootInverse * (actorRoot.position - rootOrigin);
                set.RootPositions[frame].y = 0f;
                float currentRootYaw = actorRoot.eulerAngles.y;
                if (frame > 0) accumulatedRootYaw += Mathf.DeltaAngle(previousRootYaw, currentRootYaw);
                set.RootYaw[frame] = accumulatedRootYaw;
                previousRootYaw = currentRootYaw;
                set.LeftFootRelative[frame] = actorRoot.InverseTransformPoint(leftFoot.position);
                set.RightFootRelative[frame] = actorRoot.InverseTransformPoint(rightFoot.position);
            }
            return set;
        }

        private static void ValidateRootTrajectory(SampleSet[] samples)
        {
            SampleSet loop = samples[2];
            Vector3 loopDisplacement = loop.RootPositions[loop.Count - 1];
            if (loopDisplacement.z < 0.05f)
                throw new InvalidOperationException("Walk01 root trajectory extraction produced only "
                    + loopDisplacement.z.ToString("0.000") + " m forward displacement. No locomotion clips/profile were written. "
                    + "Check the KAWAII clip root-motion import settings and the validated Humanoid Avatar before retrying.");

            foreach (int index in new[] { 0, 1, 3, 4 })
            {
                float forward = samples[index].RootPositions[samples[index].Count - 1].z;
                if (forward < 0.03f)
                    throw new InvalidOperationException(samples[index].Name + " produced only " + forward.ToString("0.000")
                        + " m forward root displacement. No locomotion clips/profile were written. "
                        + "The start/stop family must carry its own trajectory; it cannot be replaced with constant-speed translation.");
            }

            for (int i = 5; i < samples.Length; i++)
            {
                float yaw = samples[i].RootYaw[samples[i].Count - 1];
                float required = i < 7 ? 45f : 135f;
                bool expectedLeftTurn = i == 5 || i == 7;
                bool wrongDirection = expectedLeftTurn ? yaw >= 0f : yaw <= 0f;
                if (Mathf.Abs(yaw) < required || wrongDirection)
                    throw new InvalidOperationException(samples[i].Name + " root trajectory extraction produced only "
                        + yaw.ToString("0.0") + "° yaw. No locomotion clips/profile were written. "
                        + (wrongDirection ? "The clip's signed turn direction is opposite its Left/Right source name. " : string.Empty)
                        + "Check the source clip root-motion import settings before retrying.");
            }
        }

        private static AnimationClip SaveBodyClip(SampleSet set, float duration, float frameRate, bool loop)
        {
            string path = OutputFolder + "/" + set.Name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = set.Name, frameRate = frameRate };
                AssetDatabase.CreateAsset(clip, path);
            }
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);

            for (int bone = 0; bone < set.Bones.Length; bone++)
            {
                string relativePath = AnimationUtility.CalculateTransformPath(set.Bones[bone], set.Bones[0].root);
                // The model root is excluded from the bake. All movement curves are stored separately.
                if (string.IsNullOrEmpty(relativePath)) continue;
                var px = new List<Keyframe>(set.Count); var py = new List<Keyframe>(set.Count); var pz = new List<Keyframe>(set.Count);
                var rx = new List<Keyframe>(set.Count); var ry = new List<Keyframe>(set.Count); var rz = new List<Keyframe>(set.Count); var rw = new List<Keyframe>(set.Count);
                var sx = new List<Keyframe>(set.Count); var sy = new List<Keyframe>(set.Count); var sz = new List<Keyframe>(set.Count);
                for (int frame = 0; frame < set.Count; frame++)
                {
                    float time = frame == set.Count - 1 ? duration : duration * frame / (set.Count - 1);
                    Vector3 position = set.Positions[frame][bone]; Quaternion rotation = set.Rotations[frame][bone]; Vector3 scale = set.Scales[frame][bone];
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
            }
            clip.frameRate = frameRate;
            clip.EnsureQuaternionContinuity();
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void SetCurve(AnimationClip clip, string path, string property, List<Keyframe> keys)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), CreateLinearCurve(keys));
        }

        private static AnimationCurve CreateLinearCurve(List<Keyframe> keys)
        {
            var curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < keys.Count; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            for (int i = 0; i < keys.Count; i++) AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            return curve;
        }

        private static PerformerLocomotionMotion SaveMotionAsset(string name)
        {
            string path = OutputFolder + "/" + name + ".asset";
            PerformerLocomotionMotion asset = AssetDatabase.LoadAssetAtPath<PerformerLocomotionMotion>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<PerformerLocomotionMotion>();
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SaveProjectOwnedOutputs(SampleSet[] samples, MotionSpec[] specs)
        {
            var xCurves = new AnimationCurve[samples.Length]; var zCurves = new AnimationCurve[samples.Length];
            var yawCurves = new AnimationCurve[samples.Length]; var distanceCurves = new AnimationCurve[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                SampleSet sample = samples[i]; MotionSpec spec = specs[i];
                sample.BodyClip = SaveBodyClip(sample, sample.Duration, sample.FrameRate, spec.Loop);
                sample.Motion = SaveMotionAsset(sample.Name);
                float duration = sample.Duration;
                var x = new List<Keyframe>(sample.Count); var z = new List<Keyframe>(sample.Count); var yaw = new List<Keyframe>(sample.Count); var distance = new List<Keyframe>(sample.Count);
                float path = 0f;
                for (int frame = 0; frame < sample.Count; frame++)
                {
                    float time = (float)frame / (sample.Count - 1);
                    Vector3 p = sample.RootPositions[frame];
                    if (frame > 0) path += Vector3.Distance(sample.RootPositions[frame], sample.RootPositions[frame - 1]);
                    x.Add(new Keyframe(time, p.x)); z.Add(new Keyframe(time, p.z)); yaw.Add(new Keyframe(time, sample.RootYaw[frame])); distance.Add(new Keyframe(time, path));
                }
                xCurves[i] = CreateLinearCurve(x); zCurves[i] = CreateLinearCurve(z);
                yawCurves[i] = CreateLinearCurve(yaw); distanceCurves[i] = CreateLinearCurve(distance);
                sample.Motion.SetBakedData(sample.BodyClip, xCurves[i], zCurves[i], yawCurves[i], distanceCurves[i], duration,
                    sample.SourcePath, SupportAt(sample, 0), SupportAt(sample, sample.Count - 1),
                    "Sampled through laraHumanoid's imported Human Avatar at " + SampleRate + " fps; actor-root planar trajectory stored separately.");
                EditorUtility.SetDirty(sample.Motion);
            }

            float startLoopA = FindBestPhase(samples[0], samples[0].Count - 1, samples[2]);
            float startLoopB = FindBestPhase(samples[1], samples[1].Count - 1, samples[2]);
            float stopLoopA = FindBestPhase(samples[3], 0, samples[2]);
            float stopLoopB = FindBestPhase(samples[4], 0, samples[2]);
            samples[0].Motion.SetPhaseMetadata(0f, 1f, startLoopA);
            samples[1].Motion.SetPhaseMetadata(0f, 1f, startLoopB);
            samples[2].Motion.SetPhaseMetadata(0f, 1f, startLoopA);
            samples[3].Motion.SetPhaseMetadata(stopLoopA, 1f, startLoopA);
            samples[4].Motion.SetPhaseMetadata(stopLoopB, 1f, startLoopB);
            for (int i = 5; i < samples.Length; i++) samples[i].Motion.SetPhaseMetadata(0f, 1f, 0f);
            foreach (SampleSet sample in samples) EditorUtility.SetDirty(sample.Motion);

            string profilePath = ProfilePath;
            var profile = AssetDatabase.LoadAssetAtPath<PerformerLocomotionProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<PerformerLocomotionProfile>(); profile.name = "KawaiiWalk01Profile"; AssetDatabase.CreateAsset(profile, profilePath); }
            profile.Configure(samples.Select(sample => sample.Motion).ToArray());
            EditorUtility.SetDirty(profile);
            WriteBakeReport(samples, new[] { startLoopA, startLoopB, stopLoopA, stopLoopB }, profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ProfilePath);
        }

        private static float FindBestPhase(SampleSet reference, int referenceFrame, SampleSet loop)
        {
            float bestPhase = 0f; double bestScore = double.PositiveInfinity;
            int candidates = Mathf.Max(60, loop.Count - 1);
            Vector3 referenceVelocity = RootVelocityAt(reference, referenceFrame);
            float referenceYawVelocity = RootYawVelocityAt(reference, referenceFrame);
            for (int candidate = 0; candidate < candidates; candidate++)
            {
                int frame = Mathf.RoundToInt((float)candidate / candidates * (loop.Count - 1));
                double score = 0d;
                int count = Mathf.Min(reference.Bones.Length, loop.Bones.Length);
                for (int bone = 0; bone < count; bone++)
                {
                    score += (reference.Positions[referenceFrame][bone] - loop.Positions[frame][bone]).sqrMagnitude * 3d;
                    float angle = Quaternion.Angle(reference.Rotations[referenceFrame][bone], loop.Rotations[frame][bone]);
                    score += angle * angle / 100d;
                }
                Vector3 velocityDifference = referenceVelocity - RootVelocityAt(loop, frame);
                float yawVelocityDifference = referenceYawVelocity - RootYawVelocityAt(loop, frame);
                score += velocityDifference.sqrMagnitude * 10d + yawVelocityDifference * yawVelocityDifference / 90d;
                if (score >= bestScore) continue;
                bestScore = score;
                bestPhase = (float)frame / (loop.Count - 1);
            }
            return bestPhase;
        }

        private static Vector3 RootVelocityAt(SampleSet sample, int frame)
        {
            int prior = Mathf.Max(0, frame - 1);
            int next = Mathf.Min(sample.Count - 1, frame + 1);
            float sampleSeconds = sample.Duration / (sample.Count - 1);
            float seconds = Mathf.Max(0.0001f, (next - prior) * sampleSeconds);
            return (sample.RootPositions[next] - sample.RootPositions[prior]) / seconds;
        }

        private static float RootYawVelocityAt(SampleSet sample, int frame)
        {
            int prior = Mathf.Max(0, frame - 1);
            int next = Mathf.Min(sample.Count - 1, frame + 1);
            float sampleSeconds = sample.Duration / (sample.Count - 1);
            float seconds = Mathf.Max(0.0001f, (next - prior) * sampleSeconds);
            return (sample.RootYaw[next] - sample.RootYaw[prior]) / seconds;
        }

        private static LocomotionSupportFoot SupportAt(SampleSet sample, int frame)
        {
            float dt = sample.BodyClip.length / (sample.Count - 1);
            int prior = Mathf.Max(0, frame - 1); int next = Mathf.Min(sample.Count - 1, frame + 1);
            Vector3 leftVelocity = (sample.LeftFootRelative[next] - sample.LeftFootRelative[prior]) / Mathf.Max(0.0001f, (next - prior) * dt);
            Vector3 rightVelocity = (sample.RightFootRelative[next] - sample.RightFootRelative[prior]) / Mathf.Max(0.0001f, (next - prior) * dt);
            bool left = sample.LeftFootRelative[frame].y < 0.12f && leftVelocity.magnitude < 0.2f;
            bool right = sample.RightFootRelative[frame].y < 0.12f && rightVelocity.magnitude < 0.2f;
            if (left && right) return LocomotionSupportFoot.Both;
            if (left) return LocomotionSupportFoot.Left;
            if (right) return LocomotionSupportFoot.Right;
            return LocomotionSupportFoot.Neither;
        }

        private static void WriteBakeReport(SampleSet[] samples, float[] phases, PerformerLocomotionProfile profile)
        {
            var report = new StringBuilder("# KAWAII Walk01 canonical Lara bake\n\n");
            report.AppendLine("Source clips are sampled through the configured Human Avatar on `laraHumanoid.fbx` and baked to project-owned Generic transform clips. Actor planar root translation/yaw are separate curves; the body clip excludes the actor GameObject transform. Vendor FBXs/importers and canonical `lara.fbx` remain read-only.\n");
            report.AppendLine("Retarget bake setting: Unity Humanoid Foot IK **enabled** to match the accepted audit appearance; the resulting joint transforms are baked. Production playback is Generic and runs no runtime Foot IK.\n");
            report.AppendLine("Default production playback speed: **" + profile.PlaybackSpeed.ToString("0.00")
                + "×** for both body and authored trajectory. Full Start/Stop distance threshold: **"
                + profile.MinimumWalkDistance.ToString("0.00") + " m**.\n");
            report.AppendLine("| Motion | Source | Duration (s) | Root X (m) | Root Z (m) | Yaw (°) | Path (m) | Entry support | Exit support |\n|---|---|---:|---:|---:|---:|---:|---|---|");
            foreach (SampleSet sample in samples)
            {
                PerformerLocomotionMotion motion = sample.Motion;
                report.AppendLine("| " + sample.Name + " | `" + sample.SourcePath + "` | " + sample.BodyClip.length.ToString("0.000") + " | " + motion.NominalPlanarDisplacement.x.ToString("0.000") + " | " + motion.NominalPlanarDisplacement.z.ToString("0.000") + " | " + motion.NominalYawDegrees.ToString("0.0") + " | " + motion.PlanarDistanceAt(1f).ToString("0.000") + " | " + motion.EntrySupportFoot + " | " + motion.ExitSupportFoot + " |");
            }
            report.AppendLine("\n## Automatically measured phase matches\n");
            report.AppendLine("Start A → Loop: **" + phases[0].ToString("0.000") + "**; mirrored Start B → Loop: **" + phases[1].ToString("0.000") + "**.");
            report.AppendLine("Stop A entry phase: **" + phases[2].ToString("0.000") + "**; mirrored Stop B entry phase: **" + phases[3].ToString("0.000") + "**.");
            report.AppendLine("\nIn-motion reversals use a phase-matched authored Stop, then an in-place 180° turn clip, then a new Start. The 180° clips are not phase-baked into the walking cycle.");
            report.AppendLine("Phase matching minimizes local transform position/rotation error and root linear/yaw velocity difference at each Start/Loop and Loop/Stop seam. Support-foot labels are height/relative-velocity heuristics. Inspect every seam visually before treating these estimates as accepted production timing.");
            File.WriteAllText(ProjectPath(ReportPath), report.ToString());
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        private static void EnsureFolder(string path)
        {
            if (!string.Equals(path, "Assets", StringComparison.Ordinal)
                && !path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("AssetDatabase folders must be project-relative paths under Assets.", nameof(path));
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
