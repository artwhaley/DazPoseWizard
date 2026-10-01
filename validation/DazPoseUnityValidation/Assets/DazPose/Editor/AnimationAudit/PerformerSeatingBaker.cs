using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DazPose.Performer;
using DazPose.Editor.Importing;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.AnimationAudit
{
    /// <summary>Retargets KAWAII seating through the validated Lara Humanoid proxy and bakes Generic clips/metadata.</summary>
    public static class PerformerSeatingBaker
    {
        private const string ProxyPath = "Assets/TestCharacter/laraHumanoid.fbx";
        private const string OutputFolder = "Assets/Animations/Performer/Seating";
        private const string ControllerPath = OutputFolder + "/SeatingBake.controller";
        private const string ProfilePath = OutputFolder + "/KawaiiSeatingProfile.asset";
        private const string ReportPath = "docs/animation-audit/SeatingBakeReport.md";
        private const int SampleRate = 60;

        private sealed class MotionSpec
        {
            public string Name;
            public string SourcePath;
            public bool Loop;
            public int StateHash;
        }

        private sealed class SampleSet
        {
            public MotionSpec Spec;
            public AnimationClip SourceClip;
            public AnimationClip BodyClip;
            public Transform[] Bones;
            public Vector3[][] Positions;
            public Quaternion[][] Rotations;
            public Vector3[][] Scales;
            public Vector3[] RootPositions;
            public float[] RootYaw;
            public Vector3[] PelvisOffsets;
            public int BodyRootBoneIndex;
            public int LoopOffsetFrame;
            public int Count => RootPositions.Length;
            public int UniqueFrameCount => Count - 1;
            public float Duration => SourceClip.length;
        }

        [MenuItem("Tools/DAZ Pose/Seating/Bake KAWAII Seating for Generic Lara")]
        public static void BakeSeating()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Bake seating outside Play Mode.");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ProxyPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ProxyPath).OfType<Avatar>().FirstOrDefault();
            if (model == null || avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("The validated laraHumanoid.fbx and its valid Human Avatar are required. The seating baker does not modify either Lara model importer.");

            EnsureFolder("Assets/Animations");
            EnsureFolder("Assets/Animations/Performer");
            EnsureFolder(OutputFolder);
            Directory.CreateDirectory(ProjectPath("docs/animation-audit"));

            MotionSpec[] specs = CreateSpecs();
            AnimatorController controller = PrepareController(specs);
            AssetDatabase.SaveAssets();

            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = PrefabUtility.InstantiatePrefab(model, preview) as GameObject;
                if (instance == null) throw new InvalidOperationException("Could not instantiate the Humanoid Lara seating bake proxy.");
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.transform != instance.transform)
                    throw new InvalidOperationException("laraHumanoid must have its Animator on the imported model root so sampled root trajectories match SuccubusPerformer.");
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 1f;
                animator.enabled = true;
                animator.Rebind();

                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips == null)
                    throw new InvalidOperationException("The validated Humanoid Avatar does not expose its Hips transform; seating needs a measured pelvis/contact offset.");
                Transform[] bones = instance.GetComponentsInChildren<Transform>(true)
                    .Where(bone => bone != instance.transform
                        && !DazPoseExpressionBonePolicy.IsReservedFaceTransformForBodyBake(bone)).ToArray();
                var samples = new SampleSet[specs.Length];
                for (int i = 0; i < specs.Length; i++)
                {
                    samples[i] = Sample(animator, instance.transform, hips, bones, specs[i]);
                    Debug.Log("Sampled seating source " + specs[i].Name + ": "
                        + samples[i].Duration.ToString("0.000") + " s, actor root "
                        + samples[i].RootPositions[samples[i].Count - 1].ToString("F3") + " m, pelvis offset "
                        + samples[i].PelvisOffsets[samples[i].Count - 1].ToString("F3") + " m before seat-contact rebase.");
                }

                SampleSet sitStart = Find(samples, "Sit_Start");
                SampleSet crossStart = Find(samples, "Sit_CrossLegs_Start");
                SampleSet crossLoop = Find(samples, "Sit_CrossLegs_Loop");
                SampleSet crossEnd = Find(samples, "Sit_CrossLegs_End");
                SampleSet sitEnd = Find(samples, "Sit_End");
                Vector3 crossLegsBodyRootOffset = RebaseCrossLegsBodyToSeat(
                    instance.transform, sitStart, crossStart, crossLoop, crossEnd);
                Debug.Log("Rebased the Cross Legs skeleton root by " + crossLegsBodyRootOffset.ToString("F3")
                    + " m so its pelvis stays at the Sit_Start seat contact. The performer GameObject root remains fixed during Cross Legs Start, Loop, and End.");
                int loopEntry = FindBestPoseFrame(crossStart, crossStart.UniqueFrameCount, crossLoop, out double loopEntryError);
                crossLoop.LoopOffsetFrame = loopEntry;
                int crossExit = FindBestPoseFrame(crossEnd, 0, crossLoop, out double crossExitError);
                int sitEndEntry = FindBestPoseFrame(sitStart, sitStart.Count - 1, sitEnd,
                    out double sitEndEntryError, 0.45f);
                SampleSet idleCandidate = samples.FirstOrDefault(sample => sample.Spec.Name == "Idle10_Sit_Loop_Candidate");
                int idleEntry = idleCandidate == null ? 0
                    : FindBestPoseFrame(sitStart, sitStart.Count - 1, idleCandidate, out _);
                if (idleCandidate != null) idleCandidate.LoopOffsetFrame = idleEntry;

                SaveOutputs(samples, loopEntry, crossExit, sitEndEntry, crossLegsBodyRootOffset);
                WriteReport(samples, loopEntry, loopEntryError, crossExit, crossExitError,
                    sitEndEntry, sitEndEntryError, idleCandidate, idleEntry, crossLegsBodyRootOffset);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
            Debug.Log("KAWAII seating was sampled through the validated Lara Humanoid proxy and baked to Generic Lara clips. Basic seating holds the last Sit_Start frame; the Idle10 seated loop is included as an unselected audition candidate. Review " + ReportPath + " and visually inspect the seam sequence before treating it as accepted.");
        }

        private static MotionSpec[] CreateSpecs()
        {
            const string root = "Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/";
            var specs = new List<MotionSpec>
            {
                Spec("Sit_Start", root + "@KA_Sit_Start.FBX", false),
                Spec("Sit_End", root + "@KA_Sit_End.FBX", false),
                Spec("Sit_CrossLegs_Start", root + "@KA_Sit_CrossLegs_Start.FBX", false),
                Spec("Sit_CrossLegs_Loop", root + "@KA_Sit_CrossLegs_Loop.FBX", true),
                Spec("Sit_CrossLegs_End", root + "@KA_Sit_CrossLegs_End.FBX", false)
            };

            string idleCandidate = root + "@KA_Idle10_Sit_Loop.FBX";
            if (AssetDatabase.LoadAllAssetsAtPath(idleCandidate).OfType<AnimationClip>()
                .Any(AnimationAuditCatalogBuilder.IsAuditableClip))
                specs.Add(Spec("Idle10_Sit_Loop_Candidate", idleCandidate, true));
            return specs.ToArray();
        }

        private static MotionSpec Spec(string name, string path, bool loop)
        {
            if (!AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Any(AnimationAuditCatalogBuilder.IsAuditableClip))
                throw new InvalidOperationException("Required installed KAWAII source clip is missing: " + path);
            return new MotionSpec
            {
                Name = name,
                SourcePath = path,
                Loop = loop,
                StateHash = Animator.StringToHash("Base Layer." + name)
            };
        }

        private static AnimatorController PrepareController(MotionSpec[] specs)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
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
                AnimationClip clip = LoadSourceClip(spec.SourcePath);
                AnimatorState state = machine.AddState(spec.Name, new Vector3((i % 3) * 250f, (i / 3) * 100f, 0f));
                state.motion = clip;
                state.speed = 1f;
                state.mirror = false;
                state.mirrorParameterActive = false;
                // Foot IK is appropriate for the locomotion bake, but would pull the
                // seated feet toward the floor and corrupt the authored chair pose.
                state.iKOnFeet = false;
                if (i == 0) machine.defaultState = state;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static SampleSet Sample(Animator animator, Transform actorRoot, Transform hips,
            Transform[] bones, MotionSpec spec)
        {
            AnimationClip source = LoadSourceClip(spec.SourcePath);
            int frameCount = Mathf.Max(1, Mathf.CeilToInt(source.length * SampleRate));
            float dt = source.length / frameCount;
            var sample = new SampleSet
            {
                Spec = spec,
                SourceClip = source,
                Bones = bones,
                Positions = new Vector3[frameCount + 1][],
                Rotations = new Quaternion[frameCount + 1][],
                Scales = new Vector3[frameCount + 1][],
                RootPositions = new Vector3[frameCount + 1],
                RootYaw = new float[frameCount + 1],
                PelvisOffsets = new Vector3[frameCount + 1]
            };

            Transform bodyRoot = hips;
            while (bodyRoot.parent != null && bodyRoot.parent != actorRoot)
                bodyRoot = bodyRoot.parent;
            if (bodyRoot.parent != actorRoot)
                throw new InvalidOperationException("The Humanoid Hips transform is not beneath the Animator root; the seating bake cannot preserve the seated pelvis anchor without a movable skeleton root.");
            sample.BodyRootBoneIndex = Array.IndexOf(bones, bodyRoot);
            if (sample.BodyRootBoneIndex < 0)
                throw new InvalidOperationException("The seating bake could not find the skeleton root transform in the sampled bone list.");

            actorRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator.Rebind();
            animator.Play(spec.StateHash, 0, 0f);
            animator.Update(0f);
            Vector3 rootOrigin = actorRoot.position;
            float yawOrigin = actorRoot.eulerAngles.y;
            float previousYaw = yawOrigin;
            float accumulatedYaw = 0f;
            for (int frame = 0; frame <= frameCount; frame++)
            {
                if (frame > 0) animator.Update(dt);
                sample.Positions[frame] = new Vector3[bones.Length];
                sample.Rotations[frame] = new Quaternion[bones.Length];
                sample.Scales[frame] = new Vector3[bones.Length];
                for (int bone = 0; bone < bones.Length; bone++)
                {
                    sample.Positions[frame][bone] = bones[bone].localPosition;
                    sample.Rotations[frame][bone] = bones[bone].localRotation;
                    sample.Scales[frame][bone] = bones[bone].localScale;
                }

                sample.RootPositions[frame] = Quaternion.Euler(0f, -yawOrigin, 0f)
                    * (actorRoot.position - rootOrigin);
                accumulatedYaw += frame == 0 ? 0f : Mathf.DeltaAngle(previousYaw, actorRoot.eulerAngles.y);
                sample.RootYaw[frame] = accumulatedYaw;
                sample.PelvisOffsets[frame] = actorRoot.InverseTransformPoint(hips.position);
                previousYaw = actorRoot.eulerAngles.y;
            }
            return sample;
        }

        private static Vector3 RebaseCrossLegsBodyToSeat(Transform actorRoot,
            SampleSet sitStart, SampleSet crossStart, SampleSet crossLoop, SampleSet crossEnd)
        {
            Vector3 seatedPelvis = sitStart.PelvisOffsets[sitStart.Count - 1];
            Vector3 crossStartPelvis = crossStart.PelvisOffsets[0];
            Vector3 actorLocalOffset = seatedPelvis - crossStartPelvis;
            ApplyBodyRootOffset(actorRoot, crossStart, actorLocalOffset);
            ApplyBodyRootOffset(actorRoot, crossLoop, actorLocalOffset);
            ApplyBodyRootOffset(actorRoot, crossEnd, actorLocalOffset);
            return actorLocalOffset;
        }

        private static void ApplyBodyRootOffset(Transform actorRoot, SampleSet sample,
            Vector3 actorLocalOffset)
        {
            Transform bodyRoot = sample.Bones[sample.BodyRootBoneIndex];
            if (bodyRoot.parent != actorRoot)
                throw new InvalidOperationException("The sampled skeleton root must be a direct child of the Animator root so its local offset matches performer-root space.");

            // The pelvis offsets are stored in actor-root local space, and this direct child
            // uses the same parent space. Moving this child preserves actor-root world position.
            Vector3 bodyRootLocalOffset = actorLocalOffset;
            for (int frame = 0; frame < sample.Count; frame++)
            {
                sample.Positions[frame][sample.BodyRootBoneIndex] += bodyRootLocalOffset;
                sample.PelvisOffsets[frame] += actorLocalOffset;
            }
        }

        private static int FindBestPoseFrame(SampleSet reference, int referenceFrame,
            SampleSet candidate, out double bestScore, float maximumEntryPhase = 1f)
        {
            bestScore = double.PositiveInfinity;
            int bestFrame = 0;
            int candidateCount = candidate.Spec.Loop ? candidate.UniqueFrameCount : candidate.Count;
            candidateCount = Mathf.Clamp(Mathf.CeilToInt(candidateCount * Mathf.Clamp01(maximumEntryPhase)),
                1, candidateCount);
            int boneCount = Mathf.Min(reference.Bones.Length, candidate.Bones.Length);
            for (int frame = 0; frame < candidateCount; frame++)
            {
                double score = 0d;
                for (int bone = 0; bone < boneCount; bone++)
                {
                    score += (reference.Positions[referenceFrame][bone]
                        - candidate.Positions[frame][bone]).sqrMagnitude * 3d;
                    float angle = Quaternion.Angle(reference.Rotations[referenceFrame][bone],
                        candidate.Rotations[frame][bone]);
                    score += angle * angle / 100d;
                }
                score += (reference.PelvisOffsets[referenceFrame] - candidate.PelvisOffsets[frame]).sqrMagnitude * 5d;
                if (score >= bestScore) continue;
                bestScore = score;
                bestFrame = frame;
            }
            return bestFrame;
        }

        private static void SaveOutputs(SampleSet[] samples, int loopEntry, int crossExit,
            int sitEndEntry, Vector3 crossLegsBodyRootOffset)
        {
            SampleSet loop = Find(samples, "Sit_CrossLegs_Loop");
            loop.LoopOffsetFrame = loopEntry;
            foreach (SampleSet sample in samples)
            {
                sample.BodyClip = SaveBodyClip(sample);
                PerformerSeatingMotion motion = SaveMotionAsset(sample.Spec.Name);
                int[] frameIndices = FrameIndices(sample);
                AnimationCurve x = Curve(frameIndices.Select((frame, index) => new Keyframe(
                    NormalizedTime(index, frameIndices.Length), sample.RootPositions[frame].x)).ToArray());
                AnimationCurve y = Curve(frameIndices.Select((frame, index) => new Keyframe(
                    NormalizedTime(index, frameIndices.Length), sample.RootPositions[frame].y)).ToArray());
                AnimationCurve z = Curve(frameIndices.Select((frame, index) => new Keyframe(
                    NormalizedTime(index, frameIndices.Length), sample.RootPositions[frame].z)).ToArray());
                AnimationCurve yaw = Curve(frameIndices.Select((frame, index) => new Keyframe(
                    NormalizedTime(index, frameIndices.Length), sample.RootYaw[frame])).ToArray());
                AnimationCurve pelvisX = Curve(frameIndices.Select((frame, index) => new Keyframe(
                    NormalizedTime(index, frameIndices.Length), sample.PelvisOffsets[frame].x)).ToArray());
                AnimationCurve pelvisY = Curve(frameIndices.Select((frame, index) => new Keyframe(
                    NormalizedTime(index, frameIndices.Length), sample.PelvisOffsets[frame].y)).ToArray());
                AnimationCurve pelvisZ = Curve(frameIndices.Select((frame, index) => new Keyframe(
                    NormalizedTime(index, frameIndices.Length), sample.PelvisOffsets[frame].z)).ToArray());
                float sourcePhase = sample.Spec.Loop
                    ? (float)sample.LoopOffsetFrame / Mathf.Max(1, sample.UniqueFrameCount) : 0f;
                motion.SetBakedData(sample.BodyClip, x, y, z, yaw, pelvisX, pelvisY, pelvisZ,
                    sample.Duration, sample.Spec.Loop, sourcePhase, sample.Spec.SourcePath);
                EditorUtility.SetDirty(motion);
            }

            float crossExitPhase = (float)((crossExit - loopEntry + loop.UniqueFrameCount)
                % loop.UniqueFrameCount) / Mathf.Max(1, loop.UniqueFrameCount);
            float sitEndPhase = (float)sitEndEntry / Mathf.Max(1, Find(samples, "Sit_End").UniqueFrameCount);
            string profilePath = ProfilePath;
            PerformerSeatingProfile profile = AssetDatabase.LoadAssetAtPath<PerformerSeatingProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PerformerSeatingProfile>();
                profile.name = "KawaiiSeatingProfile";
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.Configure(samples.Select(sample => SaveMotionAsset(sample.Spec.Name)).ToArray(),
                crossExitPhase, sitEndPhase, crossLegsBodyRootOffset);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ProfilePath);
        }

        private static AnimationClip SaveBodyClip(SampleSet sample)
        {
            string path = OutputFolder + "/" + sample.Spec.Name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = sample.Spec.Name, frameRate = SampleRate };
                AssetDatabase.CreateAsset(clip, path);
            }
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);

            int[] frames = FrameIndices(sample);
            for (int bone = 0; bone < sample.Bones.Length; bone++)
            {
                string relativePath = AnimationUtility.CalculateTransformPath(sample.Bones[bone], sample.Bones[bone].root);
                if (string.IsNullOrEmpty(relativePath)) continue;
                if (DazPoseExpressionBonePolicy.IsReservedFaceTransformForBodyBake(sample.Bones[bone])) continue;
                var px = new List<Keyframe>(frames.Length); var py = new List<Keyframe>(frames.Length); var pz = new List<Keyframe>(frames.Length);
                var rx = new List<Keyframe>(frames.Length); var ry = new List<Keyframe>(frames.Length); var rz = new List<Keyframe>(frames.Length); var rw = new List<Keyframe>(frames.Length);
                var sx = new List<Keyframe>(frames.Length); var sy = new List<Keyframe>(frames.Length); var sz = new List<Keyframe>(frames.Length);
                for (int index = 0; index < frames.Length; index++)
                {
                    int frame = frames[index];
                    float time = NormalizedTime(index, frames.Length) * sample.Duration;
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
            }
            clip.frameRate = SampleRate;
            clip.EnsureQuaternionContinuity();
            clip.wrapMode = sample.Spec.Loop ? WrapMode.Loop : WrapMode.Once;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = sample.Spec.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static PerformerSeatingMotion SaveMotionAsset(string name)
        {
            string path = OutputFolder + "/" + name + ".asset";
            PerformerSeatingMotion motion = AssetDatabase.LoadAssetAtPath<PerformerSeatingMotion>(path);
            if (motion != null) return motion;
            motion = ScriptableObject.CreateInstance<PerformerSeatingMotion>();
            motion.name = name;
            AssetDatabase.CreateAsset(motion, path);
            return motion;
        }

        private static int[] FrameIndices(SampleSet sample)
        {
            if (!sample.Spec.Loop)
                return Enumerable.Range(0, sample.Count).ToArray();
            int count = sample.UniqueFrameCount;
            var frames = new int[count + 1];
            for (int index = 0; index < count; index++)
                frames[index] = (sample.LoopOffsetFrame + index) % count;
            frames[count] = sample.LoopOffsetFrame;
            return frames;
        }

        private static void SetCurve(AnimationClip clip, string path, string property, List<Keyframe> keys)
        {
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), property), Curve(keys.ToArray()));
        }

        private static AnimationCurve Curve(Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            return curve;
        }

        private static void WriteReport(SampleSet[] samples, int loopEntry, double loopEntryError,
            int crossExit, double crossExitError, int sitEndEntry, double sitEndEntryError,
            SampleSet idleCandidate, int idleEntry, Vector3 crossLegsBodyRootOffset)
        {
            var report = new StringBuilder("# KAWAII seating bake for Generic Lara\n\n");
            report.AppendLine("Source clips were sampled at " + SampleRate + " fps through the validated Human Avatar on `laraHumanoid.fbx`, then baked to project-owned Generic transform clips. The actor-root trajectory and actual Humanoid Hips/pelvis offset are stored separately. The baker does not modify canonical `lara.fbx`, either model importer, or vendor FBXs.\n");
            Vector3 seatedPelvis = Find(samples, "Sit_Start").PelvisOffsets[Find(samples, "Sit_Start").Count - 1];
            Vector3 crossStartPelvis = Find(samples, "Sit_CrossLegs_Start").PelvisOffsets[0];
            report.AppendLine("The Cross Legs body clips share a skeleton-root offset of **" + crossLegsBodyRootOffset.ToString("F3")
                + " m** so the first Cross Legs frame keeps the pelvis at the `Sit_Start` seat contact (measured residual: **"
                + Vector3.Distance(seatedPelvis, crossStartPelvis).ToString("0.000")
                + " m**). This rebases the child skeleton root in the Generic body clips; the performer GameObject root remains locked throughout Cross Legs Start, Loop, and End. Their measured actor-root trajectories remain diagnostic data and are not applied at runtime.\n");
            report.AppendLine("Basic seated state: **hold the final `KA_Sit_Start` frame** so downstream pose, breathing, gaze, expressions, and blink remain active. `KA_Idle10_Sit_Loop` is baked only as an audition candidate; this source-only pass cannot claim visual seam acceptance.\n");
            report.AppendLine("Body ownership: facial transforms below `head`, plus the `upperFaceRig`/`lowerJaw` facial subtrees, are excluded from sampling, seam scoring and emitted curves. Head, neck, body and finger animation remain included. No blendshape curves are emitted. Every bake replaces all configured clips in place and clears their previous curves, including facial curves from older bakes.\n");
            report.AppendLine("| Motion | Source | Duration (s) | Root X (m) | Root Y (m) | Root Z (m) | Root yaw (°) | Final pelvis offset (m) |\n|---|---|---:|---:|---:|---:|---:|---:|");
            foreach (SampleSet sample in samples)
            {
                Vector3 root = sample.RootPositions[sample.Count - 1];
                Vector3 pelvis = sample.PelvisOffsets[sample.Count - 1];
                report.AppendLine("| " + sample.Spec.Name + " | `" + sample.Spec.SourcePath + "` | "
                    + sample.Duration.ToString("0.000") + " | " + root.x.ToString("0.000") + " | "
                    + root.y.ToString("0.000") + " | " + root.z.ToString("0.000") + " | "
                    + sample.RootYaw[sample.Count - 1].ToString("0.0") + " | " + pelvis.ToString("F3") + " |");
            }
            SampleSet loop = Find(samples, "Sit_CrossLegs_Loop");
            report.AppendLine("\n## Automatically measured transition seams\n");
            report.AppendLine("`Sit_Start` end → Basic hold: exact same sampled frame (zero pose discontinuity by construction).\n");
            report.AppendLine("`CrossLegs_Start` end → rotated `CrossLegs_Loop` entry: source loop phase **"
                + ((float)loopEntry / Mathf.Max(1, loop.UniqueFrameCount)).ToString("0.000")
                + "**, pose score **" + loopEntryError.ToString("0.000") + "**. The generated loop clip is rotated so this best-matching frame becomes time zero.\n");
            report.AppendLine("`CrossLegs_Loop` → `CrossLegs_End`: wait for generated loop phase **"
                + ((float)((crossExit - loopEntry + loop.UniqueFrameCount) % loop.UniqueFrameCount)
                    / Mathf.Max(1, loop.UniqueFrameCount)).ToString("0.000")
                + "** before beginning the authored uncross animation; pose score **" + crossExitError.ToString("0.000") + "**.\n");
            SampleSet sitEnd = Find(samples, "Sit_End");
            report.AppendLine("Basic hold → `Sit_End`: start source phase **"
                + ((float)sitEndEntry / Mathf.Max(1, sitEnd.UniqueFrameCount)).ToString("0.000")
                + "** to best match the held Basic pose; pose score **" + sitEndEntryError.ToString("0.000") + "**.\n");
            if (idleCandidate != null)
                report.AppendLine("Audition-only `KA_Idle10_Sit_Loop` best entry phase: **"
                    + ((float)idleEntry / Mathf.Max(1, idleCandidate.UniqueFrameCount)).ToString("0.000")
                    + "**. It is not selected automatically.\n");
            report.AppendLine("These are numerical transform matches, not a visual quality verdict. In Tools > DAZ Pose > Seating > Edit Mode Preview, use Play Sit Down from Approach and Show Seated End Pose to tune the anchors. Sit Start and Sit End use the same actor root calculation as playback, including final anchor correction. Disable Apply playback final correction to inspect the natural landing; Fit Approach to Sit Start moves the actual ApproachAnchor so the natural endpoint meets SeatAnchor. Seated body clips hold the actor root fixed. Then in Play Mode inspect: standing → Sit_Start → Basic hold; Basic → CrossLegs_Start → loop; loop → CrossLegs_End → Basic; and Basic → Sit_End → standing. Keep the loop running for several cycles and check whether Lara drifts relative to the seat.\n");
            File.WriteAllText(ProjectPath(ReportPath), report.ToString());
        }

        private static SampleSet Find(SampleSet[] samples, string name) =>
            samples.First(sample => sample.Spec.Name == name);

        private static AnimationClip LoadSourceClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>().Where(AnimationAuditCatalogBuilder.IsAuditableClip)
            .OrderBy(clip => clip.name, StringComparer.OrdinalIgnoreCase).First();

        private static float NormalizedTime(int index, int count) =>
            count <= 1 ? 0f : (float)index / (count - 1);

        private static string ProjectPath(string relativePath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));

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
