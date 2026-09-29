using System;
using System.Linq;
using System.Text.RegularExpressions;
using DazPose.UnityValidation;
using UnityEditor;
using UnityEngine;

namespace DazPose.Editor.Importing
{
    internal static class DazPoseRestPoseService
    {
        internal static DazPoseCharacterState Capture(Transform root, bool force)
            => Capture(root, force, true);

        internal static DazPoseCharacterState Capture(Transform root, bool force, bool recordUndo)
        {
            var state = root.GetComponent<DazPoseCharacterState>();
            if (state == null)
                state = recordUndo ? Undo.AddComponent<DazPoseCharacterState>(root.gameObject) : root.gameObject.AddComponent<DazPoseCharacterState>();
            if (state.hasCapturedRestPose && !force)
            {
                var currentTransforms = root.GetComponentsInChildren<Transform>(true);
                var refreshTransforms = !RestTransformStructureMatches(root, currentTransforms, state.transforms);
                var refreshBlendShapes = !state.hasCapturedBlendShapes || !RestBlendShapeStructureMatches(root, state);
                if (recordUndo && (refreshTransforms || refreshBlendShapes))
                    Undo.RecordObject(state, "Refresh captured import rest pose");
                if (refreshTransforms)
                    state.transforms = ReconcileRestTransforms(root, currentTransforms, state.transforms);
                if (refreshBlendShapes)
                    CaptureRestBlendShapes(root, state);
                if (recordUndo && (refreshTransforms || refreshBlendShapes))
                    EditorUtility.SetDirty(state);
                return state;
            }
            var transforms = root.GetComponentsInChildren<Transform>(true);
            state.transforms = transforms.Select(item => new DazPoseRestTransform
                {
                    path = DazPoseTransformPath.Get(root, item),
                    localPosition = item.localPosition,
                    localRotation = item.localRotation,
                    localScale = item.localScale
                }).ToArray();
            CaptureRestBlendShapes(root, state);
            state.hasCapturedRestPose = true;
            if (recordUndo) EditorUtility.SetDirty(state);
            return state;
        }

