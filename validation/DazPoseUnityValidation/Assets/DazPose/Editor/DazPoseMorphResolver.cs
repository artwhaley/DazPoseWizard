using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class ResolvedUnityMorphControl
    {
        public string SourceControlName;
        public string RawControlId;
        public float SourceValue;
        public List<ResolvedUnityMorphBinding> Bindings = new List<ResolvedUnityMorphBinding>();
    }

    public sealed class ResolvedUnityMorphBinding
    {
        public SkinnedMeshRenderer Renderer;
        public int RendererComponentIndex;
        public string RendererPath;
        public string BlendShapeName;
        public int BlendShapeIndex;
        public float UnityWeight;
        public int FrameCount;
        public float[] FrameWeights = Array.Empty<float>();
    }

    public static class DazPoseMorphResolver
    {
        private const float ActiveValueTolerance = 1e-7f;

        private sealed class BlendShapeCandidate
        {
            public SkinnedMeshRenderer Renderer;
            public int ShapeIndex;
            public string ShapeName;
        }

        public static List<ResolvedUnityMorphControl> Resolve(Transform bindingRoot, DazPoseFigureControl[] controls)
        {
            if (bindingRoot == null) throw new ArgumentNullException(nameof(bindingRoot));
            var result = new List<ResolvedUnityMorphControl>();
            var controlsByName = new Dictionary<string, DazPoseFigureControl>(StringComparer.Ordinal);
            foreach (var control in controls ?? Array.Empty<DazPoseFigureControl>())
            {
                if (control == null || Mathf.Abs(control.value) <= ActiveValueTolerance) continue;
                RequireFinite(control.value, control.name);
                var name = string.IsNullOrWhiteSpace(control.name) ? control.rawControlId : control.name;
                if (string.IsNullOrWhiteSpace(name))
                    throw new InvalidOperationException("An active canonical DAZ figure control has no decoded name or raw control id.");

                if (controlsByName.TryGetValue(name, out var previous))
                {
                    if (Mathf.Abs(previous.value - control.value) > ActiveValueTolerance)
                        throw new InvalidOperationException("Canonical data contains conflicting active values for DAZ control '" + name + "'.");
                    continue;
                }
                controlsByName.Add(name, control);
            }

            if (controlsByName.Count == 0) return result;
            var renderers = bindingRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .OrderBy(renderer => AnimationUtility.CalculateTransformPath(renderer.transform, bindingRoot), StringComparer.Ordinal)
                .ToArray();
            var dazFigurePrefixes = GetDazFigurePrefixes(bindingRoot);

            foreach (var pair in controlsByName.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                var control = pair.Value;
                var resolved = new ResolvedUnityMorphControl
                {
                    SourceControlName = pair.Key,
                    RawControlId = control.rawControlId ?? string.Empty,
                    SourceValue = control.value
                };

                var exactMatches = new List<BlendShapeCandidate>();
                var figurePrefixedMatches = new List<BlendShapeCandidate>();
                foreach (var renderer in renderers)
                {
                    var mesh = renderer.sharedMesh;
                    for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                    {
                        var shapeName = mesh.GetBlendShapeName(shapeIndex);
                        var candidate = new BlendShapeCandidate
                        {
                            Renderer = renderer,
                            ShapeIndex = shapeIndex,
                            ShapeName = shapeName
                        };
                        if (string.Equals(shapeName, pair.Key, StringComparison.Ordinal))
                            exactMatches.Add(candidate);
                        else if (IsDazFigurePrefixedBlendShapeName(shapeName, pair.Key, dazFigurePrefixes))
                            figurePrefixedMatches.Add(candidate);
                    }
                }

                // Keep exact imported names authoritative. DAZ FBX imports may prepend
                // the figure node name (for example, Genesis8Female__eCTRLBrowInnerUp-Down).
                var matches = exactMatches.Count > 0 ? exactMatches : figurePrefixedMatches;
                if (matches.Count > 0)
                {
                    var matchDescription = exactMatches.Count > 0 ? "exact imported name" : "DAZ figure-prefixed imported name";
                    var duplicateOnRenderer = matches.GroupBy(candidate => candidate.Renderer)
                        .FirstOrDefault(group => group.Count() > 1);
                    if (duplicateOnRenderer != null)
                        throw new InvalidOperationException("Required DAZ control '" + pair.Key + "' is ambiguous: mesh '"
                            + duplicateOnRenderer.First().Renderer.sharedMesh.name + "' on renderer '"
                            + AnimationUtility.CalculateTransformPath(duplicateOnRenderer.First().Renderer.transform, bindingRoot)
                            + "' contains " + duplicateOnRenderer.Count() + " blendshapes matching the " + matchDescription + ": "
                            + string.Join(", ", duplicateOnRenderer.Select(candidate => "'" + candidate.ShapeName + "'")) + ".");
                }

                foreach (var candidate in matches)
                {
                    var renderer = candidate.Renderer;
                    var mesh = renderer.sharedMesh;
                    var shapeIndex = candidate.ShapeIndex;

                    var frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);
                    if (frameCount <= 0)
                        throw new InvalidOperationException("Required DAZ control '" + pair.Key + "' resolves to a blendshape with no imported frames on mesh '" + mesh.name + "'.");

                    var frameWeights = new float[frameCount];
                    var fullValueWeight = float.NegativeInfinity;
                    for (var frame = 0; frame < frameCount; frame++)
                    {
                        var frameWeight = mesh.GetBlendShapeFrameWeight(shapeIndex, frame);
                        RequireFinite(frameWeight, pair.Key + " frame weight");
                        frameWeights[frame] = frameWeight;
                        if (frameWeight > fullValueWeight) fullValueWeight = frameWeight;
                    }
                    if (!(fullValueWeight > 0f))
                        throw new InvalidOperationException("Required DAZ control '" + pair.Key + "' has no positive full-value frame weight on mesh '" + mesh.name + "'.");

                    var unityWeight = control.value * fullValueWeight;
                    RequireFinite(unityWeight, pair.Key + " mapped Unity weight");
                    resolved.Bindings.Add(new ResolvedUnityMorphBinding
                    {
                        Renderer = renderer,
                        RendererComponentIndex = Array.IndexOf(renderer.transform.GetComponents<SkinnedMeshRenderer>(), renderer),
                        RendererPath = AnimationUtility.CalculateTransformPath(renderer.transform, bindingRoot),
                        BlendShapeName = mesh.GetBlendShapeName(shapeIndex),
                        BlendShapeIndex = shapeIndex,
                        UnityWeight = unityWeight,
                        FrameCount = frameCount,
                        FrameWeights = frameWeights
                    });
                }

                if (resolved.Bindings.Count == 0)
                    throw new InvalidOperationException("Required DAZ control '" + pair.Key
                        + "' is not present as a direct blendshape on the configured reference character. Regenerate the DAZ morph export rules and refresh Lara, or classify this control through a future non-direct/ERC mapping.");
                var ambiguousBindingPath = resolved.Bindings.GroupBy(binding => binding.RendererPath + "|" + binding.BlendShapeName, StringComparer.Ordinal)
                    .FirstOrDefault(group => group.Count() > 1);
                if (ambiguousBindingPath != null)
                    throw new InvalidOperationException("Required DAZ control '" + pair.Key + "' resolves to multiple SkinnedMeshRenderer components at the same AnimationClip path '"
                        + ambiguousBindingPath.First().RendererPath + "'. Unity cannot address those components as separate blendshape curves.");
                result.Add(resolved);
            }
            return result;
        }

        private static HashSet<string> GetDazFigurePrefixes(Transform bindingRoot)
        {
            var prefixes = new HashSet<string>(StringComparer.Ordinal);
            if (bindingRoot.name.StartsWith("Genesis", StringComparison.Ordinal))
                prefixes.Add(bindingRoot.name);

            foreach (var transform in bindingRoot.GetComponentsInChildren<Transform>(true))
                if (transform.parent == bindingRoot && transform.name.StartsWith("Genesis", StringComparison.Ordinal))
                    prefixes.Add(transform.name);

            return prefixes;
        }

        private static bool IsDazFigurePrefixedBlendShapeName(string importedName, string controlName, HashSet<string> figurePrefixes)
        {
            if (string.IsNullOrEmpty(importedName) || figurePrefixes == null || figurePrefixes.Count == 0)
                return false;

            foreach (var figurePrefix in figurePrefixes)
                if (string.Equals(importedName, figurePrefix + "__" + controlName, StringComparison.Ordinal))
                    return true;

            return false;
        }

        private static void RequireFinite(float value, string description)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException("Non-finite value for DAZ morph '" + description + "'.");
        }
    }
}
