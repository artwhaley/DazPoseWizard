using System;
using System.Linq;
using DazPose.AnimationAudit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
namespace DazPose.Editor.AnimationAudit
{
    /// <summary>Scene-free checks for the installed animation catalog, rig and playback controllers.</summary>
    public static class AnimationAuditValidation
    {
        public static void RunSetupAndValidation() { ValidateGeneratedAssets(); LaraHumanoidKawaiiTestSetup.ValidateSources(); }
        [MenuItem("DazPose/Diagnostics/Validate Animation Library Assets")]
        public static void ValidateGeneratedAssets()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AnimationAuditCatalog>(AnimationAuditCatalogBuilder.CatalogPath);
            Require(catalog != null && catalog.entries != null && catalog.entries.Count > 0, "Catalog contains installed clips.");
            Require(catalog.entries.All(e => e != null && e.clip != null), "Catalog clip references exist.");
            ValidateController(catalog, AnimationAuditSceneSetup.ControllerPath);
            ValidateController(catalog, AnimationAuditSceneSetup.FootIKControllerPath);
            LaraHumanoidKawaiiTestSetup.ValidateSources();
            Debug.Log("Animation audit asset diagnostics passed. Clip appearance, retargeting and root motion remain preview/manual observations.");
        }
        private static void ValidateController(AnimationAuditCatalog catalog, string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Require(controller != null, "Playback controller exists: " + path);
            Require(controller.layers.Length == 1, "Playback uses one stock Animator layer: " + path);
            var states = controller.layers[0].stateMachine.states;
            Require(states.All(c => c.state != null && c.state.motion is AnimationClip), "States point directly to animation clips: " + path);
            int expected = catalog.entries.Count(e => !string.IsNullOrEmpty(e.animatorStateName));
            Require(states.Length == expected, "Controller state count matches playable catalog entries: " + path);
            Require(states.All(c => catalog.entries.Any(e => e.clip == c.state.motion)), "Every state clip is represented by the catalog: " + path);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Animation audit: " + message); }
    }
}
