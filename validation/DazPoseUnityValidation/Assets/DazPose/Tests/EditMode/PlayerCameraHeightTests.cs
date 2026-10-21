using System;
using System.Collections.Generic;
using DazPose.Player;
using NUnit.Framework;
using UnityEngine;

namespace DazPose.Tests
{
    public sealed class PlayerCameraHeightTests
    {
        const float Tolerance = 0.0001f;

        sealed class RecordingFade : IScreenFade
        {
            public readonly List<string> Log;
            float alpha;
            public RecordingFade(List<string> log) { Log = log; }
            public float Alpha { get => alpha; set { alpha = value; Log.Add("alpha " + value.ToString("F3")); } }
        }

        // ---- sequencing, driven by Advance without rendering ----

        [Test]
        public void OrderIsFadeOutThenMoveThenFadeIn()
        {
            var log = new List<string>();
            var fade = new RecordingFade(log);
            float alphaAtMove = -1f;
            var change = new PlayerCameraHeightChange(fade, () => { alphaAtMove = fade.Alpha; log.Add("move"); }, 1f, 1f);

            change.Advance(0.5f);
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(Tolerance), "smoothstep midpoint while fading out");
            Assert.IsFalse(change.Moved);
            change.Advance(0.5f);
            Assert.IsTrue(change.Moved);
            Assert.That(alphaAtMove, Is.EqualTo(1f), "the move happens at full black");
            Assert.AreEqual(PlayerCameraHeightChange.Stage.FadingIn, change.Current);
            change.Advance(0.5f);
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(Tolerance));
            change.Advance(0.5f);

