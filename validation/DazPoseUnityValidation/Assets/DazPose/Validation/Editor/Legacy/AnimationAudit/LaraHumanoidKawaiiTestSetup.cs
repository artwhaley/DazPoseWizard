using System;
using System.Linq;
using DazPose.AnimationAudit;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor.AnimationAudit
{
    /// <summary>Read-only checks of known Humanoid source assets; no scene or importer mutation.</summary>
    public static class LaraHumanoidKawaiiTestSetup
    {
        public const string ModelPath = AnimationAuditSceneSetup.HumanoidModelPath;
        public const string MotionPath = AnimationAuditSceneSetup.BreathingMotionPath;
        public static void ValidateSources()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Model is missing: " + ModelPath);
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Human)
                throw new InvalidOperationException("Model must already use Rig > Animation Type: Humanoid. This check does not change importer settings.");
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("Model has no valid Human Avatar. Inspect Rig > Configure; this check does not rewrite or reimport the model.");
            if (model.GetComponentsInChildren<Animator>(true).Length > 1)
                throw new InvalidOperationException("The source model has multiple Animators; preview expects one rig.");
            var motions = AssetDatabase.LoadAllAssetsAtPath(MotionPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase) && c.length > 0f).ToArray();
            if (motions.Length != 1) throw new InvalidOperationException("Expected one imported non-preview animation in " + MotionPath + "; found " + motions.Length + ".");
            if (!motions[0].isHumanMotion) throw new InvalidOperationException("The breathing idle is not imported as a Humanoid motion: " + MotionPath);
            Debug.Log("Animation sources passed: valid Human Avatar at " + ModelPath + "; one non-preview Humanoid clip at " + MotionPath + ".");
        }
        public static void OpenPreview()
        {
            ValidateSources();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var clip = AssetDatabase.LoadAllAssetsAtPath(MotionPath).OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase) && c.length > 0f);
            AnimationContentPreviewWindow.Open(model, clip);
        }
    }
}
