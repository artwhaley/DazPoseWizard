using System;
using System.Linq;
using DazPose.AnimationAudit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.AnimationAudit
{
    // Optional existing diagnostic entry point. Scene preparation does not run it.
    public static class AnimationAuditValidation
    {
        public static void RunSetupAndValidation()
        {
            AnimationAuditSceneSetup.CreateScene();
            ValidateGeneratedAssets();
        }

        public static void ValidateGeneratedAssets()
        {
            AnimationAuditCatalog catalog = AssetDatabase.LoadAssetAtPath<AnimationAuditCatalog>(AnimationAuditCatalogBuilder.CatalogPath);
            Require(catalog != null && catalog.entries.Count > 0, "Catalog contains installed clips.");
            Require(catalog.entries.All(entry => entry != null && entry.clip != null), "Catalog clip references exist.");
            Scene scene = SceneManager.GetSceneByPath(AnimationAuditSceneSetup.ScenePath);
            bool loadedHere = !scene.IsValid() || !scene.isLoaded;
            if (loadedHere) scene = EditorSceneManager.OpenScene(AnimationAuditSceneSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                AnimationAuditHarness harness = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AnimationAuditHarness>(true)).FirstOrDefault();
                Require(harness != null && harness.catalog == catalog, "Single-actor browser uses the catalog.");
                Require(harness.animator != null, "Lara Animator exists.");
                Avatar avatar = harness.animator.avatar;
                Require(avatar != null && avatar.isHuman && avatar.isValid, "Lara uses a valid imported Human Avatar.");
                AnimatorController controller = harness.animator.runtimeAnimatorController as AnimatorController;
                Require(controller != null && controller.layers.Length == 1, "Playback uses one stock Animator layer.");
                Require(controller.layers[0].stateMachine.states.All(child => child.state.motion is AnimationClip), "States point directly to animation clips.");
                Require(controller.layers[0].stateMachine.states.Length == catalog.entries.Count(entry => !string.IsNullOrEmpty(entry.animatorStateName)), "Each playable catalog entry has a state.");
                int actorCount = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Animator>(true).Length);
                Require(actorCount == 1, "The scene contains exactly one character Animator.");
                Debug.Log("Single-Lara audit asset diagnostics passed. Visual animation and root-motion evaluation remains manual.");
            }
            finally { if (loadedHere) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Animation audit: " + message);
        }
    }
}