            Assert.IsTrue(change.IsDone);
            Assert.AreEqual(PlayerCameraHeightResult.Completed, change.Result);
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            int move = log.IndexOf("move");
            Assert.That(log.IndexOf("alpha 1.000"), Is.GreaterThanOrEqualTo(0).And.LessThan(move), "fully black before the move");
            Assert.That(log.FindIndex(move, l => l.StartsWith("alpha 0.")), Is.GreaterThan(move), "fades back in only after the move");
            Assert.AreEqual(1, log.FindAll(l => l == "move").Count);
        }

        [Test]
        public void AShortBeatOfBlackFollowsTheMoveBeforeTheFadeIn()
        {
            var fade = new RecordingFade(new List<string>());
            var change = new PlayerCameraHeightChange(fade, () => { }, 0.5f, 0.5f, 0.2f);
            change.Advance(0.5f);
            Assert.AreEqual(PlayerCameraHeightChange.Stage.Holding, change.Current);
            Assert.That(fade.Alpha, Is.EqualTo(1f));
            change.Advance(0.1f);
            Assert.AreEqual(PlayerCameraHeightChange.Stage.Holding, change.Current);
            Assert.That(fade.Alpha, Is.EqualTo(1f), "still fully black half way through the hold");
            change.Advance(0.1f);
            Assert.AreEqual(PlayerCameraHeightChange.Stage.FadingIn, change.Current);
            change.Advance(0.25f);
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(Tolerance));
            change.Advance(0.25f);
            Assert.IsTrue(change.IsDone);
            Assert.That(fade.Alpha, Is.EqualTo(0f));
        }

        [Test]
        public void TheFadeEasesGentlyAtBothEnds()
        {
            var fade = new RecordingFade(new List<string>());
            var change = new PlayerCameraHeightChange(fade, () => { }, 1f, 1f);
            change.Advance(0.1f);
            // Smootherstep at 0.1 is 0.00856; plain linear would be 0.1.
            Assert.That(fade.Alpha, Is.EqualTo(0.00856f).Within(0.0001f));
            change.Advance(0.8f);
            Assert.That(fade.Alpha, Is.EqualTo(0.99144f).Within(0.0001f));
        }

        [Test]
        public void TheControllerDefaultsToAHalfSecondEachWayWithAShortHold()
        {
            var controller = Rig(out _, out _, out _);
            Assert.That(controller.FadeOutSeconds, Is.EqualTo(0.5f));
            Assert.That(controller.FadeInSeconds, Is.EqualTo(0.5f));
            Assert.That(controller.BlackHoldSeconds, Is.EqualTo(0.15f).Within(Tolerance));
        }

        [Test]
        public void TheWindSwellIsBoundedSilentAtBothEndsAndNotSilentInTheMiddle()
        {
            var samples = PlayerTransitionWind.Samples(1.15f);
            Assert.AreEqual(Mathf.RoundToInt(1.15f * PlayerTransitionWind.SampleRate), samples.Length);
            Assert.That(samples[0], Is.EqualTo(0f));
            Assert.That(samples[samples.Length - 1], Is.EqualTo(0f));
            float peak = 0f, energyMiddle = 0f, energyEdge = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
                if (i > samples.Length * 0.4f && i < samples.Length * 0.6f) energyMiddle += samples[i] * samples[i];
                if (i < samples.Length * 0.05f) energyEdge += samples[i] * samples[i];
            }
            Assert.That(peak, Is.LessThanOrEqualTo(0.9001f).And.GreaterThan(0.5f));
            Assert.That(energyMiddle, Is.GreaterThan(energyEdge * 20f), "swells in and out");
            CollectionAssert.AreEqual(samples, PlayerTransitionWind.Samples(1.15f), "deterministic");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => PlayerTransitionWind.Samples(0f));
        }

        [Test]
        public void OneLargeStepStillRunsEveryStageInOrder()
        {
            var log = new List<string>();
            var change = new PlayerCameraHeightChange(new RecordingFade(log), () => log.Add("move"), 0.35f, 0.35f);
            change.Advance(5f);
            Assert.IsTrue(change.IsDone);
            Assert.AreEqual(PlayerCameraHeightResult.Completed, change.Result);
            Assert.AreEqual(1, log.FindAll(l => l == "move").Count);
        }

        [Test]
        public void ZeroLengthFadesStillMoveOnceAndFinishClear()
        {
            var log = new List<string>();
            var fade = new RecordingFade(log);
            var change = new PlayerCameraHeightChange(fade, () => log.Add("move"), 0f, 0f);
            change.Advance(0f);
            Assert.IsTrue(change.IsDone);
            Assert.AreEqual(1, log.FindAll(l => l == "move").Count);
            Assert.That(fade.Alpha, Is.EqualTo(0f));
        }

        [Test]
        public void CancelBeforeAnyTimePassesLeavesTheFadeClear()
        {
            var log = new List<string>();
            var fade = new RecordingFade(log);
            int finished = 0;
            var change = new PlayerCameraHeightChange(fade, () => log.Add("move"), 1f, 1f);
            change.Finished += _ => finished++;
            change.Cancel();
            Assert.IsTrue(change.IsDone);
            Assert.AreEqual(PlayerCameraHeightResult.Cancelled, change.Result);
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            Assert.IsFalse(change.Moved);
            Assert.IsFalse(log.Contains("move"));
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void CancelWhileFadingOutClearsTheFadeWithoutMoving()
        {
            var fade = new RecordingFade(new List<string>());
            var change = new PlayerCameraHeightChange(fade, () => Assert.Fail("must not move"), 1f, 1f);
            change.Advance(0.7f);
            Assert.That(fade.Alpha, Is.GreaterThan(0.5f));
            change.Cancel();
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            Assert.AreEqual(PlayerCameraHeightResult.Cancelled, change.Result);
            change.Advance(5f); // a finished change ignores further time
            Assert.That(fade.Alpha, Is.EqualTo(0f));
        }

        [Test]
        public void CancelWhileFadingInClearsTheFadeAfterTheMove()
        {
            var fade = new RecordingFade(new List<string>());
            int moves = 0;
            var change = new PlayerCameraHeightChange(fade, () => moves++, 1f, 1f);
            change.Advance(1.4f);
            Assert.AreEqual(PlayerCameraHeightChange.Stage.FadingIn, change.Current);
            Assert.That(fade.Alpha, Is.GreaterThan(0f));
            change.Cancel();
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            Assert.AreEqual(PlayerCameraHeightResult.Cancelled, change.Result);
            Assert.AreEqual(1, moves);
            Assert.IsTrue(change.Moved);
        }

        [Test]
        public void CancelAfterCompletionChangesNothing()
        {
            int finished = 0;
            var change = new PlayerCameraHeightChange(new RecordingFade(new List<string>()), () => { }, 0.1f, 0.1f);
            change.Finished += _ => finished++;
            change.Advance(1f);
            change.Cancel();
            Assert.AreEqual(PlayerCameraHeightResult.Completed, change.Result);
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void AFailingMoveLeavesTheScreenClearAndReportsTheError()
        {
            var fade = new RecordingFade(new List<string>());
            var change = new PlayerCameraHeightChange(fade, () => throw new InvalidOperationException("no mount"), 0.1f, 0.1f);
            change.Advance(1f);
            Assert.IsTrue(change.IsDone);
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            Assert.AreEqual(PlayerCameraHeightResult.Cancelled, change.Result);
            Assert.IsInstanceOf<InvalidOperationException>(change.Error);
        }

        [Test]
        public void NegativeOrNonFiniteDurationsAreRejected()
        {
            var fade = new RecordingFade(new List<string>());
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerCameraHeightChange(fade, () => { }, -1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerCameraHeightChange(fade, () => { }, 0f, float.NaN));
        }

        // ---- the controller on a synthetic rig ----

        GameObject body, player, target;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { body, player, target }) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            body = player = target = null;
        }

        static Transform Child(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.SetPositionAndRotation(position, rotation);
            return child;
        }

        PlayerCameraController Rig(out PlayerView view, out PlayerCameraMounts mounts, out Transform rig)
        {
            body = new GameObject("Body");
            var presence = body.AddComponent<PlayerBodyPresence>();
            mounts = body.AddComponent<PlayerCameraMounts>();
            mounts.Configure(
                Child(body.transform, "High", new Vector3(0, 1.8f, -0.9f), Quaternion.identity),
                Child(body.transform, "Low", new Vector3(0, 1.2f, -0.9f), Quaternion.LookRotation(new Vector3(0, -0.2f, 0.9f).normalized, Vector3.up)));

            player = new GameObject("Player");
            rig = Child(player.transform, "ViewRig", Vector3.zero, Quaternion.identity);
            var head = Child(rig, "HeadPose", Vector3.zero, Quaternion.identity);
            var camera = Child(head, "MainCamera", Vector3.zero, Quaternion.identity).gameObject.AddComponent<Camera>();
            view = player.AddComponent<PlayerView>();
            view.Configure(rig, head, camera);
            var controller = player.AddComponent<PlayerController>();
            controller.ConfigureBodyPresence(presence);
            view.DefaultLookDuration = 0f; // so Release finishes without the player loop
            return player.AddComponent<PlayerCameraController>();
        }

        static bool IsComplete<T>(Awaitable<T> awaitable) => awaitable.GetAwaiter().IsCompleted;

        [Test]
        public void SetHeightToTheCurrentHeightCompletesAtOnceWithoutAFade()
        {
            var controller = Rig(out _, out var mounts, out _);
            controller.StartAt(PlayerCameraHeight.High);
            var result = controller.SetHeightAsync(PlayerCameraHeight.High);
            Assert.IsTrue(IsComplete(result));
            Assert.AreEqual(PlayerCameraHeightResult.Completed, result.GetAwaiter().GetResult());
            Assert.IsFalse(controller.IsChangingHeight);
            Assert.IsNull(player.GetComponentInChildren<PlayerScreenFade>(), "no fade was even created");
            Assert.AreEqual(PlayerCameraHeight.High, mounts.CurrentHeight);
        }

        [Test]
        public void SetHeightFadesMovesAndFadesBackIn()
        {
            var controller = Rig(out var view, out var mounts, out _);
            controller.FadeOutSeconds = 1f; controller.FadeInSeconds = 1f; controller.BlackHoldSeconds = 0f;
            controller.StartAt(PlayerCameraHeight.High);
            var result = controller.SetHeightAsync(PlayerCameraHeight.Low);
            var fade = player.GetComponentInChildren<PlayerScreenFade>();
            Assert.IsNotNull(fade);
            Assert.IsFalse(IsComplete(result));
            Assert.IsTrue(controller.IsChangingHeight);

            controller.Tick(0.5f);
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.AreEqual(PlayerCameraHeight.High, mounts.CurrentHeight, "still on High while fading out");
            controller.Tick(0.5f);
            Assert.AreEqual(PlayerCameraHeight.Low, mounts.CurrentHeight, "moved at full black");
            Assert.That(Vector3.Distance(mounts.Low.position, player.transform.position), Is.LessThan(Tolerance));
            Assert.That(fade.Alpha, Is.EqualTo(1f));
            controller.Tick(1f);
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            Assert.IsFalse(controller.IsChangingHeight);
            Assert.AreEqual(PlayerCameraHeightResult.Completed, result.GetAwaiter().GetResult());
        }

        [Test]
        public void ControllerCancellationClearsTheFade()
        {
            var controller = Rig(out _, out var mounts, out _);
            controller.StartAt(PlayerCameraHeight.High);
            var result = controller.SetHeightAsync(PlayerCameraHeight.Low);
            controller.Tick(0.2f);
            var fade = player.GetComponentInChildren<PlayerScreenFade>();
            Assert.That(fade.Alpha, Is.GreaterThan(0f));
            controller.CaptureCancellation()();
            Assert.That(fade.Alpha, Is.EqualTo(0f));
            Assert.AreEqual(PlayerCameraHeightResult.Cancelled, result.GetAwaiter().GetResult());
            Assert.AreEqual(PlayerCameraHeight.High, mounts.CurrentHeight, "cancelled before the move");
        }

        [Test]
        public void TrackingSurvivesSetHeight()
        {
            var controller = Rig(out var view, out var mounts, out var rig);
            controller.FadeOutSeconds = 0f; controller.FadeInSeconds = 0f; controller.BlackHoldSeconds = 0f;
            controller.StartAt(PlayerCameraHeight.High);
            target = new GameObject("Target");
            target.transform.position = new Vector3(2f, 1f, 4f);
            view.Track(target.transform, 0f);
            Assert.IsTrue(view.IsTracking, "setup");

            var result = controller.SetHeightAsync(PlayerCameraHeight.Low);
            Assert.IsTrue(IsComplete(result));
            Assert.IsTrue(view.IsTracking, "still tracking after the move");
            Assert.AreSame(target.transform, view.CurrentTrackTarget);
            Assert.That(Vector3.Distance(mounts.Low.position, player.transform.position), Is.LessThan(Tolerance));
            var toTarget = (target.transform.position - view.MainCamera.transform.position).normalized;
            Assert.That(Vector3.Dot(rig.forward, toTarget), Is.GreaterThan(0.999f), "the view looks at the target from the new mount");
        }

        [Test]
        public void WithoutTrackingTheViewTakesTheNewMountsOrientation()
        {
            var controller = Rig(out var view, out var mounts, out var rig);
            controller.FadeOutSeconds = 0f; controller.FadeInSeconds = 0f; controller.BlackHoldSeconds = 0f;
            controller.StartAt(PlayerCameraHeight.High);
            Assert.IsFalse(view.IsTracking);
            var result = controller.SetHeightAsync(PlayerCameraHeight.Low);
            Assert.IsTrue(IsComplete(result));
            Assert.IsFalse(view.IsTracking);
            Assert.That(Quaternion.Angle(mounts.Low.rotation, rig.rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void ReleaseStopsTrackingAndEndsAtTheMountsAuthoredOrientation()
        {
            var controller = Rig(out var view, out var mounts, out var rig);
            controller.StartAt(PlayerCameraHeight.Low);
            target = new GameObject("Target");
            target.transform.position = new Vector3(3f, 2f, -3f);
            view.Track(target.transform, 0f);
            Assert.IsTrue(view.IsTracking, "setup");
            Assert.That(Quaternion.Angle(mounts.Low.rotation, rig.rotation), Is.GreaterThan(5f), "setup: the view has turned away");

            var result = controller.ReleaseAsync();
            Assert.IsTrue(IsComplete(result));
            Assert.AreEqual(PlayerViewCompletion.Completed, result.GetAwaiter().GetResult());
            Assert.IsFalse(view.IsTracking);
            Assert.IsFalse(view.IsAcquiringTrack);
            Assert.That(Quaternion.Angle(mounts.Low.rotation, rig.rotation), Is.LessThan(0.5f));
        }

        [Test]
        public void ReleaseBeforeAnyMountFailsClearly()
        {
            var controller = Rig(out _, out _, out _);
            var error = Assert.Throws<InvalidOperationException>(() => controller.ReleaseAsync());
            StringAssert.Contains("not on a mount yet", error.Message);
        }

        [Test]
        public void StartAtCancelsAHeightChangeInProgress()
        {
            var controller = Rig(out _, out _, out _);
            controller.StartAt(PlayerCameraHeight.High);
            var result = controller.SetHeightAsync(PlayerCameraHeight.Low);
            controller.Tick(0.1f);
            controller.StartAt(PlayerCameraHeight.High);
            Assert.IsFalse(controller.IsChangingHeight);
            Assert.That(player.GetComponentInChildren<PlayerScreenFade>().Alpha, Is.EqualTo(0f));
            Assert.AreEqual(PlayerCameraHeightResult.Cancelled, result.GetAwaiter().GetResult());
        }
    }
}
