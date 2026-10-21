using System;
using DazPose.Editor;
using DazPose.Player;
using NUnit.Framework;
using UnityEngine;

namespace DazPose.Tests
{
    public sealed class PlayerCameraMountsTests
    {
        const float Tolerance = 0.0001f;
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null;
        }

        static Transform Child(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.SetPositionAndRotation(position, rotation);
            return child;
        }

        PlayerCameraMounts Body(out Transform high, out Transform low)
        {
            root = new GameObject("Body");
            var mounts = root.AddComponent<PlayerCameraMounts>();
            high = Child(root.transform, "High", new Vector3(0, 1.8f, -0.9f), Quaternion.identity);
            low = Child(root.transform, "Low", new Vector3(0, 1.2f, -0.9f), Quaternion.LookRotation(new Vector3(0, -0.2f, 0.9f).normalized, Vector3.up));
            mounts.Configure(high, low);
            return mounts;
        }

        static PlayerView Rig(GameObject player, out Transform rig)
        {
            rig = Child(player.transform, "ViewRig", Vector3.zero, Quaternion.identity);
            var head = Child(rig, "HeadPose", Vector3.zero, Quaternion.identity);
            var camera = Child(head, "MainCamera", Vector3.zero, Quaternion.identity).gameObject.AddComponent<Camera>();
            var view = player.AddComponent<PlayerView>();
            view.Configure(rig, head, camera);
            return view;
        }

        [Test]
        public void MissingMountFailsAndNamesIt()
        {
            root = new GameObject("Body");
            var mounts = root.AddComponent<PlayerCameraMounts>();
            mounts.Configure(null, Child(root.transform, "Low", Vector3.zero, Quaternion.identity));
            var error = Assert.Throws<InvalidOperationException>(() => mounts.Validate());
            StringAssert.Contains("High camera mount", error.Message);
        }

        [Test]
        public void MountOutsideTheBodyRootFails()
        {
            var mounts = Body(out _, out _);
            var stranger = new GameObject("Stranger").transform;
            try
            {
                mounts.Configure(stranger, mounts.Low);
                var error = Assert.Throws<InvalidOperationException>(() => mounts.Validate());
                StringAssert.Contains("child of the player body root", error.Message);
            }
            finally { UnityEngine.Object.DestroyImmediate(stranger.gameObject); }
        }

        [Test]
        public void MountThatIsTheBodyRootItselfFails()
        {
            var mounts = Body(out _, out var low);
            mounts.Configure(root.transform, low);
            Assert.Throws<InvalidOperationException>(() => mounts.Validate());
        }

        [Test]
        public void VerticalForwardFailsClearly()
        {
            var mounts = Body(out var high, out _);
            high.rotation = Quaternion.Euler(-90f, 0f, 0f); // forward is straight up
            var error = Assert.Throws<InvalidOperationException>(() => mounts.Validate());
            StringAssert.Contains("straight up or down", error.Message);
            high.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down
            Assert.Throws<InvalidOperationException>(() => mounts.Validate());
        }

        [Test]
        public void SameTransformForBothMountsFails()
        {
            var mounts = Body(out var high, out _);
            mounts.Configure(high, high);
            Assert.Throws<InvalidOperationException>(() => mounts.Validate());
        }

        [Test]
        public void TryParseAcceptsOnlyTheTwoStoryNames()
        {
            Assert.IsTrue(PlayerCameraMounts.TryParse("High", out var high));
            Assert.AreEqual(PlayerCameraHeight.High, high);
            Assert.IsTrue(PlayerCameraMounts.TryParse("Low", out var low));
            Assert.AreEqual(PlayerCameraHeight.Low, low);
            Assert.IsFalse(PlayerCameraMounts.TryParse("high", out _));
            Assert.IsFalse(PlayerCameraMounts.TryParse("camera_start", out _));
        }

