using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;

namespace DazPose.Editor.Diagnostics
{
    /// <summary>Actual gesture/action rejection regressions on uniquely owned copies; no saved test scene.</summary>
    public static class ContentContractDiagnostics
    {
        [Serializable] public sealed class Assertion
        {
            public string name, expected, actual;
            public bool passed;
        }
        private sealed class Report { public readonly List<Assertion> assertions = new List<Assertion>(); }

        public static Assertion[] Validate(PerformerGesture gesture, PerformerAction action)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run content checks in Edit Mode.");
            if (gesture == null || action == null || gesture.Clip == null || action.BodyClip == null)
                throw new ArgumentException("Choose an existing ready gesture and action; originals are never modified.");
            string folder = "Assets/__DazPoseContentChecks_" + Guid.NewGuid().ToString("N");
            if (AssetDatabase.IsValidFolder(folder))
                throw new IOException("Unique fixture already exists.");
            string guid = AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            if (string.IsNullOrEmpty(guid)) throw new IOException("Could not create owned fixture folder.");
            var report = new Report();
            try
            {
                RunR09NegativeAssetChecks(report, gesture.Clip, action.BodyClip, gesture, action, folder);
                if (report.assertions.Any(a => !a.passed))
                    throw new InvalidDataException(string.Join("\\n", report.assertions.Where(a => !a.passed).Select(a => a.name + ": " + a.actual)));
                return report.assertions.ToArray();
            }
            finally
            {
                if (AssetDatabase.AssetPathToGUID(folder) != guid)
                    throw new IOException("Fixture ownership changed; refusing cleanup.");
                AssetDatabase.DeleteAsset(folder);
            }
        }
        private static AnimationCurve CloneCurve(AnimationCurve source) => source == null ? null : new AnimationCurve(source.keys)
        { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
        private static void AssertR09(Report report, string name, string expected, bool passed, string actual) =>
            report.assertions.Add(new Assertion { name = name, expected = expected, passed = passed, actual = actual });

        private static void RunR09NegativeAssetChecks(Report report, AnimationClip sourceGestureClip,
            AnimationClip sourceActionClip, PerformerGesture sourceGesture, PerformerAction sourceAction, string folder)
        {
            CheckGestureNegative(report, "legacyGestureRejected", "Legacy", sourceGestureClip, sourceGesture, folder, clip => clip.legacy = true);
            CheckGestureNegative(report, "loopingGestureRejected", "looping", sourceGestureClip, sourceGesture, folder, clip =>
            {
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.wrapMode = WrapMode.Loop;
            });
            CheckGestureNegative(report, "additiveReferencePoseRejected", "reference pose", sourceGestureClip, sourceGesture, folder, clip =>
            {
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.hasAdditiveReferencePose = false;
                settings.additiveReferencePoseClip = null;
                settings.additiveReferencePoseTime = 0f;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            });
            CheckGestureNegative(report, "emptyGestureRejected", "nonempty", sourceGestureClip, sourceGesture, folder, clip =>
            {
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip)) AnimationUtility.SetEditorCurve(clip, binding, null);
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            });
            CheckGestureNegative(report, "nonfiniteGestureKeyRejected", "non-finite", sourceGestureClip, sourceGesture, folder, clip =>
            {
                string path = AssetDatabase.GetAssetPath(clip);
                string yaml = File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path));
                Match key = Regex.Match(yaml, @"(?m)^(\s*value:\s*)([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)(\s*)$");
                if (!key.Success) throw new InvalidOperationException("Could not identify a serialized numeric animation key to corrupt.");
                yaml = yaml.Substring(0, key.Index) + key.Groups[1].Value + ".nan" + key.Groups[3].Value + yaml.Substring(key.Index + key.Length);
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            });
            CheckGestureNegative(report, "nonskeletalGestureCurveRejected", "non-skeletal", sourceGestureClip, sourceGesture, folder, clip =>
            {
                var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(clip.length, 1f));
                string path = AssetDatabase.GetAssetPath(clip);
                string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
                string yaml = File.ReadAllText(absolutePath);
                Match pathMatch = Regex.Match(yaml, @"(?m)^    path: (Genesis8Female/hip[^\r\n]*)$");
                if (!pathMatch.Success) throw new InvalidOperationException("Could not find the prepared skeletal path for nonskeletal curve injection. First path: " + string.Join(" | ", yaml.Split('\n').Where(line => line.Contains("path: Genesis")).Take(2)));
                string body = "  m_FloatCurves:\n  - curve:\n      m_Curve:\n      - serializedVersion: 3\n        time: 0\n        value: 0\n      - serializedVersion: 3\n        time: 1\n        value: 1\n    attribute: m_LocalEulerAnglesRaw.x\n    path: " + pathMatch.Groups[1].Value + "\n    classID: 4\n    script: {fileID: 0}\n    flags: 0\n";
                yaml = yaml.Replace("  m_FloatCurves: []", body)
                    .Replace("    m_HasAdditiveReferencePose: 1", "    m_HasAdditiveReferencePose: 1");
                File.WriteAllText(absolutePath, yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssertR09(report, "nonskeletalCurveFixtureCreated", "nonskeletal curve serialized in fixture", File.ReadAllText(absolutePath).Contains("attribute: m_LocalEulerAnglesRaw.x"), "curve serialized");
            });
            CheckGestureNegative(report, "gestureEventRejected", "object-reference curves or animation events", sourceGestureClip, sourceGesture, folder, clip =>
                AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = 0f, functionName = "R09InvalidEvent" } }));
            CheckGestureNegative(report, "gestureObjectReferenceCurveRejected", "object-reference curves or animation events", sourceGestureClip, sourceGesture, folder, clip =>
            {
                var binding = EditorCurveBinding.PPtrCurve("Genesis8Female/hip/abdomenLower/abdomenUpper/chestLower/chestUpper/rCollar/rShldrBend", typeof(GameObject), "m_IsActive");
                AnimationUtility.SetObjectReferenceCurve(clip, binding,
                    new[] { new ObjectReferenceKeyframe { time = 0f, value = null }, new ObjectReferenceKeyframe { time = clip.length, value = null } });
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
                int curveCount = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
                if (curveCount == 0)
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
                    string yaml = File.ReadAllText(absolutePath);
                    string[] parts = yaml.Split(new[] { "  m_SampleRate:" }, 2, StringSplitOptions.None);
                    if (parts.Length != 2) throw new InvalidOperationException("Could not locate AnimationClip object curve serialization section.");
                    parts[0] = parts[0].Replace("  m_PPtrCurves: []", "  m_PPtrCurves:\n  - curve:\n      m_Curve:\n      - time: 0\n        value: {fileID: 0}\n      - time: 1.4\n        value: {fileID: 0}\n    attribute: m_IsActive\n    path: Genesis8Female/hip/abdomenLower/abdomenUpper/chestLower/chestUpper/rCollar/rShldrBend\n    classID: 1\n    script: {fileID: 0}");
                    parts[0] = parts[0].Replace("    pptrCurveMapping: []", "    pptrCurveMapping:\n    - {fileID: 0}\n    - {fileID: 0}");
                    File.WriteAllText(absolutePath, parts[0] + "  m_SampleRate:" + parts[1]);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    curveCount = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
                }
                AssertR09(report, "objectReferenceCurveFixtureCreated", "object-reference curve appears", curveCount > 0, "count=" + curveCount);
            });
            CheckActionNegative(report, "legacyActionClipRejected", sourceActionClip, sourceAction, folder, clip => clip.legacy = true,
                action => !action.IsReady(out string reason) && reason.IndexOf("non-Legacy", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "loopingActionClipRejected", sourceActionClip, sourceAction, folder, clip =>
            {
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.wrapMode = WrapMode.Loop;
            }, action => !action.IsReady(out string reason) && reason.IndexOf("nonempty, non-looping", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "emptyActionClipRejected", sourceActionClip, sourceAction, folder, clip =>
            {
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip)) AnimationUtility.SetEditorCurve(clip, binding, null);
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            }, action => !action.IsReady(out string reason) && reason.IndexOf("nonempty", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "nonfiniteActionClipKeyRejected", sourceActionClip, sourceAction, folder, clip =>
            {
                string path = AssetDatabase.GetAssetPath(clip);
                string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
                string yaml = File.ReadAllText(absolutePath);
                Match key = Regex.Match(yaml, @"(?m)^(\s*value:\s*)([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)(\s*)$");
                if (!key.Success) throw new InvalidOperationException("Could not identify a serialized numeric Action animation key to corrupt.");
                yaml = yaml.Substring(0, key.Index) + key.Groups[1].Value + ".nan" + key.Groups[3].Value + yaml.Substring(key.Index + key.Length);
                File.WriteAllText(absolutePath, yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }, action => !action.IsReady(out string reason) && reason.IndexOf("non-finite", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "nonskeletalActionCurveRejected", sourceActionClip, sourceAction, folder, clip =>
            {
                string path = AssetDatabase.GetAssetPath(clip);
                string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
                string yaml = File.ReadAllText(absolutePath);
                Match bodyPath = Regex.Match(yaml, @"(?m)^    path: ([^\r\n]+)$");
                if (!bodyPath.Success) throw new InvalidOperationException("Could not locate a body clip path for nonskeletal Action curve injection.");
                string body = "  m_FloatCurves:\n  - curve:\n      m_Curve:\n      - serializedVersion: 3\n        time: 0\n        value: 0\n      - serializedVersion: 3\n        time: 1\n        value: 1\n    attribute: m_LocalEulerAnglesRaw.x\n    path: " + bodyPath.Groups[1].Value + "\n    classID: 4\n    script: {fileID: 0}\n    flags: 0\n";
                yaml = yaml.Replace("  m_FloatCurves: []", body);
                File.WriteAllText(absolutePath, yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }, action => !action.IsReady(out string reason) && reason.IndexOf("root or non-skeletal", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "actionEventRejected", sourceActionClip, sourceAction, folder, clip =>
                AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = 0f, functionName = "R09InvalidEvent" } }),
                action => !action.IsReady(out string reason) && reason.IndexOf("events", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "actionObjectReferenceCurveRejected", sourceActionClip, sourceAction, folder, clip =>
            {
                var binding = EditorCurveBinding.PPtrCurve("Genesis8Female", typeof(GameObject), "m_IsActive");
                AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] { new ObjectReferenceKeyframe { time = 0f, value = null }, new ObjectReferenceKeyframe { time = clip.length, value = null } });
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
                int count = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
                if (count == 0)
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
                    string yaml = File.ReadAllText(absolutePath);
                    string[] parts = yaml.Split(new[] { "  m_SampleRate:" }, 2, StringSplitOptions.None);
                    if (parts.Length != 2) throw new InvalidOperationException("Could not locate serialized Action object curve metadata.");
                    parts[0] = parts[0].Replace("  m_PPtrCurves: []", "  m_PPtrCurves:\n  - curve:\n      m_Curve:\n      - time: 0\n        value: {fileID: 0}\n      - time: 1\n        value: {fileID: 0}\n    attribute: m_IsActive\n    path: Genesis8Female\n    classID: 1\n    script: {fileID: 0}");
                    parts[0] = parts[0].Replace("    pptrCurveMapping: []", "    pptrCurveMapping:\n    - {fileID: 0}\n    - {fileID: 0}");
                    File.WriteAllText(absolutePath, parts[0] + "  m_SampleRate:" + parts[1]);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    count = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
                }
                AssertR09(report, "actionObjectReferenceCurveFixtureCreated", "object-reference curve appears", count > 0, "count=" + count);
            }, action => !action.IsReady(out string reason) && reason.IndexOf("object references or events", StringComparison.OrdinalIgnoreCase) >= 0);

            PerformerAction invalidTrajectory = ScriptableObject.CreateInstance<PerformerAction>();
            invalidTrajectory.name = "R09 invalid root trajectory";
            AnimationCurve invalidY = CloneCurve(sourceAction.RootY);
            Keyframe[] yKeys = invalidY.keys;
            yKeys[yKeys.Length - 1].value = 1f;
            invalidY.keys = yKeys;
            invalidTrajectory.ConfigureInEditor(sourceActionClip, CloneCurve(sourceAction.RootX), invalidY,
                    CloneCurve(sourceAction.RootZ), CloneCurve(sourceAction.RootYaw), sourceAction.DurationSeconds,
                    sourceAction.SourceAssetPath, sourceAction.BakeNotes, sourceAction.BlendInSeconds, sourceAction.BlendOutSeconds);
            bool actionRejected = !invalidTrajectory.IsReady(out string actionReason)
                && actionReason.IndexOf("ends", StringComparison.OrdinalIgnoreCase) >= 0;
            AssertR09(report, "invalidActionRootTrajectoryRejected", "trajectory endpoint rejected", actionRejected, actionRejected ? actionReason : "unexpectedly accepted: " + actionReason);
            UnityEngine.Object.DestroyImmediate(invalidTrajectory);
        }

        private static EditorCurveBinding AnimationCurveBindingForFirstSkeletal(AnimationClip clip) =>
            AnimationUtility.GetCurveBindings(clip).FirstOrDefault(binding => binding.type == typeof(Transform)
                && binding.propertyName == "m_LocalPosition.x");

        private static void CheckActionNegative(Report report, string assertionName, AnimationClip sourceClip,
            PerformerAction sourceAction, string folder, Action<AnimationClip> corrupt,
            Func<PerformerAction, bool> rejectsForCause)
        {
            string path = folder + "/" + assertionName + ".anim";
            PerformerAction probe = null;
            try
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceClip), path))
                    throw new IOException("Could not create isolated negative clip copy " + path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) throw new InvalidOperationException("Negative clip copy did not import: " + path);
                corrupt(clip);
                probe = ScriptableObject.CreateInstance<PerformerAction>();
                probe.ConfigureInEditor(clip, CloneCurve(sourceAction.RootX), CloneCurve(sourceAction.RootY),
                    CloneCurve(sourceAction.RootZ), CloneCurve(sourceAction.RootYaw), sourceAction.DurationSeconds,
                    sourceAction.SourceAssetPath, sourceAction.BakeNotes, sourceAction.BlendInSeconds, sourceAction.BlendOutSeconds);
                bool rejected = rejectsForCause(probe);
                probe.IsReady(out string reason);
                AssertR09(report, assertionName, "rejected for source clip defect", rejected, rejected ? reason : "unexpectedly accepted or wrong reason: " + reason);
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.DeleteAsset(path + ".meta");
            }
        }

        private static void CheckGestureNegative(Report report, string assertionName, string expectedReason,
            AnimationClip sourceClip, PerformerGesture sourceGesture, string folder, Action<AnimationClip> corrupt)
        {
            string path = folder + "/" + assertionName + ".anim";
            PerformerGesture probe = null;
            try
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceClip), path))
                    throw new IOException("Could not create isolated negative clip copy " + path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) throw new InvalidOperationException("Negative clip copy did not import: " + path);
                corrupt(clip);
                probe = ScriptableObject.CreateInstance<PerformerGesture>();
                probe.ConfigureInEditor(clip, sourceGesture.BlendInSeconds, sourceGesture.BlendOutSeconds);
                bool ready = probe.IsReady(out string reason);
                bool rejected = !ready && reason.IndexOf(expectedReason, StringComparison.OrdinalIgnoreCase) >= 0;
                string sampledCurve = AnimationUtility.GetEditorCurve(clip, AnimationCurveBindingForFirstSkeletal(clip))?.keys[0].value.ToString() ?? "none";
                if (!rejected && !ready && assertionName == "nonskeletalGestureCurveRejected")
                {
                    AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.hasAdditiveReferencePose = true;
                    settings.additiveReferencePoseClip = null;
                    settings.additiveReferencePoseTime = 0f;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    ready = probe.IsReady(out reason);
                    rejected = !ready && reason.IndexOf(expectedReason, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                AssertR09(report, assertionName, "rejected for " + expectedReason, rejected,
                    rejected ? reason : "ready=" + ready + "; reason=" + reason + "; firstSkeletalKey=" + sampledCurve);
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.DeleteAsset(path + ".meta");
            }
        }

    }
}
