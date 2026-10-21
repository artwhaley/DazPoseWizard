using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Motion Variant", fileName = "Performer Motion Variant")]
    public sealed class PerformerMotionVariant : ScriptableObject
    {
        [SerializeField] private AnimationClip clip;
        [SerializeField] private string displayName;
        [SerializeField] private string sourceAssetPath;
        [SerializeField, TextArea(2, 5)] private string bakeNotes;

        public AnimationClip Clip => clip;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string SourceAssetPath => sourceAssetPath;
        public string BakeNotes => bakeNotes;

        public bool IsReady(out string reason)
        {
            if (clip == null)
            {
                reason = "A PerformerMotionVariant requires a Generic AnimationClip.";
                return false;
            }
            if (!IsFinite(clip.length) || clip.length <= 0f
                || !IsFinite(clip.frameRate) || clip.frameRate <= 0f || clip.empty)
            {
                reason = "Motion variant '" + name + "' needs finite animation curves and a positive duration and frame rate.";
                return false;
            }
            if (clip.legacy || clip.isLooping)
            {
                reason = "Motion variant '" + name + "' must be a finite, non-looping Playables clip.";
                return false;
            }
#if UNITY_EDITOR
            ModelImporter modelImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as ModelImporter;
            if (modelImporter != null && modelImporter.animationType != ModelImporterAnimationType.Generic)
            {
                reason = "Motion variant '" + name + "' must use Generic skeletal animation; Humanoid retargeting is not supported.";
                return false;
            }
            if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0
                || AnimationUtility.GetAnimationEvents(clip).Length != 0)
            {
                reason = "Motion variant '" + name + "' cannot contain object-reference curves or AnimationEvents.";
                return false;
            }
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length == 0)
            {
                reason = "Motion variant '" + name + "' must contain skeletal Transform curves.";
                return false;
            }
            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path)
                    || !IsTransformProperty(binding.propertyName))
                {
                    reason = "Motion variant '" + name + "' contains a non-skeletal or actor-root curve: "
                        + binding.path + " / " + binding.propertyName + ".";
                    return false;
                }
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0)
                {
                    reason = "Motion variant '" + name + "' contains an empty Transform curve.";
                    return false;
                }
                foreach (Keyframe key in curve.keys)
                    if (!IsFinite(key.time) || !IsFinite(key.value)
                        || float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent))
                    {
                        reason = "Motion variant '" + name + "' contains a non-finite animation key.";
                        return false;
                    }
            }
#endif
            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureInEditor(AnimationClip sourceClip, string label,
            string sourcePath = null, string notes = null)
        {
            Configure(sourceClip, label, sourcePath, notes);
            EditorUtility.SetDirty(this);
        }
#endif

        internal void Configure(AnimationClip sourceClip, string label,
            string sourcePath = null, string notes = null)
        {
            clip = sourceClip;
            displayName = label;
            sourceAssetPath = sourcePath;
            bakeNotes = notes;
        }

#if UNITY_EDITOR
        private static bool IsTransformProperty(string propertyName) =>
            propertyName == "m_LocalPosition.x" || propertyName == "m_LocalPosition.y" || propertyName == "m_LocalPosition.z"
            || propertyName == "m_LocalRotation.x" || propertyName == "m_LocalRotation.y"
            || propertyName == "m_LocalRotation.z" || propertyName == "m_LocalRotation.w"
            || propertyName == "m_LocalScale.x" || propertyName == "m_LocalScale.y" || propertyName == "m_LocalScale.z";
#endif

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
