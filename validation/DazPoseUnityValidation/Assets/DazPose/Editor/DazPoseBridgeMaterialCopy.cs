using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>
    /// Applies the known-good DAZ-to-Unity bridge materials to an imported Lara
    /// scene instance. This writes a normal scene/prefab-instance override and
    /// deliberately leaves the FBX importer and its embedded materials alone.
    /// </summary>
    internal static class DazPoseBridgeMaterialCopy
    {
        private const string BridgePrefabPath = "Assets/Daz3D/larabridge/Prefabs/larabridge_Prefab.prefab";
        private const string BridgeRendererName = "Genesis8Female.Shape";
        private const string MenuPath = "Tools/DAZ Pose/Copy Bridge Materials to Selected Lara";

        [MenuItem(MenuPath, false, 20)]
        private static void CopyBridgeMaterialsToSelectedLara()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogError("Select Lara in the scene before copying the bridge materials.");
                return;
            }

            if (EditorUtility.IsPersistent(selected) || selected.scene.IsValid() == false)
            {
                Debug.LogError("Select the Lara scene instance, not an asset in the Project window.");
                return;
            }

            if (!TryGetBridgeMaterials(out var bridgeMaterials, out var bridgeError))
            {
                Debug.LogError(bridgeError);
                return;
            }

            var renderers = selected.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .ToArray();

            var candidates = renderers
                .Where(renderer => renderer.sharedMesh.name == BridgeRendererName || renderer.name == BridgeRendererName)
                .ToArray();

            // A future DAZ export may rename the mesh object. In that case, use
            // the slot names as the identity check instead of assigning by index.
            if (candidates.Length == 0)
            {
                candidates = renderers
                    .Where(renderer => HasExactBridgeSlots(renderer, bridgeMaterials))
                    .ToArray();
            }

            if (candidates.Length != 1)
            {
                var reason = candidates.Length == 0
                    ? "No Genesis8Female.Shape renderer with the bridge material slots was found beneath the selected object."
                    : $"Found {candidates.Length} matching renderers beneath the selected object; select Lara's character root or mesh object so the target is unambiguous.";
                Debug.LogError($"Could not copy bridge materials to '{selected.name}'. {reason}", selected);
                return;
            }

            var target = candidates[0];
            if (!TryMapMaterialsBySlotName(target, bridgeMaterials, out var mappedMaterials, out var mappingError))
            {
                Debug.LogError($"Could not copy bridge materials to '{target.name}': {mappingError}", target);
                return;
            }

            Undo.RecordObject(target, "Copy DAZ Bridge Materials to Lara");
            target.sharedMaterials = mappedMaterials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);

            Debug.Log($"Copied {mappedMaterials.Length} DAZ bridge materials to '{target.name}'. " +
                      "This is an undoable scene override; Lara's FBX importer was not changed.", target);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateCopyBridgeMaterialsToSelectedLara()
        {
            var selected = Selection.activeGameObject;
            return selected != null
                   && !EditorUtility.IsPersistent(selected)
                   && selected.scene.IsValid()
                   && selected.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
        }

        private static bool TryGetBridgeMaterials(out Dictionary<string, Material> materialsByName, out string error)
        {
            materialsByName = null;
            error = null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePrefabPath);
            if (prefab == null)
            {
                error = $"The DAZ bridge prefab was not found at '{BridgePrefabPath}'.";
                return false;
            }

            var sourceRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.name == BridgeRendererName)
                .ToArray();
            if (sourceRenderers.Length != 1)
            {
                error = $"Expected one '{BridgeRendererName}' skinned mesh renderer in '{BridgePrefabPath}', found {sourceRenderers.Length}.";
                return false;
            }

            var sourceMaterials = sourceRenderers[0].sharedMaterials;
            if (sourceMaterials == null || sourceMaterials.Length == 0 || sourceMaterials.Any(material => material == null))
            {
                error = $"The bridge renderer in '{BridgePrefabPath}' does not have a complete material list.";
                return false;
            }

            materialsByName = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var material in sourceMaterials)
            {
                if (materialsByName.ContainsKey(material.name))
                {
                    error = $"The bridge prefab has duplicate material slot name '{material.name}', so slots cannot be mapped safely.";
                    materialsByName = null;
                    return false;
                }

                materialsByName.Add(material.name, material);
            }

            return true;
        }

        private static bool HasExactBridgeSlots(SkinnedMeshRenderer renderer, IReadOnlyDictionary<string, Material> bridgeMaterials)
        {
            var currentMaterials = renderer.sharedMaterials;
            return currentMaterials != null
                   && currentMaterials.Length == bridgeMaterials.Count
                   && currentMaterials.All(material => material != null && bridgeMaterials.ContainsKey(material.name));
        }

        private static bool TryMapMaterialsBySlotName(
            SkinnedMeshRenderer target,
            IReadOnlyDictionary<string, Material> bridgeMaterials,
            out Material[] mappedMaterials,
            out string error)
        {
            var currentMaterials = target.sharedMaterials;
            mappedMaterials = null;
            error = null;

            if (currentMaterials == null || currentMaterials.Length != bridgeMaterials.Count)
            {
                error = $"Lara has {currentMaterials?.Length ?? 0} material slots, while the bridge has {bridgeMaterials.Count}. No materials were changed.";
                return false;
            }

            mappedMaterials = new Material[currentMaterials.Length];
            var missingNames = new List<string>();
            for (var index = 0; index < currentMaterials.Length; index++)
            {
                var current = currentMaterials[index];
                if (current == null || !bridgeMaterials.TryGetValue(current.name, out mappedMaterials[index]))
                {
                    missingNames.Add(current == null ? $"slot {index + 1} (empty)" : current.name);
                }
            }

            if (missingNames.Count == 0)
            {
                return true;
            }

            mappedMaterials = null;
            error = "Lara's current material slots could not be matched by name: " + string.Join(", ", missingNames) + ". No materials were changed.";
            return false;
        }
    }
}
