using System;
using System.Linq;
using DazPose.Performer;
using DazPose.Player;
using NUnit.Framework;
using UnityEngine;

namespace DazPose.Tests
{
    public sealed class PlayerBreathingTests
    {
        const float Tolerance = 0.00001f;
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null;
        }

        static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = Quaternion.Euler(1f, 2f, 3f); // a non-identity rest rotation
            return child;
        }

        // hip -> pelvis -> Penis1, and hip -> abdomenLower -> abdomenUpper -> chestLower -> chestUpper
        PlayerBreathing Body()
        {
            root = new GameObject("Body");
            var hip = Child(root.transform, "hip", new Vector3(0, 1, 0));
            var pelvis = Child(hip, "pelvis", new Vector3(0, 0, 0));
            Child(pelvis, "Penis1", new Vector3(0, -0.1f, 0.1f));
            var lower = Child(hip, "abdomenLower", new Vector3(0, 0.1f, 0));
            var upper = Child(lower, "abdomenUpper", new Vector3(0, 0.1f, 0));
            var chestLower = Child(upper, "chestLower", new Vector3(0, 0.1f, 0));
            Child(chestLower, "chestUpper", new Vector3(0, 0.1f, 0));
            return root.AddComponent<PlayerBreathing>();
        }

        Transform Find(string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        static BreathingBoneChannel One(string bone) =>
            new BreathingBoneChannel { boneName = bone, fullInhaleLocalRotationDelta = new Vector3(-1, 0, 0), fullInhaleLocalPositionDelta = new Vector3(0, 0.01f, 0) };

        [Test]
        public void FullInhaleMovesBonesByTheScaledDeltasOnTopOfRest()
        {
            var breathing = Body();
            var chest = Find("chestLower");
            var restPosition = chest.localPosition;
            var restRotation = chest.localRotation;
            breathing.Prepare();
            breathing.ApplyAtCycles(0.5f); // full inhale
            // Hand computed: strength 0.3, rotation (-0.4,0,0) -> (-0.12,0,0) degrees; position (0,0.0005,0) -> (0,0.00015,0).
            Assert.That(chest.localPosition.y - restPosition.y, Is.EqualTo(0.00015f).Within(Tolerance));
            Assert.That(Quaternion.Angle(restRotation * Quaternion.Euler(-0.12f, 0, 0), chest.localRotation), Is.LessThan(0.001f));
            // abdomenLower has no position delta.
            Assert.That(Find("abdomenLower").localPosition.y, Is.EqualTo(0.1f).Within(Tolerance));
        }

        [Test]
        public void NoDriftAfterManyCycles()
        {
            var breathing = Body();
            var chest = Find("chestLower");
            var restPosition = chest.localPosition;
            var restRotation = chest.localRotation;
            breathing.Prepare();
            var random = new System.Random(7);
            for (int i = 0; i < 20000; i++) breathing.ApplyAtCycles((float)random.NextDouble() * 50f);
            breathing.ApplyAtCycles(0f);
            Assert.That(Vector3.Distance(restPosition, chest.localPosition), Is.LessThan(Tolerance));
            Assert.That(Quaternion.Angle(restRotation, chest.localRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void TickAdvancesTheCycleAndRestoresRestWhenDisabled()
        {
            var breathing = Body();
            breathing.BreathsPerMinute = 60f; breathing.Strength = 5f;
            var presence = root.AddComponent<PlayerBodyPresence>();
            presence.Configure(Array.Empty<PlayerBodyDissolvePart>(), false); // visible
            var chest = Find("chestLower");
            var rest = chest.localPosition;
            breathing.Prepare();
            breathing.Tick(0.5f); // half a cycle at 60 per minute: full inhale
            Assert.That(chest.localPosition.y - rest.y, Is.EqualTo(0.0005f * 5f).Within(Tolerance));
            breathing.RestoreRest();
            Assert.That(Vector3.Distance(rest, chest.localPosition), Is.LessThan(Tolerance));
        }

        [Test]
        public void AStablyHiddenBodyDoesNotBreatheAndIsLeftAtRest()
        {
            var breathing = Body();
            breathing.BreathsPerMinute = 60f; breathing.Strength = 5f;
            var presence = root.AddComponent<PlayerBodyPresence>();
            presence.Configure(Array.Empty<PlayerBodyDissolvePart>(), true); // hidden
            var chest = Find("chestLower");
            var rest = chest.localPosition;
            breathing.Prepare();
            breathing.Tick(0.5f);
            Assert.That(Vector3.Distance(rest, chest.localPosition), Is.LessThan(Tolerance));
        }

        [TestCase("pelvis")]
        [TestCase("hip")]
        [TestCase("Penis1")]
        public void PelvisHipAndShaftBonesAreRejectedByName(string bone)
        {
            var breathing = Body();
            breathing.SetChannels(new[] { One("chestLower"), One(bone) });
            var error = Assert.Throws<InvalidOperationException>(() => breathing.Prepare());
            StringAssert.Contains(bone, error.Message);
            Assert.IsFalse(breathing.IsPrepared);
        }

        [Test]
        public void AnAncestorOfThePelvisIsRejectedEvenWithAnInnocentName()
        {
            var breathing = Body();
            var pelvis = Find("pelvis");
            var spine = new GameObject("spineBase").transform;
            spine.SetParent(pelvis.parent, false);
            pelvis.SetParent(spine, false);
            breathing.SetChannels(new[] { One("spineBase") });
            var error = Assert.Throws<InvalidOperationException>(() => breathing.Prepare());
            StringAssert.Contains("spineBase", error.Message);
            StringAssert.Contains("ancestor", error.Message);
        }

        [Test]
        public void AMissingBoneFailsNamingIt()
        {
            var breathing = Body();
            breathing.SetChannels(new[] { One("chestLower"), One("noSuchBone") });
            var error = Assert.Throws<InvalidOperationException>(() => breathing.Prepare());
            StringAssert.Contains("noSuchBone", error.Message);
        }

        [Test]
        public void ThePlayerBodyPostureDefaultsToLying()
        {
            root = new GameObject("Body");
            Assert.AreEqual(DazPose.Story.PlayerPosture.Lying, root.AddComponent<PlayerBodyPresence>().Posture);
        }

        [Test]
        public void TheDefaultChannelsAreLarasChestAndAbdomenValuesAndAreAccepted()
        {
            var breathing = Body();
            var names = PlayerBreathing.DefaultChannels().Select(c => c.boneName).ToArray();
            CollectionAssert.AreEqual(new[] { "abdomenLower", "abdomenUpper", "chestLower", "chestUpper" }, names);
            Assert.DoesNotThrow(() => breathing.Prepare());
            Assert.IsTrue(breathing.IsPrepared);
        }
    }
}
