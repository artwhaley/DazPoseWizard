using System;
using System.IO;
using DazPose.Performer.HandGrip;
using DazPose.Performer;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;

namespace DazPose.Editor.HandGrip
{
    public static class HandGripValidationRunner
    {
        private const string Report = "TestOutput/TargetDrivenGrip/tests.txt";

        // Batch-only: never change the interactive user's scene or calibration
        // through an automatic editor startup callback.
        public static void BatchCalibration()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("BatchCalibration requires Unity batch mode.");
            try
            {
                EditorSceneManager.OpenScene(HandGripAcceptanceSetup.AcceptanceScenePath);
                SuccubusPerformer performer = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
                Animator animator = performer.GetComponent<Animator>();
                HandGripRigProfile profile = AssetDatabase.LoadAssetAtPath<HandGripRigProfile>(HandGripAcceptanceSetup.ProfilePath);
                HandGripCalibrationWindow.ConfigureFromRig(animator, profile);
                if (!profile.IsReady(animator, out string reason)) throw new InvalidOperationException(reason);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Run();
                foreach (HandGripDigitProfile digit in profile.Digits)
                {
                    Transform first = animator.transform.Find(digit.JointPaths[0]);
                    Vector3 position = first.parent.position;
                    Quaternion rotation = first.parent.rotation;
                    for (int joint = 0; joint < digit.JointPaths.Length; joint++)
                    {
                        position += rotation * digit.JointLocalPositions[joint];
                        rotation *= digit.ClosedLocalRotations[joint];
                    }
                    File.AppendAllText(Report, "\n" + digit.Digit + ": joints=" + digit.JointPaths.Length
                        + ", probes=" + digit.Probes.Length + ", closed terminal=" + position.ToString("F5"));
                }
                if (File.ReadAllText(Report).Contains("FAIL")) throw new InvalidOperationException("Geometry tests failed.");
                var serialized = new SerializedObject(performer);
                PerformerPose pose = serialized.FindProperty("initialPose").objectReferenceValue as PerformerPose;
                if (pose == null || pose.Clip == null) throw new InvalidOperationException("Initial pose clip missing.");
                File.AppendAllText(Report, "\n" + HandGripSpatialProof.Run(animator, profile, pose.Clip));

                File.AppendAllText(Report, "\nCALIBRATION_CAPTURED: " + profile.HandPath
                    + "\nVisual inward-curl approval remains required.\n");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report, "FAIL: " + error);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/DAZ Pose/Handjob/Validate Target Driven Grip")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            try
            {
                string[] failures = HandGripRuntimeSelfTests.Run();
                File.WriteAllText(Report, DateTime.UtcNow.ToString("O") + "\n"
                    + (failures.Length == 0 ? "PASS: " + HandGripRuntimeSelfTests.AssertionCount + " geometry, palm, reach and synthetic contact assertions\n"
                        : "FAIL:\n" + string.Join("\n", failures)));
                if (failures.Length != 0) throw new InvalidOperationException(string.Join("\n", failures));
                Debug.Log("TARGET_DRIVEN_GRIP_TESTS_PASS: " + Report);
            }
            catch (Exception error)
            {
                File.AppendAllText(Report, "\n" + error);
                Debug.LogException(error);
            }
        }
    }
}
