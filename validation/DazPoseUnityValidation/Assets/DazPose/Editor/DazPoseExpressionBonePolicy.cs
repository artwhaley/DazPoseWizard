using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Performer;
using DazPose.UnityValidation;
using UnityEngine;

namespace DazPose.Editor.Importing
{
    internal sealed class DazPoseExpressionBoneSelection
    {
        public Dictionary<string, PerformerExpressionBoneProperties> ActiveFacialProperties { get; } =
            new Dictionary<string, PerformerExpressionBoneProperties>(StringComparer.Ordinal);
        public List<string> Diagnostics { get; } = new List<string>();
        public int ActiveSkeletalChannelCount { get; set; }
        public int RetainedSkeletalChannelCount { get; set; }
        public int IgnoredSkeletalChannelCount { get; set; }
        public int UnsupportedFacialChannelCount { get; set; }
        public string Failure { get; set; }
    }

    internal static class DazPoseExpressionBonePolicy
    {
        private const string UpperFaceAnchor = "upperFaceRig";
        private const string LowerFaceAnchor = "lowerJaw";
        private const float ActiveTolerance = 1e-7f;

        /// <summary>
        /// Body bakes leave facial articulation and gaze-owned transforms to their own systems.
        /// Keep the head itself; exclude its descendants, including jaw, eyes, teeth and ears.
        /// The expression anchors also identify facial subtrees if the head is absent.
        /// This broader body exclusion does not change which bones Expressions may animate.
        /// </summary>
        internal static bool IsReservedFaceTransformForBodyBake(Transform target)
        {
            for (Transform current = target; current != null; current = current.parent)
            {
                if (current.name == UpperFaceAnchor || current.name == LowerFaceAnchor
                    || current.name == "lowerFaceRig")
                    return true;
                if (current.name == "head")
                    return current != target;
            }
            return false;
        }

        public static DazPoseExpressionBoneSelection Analyze(DazPoseDefinition definition)
        {
            var result = new DazPoseExpressionBoneSelection();
            if (definition == null)
            {
                result.Failure = "Expression skeletal selection requires canonical DAZ pose data.";
                return result;
            }

            var bones = definition.bones ?? Array.Empty<DazPoseBone>();
            var groups = bones.Where(bone => bone != null && !string.IsNullOrEmpty(bone.id))
                .GroupBy(bone => bone.id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var activeChannels = (definition.poseChannels ?? Array.Empty<DazPoseChannel>())
                .Where(IsActiveSkeletalChannel).ToArray();
            result.ActiveSkeletalChannelCount = activeChannels.Length;

            if (activeChannels.Length > 0)
            {
                foreach (var anchor in new[] { UpperFaceAnchor, LowerFaceAnchor })
                {
                    if (!groups.TryGetValue(anchor, out var matches) || matches.Length != 1)
                    {
                        result.Failure = "Expression preset contains active skeletal channels, but canonical Genesis 8 Female bone anchor '"
                            + anchor + "' is missing or ambiguous. Regenerate the canonical preset from a matching figure.";
                        return result;
                    }
                }
            }

            foreach (var channel in activeChannels)
            {
                var targetId = channel.targetId ?? string.Empty;
                if (targetId == "head" || targetId == "lEye" || targetId == "rEye")
                {
                    result.IgnoredSkeletalChannelCount++;
                    result.Diagnostics.Add("ignored gaze-owned targetId='" + targetId + "' property='" + channel.property + "'");
                    continue;
                }

                if (groups.TryGetValue(targetId, out var targetMatches) && targetMatches.Length > 1)
                {
                    result.Failure = "Expression skeletal target '" + targetId + "' is ambiguous in canonical DAZ bone data.";
                    result.Diagnostics.Add("rejected ambiguous targetId='" + targetId + "' property='" + channel.property + "'");
                    return result;
                }

                if (!IsFacialArticulationBone(targetId, groups))
                {
                    result.IgnoredSkeletalChannelCount++;
                    result.Diagnostics.Add("ignored targetId='" + targetId + "' property='" + channel.property
                        + "' axis='" + channel.axis + "': target is outside the upperFaceRig/lowerJaw facial articulation trees");
                    continue;
                }

                if (!channel.supported || (channel.property != "rotation" && channel.property != "translation"))
                {
                    result.UnsupportedFacialChannelCount++;
                    result.Failure = "Expression preset actively drives unsupported facial skeletal property '"
                        + channel.url + "'. Only facial rotation and translation are supported.";
                    result.Diagnostics.Add("rejected unsupported facial channel targetId='" + targetId + "' property='" + channel.property
                        + "' axis='" + channel.axis + "'");
                    return result;
                }

                var property = channel.property == "rotation"
                    ? PerformerExpressionBoneProperties.LocalRotation
                    : PerformerExpressionBoneProperties.LocalPosition;
                if (result.ActiveFacialProperties.TryGetValue(targetId, out var existing))
                    result.ActiveFacialProperties[targetId] = existing | property;
                else
                    result.ActiveFacialProperties.Add(targetId, property);
                result.RetainedSkeletalChannelCount++;
                result.Diagnostics.Add("retained facial targetId='" + targetId + "' property='" + channel.property
                    + "' axis='" + channel.axis + "'");
            }

            foreach (var targetId in result.ActiveFacialProperties.Keys)
            {
                if (!groups.TryGetValue(targetId, out var matches) || matches.Length != 1)
                {
                    result.Failure = "Expression facial bone target '" + targetId + "' is missing or ambiguous in canonical DAZ bone data.";
                    return result;
                }
            }
            return result;
        }

        internal static bool IsFacialArticulationBone(string targetId,
            IReadOnlyDictionary<string, DazPoseBone[]> bonesById)
        {
            if (string.IsNullOrEmpty(targetId) || bonesById == null
                || !bonesById.TryGetValue(targetId, out var matches) || matches.Length != 1)
                return false;
            if (targetId == "head" || targetId == "lEye" || targetId == "rEye") return false;

            var current = matches[0];
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (current != null && !string.IsNullOrEmpty(current.id) && visited.Add(current.id))
            {
                if (current.id == UpperFaceAnchor || current.id == LowerFaceAnchor) return true;
                if (string.IsNullOrEmpty(current.parentId)
                    || !bonesById.TryGetValue(current.parentId, out var parents) || parents.Length != 1)
                    return false;
                current = parents[0];
            }
            return false;
        }

        private static bool IsActiveSkeletalChannel(DazPoseChannel channel)
        {
            if (channel == null || (channel.property != "rotation" && channel.property != "translation" && channel.property != "scale"))
                return false;
            var neutral = channel.property == "scale" ? 1f : 0f;
            var keys = channel.keys ?? Array.Empty<DazPoseKey>();
            return keys.Length > 0 && keys[0] != null && Math.Abs(keys[0].value - neutral) > ActiveTolerance;
        }
    }
}