        [Test]
        public void PlaceViewMovesPlayerRootAndRigToTheMountAndClearsTracking()
        {
            var mounts = Body(out var high, out var low);
            var player = new GameObject("Player");
            var target = new GameObject("Target");
            try
            {
                var view = Rig(player, out var rig);
                player.transform.position = new Vector3(5, 5, 5);
                target.transform.position = new Vector3(0, 1, 10);

                view.Track(target.transform, 0f);
                Assert.IsTrue(view.IsTracking, "setup");

                mounts.PlaceView(view, PlayerCameraHeight.High);
                Assert.IsFalse(view.IsTracking);
                Assert.IsFalse(view.IsAcquiringTrack);
                Assert.That(Vector3.Distance(high.position, player.transform.position), Is.LessThan(Tolerance));
                Assert.That(Quaternion.Angle(high.rotation, rig.rotation), Is.LessThan(0.01f));

                mounts.PlaceView(view, PlayerCameraHeight.Low);
                Assert.That(Vector3.Distance(low.position, player.transform.position), Is.LessThan(Tolerance));
                Assert.That(Quaternion.Angle(low.rotation, rig.rotation), Is.LessThan(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void PlaceViewFollowsTheBodyWhenItMoves()
        {
            var mounts = Body(out _, out _);
            var player = new GameObject("Player");
            try
            {
                var view = Rig(player, out var rig);
                root.transform.SetPositionAndRotation(new Vector3(10, 0, 3), Quaternion.Euler(0, 90, 0));
                mounts.PlaceView(view, PlayerCameraHeight.High);
                // Hand computed: yaw 90 maps local (0,1.8,-0.9) to (-0.9,1.8,0), plus the body offset (10,0,3).
                Assert.That(Vector3.Distance(new Vector3(9.1f, 1.8f, 3f), player.transform.position), Is.LessThan(Tolerance));
                Assert.That(Vector3.Distance(Vector3.right, rig.forward), Is.LessThan(Tolerance));
            }
            finally { UnityEngine.Object.DestroyImmediate(player); }
        }

        [Test]
        public void MountPlanIsAboveTheHeadLevelAndAngledDown()
        {
            // Lying face up: head at z=-0.9, feet at z=+0.9, pelvis in the middle, 1 m above the floor.
            var plan = PlayerCameraMountsSetup.Plan(new Vector3(0, 1, -0.9f), new Vector3(0, 1, 0.9f), new Vector3(0, 1, 0));
            Assert.That(Vector3.Distance(new Vector3(0, 1.8f, -0.9f), plan.highPosition), Is.LessThan(Tolerance), "High is 0.8 m above the head");
            Assert.That(Vector3.Distance(new Vector3(0, 1.2f, -0.9f), plan.lowPosition), Is.LessThan(Tolerance), "Low is 0.2 m above the head");
            Assert.That(Vector3.Distance(Vector3.forward, plan.highRotation * Vector3.forward), Is.LessThan(Tolerance), "High looks level toward the feet");
            // Hand computed: from the Low mount toward the pelvis is (0,-0.2,0.9), normalized.
            Assert.That(Vector3.Distance(new Vector3(0, -0.2f, 0.9f).normalized, plan.lowRotation * Vector3.forward), Is.LessThan(Tolerance), "Low looks at the pelvis");
        }

        [Test]
        public void MountPlanIgnoresFootHeightWhenFindingTheLevelDirection()
        {
            var plan = PlayerCameraMountsSetup.Plan(new Vector3(1, 1, 1), new Vector3(1, 1.3f, 3), new Vector3(1, 1, 2));
            Assert.That(Vector3.Distance(Vector3.forward, plan.highRotation * Vector3.forward), Is.LessThan(Tolerance));
        }

        [Test]
        public void MountPlanRejectsAStandingBody()
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                PlayerCameraMountsSetup.Plan(new Vector3(0, 1.7f, 0), new Vector3(0, 0, 0), new Vector3(0, 1, 0)));
            StringAssert.Contains("not lying", error.Message);
        }
    }
}
