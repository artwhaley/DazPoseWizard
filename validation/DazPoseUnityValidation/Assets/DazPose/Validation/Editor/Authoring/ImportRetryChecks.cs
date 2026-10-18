using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Explicit, isolated regression check for the browser-import retry guard. No menu or startup hook.</summary>
    public static class ImportRetryChecks
    {
        [Serializable]
        private sealed class CheckResult
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        private sealed class Report
        {
            public string startedUtc;
            public string completedUtc;
            public bool passed;
            public bool successfulOutputSampleAvailable;
            public CheckResult[] checks;
        }

        public static void Run()
        {
            var started = DateTime.UtcNow;
            var checks = new List<CheckResult>();
            var report = new Report { startedUtc = started.ToString("O"), checks = Array.Empty<CheckResult>() };
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var fixtureRoot = Path.Combine(projectRoot, ".dazposewizard", "cleanup-task1", "import-retry-" + Guid.NewGuid().ToString("N"));
            var fixturePath = Path.Combine(fixtureRoot, "synthetic.dazpose.json");
            var reportPath = Path.Combine(projectRoot, "TestOutput", "CleanupTask1", "import-retry.json");
            string fixtureOutputFolder = null;
            bool fixtureOutputCreated = false;

            try
            {
                Directory.CreateDirectory(fixtureRoot);
                File.WriteAllText(fixturePath, "synthetic import retry fixture v1", new UTF8Encoding(false));

                var queueType = FindType("DazPose.Editor.Importing.DazPoseBrowserImportQueue");
                var statusType = FindType("DazPose.Editor.Importing.DazPoseBrowserJobStatus");
                var tryLoadBridge = queueType.GetMethod("TryLoadBridge", BindingFlags.Static | BindingFlags.NonPublic);
                var bridgeArgs = new object[] { null, false };
                if (tryLoadBridge == null || !(bool)tryLoadBridge.Invoke(null, bridgeArgs))
                    throw new InvalidOperationException("The existing DazPoseWizard.project.json bridge configuration was not available.");

                var validateBridge = queueType.GetMethod("ValidateBridge", BindingFlags.Static | BindingFlags.NonPublic);
                var roots = new object[] { bridgeArgs[0], null, null, null, null };
                validateBridge.Invoke(null, roots);
                var poseImportRoot = (string)roots[1];
                var poseOutputRoot = (string)roots[2];
                var fixtureAssetPath = poseImportRoot + "/__cleanup_task1_fixture__/synthetic.dazpose.json";

                var contentHashMethod = queueType.GetMethod("ContentHash", BindingFlags.Static | BindingFlags.NonPublic);
                var needsProcessing = queueType.GetMethod("NeedsProcessing", BindingFlags.Static | BindingFlags.NonPublic);
                if (contentHashMethod == null || needsProcessing == null)
                    throw new MissingMethodException("The import queue guard or its content hash helper was not found.");
                string hash = (string)contentHashMethod.Invoke(null, new object[] { fixturePath });
                object status = Activator.CreateInstance(statusType, true);
                Set(status, "state", "Failed");
                Set(status, "canonicalContentHash", hash);

                bool unchangedFirst = Evaluate(needsProcessing, fixturePath, fixtureAssetPath, status, hash, false);
                bool unchangedSecond = Evaluate(needsProcessing, fixturePath, fixtureAssetPath, status, hash, false);
                Add(checks, "unchanged failed import skips repeatedly", !unchangedFirst && !unchangedSecond,
                    "retryFailed=false returned false on two consecutive guard calls");

                File.WriteAllText(fixturePath, "synthetic import retry fixture v2", new UTF8Encoding(false));
                string changedHash = (string)contentHashMethod.Invoke(null, new object[] { fixturePath });
                bool changedEligible = Evaluate(needsProcessing, fixturePath, fixtureAssetPath, status, changedHash, false);
                Add(checks, "changed failed import is eligible", changedEligible,
                    "new fixture bytes/hash bypassed the unchanged-failure skip");

                Set(status, "canonicalContentHash", changedHash);
                bool explicitRetryEligible = Evaluate(needsProcessing, fixturePath, fixtureAssetPath, status, changedHash, true);
                Add(checks, "explicit retry is eligible", explicitRetryEligible,
                    "retryFailed=true bypassed the unchanged-failure skip");

                string folderName = "__ImportRetryChecks_" + Guid.NewGuid().ToString("N");
                fixtureOutputFolder = poseOutputRoot + "/" + folderName;
                if (AssetDatabase.IsValidFolder(fixtureOutputFolder))
                    throw new IOException("Unique success-fixture folder unexpectedly already exists.");
                string fixtureFolderGuid = AssetDatabase.CreateFolder(poseOutputRoot, folderName);
                fixtureOutputCreated = !string.IsNullOrEmpty(fixtureFolderGuid) || AssetDatabase.IsValidFolder(fixtureOutputFolder);
                if (!fixtureOutputCreated || !AssetDatabase.IsValidFolder(fixtureOutputFolder))
                    throw new IOException("Could not create a unique success-fixture folder under the configured pose output root.");

                string animationPath = fixtureOutputFolder + "/RetryFixture.anim";
                string posePath = fixtureOutputFolder + "/RetryFixture.asset";
                var animation = new AnimationClip { name = "RetryFixture" };
                AssetDatabase.CreateAsset(animation, animationPath);
                var pose = ScriptableObject.CreateInstance(FindType("DazPose.Performer.PerformerPose"));
                var poseData = new SerializedObject(pose);
                var clipProperty = poseData.FindProperty("clip");
                if (clipProperty == null)
                    throw new InvalidOperationException("PerformerPose no longer exposes the serialized clip field required by the import guard.");
                clipProperty.objectReferenceValue = animation;
                poseData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(pose, posePath);

                object successfulStatus = Activator.CreateInstance(statusType, true);
                Set(successfulStatus, "state", "Converted");
                Set(successfulStatus, "canonicalContentHash", changedHash);
                Set(successfulStatus, "expectedAnimPath", animationPath);
                Set(successfulStatus, "expectedWrapperAssetPath", posePath);
                string successfulAssetPath = poseImportRoot + "/__cleanup_task1_fixture__/successful-output.dazpose.json";
                bool unchangedSuccessSkipped = !Evaluate(needsProcessing, fixturePath, successfulAssetPath,
                    successfulStatus, changedHash, false);
                Add(checks, "unchanged successful output skips", unchangedSuccessSkipped,
                    "synthetic AnimationClip and PerformerPose under configured output root were usable");

                File.WriteAllText(fixturePath, "synthetic import retry fixture v3", new UTF8Encoding(false));
                string changedSuccessHash = (string)contentHashMethod.Invoke(null, new object[] { fixturePath });
                bool changedSuccessEligible = Evaluate(needsProcessing, fixturePath, successfulAssetPath,
                    successfulStatus, changedSuccessHash, false);
                Add(checks, "changed successful input is eligible", changedSuccessEligible,
                    "changed input hash bypassed the unchanged Converted-output skip");
                report.successfulOutputSampleAvailable = true;
            }
            catch (Exception error)
            {
                Add(checks, "check execution", false, error.GetBaseException().Message);
            }
            finally
            {
                if (fixtureOutputCreated && !string.IsNullOrEmpty(fixtureOutputFolder))
                {
                    try
                    {
                        bool deleted = AssetDatabase.DeleteAsset(fixtureOutputFolder) && !AssetDatabase.IsValidFolder(fixtureOutputFolder);
                        Add(checks, "success fixture cleanup", deleted,
                            deleted ? "deleted only the unique fixture folder through AssetDatabase" : "AssetDatabase did not remove the unique fixture folder");
                    }
                    catch (Exception error) { Add(checks, "success fixture cleanup", false, error.GetBaseException().Message); }
                }
                try { if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, true); }
                catch (Exception error) { Add(checks, "disk fixture cleanup", false, error.Message); }
            }

            report.completedUtc = DateTime.UtcNow.ToString("O");
            report.checks = checks.ToArray();
            report.passed = checks.All(check => check.passed) && checks.Count >= 3;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            if (!report.passed)
                throw new InvalidOperationException("Import retry checks failed. See " + reportPath);
            Debug.Log("Import retry checks passed. Report: " + reportPath);
        }

        private static bool Evaluate(MethodInfo method, string diskPath, string assetPath, object status, string hash, bool retryFailed)
        {
            return (bool)method.Invoke(null, new[] { diskPath, assetPath, status, hash, (object)retryFailed });
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            throw new TypeLoadException("Could not find " + fullName + " in loaded assemblies.");
        }

        private static void Set(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void Add(List<CheckResult> checks, string name, bool passed, string detail)
        {
            checks.Add(new CheckResult { name = name, passed = passed, detail = detail });
        }
    }
}
