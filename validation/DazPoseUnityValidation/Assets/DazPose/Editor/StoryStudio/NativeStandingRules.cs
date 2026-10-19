using System;
using UnityEngine;
namespace DazPose.Editor.Studio
{
    public static class NativeStandingRules
    {
        // SuccubusPerformer.StartHiddenAt / DissolveInAtAsync retain full mark rotation.
        // PerformerLocomotion.BeginFinalAlignment and PerformerDissolve use destination yaw.
        public static Quaternion Facing(Quaternion fullMarkRotation,Vector3 markForward,bool yawOnly)
        {
            if(!yawOnly)return fullMarkRotation;
            var forward=Vector3.ProjectOnPlane(markForward,Vector3.up);
            if(forward.sqrMagnitude<1e-8f)throw new InvalidOperationException("Standing mark has no horizontal facing.");
            return Quaternion.LookRotation(forward.normalized,Vector3.up);
        }
    }
}
