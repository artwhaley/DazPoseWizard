using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Defines the Generic Genesis 8 upper-body region from actual skin-bone references.</summary>
    public static class PerformerGestureMaskUtility
    {
        public static bool TryCollectUpperBody(Animator animator, out Transform chestLower,
            out Transform[] bones, out string[] paths, out string reason)
        {
            chestLower = null;
            bones = Array.Empty<Transform>();
            paths = Array.Empty<string>();
            if (animator == null)
            {
                reason = "A Lara Animator is required to resolve the Gesture mask.";
                return false;
            }

            Transform[] namedAnchors = animator.transform.GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate.name == "chestLower").ToArray();
            if (namedAnchors.Length != 1)
            {
                reason = "Expected exactly one Genesis 8 'chestLower' bone beneath the Animator. Found "
                    + namedAnchors.Length + ".";
                return false;
            }
            chestLower = namedAnchors[0];

            var skeletalTransforms = new HashSet<Transform> { chestLower };
            SkinnedMeshRenderer[] renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer == null || renderer.bones == null) continue;
                foreach (Transform bone in renderer.bones)
                {
                    if (bone == null || (bone != chestLower && !bone.IsChildOf(chestLower))) continue;
                    // Include the complete skeletal chain between chestLower and each weighted bone.
                    for (Transform current = bone; current != null; current = current.parent)
                    {
                        if (current != chestLower && !current.IsChildOf(chestLower)) break;
                        skeletalTransforms.Add(current);
                        if (current == chestLower) break;
                    }
                }
            }

            Transform[] orderedBones = skeletalTransforms
                .OrderBy(candidate => GetPath(animator.transform, candidate), StringComparer.Ordinal)
                .ToArray();
            if (orderedBones.Length < 2)
            {
                reason = "The skinned Lara hierarchy does not expose skeletal descendants beneath chestLower.";
                return false;
            }

            string[] orderedPaths = orderedBones
                .Select(candidate => GetPath(animator.transform, candidate)).ToArray();
            if (orderedPaths.Any(string.IsNullOrEmpty))
            {
                reason = "The chestLower subtree must be below the Animator root; actor-root animation is not part of Gesture.";
                return false;
            }
            if (orderedPaths.Distinct(StringComparer.Ordinal).Count() != orderedPaths.Length)
            {
                reason = "The chestLower subtree has duplicate Transform paths, so Generic animation bindings cannot resolve its bones uniquely.";
                return false;
            }

            bones = orderedBones;
            paths = orderedPaths;
            reason = null;
            return true;
        }

        public static bool TryCreateMask(Animator animator, out AvatarMask mask, out string reason)
        {
            mask = null;
            if (!TryCollectUpperBody(animator, out _, out _, out string[] paths, out reason)) return false;

            var result = new AvatarMask();
            for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
                result.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part, false);
            result.transformCount = paths.Length;
            for (int i = 0; i < paths.Length; i++)
            {
                result.SetTransformPath(i, paths[i]);
                result.SetTransformActive(i, true);
            }
            mask = result;
            reason = null;
            return true;
        }

        public static bool IsValidMask(Animator animator, AvatarMask mask, out string reason)
        {
            if (mask == null)
            {
                reason = "Assign the generated PerformerUpperBodyGesture AvatarMask.";
                return false;
            }
            if (!TryCollectUpperBody(animator, out _, out _, out string[] expectedPaths, out reason)) return false;

            var expected = new HashSet<string>(expectedPaths, StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < mask.transformCount; i++)
            {
                if (!mask.GetTransformActive(i)) continue;
                string path = mask.GetTransformPath(i);
                if (string.IsNullOrEmpty(path) || !actual.Add(path))
                {
                    reason = "The Gesture mask contains an empty or duplicate active transform path.";
                    return false;
                }
            }
            for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
            {
                if (mask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)part))
                {
                    reason = "The Gesture mask must use only the Generic chestLower Transform subtree; Humanoid body parts must be disabled.";
                    return false;
                }
            }
            if (!actual.SetEquals(expected))
            {
                string unexpected = actual.Except(expected).FirstOrDefault();
                string missing = expected.Except(actual).FirstOrDefault();
                reason = "The Gesture mask does not exactly match Lara's chestLower skeletal subtree."
                    + (unexpected != null ? " Unexpected active path: '" + unexpected + "'." : string.Empty)
                    + (missing != null ? " Missing active path: '" + missing + "'." : string.Empty);
                return false;
            }

            reason = null;
            return true;
        }

        public static string GetPath(Transform root, Transform target)
        {
            if (root == null || target == null || target == root) return string.Empty;
            var parts = new List<string>();
            for (Transform current = target; current != null && current != root; current = current.parent)
                parts.Add(current.name);
            if (target.root != root.root || !target.IsChildOf(root)) return string.Empty;
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
