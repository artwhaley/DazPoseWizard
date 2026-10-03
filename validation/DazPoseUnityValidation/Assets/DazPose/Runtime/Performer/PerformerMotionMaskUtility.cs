using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Resolves and validates the Generic right-collar subtree used by Motion ownership.</summary>
    public static class PerformerMotionMaskUtility
    {
        private static readonly string[] RequiredBones =
        {
            "rCollar", "rShldrBend", "rShldrTwist", "rForearmBend", "rForearmTwist", "rHand"
        };

        public static bool TryResolveRightArm(Animator animator, out Transform collar,
            out Transform shoulder, out Transform forearm, out Transform hand,
            out string[] paths, out string reason)
        {
            collar = shoulder = forearm = hand = null;
            paths = Array.Empty<string>();
            if (animator == null)
            {
                reason = "A Lara Animator is required to resolve the right-arm Motion mask.";
                return false;
            }

            var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
            Transform[] hierarchy = animator.transform.GetComponentsInChildren<Transform>(true);
            foreach (string name in RequiredBones)
            {
                Transform[] matches = hierarchy.Where(candidate => candidate.name == name).ToArray();
                if (matches.Length != 1)
                {
                    reason = "Expected exactly one Generic bone named '" + name + "' beneath Lara's Animator. Found "
                        + matches.Length + ".";
                    return false;
                }
                byName.Add(name, matches[0]);
            }

            collar = byName["rCollar"];
            shoulder = byName["rShldrBend"];
            forearm = byName["rForearmBend"];
            hand = byName["rHand"];
            foreach (string name in RequiredBones.Skip(1))
            {
                Transform bone = byName[name];
                if (!bone.IsChildOf(collar))
                {
                    reason = "Generic bone '" + name + "' is not beneath the uniquely resolved rCollar subtree.";
                    return false;
                }
            }

            var skeletalTransforms = new HashSet<Transform> { collar };
            foreach (SkinnedMeshRenderer renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer == null || renderer.bones == null) continue;
                foreach (Transform bone in renderer.bones)
                {
                    if (bone == null || (bone != collar && !bone.IsChildOf(collar))) continue;
                    for (Transform current = bone; current != null; current = current.parent)
                    {
                        if (current != collar && !current.IsChildOf(collar)) break;
                        skeletalTransforms.Add(current);
                        if (current == collar) break;
                    }
                }
            }
            foreach (string name in RequiredBones)
                if (!skeletalTransforms.Contains(byName[name]))
                {
                    reason = "Generic bone '" + name + "' is not part of Lara's skinned right-arm chain.";
                    return false;
                }

            paths = skeletalTransforms.Select(candidate => PerformerGestureMaskUtility.GetPath(animator.transform, candidate))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (paths.Length < RequiredBones.Length || paths.Any(string.IsNullOrEmpty)
                || paths.Distinct(StringComparer.Ordinal).Count() != paths.Length)
            {
                reason = "Lara's right-collar subtree does not expose unique Generic skeletal paths below the actor root.";
                return false;
            }
            string collarPath = PerformerGestureMaskUtility.GetPath(animator.transform, collar);
            if (paths.Any(path => path != collarPath && !path.StartsWith(collarPath + "/", StringComparison.Ordinal)))
            {
                reason = "The right-arm mask resolved a transform outside the rCollar subtree.";
                return false;
            }

            reason = null;
            return true;
        }

        public static bool TryCreateMask(Animator animator, out AvatarMask mask, out string reason)
        {
            mask = null;
            if (!TryResolveRightArm(animator, out _, out _, out _, out _, out string[] paths, out reason)) return false;
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
                reason = "Assign the generated RightArmMotion AvatarMask.";
                return false;
            }
            if (!TryResolveRightArm(animator, out _, out _, out _, out _, out string[] expectedPaths, out reason)) return false;
            var expected = new HashSet<string>(expectedPaths, StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < mask.transformCount; i++)
            {
                if (!mask.GetTransformActive(i)) continue;
                string path = mask.GetTransformPath(i);
                if (string.IsNullOrEmpty(path) || !actual.Add(path))
                {
                    reason = "The right-arm Motion mask contains an empty or duplicate active transform path.";
                    return false;
                }
            }
            for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
                if (mask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)part))
                {
                    reason = "The right-arm Motion mask must use only Generic Transform paths; Humanoid body parts must be disabled.";
                    return false;
                }
            if (!actual.SetEquals(expected))
            {
                string unexpected = actual.Except(expected).FirstOrDefault();
                string missing = expected.Except(actual).FirstOrDefault();
                reason = "The right-arm Motion mask does not exactly match Lara's skinned rCollar subtree."
                    + (unexpected != null ? " Unexpected active path: '" + unexpected + "'." : string.Empty)
                    + (missing != null ? " Missing active path: '" + missing + "'." : string.Empty);
                return false;
            }
            reason = null;
            return true;
        }
    }
}
