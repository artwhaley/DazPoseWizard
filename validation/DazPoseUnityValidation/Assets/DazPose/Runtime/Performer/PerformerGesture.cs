using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Gesture", fileName = "Performer Gesture")]
    public sealed class PerformerGesture : ScriptableObject
    {
        [SerializeField] private AnimationClip clip;
        [SerializeField, Min(0f)] private float blendInSeconds = 0.12f;
        [SerializeField, Min(0f)] private float blendOutSeconds = 0.18f;

        public AnimationClip Clip => clip;
        public float BlendInSeconds => blendInSeconds;
        public float BlendOutSeconds => blendOutSeconds;

        public bool IsReady(out string reason)
        {
            if (clip == null)
            {
                reason = "A PerformerGesture requires a baked Generic AnimationClip.";
                return false;
            }
            if (!IsFinite(clip.length) || clip.length <= 0f
                || !IsFinite(clip.frameRate) || clip.frameRate <= 0f || clip.empty)
            {
                reason = "Gesture clip '" + clip.name + "' must contain finite, nonempty animation curves and have positive duration and frame rate.";
                return false;
            }
            if (clip.legacy)
            {
                reason = "Gesture clip '" + clip.name + "' is Legacy. Performer Gestures require a project-owned Generic .anim clip.";
                return false;
            }
            if (clip.isLooping)
            {
                reason = "Gesture clip '" + clip.name + "' is looping. Gesture actions must be finite and non-looping.";
                return false;
            }
#if UNITY_EDITOR
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(assetPath)
                || UnityEditor.AssetDatabase.IsSubAsset(clip)
                || !assetPath.StartsWith("Assets/DazPose/Generated/Gestures/", System.StringComparison.OrdinalIgnoreCase)
                || !assetPath.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
            {
                reason = "Gesture clip '" + clip.name + "' must be a project-owned .anim asset under Assets/DazPose/Generated/Gestures/. Bake the compatible Generic source clip first.";
                return false;
            }
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length == 0 || AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0
                || AnimationUtility.GetAnimationEvents(clip).Length != 0)
            {
                reason = "Gesture clip '" + clip.name + "' must contain only finite skeletal Transform curves, with no object-reference curves or animation events.";
                return false;
            }
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime || !settings.hasAdditiveReferencePose
                || settings.additiveReferencePoseTime != 0f || settings.additiveReferencePoseClip != null)
            {
                reason = "Gesture clip '" + clip.name + "' must be non-looping and use its own frame zero as its additive reference pose.";
                return false;
            }
            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.type != typeof(Transform) || !IsSkeletalTransformProperty(binding.propertyName))
                {
                    reason = "Gesture clip '" + clip.name + "' contains a non-skeletal curve binding: "
                        + binding.path + " / " + binding.propertyName + ".";
                    return false;
                }
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0)
                {
                    reason = "Gesture clip '" + clip.name + "' contains an empty Transform curve.";
                    return false;
                }
                foreach (Keyframe key in curve.keys)
                {
                    if (!IsFinite(key.time) || !IsFinite(key.value)
                        || float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent))
                    {
                        reason = "Gesture clip '" + clip.name + "' contains a non-finite animation key.";
                        return false;
                    }
                }
            }
#endif
            if (!IsFinite(blendInSeconds) || blendInSeconds < 0f
                || !IsFinite(blendOutSeconds) || blendOutSeconds < 0f)
            {
                reason = "Gesture blend-in and blend-out times must be finite and nonnegative.";
                return false;
            }

            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureInEditor(AnimationClip sourceClip, float blendIn, float blendOut)
        {
            clip = sourceClip;
            blendInSeconds = blendIn;
            blendOutSeconds = blendOut;
        }
#endif

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsSkeletalTransformProperty(string propertyName)
        {
            return propertyName == "m_LocalPosition.x" || propertyName == "m_LocalPosition.y"
                || propertyName == "m_LocalPosition.z" || propertyName == "m_LocalRotation.x"
                || propertyName == "m_LocalRotation.y" || propertyName == "m_LocalRotation.z"
                || propertyName == "m_LocalRotation.w" || propertyName == "m_LocalScale.x"
                || propertyName == "m_LocalScale.y" || propertyName == "m_LocalScale.z";
        }
    }
}
