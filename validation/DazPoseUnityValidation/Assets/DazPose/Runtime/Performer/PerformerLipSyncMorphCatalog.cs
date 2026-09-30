using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    public readonly struct PerformerLipSyncMorphDefinition
    {
        public PerformerLipSyncMorphDefinition(string viseme, string blendShapeName, string sourceControlName,
            float maximumSalsaAmount)
        {
            Viseme = viseme;
            BlendShapeName = blendShapeName;
            SourceControlName = sourceControlName;
            MaximumSalsaAmount = maximumSalsaAmount;
        }

        public string Viseme { get; }
        public string BlendShapeName { get; }
        public string SourceControlName { get; }
        public float RestSalsaAmount => PerformerLipSyncMorphCatalog.RestSalsaAmount;
        public float RestUnityWeight => PerformerLipSyncMorphCatalog.RestUnityWeight;
        public float MaximumSalsaAmount { get; }
        public float MaximumUnityWeight => MaximumSalsaAmount * PerformerLipSyncMorphCatalog.UnityWeightPerSalsaAmount;
        public float DurationOn => 0.12f;
        public float DurationHold => 0f;
        public float DurationOff => 0.06f;
        public string ControllerName => PerformerLipSyncMorphCatalog.ControllerNamePrefix + Viseme;
    }

    public readonly struct PerformerLipSyncMorphBinding
    {
        public PerformerLipSyncMorphBinding(PerformerLipSyncMorphDefinition definition,
            SkinnedMeshRenderer renderer, int index)
        {
            Definition = definition;
            Renderer = renderer;
            Index = index;
        }

        public PerformerLipSyncMorphDefinition Definition { get; }
        public SkinnedMeshRenderer Renderer { get; }
        public int Index { get; }
    }

    /// <summary>
    /// Exact Lara speech-shape ownership contract shared by SALSA setup, Expression sanitation,
    /// BodyPose graph binding, runtime validation, and diagnostics.
    /// </summary>
    public static class PerformerLipSyncMorphCatalog
    {
        public const string RendererPath = "Genesis8Female/Genesis8Female.Shape";
        public const string ControllerNamePrefix = "DazPose.P0.9B/";
        // SALSA's ShapeController takes normalized amounts (0..1) and converts them to
        // Unity blendshape weights (0..100). Keep these units explicit at every call site.
        public const float RestSalsaAmount = 0f;
        public const float RestUnityWeight = 0f;
        public const float UnityWeightPerSalsaAmount = 100f;

        private static readonly PerformerLipSyncMorphDefinition[] Morphs =
        {
            new PerformerLipSyncMorphDefinition("W", "Genesis8Female__eCTRLvW", "eCTRLvW", 1f),
            new PerformerLipSyncMorphDefinition("F", "Genesis8Female__eCTRLvF", "eCTRLvF", 1f),
            new PerformerLipSyncMorphDefinition("T", "Genesis8Female__eCTRLvT", "eCTRLvT", 1f),
            new PerformerLipSyncMorphDefinition("TH", "Genesis8Female__eCTRLvTH", "eCTRLvTH", 1f),
            new PerformerLipSyncMorphDefinition("OW", "Genesis8Female__eCTRLvOW", "eCTRLvOW", 1f),
            new PerformerLipSyncMorphDefinition("EE", "Genesis8Female__eCTRLvEE", "eCTRLvEE", 1f),
            new PerformerLipSyncMorphDefinition("UW", "Genesis8Female__eCTRLvUW", "eCTRLvUW", 1f),
            new PerformerLipSyncMorphDefinition("AA", "Genesis8Female__eCTRLvAA", "eCTRLvAA", 1f)
        };

        private static readonly IReadOnlyList<PerformerLipSyncMorphDefinition> ReadOnlyMorphs =
            Array.AsReadOnly(Morphs);

        public static IReadOnlyList<PerformerLipSyncMorphDefinition> Definitions => ReadOnlyMorphs;

        public static bool IsOwnedBinding(string rendererPath, string blendShapeName)
        {
            if (!string.Equals(rendererPath, RendererPath, StringComparison.Ordinal)
                || string.IsNullOrEmpty(blendShapeName)) return false;
            foreach (var morph in Morphs)
                if (string.Equals(morph.BlendShapeName, blendShapeName, StringComparison.Ordinal)) return true;
            return false;
        }

        public static bool TryResolveBindings(Animator animator, out PerformerLipSyncMorphBinding[] bindings,
            out string failure)
        {
            bindings = Array.Empty<PerformerLipSyncMorphBinding>();
            failure = null;
            if (animator == null)
            {
                failure = "The performer Animator is missing.";
                return false;
            }

            var renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer renderer = null;
            var rendererMatches = 0;
            foreach (var candidate in renderers)
            {
                if (!string.Equals(GetRelativePath(animator.transform, candidate.transform), RendererPath,
                        StringComparison.Ordinal)) continue;
                renderer = candidate;
                rendererMatches++;
            }
            if (rendererMatches != 1 || renderer == null)
            {
                failure = "Expected exactly one Lara speech renderer at '" + RendererPath
                    + "' beneath Animator '" + animator.name + "'; found " + rendererMatches + ".";
                return false;
            }
            if (renderer.sharedMesh == null)
            {
                failure = "Speech renderer '" + RendererPath + "' has no mesh.";
                return false;
            }

            var resolved = new PerformerLipSyncMorphBinding[Morphs.Length];
            for (var morphIndex = 0; morphIndex < Morphs.Length; morphIndex++)
            {
                var definition = Morphs[morphIndex];
                var foundIndex = -1;
                var matches = 0;
                for (var shapeIndex = 0; shapeIndex < renderer.sharedMesh.blendShapeCount; shapeIndex++)
                {
                    if (!string.Equals(renderer.sharedMesh.GetBlendShapeName(shapeIndex), definition.BlendShapeName,
                            StringComparison.Ordinal)) continue;
                    foundIndex = shapeIndex;
                    matches++;
                }

                if (matches != 1 || foundIndex < 0
                    || !string.Equals(renderer.sharedMesh.GetBlendShapeName(foundIndex), definition.BlendShapeName,
                        StringComparison.Ordinal))
                {
                    failure = "Expected exactly one blendshape '" + definition.BlendShapeName + "' at '"
                        + RendererPath + "'; found " + matches + ".";
                    return false;
                }
                resolved[morphIndex] = new PerformerLipSyncMorphBinding(definition, renderer, foundIndex);
            }

            bindings = resolved;
            return true;
        }

        public static string GetRelativePath(Transform root, Transform target)
        {
            if (root == null || target == null) return "<missing>";
            if (root == target) return string.Empty;
            var segments = new Stack<string>();
            var current = target;
            while (current != null && current != root)
            {
                segments.Push(current.name);
                current = current.parent;
            }
            return current == root ? string.Join("/", segments) : "<outside-root>";
        }
    }
}
