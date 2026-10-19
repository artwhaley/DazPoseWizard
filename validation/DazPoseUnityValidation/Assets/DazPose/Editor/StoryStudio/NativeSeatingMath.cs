using System;
using UnityEngine;
namespace DazPose.Editor.Studio
{
    public enum NativeSeatCommand { DefaultSitAt, PersistentSitPose, SeatedDissolve }
    public static class NativeSeatingMath
    {
        public static bool AlignsLiveHip(NativeSeatCommand command)
        {
            switch(command)
            {
                case NativeSeatCommand.DefaultSitAt:return false;
                case NativeSeatCommand.PersistentSitPose:
                case NativeSeatCommand.SeatedDissolve:return true;
                default:throw new InvalidOperationException("Unknown seated endpoint command.");
            }
        }
        public static float BakedSampleSeconds(bool crossLegs,float duration)=>crossLegs?0:duration;
        public const float BakedResidualTolerance=.01f;
        public const float PersistentContactTolerance=.001f;
        // PerformerSeating.BeginSitStart + ScaleOffsetForRotation. Default sitting retains this root.
        public static Vector3 BakedRoot(Vector3 seat,Quaternion seatYaw,Vector3 actorLossyScale,Vector3 sitStartPelvis)
            => seat-seatYaw*Vector3.Scale(actorLossyScale,sitStartPelvis);
        // PerformerSeating.AnchorPersistentPose: live sampled world hip, not a generic baked offset.
        public static Vector3 AlignLiveHip(Vector3 actorRoot,Vector3 seat,Vector3 sampledHip)
            => actorRoot+(seat-sampledHip);
        public static float Error(Vector3 expected,Vector3 actual)=>Mathf.Sqrt((expected-actual).sqrMagnitude);
        public static void RequireUsableScale(Vector3 scale,string role)
        {
            foreach(float v in new[]{scale.x,scale.y,scale.z})
                if(float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v)<.00001f)
                    throw new InvalidOperationException(role+" has a zero/nonfinite world scale; restore a usable scale before seating.");
        }
        public static void RequireContact(float residual,float tolerance,string cue)
        {
            if(float.IsNaN(residual) || float.IsInfinity(residual) || residual>tolerance)
                throw new InvalidOperationException(cue+": seating residual "+residual.ToString("0.000000")+" m exceeds "+tolerance.ToString("0.000000")+" m. Check the saved rig, baked profile and seat anchors; no fallback pose is staged.");
        }
    }
}
