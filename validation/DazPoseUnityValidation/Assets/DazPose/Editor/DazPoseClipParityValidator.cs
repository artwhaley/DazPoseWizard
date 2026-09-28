using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class DazPoseClipParityResult
    {
        public bool Passed;
        public float MaximumRotationErrorDegrees;
        public float MaximumPositionErrorMeters;
        public float MaximumScaleError;
        public float[] SampleTimes = new[] { 0f, 0.5f, 1f };
        public string Summary;
    }

    public static class DazPoseClipParityValidator
    {
        private const float PositionToleranceMeters = 1e-5f;
        private const float RotationToleranceDegrees = 0.001f;
        private const float ScaleTolerance = 1e-5f;
        private const float KeyTimeTolerance = 1e-5f;

        public static DazPoseClipParityResult Validate(ResolvedUnityPose pose, AnimationClip clip)
        {
            var result = new DazPoseClipParityResult();
            if (pose == null || clip == null || pose.CharacterRoot == null || pose.AnimationRoot == null)
            {
                result.Summary = "Pose, clip, or animation root is missing.";
                return result;
            }

            var bindings = AnimationUtility.GetCurveBindings(clip);
            var structureErrors = ValidateBindings(pose, clip, bindings);
            var objectReferenceBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (objectReferenceBindings.Length > 0)
                structureErrors.Add("Generated static pose clips must not contain object-reference curves.");
            if (structureErrors.Count > 0)
            {
                result.Summary = string.Join(" ", structureErrors);
                return result;
            }

            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(pose.CharacterRoot.gameObject, pose.CharacterRoot.parent, false);
                clone.name = pose.CharacterRoot.name + "__PoseParityTemporary";
                clone.hideFlags = HideFlags.HideAndDontSave;
                var cloneRoot = clone.transform;
                DazPoseEditorCommands.RestoreSnapshot(cloneRoot, pose.RestState);
                var instanceTransforms = cloneRoot.GetComponentsInChildren<Transform>(true)
                    .ToDictionary(item => DazPoseTransformPath.Get(cloneRoot, item), StringComparer.Ordinal);
                var animationRootInstancePath = DazPoseTransformPath.Get(pose.CharacterRoot, pose.AnimationRoot);
                if (!instanceTransforms.TryGetValue(animationRootInstancePath, out var cloneAnimationRoot))
                {
                    result.Summary = "The stable animation root could not be mapped onto the parity-test clone.";
                    return result;
                }

                foreach (var sampleTime in result.SampleTimes)
                {
                    DazPoseEditorCommands.RestoreSnapshot(cloneRoot, pose.RestState);
                    clip.SampleAnimation(cloneAnimationRoot.gameObject, sampleTime);
                    foreach (var bone in pose.Bones.Where(item => item.HasPosition || item.HasRotation || item.HasScale))
                    {
                        if (!instanceTransforms.TryGetValue(bone.InstancePath, out var actual))
                        {
                            result.Summary = "Resolved DAZ bone '" + bone.DazBoneId + "' could not be found on the parity-test clone.";
                            return result;
                        }

                        if (bone.HasRotation)
                            result.MaximumRotationErrorDegrees = Mathf.Max(result.MaximumRotationErrorDegrees, Quaternion.Angle(bone.LocalRotation, actual.localRotation));
                        if (bone.HasPosition)
                            result.MaximumPositionErrorMeters = Mathf.Max(result.MaximumPositionErrorMeters, Vector3.Distance(bone.LocalPosition, actual.localPosition));
                        if (bone.HasScale)
                            result.MaximumScaleError = Mathf.Max(result.MaximumScaleError, Vector3.Distance(bone.LocalScale, actual.localScale));
                    }
                }

                var durationError = Mathf.Abs(clip.length - DazPoseAnimationClipGenerator.DurationSeconds);
                var passed = durationError <= KeyTimeTolerance
                    && result.MaximumRotationErrorDegrees <= RotationToleranceDegrees
                    && result.MaximumPositionErrorMeters <= PositionToleranceMeters
                    && result.MaximumScaleError <= ScaleTolerance;
                result.Passed = passed;
                result.Summary = passed
                    ? "Direct-pose parity passed at 0.0, 0.5, and 1.0 seconds."
                    : "Parity exceeded tolerance: rotation " + result.MaximumRotationErrorDegrees.ToString("G6") + " deg, position "
                        + result.MaximumPositionErrorMeters.ToString("G6") + " m, scale " + result.MaximumScaleError.ToString("G6") + ".";
                return result;
            }
            catch (Exception exception)
            {
                result.Summary = "Parity validation threw: " + exception.Message;
                return result;
            }
            finally
            {
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        private static List<string> ValidateBindings(ResolvedUnityPose pose, AnimationClip clip, EditorCurveBinding[] bindings)
        {
            var errors = new List<string>();
            if (clip.legacy) errors.Add("Generated clip is marked Legacy.");
            if (Mathf.Abs(clip.length - DazPoseAnimationClipGenerator.DurationSeconds) > KeyTimeTolerance)
                errors.Add("Generated clip duration is not exactly one second.");
            if (bindings.Length == 0) errors.Add("Generated clip has no Transform curves.");

            var animatedPaths = new HashSet<string>(pose.Bones.Where(item => item.HasPosition || item.HasRotation || item.HasScale)
                .Select(item => item.AnimationPath), StringComparer.Ordinal);
            foreach (var binding in bindings)
            {
                if (binding.type != typeof(Transform)) errors.Add("Unexpected non-Transform curve binding at '" + binding.path + "'.");
                if (!animatedPaths.Contains(binding.path)) errors.Add("Curve path is not a driven DAZ bone path: '" + binding.path + "'.");
                if (binding.path.IndexOf("[", StringComparison.Ordinal) >= 0 || binding.path.IndexOf("]", StringComparison.Ordinal) >= 0)
                    errors.Add("Animation curve path contains a sibling index: '" + binding.path + "'.");
                if (binding.path.StartsWith(pose.CharacterName + "/", StringComparison.Ordinal))
                    errors.Add("Animation curve path contains the scene character name: '" + binding.path + "'.");
                if (binding.propertyName.StartsWith("localEulerAnglesRaw", StringComparison.Ordinal)
                    || binding.propertyName.StartsWith("localEulerAnglesBaked", StringComparison.Ordinal)
                    || binding.propertyName.StartsWith("m_LocalEuler", StringComparison.Ordinal))
                    errors.Add("Euler rotation curve is not permitted: " + binding.propertyName + ".");

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length != 2)
                {
                    errors.Add("Each static curve must have exactly two keys: " + binding.path + "/" + binding.propertyName + ".");
                    continue;
                }
                var first = curve.keys[0];
                var second = curve.keys[1];
                if (Mathf.Abs(first.time) > KeyTimeTolerance || Mathf.Abs(second.time - 1f) > KeyTimeTolerance)
                    errors.Add("Curve keys must be at 0 and 1 seconds: " + binding.path + "/" + binding.propertyName + ".");
                if (Mathf.Abs(first.value - second.value) > 1e-6f)
                    errors.Add("Static curve start/end values differ: " + binding.path + "/" + binding.propertyName + ".");
                if (float.IsNaN(first.value) || float.IsInfinity(first.value) || float.IsNaN(second.value) || float.IsInfinity(second.value))
                    errors.Add("Curve contains NaN or Infinity: " + binding.path + "/" + binding.propertyName + ".");
            }

            foreach (var bone in pose.Bones.Where(item => item.HasPosition || item.HasRotation || item.HasScale))
            {
                var atPath = bindings.Where(item => item.path == bone.AnimationPath).Select(item => item.propertyName).ToArray();
                if (bone.HasRotation) CheckExactProperties(errors, bone.AnimationPath, atPath, new[]
                    { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" });
                else if (atPath.Any(item => item.StartsWith("m_LocalRotation.", StringComparison.Ordinal)))
                    errors.Add("Undriven rotation has curves at '" + bone.AnimationPath + "'.");
                if (bone.HasPosition) CheckExactProperties(errors, bone.AnimationPath, atPath, new[]
                    { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" });
                else if (atPath.Any(item => item.StartsWith("m_LocalPosition.", StringComparison.Ordinal)))
                    errors.Add("Undriven position has curves at '" + bone.AnimationPath + "'.");
                if (bone.HasScale) CheckExactProperties(errors, bone.AnimationPath, atPath, new[]
                    { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" });
                else if (atPath.Any(item => item.StartsWith("m_LocalScale.", StringComparison.Ordinal)))
                    errors.Add("Undriven scale has curves at '" + bone.AnimationPath + "'.");
            }
            return errors;
        }

        private static void CheckExactProperties(List<string> errors, string path, string[] actual, string[] expected)
        {
            var propertyFamily = expected[0].Substring(0, expected[0].IndexOf(".", StringComparison.Ordinal)) + ".";
            foreach (var property in expected)
                if (actual.Count(item => item == property) != 1)
                    errors.Add("Expected exactly one curve for " + path + "/" + property + ".");
            foreach (var property in actual)
                if (property.StartsWith(propertyFamily, StringComparison.Ordinal) && !expected.Contains(property, StringComparer.Ordinal))
                    errors.Add("Unexpected local-transform component " + path + "/" + property + ".");
        }
    }
}
