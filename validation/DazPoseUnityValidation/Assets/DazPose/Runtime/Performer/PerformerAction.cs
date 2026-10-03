using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Action", fileName = "Performer Action")]
    public sealed class PerformerAction : ScriptableObject
    {
        public const float GroundReturnToleranceMeters = 0.02f;

        [SerializeField] private AnimationClip bodyClip;
        [SerializeField] private AnimationCurve rootX = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve rootY = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve rootZ = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve rootYaw = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField, Min(0f)] private float durationSeconds;
        [SerializeField] private Vector3 nominalDisplacement;
        [SerializeField] private float nominalYawDegrees;
        [SerializeField, Min(0f)] private float blendInSeconds = 0.15f;
        [SerializeField, Min(0f)] private float blendOutSeconds = 0.20f;
        [SerializeField] private string sourceAssetPath;
        [SerializeField, TextArea(2, 5)] private string bakeNotes;

        public AnimationClip BodyClip => bodyClip;
        public AnimationCurve RootX => rootX;
        public AnimationCurve RootY => rootY;
        public AnimationCurve RootZ => rootZ;
        public AnimationCurve RootYaw => rootYaw;
        public float DurationSeconds => durationSeconds;
        public Vector3 NominalDisplacement => nominalDisplacement;
        public float NominalYawDegrees => nominalYawDegrees;
        public float BlendInSeconds => blendInSeconds;
        public float BlendOutSeconds => blendOutSeconds;
        public string SourceAssetPath => sourceAssetPath;
        public string BakeNotes => bakeNotes;

        public bool IsReady(out string reason)
        {
            if (bodyClip == null)
            {
                reason = "A PerformerAction requires a project-owned baked Generic body clip.";
                return false;
            }
            if (!IsFinite(durationSeconds) || durationSeconds <= 0f
                || !IsFinite(bodyClip.length) || bodyClip.length <= 0f
                || Mathf.Abs(bodyClip.length - durationSeconds) > 0.02f)
            {
                reason = "PerformerAction '" + name + "' needs a positive duration matching its body clip.";
                return false;
            }
            if (!IsFinite(bodyClip.frameRate) || bodyClip.frameRate <= 0f || bodyClip.empty
                || bodyClip.legacy || bodyClip.isLooping)
            {
                reason = "PerformerAction '" + name + "' needs a nonempty, non-looping, non-Legacy Generic body clip.";
                return false;
            }
            if (!ValidateTrajectoryCurve(rootX, "RootX", out reason)
                || !ValidateTrajectoryCurve(rootY, "RootY", out reason)
                || !ValidateTrajectoryCurve(rootZ, "RootZ", out reason)
                || !ValidateTrajectoryCurve(rootYaw, "RootYaw", out reason))
                return false;

            const float originTolerance = 0.001f;
            if (Mathf.Abs(rootX.Evaluate(0f)) > originTolerance
                || Mathf.Abs(rootY.Evaluate(0f)) > originTolerance
                || Mathf.Abs(rootZ.Evaluate(0f)) > originTolerance
                || Mathf.Abs(rootYaw.Evaluate(0f)) > originTolerance)
            {
                reason = "PerformerAction '" + name + "' root trajectories must begin at zero displacement and zero yaw.";
                return false;
            }
            if (Mathf.Abs(rootY.Evaluate(1f)) > GroundReturnToleranceMeters)
            {
                reason = "PerformerAction '" + name + "' ends " + rootY.Evaluate(1f).ToString("0.000")
                    + " m from its starting elevation. Temporary actions must return within "
                    + GroundReturnToleranceMeters.ToString("0.000") + " m.";
                return false;
            }
            if (!IsFinite(nominalDisplacement.x) || !IsFinite(nominalDisplacement.y)
                || !IsFinite(nominalDisplacement.z) || !IsFinite(nominalYawDegrees)
                || Vector3.Distance(nominalDisplacement,
                    new Vector3(rootX.Evaluate(1f), rootY.Evaluate(1f), rootZ.Evaluate(1f))) > 0.001f
                || Mathf.Abs(nominalYawDegrees - rootYaw.Evaluate(1f)) > 0.01f)
            {
                reason = "PerformerAction '" + name + "' nominal displacement/yaw metadata does not match its final trajectory keys.";
                return false;
            }
            if (!IsFinite(blendInSeconds) || blendInSeconds < 0f
                || !IsFinite(blendOutSeconds) || blendOutSeconds < 0f)
            {
                reason = "PerformerAction blend times must be finite and nonnegative.";
                return false;
            }

#if UNITY_EDITOR
            string assetPath = AssetDatabase.GetAssetPath(bodyClip);
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsSubAsset(bodyClip)
                || !assetPath.StartsWith("Assets/DazPose/Generated/Actions/", StringComparison.OrdinalIgnoreCase)
                || !assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                reason = "PerformerAction '" + name + "' body clip must be a project-owned .anim under Assets/DazPose/Generated/Actions/.";
                return false;
            }
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(bodyClip);
            if (bindings.Length == 0 || AnimationUtility.GetObjectReferenceCurveBindings(bodyClip).Length != 0
                || AnimationUtility.GetAnimationEvents(bodyClip).Length != 0)
            {
                reason = "PerformerAction body clips must contain skeletal Transform curves only, without object references or events.";
                return false;
            }
            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path)
                    || !IsSkeletalTransformProperty(binding.propertyName))
                {
                    reason = "PerformerAction body clip contains a root or non-skeletal curve: "
                        + binding.path + " / " + binding.propertyName + ".";
                    return false;
                }
                AnimationCurve curve = AnimationUtility.GetEditorCurve(bodyClip, binding);
                if (curve == null || curve.length == 0)
                {
                    reason = "PerformerAction body clip contains an empty transform curve.";
                    return false;
                }
                foreach (Keyframe key in curve.keys)
                {
                    if (!IsFinite(key.time) || !IsFinite(key.value)
                        || !IsFinite(key.inTangent) || !IsFinite(key.outTangent))
                    {
                        reason = "PerformerAction body clip contains a non-finite transform key.";
                        return false;
                    }
                }
            }
