using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    [InitializeOnLoad]
    public static class DazPosePlayableBatchMonitor
    {
        private const string PendingKey = "DazPose.Phase2.PlayableSmoke.Pending";
        private const string PassedKey = "DazPose.Phase2.PlayableSmoke.Passed";
        private const string StartedAtKey = "DazPose.Phase2.PlayableSmoke.StartedAt";
        private const double TimeoutSeconds = 120;

        static DazPosePlayableBatchMonitor()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void Arm()
        {
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
        }

        private static void Update()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            var elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
            if (elapsed > TimeoutSeconds)
            {
                Debug.LogError("DAZ Pose batch Play Mode smoke test timed out before the Playables driver reported its result.");
                EditorApplication.Exit(2);
                return;
            }
            if (!EditorApplication.isPlaying) return;

            var driver = Resources.FindObjectsOfTypeAll<DazPosePlayableValidationDriver>()
                .FirstOrDefault(item => item != null && item.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(item));
            if (driver == null || !driver.validationComplete) return;
            if (!driver.validationPassed)
            {
                Debug.LogError("DAZ Pose batch Play Mode smoke test failed. See the runtime driver diagnostic above.");
                EditorApplication.Exit(1);
                return;
            }

            SessionState.SetBool(PassedKey, true);
            EditorApplication.isPlaying = false;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) CleanupTemporaryComponents();
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(PendingKey, false)) return;

            var passed = SessionState.GetBool(PassedKey, false);
            SessionState.SetBool(PendingKey, false);
            SessionState.SetBool(PassedKey, false);
            if (passed) Debug.Log("DAZ Pose batch Play Mode smoke test completed successfully; validation-only Animator and driver were cleaned up.");
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void CleanupTemporaryComponents()
        {
            foreach (var driver in Resources.FindObjectsOfTypeAll<DazPosePlayableValidationDriver>()
                         .Where(item => item != null && item.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(item)).ToArray())
            {
                var animator = driver.GetComponent<Animator>();
                var addedAnimator = driver.validationAddedAnimator;
                UnityEngine.Object.DestroyImmediate(driver);
                if (addedAnimator && animator != null) UnityEngine.Object.DestroyImmediate(animator);
            }
        }
    }
}
