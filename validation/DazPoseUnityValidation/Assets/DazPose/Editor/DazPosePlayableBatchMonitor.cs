using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    [InitializeOnLoad]
    public static class DazPosePlayableBatchMonitor
    {
        private const string PendingKey = "DazPose.PlayableSmoke.Pending";
        private const string PassedKey = "DazPose.PlayableSmoke.Passed";
        private const string StartedAtKey = "DazPose.PlayableSmoke.StartedAt";
        private const string OwnerKey = "DazPose.PlayableSmoke.Owner";
        private const double TimeoutSeconds = 120;

        static DazPosePlayableBatchMonitor()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static string CreateOwnerId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static void RegisterOwner(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId)) throw new ArgumentException("A smoke-test ownership token is required.", nameof(ownerId));
            SessionState.SetString(OwnerKey, ownerId);
        }

        public static void Arm(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId)) throw new ArgumentException("A smoke-test ownership token is required.", nameof(ownerId));
            RegisterOwner(ownerId);
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

            var ownerId = SessionState.GetString(OwnerKey, string.Empty);
            if (string.IsNullOrEmpty(ownerId))
            {
                Debug.LogError("DAZ Pose batch Play Mode smoke test has no ownership token; it will not inspect or commandeer any validation driver.");
                SessionState.SetBool(PassedKey, false);
                EditorApplication.isPlaying = false;
                return;
            }
            var ownedDrivers = Resources.FindObjectsOfTypeAll<DazPosePlayableValidationDriver>()
                .Where(item => item != null && item.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(item)
                    && string.Equals(item.validationSmokeTestOwner, ownerId, StringComparison.Ordinal)).ToArray();
            if (ownedDrivers.Length > 1)
            {
                Debug.LogError("DAZ Pose batch Play Mode smoke test found multiple drivers with its ownership token.");
                SessionState.SetBool(PassedKey, false);
                EditorApplication.isPlaying = false;
                return;
            }
            var driver = ownedDrivers.FirstOrDefault();
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
            var ownerId = SessionState.GetString(OwnerKey, string.Empty);
            if (string.IsNullOrEmpty(ownerId)) return;

            var ownedDrivers = Resources.FindObjectsOfTypeAll<DazPosePlayableValidationDriver>()
                .Where(item => item != null && item.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(item)
                    && string.Equals(item.validationSmokeTestOwner, ownerId, StringComparison.Ordinal)).ToArray();
            if (ownedDrivers.Length > 1)
            {
                Debug.LogError("DAZ Pose smoke-test cleanup found multiple components with its ownership token; it left them untouched.");
                if (SessionState.GetBool(PendingKey, false)) SessionState.SetBool(PassedKey, false);
                return;
            }
            if (ownedDrivers.Length == 0)
            {
                if (SessionState.GetBool(PendingKey, false))
                {
                    Debug.LogError("DAZ Pose smoke-test cleanup could not find its owned driver.");
                    SessionState.SetBool(PassedKey, false);
                }
                SessionState.SetString(OwnerKey, string.Empty);
                return;
            }

            var ownedDriver = ownedDrivers[0];
            var ownedAnimator = ownedDriver.validationAddedAnimator ? ownedDriver.validationOwnedAnimator : null;
            UnityEngine.Object.DestroyImmediate(ownedDriver);
            if (ownedAnimator != null) UnityEngine.Object.DestroyImmediate(ownedAnimator);
            SessionState.SetString(OwnerKey, string.Empty);
        }
    }
}