        private static bool RestTransformStructureMatches(Transform root, Transform[] current,
            DazPoseRestTransform[] saved)
        {
            if (current == null || saved == null || current.Length != saved.Length) return false;
            for (var index = 0; index < current.Length; index++)
            {
                if (saved[index] == null
                    || !string.Equals(saved[index].path, DazPoseTransformPath.Get(root, current[index]), StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static DazPoseRestTransform[] ReconcileRestTransforms(Transform root, Transform[] current,
            DazPoseRestTransform[] saved)
        {
            var savedByPath = (saved ?? Array.Empty<DazPoseRestTransform>())
                .Where(item => item != null && item.path != null)
                .GroupBy(item => item.path, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var savedByStructure = savedByPath.Values
                .GroupBy(item => NormalizeIndexedTransformPath(item.path), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var reconciled = new DazPoseRestTransform[current.Length];

            for (var index = 0; index < current.Length; index++)
            {
                var transform = current[index];
                var path = DazPoseTransformPath.Get(root, transform);
                if (savedByPath.TryGetValue(path, out var exact))
                {
                    reconciled[index] = exact;
                    continue;
                }

                var structurePath = NormalizeIndexedTransformPath(path);
                if (savedByStructure.TryGetValue(structurePath, out var structuralMatches)
                    && structuralMatches.Length == 1)
                {
                    var previous = structuralMatches[0];
                    reconciled[index] = new DazPoseRestTransform
                    {
                        path = path,
                        localPosition = previous.localPosition,
                        localRotation = previous.localRotation,
                        localScale = previous.localScale
                    };
                    continue;
                }

                // A newly imported transform has no previous captured value. Its current
                // imported local pose is the best available neutral baseline.
                reconciled[index] = new DazPoseRestTransform
                {
                    path = path,
                    localPosition = transform.localPosition,
                    localRotation = transform.localRotation,
                    localScale = transform.localScale
                };
            }

            return reconciled;
        }

        private static string NormalizeIndexedTransformPath(string path)
        {
            return Regex.Replace(path ?? string.Empty, @"\[\d+\](?=/|$)", string.Empty);
        }

        private static void CaptureRestBlendShapes(Transform root, DazPoseCharacterState state)
        {
            state.blendShapes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .Select(renderer =>
                {
                    var components = renderer.transform.GetComponents<SkinnedMeshRenderer>();
                    var componentIndex = Array.IndexOf(components, renderer);
                    var shapeCount = renderer.sharedMesh.blendShapeCount;
                    var weights = new float[shapeCount];
                    var shapeNames = new string[shapeCount];
                    for (var index = 0; index < shapeCount; index++)
                    {
                        weights[index] = renderer.GetBlendShapeWeight(index);
                        shapeNames[index] = renderer.sharedMesh.GetBlendShapeName(index);
                    }
                    return new DazPoseRestBlendShape
                    {
                        rendererPath = DazPoseTransformPath.Get(root, renderer.transform),
                        rendererComponentIndex = componentIndex,
                        blendShapeCount = shapeCount,
                        blendShapeNames = shapeNames,
                        weights = weights
                    };
                }).ToArray();
            state.hasCapturedBlendShapes = true;
        }

        private static bool RestBlendShapeStructureMatches(Transform root, DazPoseCharacterState state)
        {
            var saved = state.blendShapes ?? Array.Empty<DazPoseRestBlendShape>();
            var current = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMesh != null).ToArray();
            if (saved.Length != current.Length) return false;
            for (var index = 0; index < current.Length; index++)
            {
                var renderer = current[index];
                var savedItem = saved.FirstOrDefault(item => item != null
                    && item.rendererPath == DazPoseTransformPath.Get(root, renderer.transform)
                    && item.rendererComponentIndex == Array.IndexOf(renderer.transform.GetComponents<SkinnedMeshRenderer>(), renderer));
                if (savedItem == null || savedItem.blendShapeCount != renderer.sharedMesh.blendShapeCount
                    || savedItem.blendShapeNames == null || savedItem.blendShapeNames.Length != renderer.sharedMesh.blendShapeCount)
                    return false;
                for (var shapeIndex = 0; shapeIndex < renderer.sharedMesh.blendShapeCount; shapeIndex++)
                    if (!string.Equals(savedItem.blendShapeNames[shapeIndex], renderer.sharedMesh.GetBlendShapeName(shapeIndex), StringComparison.Ordinal))
                        return false;
            }
            return true;
        }

        internal static void RestoreSnapshot(Transform root, DazPoseCharacterState state)
        {
            if (root == null || state == null) return;
            var byPath = state.transforms.ToDictionary(item => item.path, StringComparer.Ordinal);
            foreach (var item in root.GetComponentsInChildren<Transform>(true).OrderBy(value => DazPoseTransformPath.DepthFrom(root, value)))
            {
                if (!byPath.TryGetValue(DazPoseTransformPath.Get(root, item), out var snapshot)) continue;
                item.localPosition = snapshot.localPosition;
                item.localRotation = snapshot.localRotation;
                item.localScale = snapshot.localScale;
            }

            var transformByPath = root.GetComponentsInChildren<Transform>(true)
                .ToDictionary(item => DazPoseTransformPath.Get(root, item), StringComparer.Ordinal);
            foreach (var snapshot in state.blendShapes ?? Array.Empty<DazPoseRestBlendShape>())
            {
                if (snapshot == null || !transformByPath.TryGetValue(snapshot.rendererPath, out var rendererTransform)) continue;
                var renderers = rendererTransform.GetComponents<SkinnedMeshRenderer>();
                if (snapshot.rendererComponentIndex < 0 || snapshot.rendererComponentIndex >= renderers.Length) continue;
                var renderer = renderers[snapshot.rendererComponentIndex];
                if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount != snapshot.blendShapeCount
                    || snapshot.weights == null || snapshot.weights.Length != snapshot.blendShapeCount
                    || snapshot.blendShapeNames == null || snapshot.blendShapeNames.Length != snapshot.blendShapeCount) continue;
                var namesMatch = true;
                for (var shapeIndex = 0; shapeIndex < snapshot.blendShapeCount; shapeIndex++)
                    if (!string.Equals(snapshot.blendShapeNames[shapeIndex], renderer.sharedMesh.GetBlendShapeName(shapeIndex), StringComparison.Ordinal))
                    {
                        namesMatch = false;
                        break;
                    }
                if (!namesMatch) continue;
                for (var index = 0; index < snapshot.weights.Length; index++) renderer.SetBlendShapeWeight(index, snapshot.weights[index]);
            }
        }

    }
}