using System;
using DazPose.Editor.Studio;
using NUnit.Framework;
using UnityEngine;

namespace DazPose.Tests
{
    public sealed class PlacementMathTests
    {
        // S01 changes this positive marker to force one incremental compilation, then restores it.
        const int IncrementalMarker = 1;
        const float Tolerance = 0.0001f;

        static void EqualVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), "y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance), "z");
        }

        [Test]
        public void BakedRoot_UsesEachScaleAxis()
        {
            Assert.That(IncrementalMarker, Is.GreaterThan(0));
            // Hand computed: (10,5,-2) - (2,6,12) = (8,-1,-14).
            EqualVector(new Vector3(8, -1, -14), NativeSeatingMath.BakedRoot(
                new Vector3(10, 5, -2), Quaternion.identity,
                new Vector3(2, 3, 4), new Vector3(1, 2, 3)));
        }

        [Test]
        public void BakedRoot_RotatesScaledPelvisBySeatYaw()
        {
            // Scaled pelvis (2,6,12), yaw 90 => (12,6,-2); (3,5,7) minus that => (-9,-1,9).
            EqualVector(new Vector3(-9, -1, 9), NativeSeatingMath.BakedRoot(
                new Vector3(3, 5, 7), Quaternion.Euler(0, 90, 0),
                new Vector3(2, 3, 4), new Vector3(1, 2, 3)));
        }

        [Test]
        public void BakedRoot_ZeroPelvisLeavesSeatPosition()
        {
            EqualVector(new Vector3(4, 2, -3), NativeSeatingMath.BakedRoot(
                new Vector3(4, 2, -3), Quaternion.Euler(0, 45, 0), Vector3.one, Vector3.zero));
        }

        [Test]
        public void AlignLiveHip_AppliesWorldContactDelta()
        {
            // (3,0,1) + ((4,2,8) - (2,1,5)) = (5,1,4).
            EqualVector(new Vector3(5, 1, 4), NativeSeatingMath.AlignLiveHip(
                new Vector3(3, 0, 1), new Vector3(4, 2, 8), new Vector3(2, 1, 5)));
        }

        [Test]
        public void AlignLiveHip_AlreadyAtSeatDoesNotMoveRoot()
        {
            EqualVector(new Vector3(-2, 4, 9), NativeSeatingMath.AlignLiveHip(
                new Vector3(-2, 4, 9), new Vector3(1, 2, 3), new Vector3(1, 2, 3)));
        }

        [Test]
        public void Facing_FullMarkRetainsPitchAndRoll()
        {
            var mark = Quaternion.Euler(15, 35, 10);
            Assert.That(Quaternion.Angle(mark, NativeStandingRules.Facing(mark, Vector3.up, false)), Is.LessThan(Tolerance));
        }

        [Test]
        public void Facing_YawOnlyProjectsElevatedForwardOntoFloor()
        {
            var facing = NativeStandingRules.Facing(Quaternion.Euler(20, 10, 30), new Vector3(2, 3, 0), true);
            EqualVector(Vector3.right, facing * Vector3.forward);
            EqualVector(Vector3.up, facing * Vector3.up);
        }

        [Test]
        public void Facing_YawOnlyRejectsVerticalForward()
        {
            Assert.Throws<InvalidOperationException>(() => NativeStandingRules.Facing(Quaternion.identity, Vector3.up, true));
        }

        [Test]
        public void Facing_YawOnlyRejectsZeroForward()
        {
            Assert.Throws<InvalidOperationException>(() => NativeStandingRules.Facing(Quaternion.identity, Vector3.zero, true));
        }
    }
}
