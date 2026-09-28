using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    [Serializable]
    public sealed class DazPoseClipBindingEntry
    {
        public string path;
        public string property;
        public string componentType;
        public bool objectReference;
    }

    [Serializable]
    public sealed class DazPoseClipBindingComparisonReport
    {
        public string generatedAtUtc;
        public string clipAName;
        public string clipAAssetPath;
        public string clipBName;
        public string clipBAssetPath;
        public int bindingsInA;
        public int bindingsInB;
        public int bindingsInBothCount;
        public int bindingsOnlyACount;
        public int bindingsOnlyBCount;
        public DazPoseClipBindingEntry[] bindingsInBoth;
        public DazPoseClipBindingEntry[] bindingsOnlyA;
        public DazPoseClipBindingEntry[] bindingsOnlyB;
    }

    public static class DazPoseClipBindingComparison
    {
        [MenuItem("Tools/DAZ Pose/Compare AnimationClip Bindings A vs B")]
        public static void CompareSelectedPoseClips()
        {
            var startFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                DazPoseAnimationClipGenerator.OutputFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(startFolder)) startFolder = Application.dataPath;

            var firstPath = EditorUtility.OpenFilePanel("Select Pose A AnimationClip", startFolder, "anim");
            if (string.IsNullOrEmpty(firstPath)) return;
            var first = DazPoseAnimationClipGenerator.LoadClipFromAbsolutePath(firstPath);
            var secondPath = EditorUtility.OpenFilePanel("Select Pose B AnimationClip", startFolder, "anim");
            if (string.IsNullOrEmpty(secondPath)) return;
            var second = DazPoseAnimationClipGenerator.LoadClipFromAbsolutePath(secondPath);
            if (first == null || second == null)
                throw new InvalidOperationException("Select two generated .anim assets inside this Unity project's Assets folder.");

            var firstBindings = GetBindings(first);
            var secondBindings = GetBindings(second);
            var firstByKey = firstBindings.ToDictionary(Key, StringComparer.Ordinal);
            var secondByKey = secondBindings.ToDictionary(Key, StringComparer.Ordinal);
            var sharedKeys = firstByKey.Keys.Intersect(secondByKey.Keys, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var onlyFirstKeys = firstByKey.Keys.Except(secondByKey.Keys, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var onlySecondKeys = secondByKey.Keys.Except(firstByKey.Keys, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var report = new DazPoseClipBindingComparisonReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                clipAName = first.name,
                clipAAssetPath = AssetDatabase.GetAssetPath(first),
                clipBName = second.name,
                clipBAssetPath = AssetDatabase.GetAssetPath(second),
                bindingsInA = firstBindings.Length,
                bindingsInB = secondBindings.Length,
                bindingsInBothCount = sharedKeys.Length,
                bindingsOnlyACount = onlyFirstKeys.Length,
                bindingsOnlyBCount = onlySecondKeys.Length,
                bindingsInBoth = sharedKeys.Select(key => firstByKey[key]).ToArray(),
                bindingsOnlyA = onlyFirstKeys.Select(key => firstByKey[key]).ToArray(),
                bindingsOnlyB = onlySecondKeys.Select(key => secondByKey[key]).ToArray()
            };

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var reportPath = Path.Combine(projectRoot, "TestOutput", "pose-binding-comparison.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log("DAZ Pose binding comparison: " + first.name + " (" + report.bindingsInA + ") vs "
                + second.name + " (" + report.bindingsInB + ") | shared " + report.bindingsInBothCount
                + " | only A " + report.bindingsOnlyACount + " | only B " + report.bindingsOnlyBCount
                + " | exact paths and properties: " + reportPath);
        }

        private static DazPoseClipBindingEntry[] GetBindings(AnimationClip clip)
        {
            var floatBindings = AnimationUtility.GetCurveBindings(clip).Select(binding => new DazPoseClipBindingEntry
            {
                path = binding.path,
                property = binding.propertyName,
                componentType = binding.type == null ? string.Empty : binding.type.FullName,
                objectReference = false
            });
            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip).Select(binding => new DazPoseClipBindingEntry
            {
                path = binding.path,
                property = binding.propertyName,
                componentType = binding.type == null ? string.Empty : binding.type.FullName,
                objectReference = true
            });
            return floatBindings.Concat(objectBindings).OrderBy(item => item.path, StringComparer.Ordinal)
                .ThenBy(item => item.property, StringComparer.Ordinal).ThenBy(item => item.objectReference).ToArray();
        }

        private static string Key(DazPoseClipBindingEntry binding)
            => (binding.objectReference ? "object" : "float") + "|" + binding.componentType + "|" + binding.path + "|" + binding.property;
    }
}