#endif
            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureInEditor(AnimationClip clip, AnimationCurve x, AnimationCurve y,
            AnimationCurve z, AnimationCurve yaw, float duration, string source,
            string notes, float blendIn = 0.15f, float blendOut = 0.20f)
        {
            bodyClip = clip;
            rootX = x;
            rootY = y;
            rootZ = z;
            rootYaw = yaw;
            durationSeconds = duration;
            nominalDisplacement = new Vector3(x.Evaluate(1f), y.Evaluate(1f), z.Evaluate(1f));
            nominalYawDegrees = yaw.Evaluate(1f);
            sourceAssetPath = source;
            bakeNotes = notes;
            blendInSeconds = blendIn;
            blendOutSeconds = blendOut;
        }
#endif

        private static bool ValidateTrajectoryCurve(AnimationCurve curve, string label, out string reason)
        {
            if (curve == null || curve.length < 2)
            {
                reason = label + " must contain at least two normalized-time keys.";
                return false;
            }
            Keyframe[] keys = curve.keys;
            if (Mathf.Abs(keys[0].time) > 0.0001f || Mathf.Abs(keys[keys.Length - 1].time - 1f) > 0.0001f)
            {
                reason = label + " must start at t=0 and end at t=1.";
                return false;
            }
            for (int i = 0; i < keys.Length; i++)
            {
                Keyframe key = keys[i];
                if (!IsFinite(key.time) || !IsFinite(key.value)
                    || !IsFinite(key.inTangent) || !IsFinite(key.outTangent)
                    || (i > 0 && key.time <= keys[i - 1].time))
                {
                    reason = label + " contains unordered or non-finite keys.";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        private static bool IsSkeletalTransformProperty(string propertyName) =>
            propertyName == "m_LocalPosition.x" || propertyName == "m_LocalPosition.y"
            || propertyName == "m_LocalPosition.z" || propertyName == "m_LocalRotation.x"
            || propertyName == "m_LocalRotation.y" || propertyName == "m_LocalRotation.z"
            || propertyName == "m_LocalRotation.w" || propertyName == "m_LocalScale.x"
            || propertyName == "m_LocalScale.y" || propertyName == "m_LocalScale.z";

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
