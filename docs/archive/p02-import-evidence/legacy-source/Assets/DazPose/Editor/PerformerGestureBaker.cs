using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DazPose.FirstPerformanceVoid;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor
{
    /// <summary>Bakes project-owned Generic upper-body Gesture clips without editing source clips.</summary>
    public static class PerformerGestureBaker
    {
        private const string GestureFolder = "Assets/DazPose/Generated/Gestures";
        private const string AcceptanceFolder = GestureFolder + "/Acceptance";
        private const string MaskPath = GestureFolder + "/PerformerUpperBodyGesture.mask";
        private const string ScenePath = "Assets/Scenes/FirstPerformanceVoid.unity";
        private const float WaveDuration = 1.4f;

        private static readonly string[] SkeletalTransformProperties =
        {
            "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
            "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
            "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z"
        };

        [MenuItem("Tools/DAZ Pose/Gesture/Bake Selected Clip as Performer Gesture")]
        public static void BakeSelectedClip()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Bake Performer Gestures outside Play Mode.");
            AnimationClip source = Selection.activeObject as AnimationClip;
            if (source == null)
                throw new InvalidOperationException("Select one Generic AnimationClip asset in the Project window before baking a Performer Gesture.");

            Scene scene = SceneManager.GetActiveScene();
            SuccubusPerformer performer = FindSinglePerformer(scene);
            Animator animator = RequireRootAnimator(performer);
            if (!PerformerGestureMaskUtility.TryCollectUpperBody(animator, out Transform chestLower,
                    out _, out string[] paths, out string reason))
                throw new InvalidOperationException(reason);

            EnsureFolder(GestureFolder);
            AvatarMask mask = CreateOrUpdateMask(animator);
            string sourceName = Sanitize(source.name);
            string clipPath = GestureFolder + "/" + sourceName + "_Gesture.anim";
            string gesturePath = GestureFolder + "/" + sourceName + ".asset";
            AnimationClip baked = GetOrCreateClip(clipPath, sourceName + "_Gesture");
            int copiedCurveCount = BakeFilteredCurves(source, baked, animator.transform,
                chestLower, paths);
            ValidateBakedClip(baked, paths);
            PerformerGesture gesture = GetOrCreateGesture(gesturePath, sourceName);
            gesture.ConfigureInEditor(baked, 0.12f, 0.18f);
            if (!gesture.IsReady(out reason)) throw new InvalidOperationException(reason);
            EditorUtility.SetDirty(gesture);

            AssignMaskAndSaveScene(scene, performer, mask);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("PERFORMER_GESTURE_BAKED: source=" + AssetDatabase.GetAssetPath(source)
                + "; copied=" + copiedCurveCount + " upper-body transform curves; removed root/pelvis/lower-body, component, blendshape, object-reference, and event data; clip="
                + clipPath + "; gesture=" + gesturePath + "; mask=" + MaskPath
                + ". The selected source asset was read-only and was not edited.", gesture);
        }

        [MenuItem("Tools/DAZ Pose/Gesture/Generate Gesture Acceptance Assets")]
        public static void GenerateAcceptanceWave()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Generate Gesture acceptance assets in Edit Mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the existing FirstPerformanceVoid scene before generating the Gesture acceptance Wave.");

            FirstPerformanceVoidControls[] controlMatches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FirstPerformanceVoidControls>(true)).ToArray();
            if (controlMatches.Length != 1)
                throw new InvalidOperationException("Expected exactly one FirstPerformanceVoidControls in FirstPerformanceVoid. Found "
                    + controlMatches.Length + ".");
            FirstPerformanceVoidControls controls = controlMatches[0];
            var controlsData = new SerializedObject(controls);
            SuccubusPerformer performer = Read<SuccubusPerformer>(controlsData, "performer");
            if (performer == null || performer.gameObject.scene != scene)
                throw new InvalidOperationException("The existing Lara reference on FirstPerformanceVoidControls is required.");
            Animator animator = RequireRootAnimator(performer);
            if (!PerformerGestureMaskUtility.TryCollectUpperBody(animator, out _, out _,
                    out string[] paths, out string reason))
                throw new InvalidOperationException(reason);

            Transform shoulder = FindUniqueBone(animator, "rShldrBend");
            Transform forearm = FindUniqueBone(animator, "rForearmBend");
            Transform hand = FindUniqueBone(animator, "rHand");
            string[] pathArray = { PerformerGestureMaskUtility.GetPath(animator.transform, shoulder),
                PerformerGestureMaskUtility.GetPath(animator.transform, forearm),
                PerformerGestureMaskUtility.GetPath(animator.transform, hand) };
            var maskPaths = new HashSet<string>(paths, StringComparer.Ordinal);
            foreach (string path in pathArray)
                if (!maskPaths.Contains(path))
                    throw new InvalidOperationException("Acceptance Wave bone path '" + path + "' is outside the resolved chestLower mask.");

            EnsureFolder(GestureFolder);
            EnsureFolder(AcceptanceFolder);
            AvatarMask mask = CreateOrUpdateMask(animator);
            AnimationClip clip = GetOrCreateClip(AcceptanceFolder + "/RightHandWave_Gesture.anim", "RightHandWave_Gesture");
            BuildAcceptanceWave(clip, animator.transform, shoulder, forearm, hand);
            ValidateAcceptanceWave(clip, pathArray);
            PerformerGesture gesture = GetOrCreateGesture(AcceptanceFolder + "/RightHandWave.asset", "RightHandWave");
            gesture.ConfigureInEditor(clip, 0.12f, 0.18f);
            if (!gesture.IsReady(out reason)) throw new InvalidOperationException(reason);

            Undo.RecordObjects(new UnityEngine.Object[] { performer, controls }, "Configure Gesture Acceptance");
            AssignMask(performer, mask);
            controls.ConfigureGestureAcceptance(gesture);
            EditorUtility.SetDirty(performer);
            EditorUtility.SetDirty(controls);
            EditorUtility.SetDirty(gesture);

            PerformerPoseAcceptanceHarness harness = performer.GetComponent<PerformerPoseAcceptanceHarness>();
            if (harness != null)
            {
                Undo.RecordObject(harness, "Assign Gesture Acceptance Wave");
                var harnessData = new SerializedObject(harness);
                SerializedProperty waveProperty = harnessData.FindProperty("gestureAcceptanceWave");
                if (waveProperty != null)
                {
                    waveProperty.objectReferenceValue = gesture;
                    harnessData.ApplyModifiedProperties();
                    EditorUtility.SetDirty(harness);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the Gesture references to FirstPerformanceVoid.");
            Debug.Log("GESTURE_ACCEPTANCE_INSTALLED: " + AssetDatabase.GetAssetPath(gesture)
                + " and " + AssetDatabase.GetAssetPath(clip) + "; mask root=chestLower; active transforms="
                + paths.Length + "; animated bones=" + string.Join(", ", pathArray)
                + ". Only Gesture and acceptance references were written; scene camera/environment values were not changed.", controls);
        }

        private static int BakeFilteredCurves(AnimationClip source, AnimationClip destination,
            Transform animatorRoot, Transform chestLower, string[] allowedPaths)
        {
            if (source == destination)
                throw new InvalidOperationException("The selected source clip resolves to the generated destination. Choose a vendor or project source clip outside DazPose/Generated/Gestures.");
            if (source.legacy)
                throw new InvalidOperationException("Legacy AnimationClips cannot be used by the performer's PlayableGraph Gesture layer.");
            ModelImporter modelImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source)) as ModelImporter;
            if (modelImporter != null && modelImporter.animationType != ModelImporterAnimationType.Generic)
                throw new InvalidOperationException("The selected clip comes from a non-Generic model importer. Gesture baking does not retarget Humanoid motion.");
            if (!IsFinite(source.length) || source.length <= 0f
                || !IsFinite(source.frameRate) || source.frameRate <= 0f || source.empty)
                throw new InvalidOperationException("The selected source clip has no finite animation data.");

            var allowed = new HashSet<string>(allowedPaths, StringComparer.Ordinal);
            string anchorPath = PerformerGestureMaskUtility.GetPath(animatorRoot, chestLower);
            var keptBindings = new List<EditorCurveBinding>();
            var keptCurves = new List<AnimationCurve>();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.type != typeof(Transform) || !IsSkeletalTransformProperty(binding.propertyName)) continue;
                bool insideUpperBody = binding.path == anchorPath
                    || binding.path.StartsWith(anchorPath + "/", StringComparison.Ordinal);
                if (!insideUpperBody) continue;
                if (!allowed.Contains(binding.path))
                    throw new InvalidOperationException("The selected clip animates upper-body Transform path '" + binding.path
                        + "', which does not resolve to a skeletal Transform in Lara's Generic hierarchy. No clip was baked.");
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                if (curve == null || curve.length == 0) continue;
                keptBindings.Add(binding);
                keptCurves.Add(new AnimationCurve(curve.keys)
                {
                    preWrapMode = curve.preWrapMode,
                    postWrapMode = curve.postWrapMode
                });
            }
            if (keptBindings.Count == 0)
                throw new InvalidOperationException("The selected clip has no skeletal Transform curves under Lara's chestLower anchor. Humanoid clips are not retargeted by this baker.");

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(destination))
                AnimationUtility.SetEditorCurve(destination, binding, null);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(destination))
                AnimationUtility.SetObjectReferenceCurve(destination, binding, null);
            AnimationUtility.SetAnimationEvents(destination, Array.Empty<AnimationEvent>());
            AnimationUtility.SetEditorCurves(destination, keptBindings.ToArray(), keptCurves.ToArray());
            destination.frameRate = source.frameRate;
            destination.wrapMode = WrapMode.Once;
            destination.EnsureQuaternionContinuity();
            ConfigureFiniteAdditiveClip(destination);
            EditorUtility.SetDirty(destination);
            return keptBindings.Count;
        }

        private static void BuildAcceptanceWave(AnimationClip clip, Transform animatorRoot,
            Transform shoulder, Transform forearm, Transform hand)
        {
            ClearClip(clip);
            const float duration = WaveDuration;
            float[] times = { 0f, 0.18f, 0.34f, 0.48f, 0.62f, 0.76f, 0.90f, 1.04f, 1.20f, duration };
            float[] shoulderAngles = { 0f, 0f, 38f, 52f, 54f, 54f, 54f, 52f, 0f, 0f };
            float[] elbowAngles = { 0f, 0f, 18f, 30f, 32f, 32f, 32f, 30f, 0f, 0f };
            float[] wristAngles = { 0f, 0f, 8f, 24f, -24f, 24f, -24f, 18f, 0f, 0f };

            Vector3 upperDirection = forearm.position - shoulder.position;
            Vector3 lowerDirection = hand.position - forearm.position;
            Vector3 shoulderWorldAxis = PerpendicularCurlAxis(upperDirection);
            Vector3 elbowWorldAxis = PerpendicularCurlAxis(lowerDirection);
            Vector3 shoulderLocalAxis = shoulder.InverseTransformDirection(shoulderWorldAxis).normalized;
            Vector3 elbowLocalAxis = forearm.InverseTransformDirection(elbowWorldAxis).normalized;
            Vector3 wristLocalAxis = hand.InverseTransformDirection(Vector3.up).normalized;

            AddRotationCurves(clip, PerformerGestureMaskUtility.GetPath(animatorRoot, shoulder),
                shoulder.localRotation, shoulderLocalAxis, times, shoulderAngles);
            AddRotationCurves(clip, PerformerGestureMaskUtility.GetPath(animatorRoot, forearm),
                forearm.localRotation, elbowLocalAxis, times, elbowAngles);
            AddRotationCurves(clip, PerformerGestureMaskUtility.GetPath(animatorRoot, hand),
                hand.localRotation, wristLocalAxis, times, wristAngles);
            clip.frameRate = 60f;
            clip.wrapMode = WrapMode.Once;
            clip.EnsureQuaternionContinuity();
            ConfigureFiniteAdditiveClip(clip);
            EditorUtility.SetDirty(clip);
        }

        private static Vector3 PerpendicularCurlAxis(Vector3 boneDirection)
        {
            Vector3 direction = boneDirection.sqrMagnitude > 0.000001f ? boneDirection.normalized : Vector3.right;
            Vector3 axis = Vector3.Cross(direction, Vector3.up);
            if (axis.sqrMagnitude < 0.000001f) axis = Vector3.Cross(direction, Vector3.forward);
            return axis.normalized;
        }

        private static void AddRotationCurves(AnimationClip clip, string path,
            Quaternion reference, Vector3 localAxis, float[] times, float[] degrees)
        {
            var x = new List<Keyframe>(times.Length);
            var y = new List<Keyframe>(times.Length);
            var z = new List<Keyframe>(times.Length);
            var w = new List<Keyframe>(times.Length);
            for (int i = 0; i < times.Length; i++)
            {
                Quaternion rotation = reference * Quaternion.AngleAxis(degrees[i], localAxis);
                x.Add(new Keyframe(times[i], rotation.x));
                y.Add(new Keyframe(times[i], rotation.y));
                z.Add(new Keyframe(times[i], rotation.z));
                w.Add(new Keyframe(times[i], rotation.w));
            }
            SetCurve(clip, path, "m_LocalRotation.x", x);
            SetCurve(clip, path, "m_LocalRotation.y", y);
            SetCurve(clip, path, "m_LocalRotation.z", z);
            SetCurve(clip, path, "m_LocalRotation.w", w);
        }

        private static void SetCurve(AnimationClip clip, string path, string property, List<Keyframe> keys)
        {
            var curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static void ValidateAcceptanceWave(AnimationClip clip, string[] expectedBonePaths)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length != expectedBonePaths.Length * 4)
                throw new InvalidOperationException("Acceptance Wave must contain exactly four local-rotation curves for each of the three resolved arm bones.");
            var expected = new HashSet<string>(expectedBonePaths, StringComparer.Ordinal);
            foreach (EditorCurveBinding binding in bindings)
                if (binding.type != typeof(Transform) || !expected.Contains(binding.path)
                    || !binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                    throw new InvalidOperationException("Acceptance Wave contains an unexpected curve binding: " + binding.path + " / " + binding.propertyName + ".");
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime || !settings.hasAdditiveReferencePose || settings.additiveReferencePoseTime != 0f)
                throw new InvalidOperationException("Acceptance Wave must be finite and use its frame-zero additive reference pose.");
            if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0
                || AnimationUtility.GetAnimationEvents(clip).Length != 0)
                throw new InvalidOperationException("Acceptance Wave cannot contain object-reference curves or AnimationEvents.");
        }

        private static void ValidateBakedClip(AnimationClip clip, string[] allowedPaths)
        {
            var allowed = new HashSet<string>(allowedPaths, StringComparer.Ordinal);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) || !IsSkeletalTransformProperty(binding.propertyName)
                    || !allowed.Contains(binding.path))
                    throw new InvalidOperationException("Baked Gesture clip contains a curve outside the chestLower skeletal subtree: "
                        + binding.path + " / " + binding.propertyName + ".");
            }
            if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0
                || AnimationUtility.GetAnimationEvents(clip).Length != 0)
                throw new InvalidOperationException("Baked Gesture clips cannot contain object-reference curves or AnimationEvents.");
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime || !settings.hasAdditiveReferencePose
                || settings.additiveReferencePoseTime != 0f || settings.additiveReferencePoseClip != null)
                throw new InvalidOperationException("Baked Gesture clip must be non-looping and use its own frame zero as the additive reference pose.");
        }

        private static void ConfigureFiniteAdditiveClip(AnimationClip clip)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.loopBlend = false;
            settings.hasAdditiveReferencePose = true;
            settings.additiveReferencePoseClip = null;
            settings.additiveReferencePoseTime = 0f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void ClearClip(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        }

        private static bool IsSkeletalTransformProperty(string propertyName) =>
            Array.IndexOf(SkeletalTransformProperties, propertyName) >= 0;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static AvatarMask CreateOrUpdateMask(Animator animator)
        {
            if (!PerformerGestureMaskUtility.TryCreateMask(animator, out AvatarMask generated, out string reason))
                throw new InvalidOperationException(reason);
            generated.name = "PerformerUpperBodyGesture";
            AvatarMask asset = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(generated, MaskPath);
                return generated;
            }

            EditorUtility.CopySerialized(generated, asset);
            asset.name = "PerformerUpperBodyGesture";
            EditorUtility.SetDirty(asset);
            UnityEngine.Object.DestroyImmediate(generated);
            return asset;
        }

        private static AnimationClip GetOrCreateClip(string path, string name)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
            clip = new AnimationClip { name = name, frameRate = 60f, wrapMode = WrapMode.Once };
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static PerformerGesture GetOrCreateGesture(string path, string name)
        {
            PerformerGesture gesture = AssetDatabase.LoadAssetAtPath<PerformerGesture>(path);
            if (gesture != null) return gesture;
            gesture = ScriptableObject.CreateInstance<PerformerGesture>();
            gesture.name = name;
            AssetDatabase.CreateAsset(gesture, path);
            return gesture;
        }

        private static void AssignMaskAndSaveScene(Scene scene, SuccubusPerformer performer, AvatarMask mask)
        {
            AssignMask(performer, mask);
            EditorUtility.SetDirty(performer);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the PerformerUpperBodyGesture reference to the active scene.");
        }

        private static void AssignMask(SuccubusPerformer performer, AvatarMask mask)
        {
            var data = new SerializedObject(performer);
            SerializedProperty property = data.FindProperty("gestureUpperBodyMask");
            if (property == null)
                throw new InvalidOperationException("SuccubusPerformer does not expose its serialized Gesture upper-body mask.");
            property.objectReferenceValue = mask;
            data.ApplyModifiedProperties();
        }

        private static SuccubusPerformer FindSinglePerformer(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the Lara scene before baking a Gesture clip.");
            SuccubusPerformer[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SuccubusPerformer>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("The active scene must contain exactly one SuccubusPerformer for Generic path validation. Found "
                    + matches.Length + ".");
            return matches[0];
        }

        private static Animator RequireRootAnimator(SuccubusPerformer performer)
        {
            Animator animator = performer != null ? performer.GetComponent<Animator>() : null;
            if (animator == null)
                throw new InvalidOperationException("The selected scene performer needs an Animator on the SuccubusPerformer root.");
            if (animator.transform != performer.transform)
                throw new InvalidOperationException("The Gesture source paths must be relative to the Generic Lara Animator on the SuccubusPerformer root.");
            return animator;
        }

        private static Transform FindUniqueBone(Animator animator, string name)
        {
            Transform[] matches = animator.transform.GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate.name == name).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Acceptance Wave requires exactly one '" + name + "' transform beneath Lara's Animator. Found "
                    + matches.Length + ".");
            return matches[0];
        }

        private static T Read<T>(SerializedObject serializedObject, string propertyName) where T : UnityEngine.Object
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Cannot create asset folder: " + path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string Sanitize(string value)
        {
            string sanitized = Regex.Replace(value ?? string.Empty, "[^A-Za-z0-9_-]", "_").Trim('_');
            return string.IsNullOrEmpty(sanitized) ? "Gesture" : sanitized;
        }
    }
}
