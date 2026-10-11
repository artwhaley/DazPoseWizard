using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    /// <summary>Deterministic in-place facing checks against the configured baked locomotion profile.</summary>
    internal static class PerformerLocomotionRuntimeSelfTests
    {
        private const float TestDeltaTime = 1f / 60f;
        private const float PositionTolerance = 0.001f;

        public static string[] Run(PerformerLocomotionProfile profile)
        {
            var failures = new List<string>();
            string reason = null;
            if (profile == null || !profile.IsReady(out reason))
            {
                failures.Add("P0.TurnTo: locomotion profile is unavailable or invalid. "
                    + (reason ?? "Assign the generated KAWAII Walk01 profile."));
                return failures.ToArray();
            }

            var host = new GameObject("P0TurnTo_LocomotionRuntimeSelfTest");
            var target = new GameObject("P0TurnTo_TargetSnapshotSelfTest");
            PlayableGraph graph = PlayableGraph.Create("P0 TurnTo locomotion self-test");
            PerformerBodySourceMixer body = null;
            PerformerLocomotion locomotion = null;
            try
            {
                Animator animator = host.AddComponent<Animator>();
                Playable poseSource = AnimationClipPlayable.Create(graph, profile.StartA.BodyClip);
                body = new PerformerBodySourceMixer(animator, graph, poseSource, profile);
                locomotion = new PerformerLocomotion(host.transform, profile, body);
                Vector3 origin = new Vector3(2f, 0.2f, -3f);

                CheckImmediateAlignment(locomotion, host.transform, origin, profile, failures);
                CheckInvalidTargets(locomotion, host.transform, origin, failures);
                CheckTransformSnapshot(locomotion, host.transform, target.transform, origin, profile, failures);
                CheckVerticalTarget(locomotion, host.transform, origin, profile, failures);
                CheckSynchronousVectorApi(locomotion, host.transform, origin, profile, failures);
                CheckSmallAngles(locomotion, body, host.transform, origin, profile, failures);
                CheckAuthoredAngles(locomotion, body, host.transform, origin, profile, failures);
                CheckTiltedRootTurns(locomotion, host.transform, origin, profile, failures);
                CheckArrivalPoseHold(locomotion, body, host.transform, origin, profile, failures);
                CheckWalkReplacementRejected(locomotion, host.transform, origin, failures);
            }
            catch (Exception exception)
            {
                failures.Add("P0.TurnTo: runtime self-test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                locomotion?.Dispose();
                body?.Dispose();
                if (graph.IsValid()) graph.Destroy();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(target);
            }

            CheckTurnToDisableResolvesWaiter(profile, failures);
            return failures.ToArray();
        }

        private static void CheckImmediateAlignment(PerformerLocomotion locomotion, Transform actor,
            Vector3 origin, PerformerLocomotionProfile profile, List<string> failures)
        {
            Reset(actor, origin, Quaternion.identity);
            Quaternion before = actor.rotation;
            Vector3 almostForward = Quaternion.AngleAxis(profile.ArrivalHeadingTolerance * 0.5f, Vector3.up)
                * Vector3.forward;
            Awaitable<LocomotionCompletion> completion = locomotion.TurnToAsync(origin + almostForward * 3f);
            Check(!locomotion.IsLocomoting && locomotion.State == PerformerLocomotionState.Idle
                && locomotion.CurrentMotion == null && Quaternion.Angle(actor.rotation, before) < 0.0001f
                && completion.GetAwaiter().GetResult() == LocomotionCompletion.Arrived,
                "already-aligned request completes immediately without an animation or root twitch", failures);
        }

        private static void CheckInvalidTargets(PerformerLocomotion locomotion, Transform actor,
            Vector3 origin, List<string> failures)
        {
            Reset(actor, origin, Quaternion.identity);
            bool samePlanarPositionRejected = false;
            try { locomotion.TurnTo(origin + Vector3.up * 5f); }
            catch (ArgumentException) { samePlanarPositionRejected = true; }
            Check(samePlanarPositionRejected && !locomotion.IsLocomoting,
                "a target with the same planar position is rejected", failures);

            bool nanRejected = false;
            try { locomotion.TurnTo(new Vector3(float.NaN, 0f, 1f)); }
            catch (ArgumentOutOfRangeException) { nanRejected = true; }
            Check(nanRejected && !locomotion.IsLocomoting, "NaN target components are rejected", failures);

            bool infinityRejected = false;
            try { locomotion.TurnToAsync(new Vector3(1f, float.PositiveInfinity, 0f)); }
            catch (ArgumentOutOfRangeException) { infinityRejected = true; }
            Check(infinityRejected && !locomotion.IsLocomoting, "infinite target components are rejected", failures);
        }

        private static void CheckTransformSnapshot(PerformerLocomotion locomotion, Transform actor,
            Transform target, Vector3 origin, PerformerLocomotionProfile profile, List<string> failures)
        {
            Reset(actor, origin, Quaternion.identity);
            Vector3 capturedDirection = Vector3.right;
            target.SetPositionAndRotation(origin + capturedDirection * 4f,
                Quaternion.LookRotation(Vector3.left, Vector3.up));
            Awaitable<LocomotionCompletion> completion = locomotion.TurnToAsync(target);
            target.SetPositionAndRotation(origin + Vector3.left * 4f,
                Quaternion.LookRotation(Vector3.forward, Vector3.up));

            bool finished = AdvanceUntilIdle(locomotion, actor, origin, out float maximumDrift);
            Check(finished && completion.GetAwaiter().GetResult() == LocomotionCompletion.Arrived,
                "Transform request completes after taking its target-position snapshot", failures);
            CheckFacing(actor, capturedDirection, profile, "Transform target position is snapshotted and Transform.forward is ignored", failures);
            Check(maximumDrift < PositionTolerance && Vector3.Distance(actor.position, origin) < PositionTolerance,
                "Transform TurnTo preserves actor position throughout and at completion", failures);
        }

        private static void CheckVerticalTarget(PerformerLocomotion locomotion, Transform actor,
            Vector3 origin, PerformerLocomotionProfile profile, List<string> failures)
        {
            Reset(actor, origin, Quaternion.identity);
            Vector3 direction = Vector3.right;
            Awaitable<LocomotionCompletion> completion = locomotion.TurnToAsync(origin + direction * 3f + Vector3.up * 80f);
            bool finished = AdvanceUntilIdle(locomotion, actor, origin, out float maximumDrift);
            Check(finished && completion.GetAwaiter().GetResult() == LocomotionCompletion.Arrived,
                "Vector3 TurnTo completes for a target with a large vertical offset", failures);
            CheckFacing(actor, direction, profile, "vertical target offset does not pitch or roll the actor root", failures);
            Check(maximumDrift < PositionTolerance && Vector3.Distance(actor.position, origin) < PositionTolerance,
                "vertical-offset TurnTo remains in place", failures);
        }

        private static void CheckSynchronousVectorApi(PerformerLocomotion locomotion, Transform actor,
            Vector3 origin, PerformerLocomotionProfile profile, List<string> failures)
        {
            Reset(actor, origin, Quaternion.identity);
            Vector3 direction = Direction(-60f);
            locomotion.TurnTo(origin + direction * 3f);
            Check(locomotion.IsLocomoting && locomotion.State == PerformerLocomotionState.Turning
                && locomotion.CurrentMotion == profile.TurnLeft90,
                "synchronous TurnTo(Vector3) starts the expected authored facing turn", failures);
            bool finished = AdvanceUntilIdle(locomotion, actor, origin, out float maximumDrift);
            Check(finished && CheckFacingResult(actor, direction, profile)
                && maximumDrift < PositionTolerance && Vector3.Distance(actor.position, origin) < PositionTolerance,
                "synchronous TurnTo(Vector3) finishes facing the target without translation", failures);
        }

        private static void CheckSmallAngles(PerformerLocomotion locomotion, PerformerBodySourceMixer body,
            Transform actor, Vector3 origin, PerformerLocomotionProfile profile, List<string> failures)
        {
            float[] angles = { 10f, -25f, 35f, 39.9f };
            foreach (float angle in angles)
            {
                Reset(actor, origin, Quaternion.identity);
                Vector3 direction = Direction(angle);
                Awaitable<LocomotionCompletion> completion = locomotion.TurnToAsync(origin + direction * 3f);
                Check(locomotion.IsLocomoting && locomotion.State == PerformerLocomotionState.Settling
                    && locomotion.CurrentMotion == null && locomotion.SelectedTurn == "none"
                    && Mathf.Abs(locomotion.HeadingError - angle) < 0.01f,
                    angle.ToString("0") + "° uses smooth root alignment instead of a compressed authored clip", failures);

                bool replacementRejected = false;
                PerformerLocomotionState state = locomotion.State;
                try { locomotion.TurnTo(origin + Direction(angle + 40f) * 3f); }
                catch (InvalidOperationException) { replacementRejected = true; }
                Check(replacementRejected && locomotion.State == state && locomotion.IsLocomoting,
                    angle.ToString("0") + "° TurnTo cannot be replaced during Settling", failures);

                bool finished = AdvanceUntilIdle(locomotion, actor, origin, out float maximumDrift);
                Check(finished && completion.GetAwaiter().GetResult() == LocomotionCompletion.Arrived,
                    angle.ToString("0") + "° smooth alignment completes Arrived", failures);
                CheckFacing(actor, direction, profile, angle.ToString("0") + "° finishes at exact facing", failures);
                Check(maximumDrift < PositionTolerance && Vector3.Distance(actor.position, origin) < PositionTolerance,
                    angle.ToString("0") + "° smooth alignment preserves position", failures);
                Check(BodyWeightIsReleased(locomotion, body),
                    angle.ToString("0") + "° smooth alignment relinquishes locomotion ownership", failures);
            }
        }

        private static void CheckAuthoredAngles(PerformerLocomotion locomotion, PerformerBodySourceMixer body,
            Transform actor, Vector3 origin, PerformerLocomotionProfile profile, List<string> failures)
        {
            float[] magnitudes = { 40f, 60f, 90f, 120f, 130f, 135f, 136f, 140f, 150f, 175f };
            foreach (float magnitude in magnitudes)
            {
                TestAuthoredAngle(locomotion, body, actor, origin, profile, magnitude, failures);
                TestAuthoredAngle(locomotion, body, actor, origin, profile, -magnitude, failures);
            }
        }

        private static void CheckTiltedRootTurns(PerformerLocomotion locomotion, Transform actor,
            Vector3 origin, PerformerLocomotionProfile profile, List<string> failures)
        {
            Quaternion initialRotation = Quaternion.Euler(12f, 18f, -7f);
            float[] angles = { 25f, -90f };
            foreach (float angle in angles)
            {
                Reset(actor, origin, initialRotation);
                Vector3 planarForward = Vector3.ProjectOnPlane(actor.forward, Vector3.up).normalized;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * planarForward;
                Quaternion expectedRotation = Quaternion.AngleAxis(angle, Vector3.up) * initialRotation;
                Awaitable<LocomotionCompletion> completion = locomotion.TurnToAsync(origin + direction * 3f);
                bool finished = AdvanceUntilIdle(locomotion, actor, origin, out float maximumDrift);
                Check(finished && completion.GetAwaiter().GetResult() == LocomotionCompletion.Arrived,
                    angle.ToString("0") + "° turn from a tilted root completes Arrived", failures);
                Check(Quaternion.Angle(actor.rotation, expectedRotation) < 0.05f,
                    angle.ToString("0") + "° turn applies world-up yaw without changing root pitch or roll", failures);
                Check(maximumDrift < PositionTolerance && Vector3.Distance(actor.position, origin) < PositionTolerance,
                    angle.ToString("0") + "° turn from a tilted root preserves position", failures);
            }
        }

        private static void TestAuthoredAngle(PerformerLocomotion locomotion, PerformerBodySourceMixer body, Transform actor,
            Vector3 origin, PerformerLocomotionProfile profile, float signedAngle,
            List<string> failures)
        {
            Reset(actor, origin, Quaternion.identity);
            Vector3 direction = Direction(signedAngle);
            Awaitable<LocomotionCompletion> completion = locomotion.TurnToAsync(origin + direction * 3f);
            PerformerLocomotionMotion expected = ExpectedTurn(profile, signedAngle);
            Check(expected != null && locomotion.State == PerformerLocomotionState.Turning
                && locomotion.CurrentMotion == expected && locomotion.SelectedTurn == expected.name,
                signedAngle.ToString("0") + "° selects the closest safe authored turn in the correct direction", failures);
            if (expected == null)
            {
                locomotion.Dispose();
                return;
            }

            float warp = Mathf.Abs(Mathf.Abs(expected.NominalYawDegrees) - Mathf.Abs(signedAngle));
            Check(warp <= profile.MaximumTurnWarpDegrees + 0.0001f,
                signedAngle.ToString("0") + "° never selects an authored clip outside the configured warp bound", failures);

            if (signedAngle == 60f)
            {
                PerformerLocomotionMotion activeMotion = locomotion.CurrentMotion;
                bool replacementRejected = false;
                try { locomotion.TurnTo(origin + Vector3.forward * 3f); }
                catch (InvalidOperationException) { replacementRejected = true; }
                Check(replacementRejected && locomotion.State == PerformerLocomotionState.Turning
                    && locomotion.CurrentMotion == activeMotion,
                    "a second TurnTo is rejected without replacing the active authored turn", failures);
            }

            bool finished = AdvanceUntilIdle(locomotion, actor, origin, out float maximumDrift);
            Check(finished && completion.GetAwaiter().GetResult() == LocomotionCompletion.Arrived,
                signedAngle.ToString("0") + "° authored TurnTo completes Arrived", failures);
            CheckFacing(actor, direction, profile, signedAngle.ToString("0") + "° authored turn converges to exact facing", failures);
            Check(maximumDrift < PositionTolerance && Vector3.Distance(actor.position, origin) < PositionTolerance,
                signedAngle.ToString("0") + "° authored turn preserves position throughout", failures);
            Check(BodyWeightIsReleased(locomotion, body),
                signedAngle.ToString("0") + "° authored turn blends locomotion ownership back out", failures);
        }

        private static void CheckArrivalPoseHold(PerformerLocomotion locomotion, PerformerBodySourceMixer body,
            Transform actor, Vector3 origin, PerformerLocomotionProfile profile, List<string> failures)
        {
            Reset(actor, origin, Quaternion.identity);
            locomotion.SetHoldArrivalPose(true);
            body.SetOwnership(true, true);
            Vector3 direction = Direction(90f);
            Awaitable<LocomotionCompletion> completion = locomotion.TurnToAsync(origin + direction * 3f);
            bool finished = AdvanceUntilIdle(locomotion, actor, origin, out float maximumDrift);
            Check(finished && completion.GetAwaiter().GetResult() == LocomotionCompletion.Arrived
                && body.LocomotionWeight < 0.001f && body.HoldArrivalPose,
                "TurnTo releases a held locomotion arrival layer back to persistent Pose and restores the hold policy", failures);
            Check(maximumDrift < PositionTolerance && Vector3.Distance(actor.position, origin) < PositionTolerance,
                "TurnTo with a held arrival pose remains in place", failures);
            CheckFacing(actor, direction, profile, "TurnTo with a held arrival pose retains exact facing", failures);
            locomotion.SetHoldArrivalPose(false);
        }

        private static void CheckWalkReplacementRejected(PerformerLocomotion locomotion, Transform actor,
            Vector3 origin, List<string> failures)
        {
            Reset(actor, origin, Quaternion.identity);
            Awaitable<LocomotionCompletion> walk = locomotion.WalkToAsync(origin + Vector3.forward * 8f);
            int frames = 0;
            while (locomotion.State == PerformerLocomotionState.Starting && frames++ < 600)
                locomotion.Advance(TestDeltaTime);

            PerformerLocomotionState state = locomotion.State;
            PerformerLocomotionMotion motion = locomotion.CurrentMotion;
            bool rejected = false;
            try { locomotion.TurnTo(origin + Vector3.right * 3f); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && locomotion.IsLocomoting && state == PerformerLocomotionState.Walking
                && locomotion.State == state && locomotion.CurrentMotion == motion,
                "TurnTo is rejected during WalkTo without replacing or mutating the walk", failures);

            locomotion.Dispose();
            Check(walk.GetAwaiter().GetResult() == LocomotionCompletion.PerformerDisabled,
                "disabling the locomotion runtime resolves a pending WalkTo waiter", failures);
        }

        private static void CheckTurnToDisableResolvesWaiter(PerformerLocomotionProfile profile,
            List<string> failures)
        {
            var host = new GameObject("P0TurnTo_DisableSelfTest");
            PlayableGraph graph = PlayableGraph.Create("P0 TurnTo disable self-test");
            PerformerBodySourceMixer body = null;
            PerformerLocomotion locomotion = null;
            try
            {
                Animator animator = host.AddComponent<Animator>();
                Playable poseSource = AnimationClipPlayable.Create(graph, profile.StartA.BodyClip);
                body = new PerformerBodySourceMixer(animator, graph, poseSource, profile);
                locomotion = new PerformerLocomotion(host.transform, profile, body);
                Vector3 origin = new Vector3(-2f, 0.2f, 3f);
                host.transform.position = origin;
                Awaitable<LocomotionCompletion> completion = locomotion.TurnToAsync(origin + Vector3.right * 3f);
                locomotion.Dispose();
                Check(completion.GetAwaiter().GetResult() == LocomotionCompletion.PerformerDisabled
                    && !locomotion.IsLocomoting && locomotion.State == PerformerLocomotionState.Idle
                    && body.LocomotionWeight < 0.001f,
                    "disabling the locomotion runtime resolves a pending TurnTo waiter and releases body ownership", failures);
            }
            catch (Exception exception)
            {
                failures.Add("P0.TurnTo: disable self-test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                locomotion?.Dispose();
                body?.Dispose();
                if (graph.IsValid()) graph.Destroy();
                UnityEngine.Object.Destroy(host);
            }
        }

        private static PerformerLocomotionMotion ExpectedTurn(PerformerLocomotionProfile profile, float signedYaw)
        {
            PerformerLocomotionMotion turn90 = signedYaw < 0f ? profile.TurnLeft90 : profile.TurnRight90;
            PerformerLocomotionMotion turn180 = signedYaw < 0f ? profile.TurnLeft180 : profile.TurnRight180;
            float magnitude = Mathf.Abs(signedYaw);
            float difference90 = Mathf.Abs(magnitude - Mathf.Abs(turn90.NominalYawDegrees));
            float difference180 = Mathf.Abs(magnitude - Mathf.Abs(turn180.NominalYawDegrees));
            bool valid90 = difference90 <= profile.MaximumTurnWarpDegrees + 0.0001f;
            bool valid180 = difference180 <= profile.MaximumTurnWarpDegrees + 0.0001f;
            if (!valid90 && !valid180) return null;
            if (valid90 && (!valid180 || difference90 <= difference180)) return turn90;
            return turn180;
        }

        private static bool AdvanceUntilIdle(PerformerLocomotion locomotion, Transform actor,
            Vector3 origin, out float maximumDrift)
        {
            maximumDrift = Vector3.Distance(actor.position, origin);
            int frames = 0;
            const int maximumFrames = 900;
            while (locomotion.IsLocomoting && frames++ < maximumFrames)
            {
                locomotion.Advance(TestDeltaTime);
                maximumDrift = Mathf.Max(maximumDrift, Vector3.Distance(actor.position, origin));
            }
            return !locomotion.IsLocomoting;
        }

        private static bool BodyWeightIsReleased(PerformerLocomotion locomotion, PerformerBodySourceMixer body)
        {
            // Completion is held until locomotion relinquishes its body-source weight.
            return !locomotion.IsLocomoting && locomotion.State == PerformerLocomotionState.Idle
                && body.LocomotionWeight < 0.001f;
        }

        private static Vector3 Direction(float signedYaw) =>
            Quaternion.AngleAxis(signedYaw, Vector3.up) * Vector3.forward;

        private static void CheckFacing(Transform actor, Vector3 direction, PerformerLocomotionProfile profile,
            string description, List<string> failures)
        {
            float error = FacingError(actor, direction);
            Check(error <= profile.ArrivalHeadingTolerance + 0.01f,
                description + " (error " + error.ToString("0.000") + "°)", failures);
        }

        private static bool CheckFacingResult(Transform actor, Vector3 direction,
            PerformerLocomotionProfile profile) => FacingError(actor, direction)
            <= profile.ArrivalHeadingTolerance + 0.01f;

        private static float FacingError(Transform actor, Vector3 direction)
        {
            Vector3 forward = Vector3.ProjectOnPlane(actor.forward, Vector3.up).normalized;
            return Vector3.Angle(forward, direction.normalized);
        }

        private static void Reset(Transform actor, Vector3 position, Quaternion rotation)
        {
            actor.SetPositionAndRotation(position, rotation);
        }

        private static void Check(bool condition, string description, List<string> failures)
        {
            if (!condition) failures.Add("P0.TurnTo: " + description + ".");
        }
    }
}
