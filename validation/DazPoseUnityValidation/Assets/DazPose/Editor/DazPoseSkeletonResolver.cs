using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class DazPoseBoneResolution
    {
        public DazPoseBone Definition;
        public Transform Transform;
        public string Method;
        public string Status;
        public string Detail;
    }

    public static class DazPoseSkeletonResolver
    {
        public static DazPoseBoneResolution[] Resolve(Transform root, DazPoseDefinition pose)
        {
            var nameIndex = new Dictionary<string, List<Transform>>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (!nameIndex.TryGetValue(transform.name, out var list)) nameIndex[transform.name] = list = new List<Transform>();
                list.Add(transform);
            }

            var bonesById = pose.bones.ToDictionary(bone => bone.id, StringComparer.Ordinal);
            var resolvedById = new Dictionary<string, DazPoseBoneResolution>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            DazPoseBoneResolution ResolveBone(DazPoseBone bone)
            {
                if (resolvedById.TryGetValue(bone.id, out var cached)) return cached;
                if (!visiting.Add(bone.id))
                    return new DazPoseBoneResolution { Definition = bone, Method = "none", Status = "missing", Detail = "DAZ parent cycle." };

                Transform expectedParent = null;
                if (!string.IsNullOrEmpty(bone.parentId))
                {
                    if (bonesById.TryGetValue(bone.parentId, out var dazParent))
                    {
                        var parentResolution = ResolveBone(dazParent);
                        if (parentResolution.Status == "resolved") expectedParent = parentResolution.Transform;
                        else
                        {
                            var failedParent = new DazPoseBoneResolution { Definition = bone, Method = "none", Status = "missing", Detail = "Expected DAZ parent '" + bone.parentId + "' did not resolve." };
                            resolvedById[bone.id] = failedParent;
                            visiting.Remove(bone.id);
                            return failedParent;
                        }
                    }
                    else
                    {
                        var parentMatches = nameIndex.TryGetValue(bone.parentId, out var namedParent) ? namedParent : null;
                        if (parentMatches == null || parentMatches.Count != 1)
                        {
                            var failedParent = new DazPoseBoneResolution { Definition = bone, Method = "none", Status = "missing", Detail = "Expected non-bone DAZ parent '" + bone.parentId + "' was not uniquely found by exact imported name." };
                            resolvedById[bone.id] = failedParent;
                            visiting.Remove(bone.id);
                            return failedParent;
                        }
                        expectedParent = parentMatches[0];
                    }
                }

                var result = ResolveOne(nameIndex, bone, expectedParent);
                resolvedById[bone.id] = result;
                visiting.Remove(bone.id);
                return result;
            }

            return pose.bones.Select(ResolveBone).ToArray();
        }

        private static DazPoseBoneResolution ResolveOne(Dictionary<string, List<Transform>> nameIndex, DazPoseBone bone, Transform expectedParent)
        {
            var hasName = nameIndex.TryGetValue(bone.name, out var byName);
            var hasId = nameIndex.TryGetValue(bone.id, out var byId);
            var matches = hasName && byName.Count > 0 ? byName : hasId ? byId : null;
            var method = hasName && byName.Count > 0 ? "exact name" : hasId ? "exact id" : "none";
            if (matches == null || matches.Count == 0)
                return new DazPoseBoneResolution { Definition = bone, Method = method, Status = "missing", Detail = "No exact imported transform name or ID match." };

            var qualified = expectedParent == null ? matches : matches.Where(candidate => candidate.parent == expectedParent).ToList();
            if (qualified.Count == 1)
                return new DazPoseBoneResolution { Definition = bone, Transform = qualified[0], Method = method + (expectedParent == null ? string.Empty : " + exact parent"), Status = "resolved" };
            if (qualified.Count > 1)
                return new DazPoseBoneResolution { Definition = bone, Method = method + " + exact parent", Status = "ambiguous", Detail = "More than one exact transform has the expected direct parent." };
            return new DazPoseBoneResolution { Definition = bone, Method = method, Status = "missing", Detail = "Exact transform match exists, but its direct parent does not match DAZ parent id '" + bone.parentId + "'." };
        }
    }

    public static class DazPoseTransformPath
    {
        public static string Get(Transform root, Transform transform)
        {
            if (root == transform) return string.Empty;
            var segments = new Stack<string>();
            var current = transform;
            while (current != null && current != root)
            {
                segments.Push(current.name.Replace("/", "\\/") + "[" + current.GetSiblingIndex() + "]");
                current = current.parent;
            }
            return current == root ? string.Join("/", segments) : "<outside-root>";
        }

        public static int DepthFrom(Transform root, Transform transform)
        {
            var depth = 0;
            while (transform != null && transform != root) { depth++; transform = transform.parent; }
            return depth;
        }
    }
}
