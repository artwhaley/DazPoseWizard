using System;
using System.Linq;
using DazPose.Performer;
using DazPose.Story;
using UnityEngine;
namespace DazPose.Editor.Studio
{
    public sealed class NativeSeatedProjection
    {
        public PerformerSeat Seat {get;}
        public Transform CameraMark {get;}
        public NativeSeatCommand Command {get;}
        public PerformerPose Pose {get;}
        public PlayerPosture Posture {get;}
        public bool TrackLara {get;}
        public bool HideBody {get;}
        public Vector3 ActorScale {get;}
        public NativeSeatedProjection(PerformerSeat seat,Transform cameraMark,NativeSeatCommand command,
            PerformerPose pose=null,PlayerPosture posture=PlayerPosture.Unknown,bool trackLara=true,bool hideBody=true,Vector3? actorScale=null)
        {Seat=seat;CameraMark=cameraMark;Command=command;Pose=pose;Posture=posture;TrackLara=trackLara;HideBody=hideBody;ActorScale=actorScale ?? Vector3.one;}
    }
    [Serializable] public sealed class NativeSeatObservation
    {
        public string command,style,clip,hipPath,seatPath,warning,rule;
        public float sampleSeconds,hipToSeatMeters,expectedHipResidualMeters,toleranceMeters,facingDisagreementDegrees;
        public Vector3 actorRoot,actorWorldScale,hip,seat,expectedHip,approach;
        public Quaternion actorRotation;
        public bool liveHipAligned;
    }
    /// <summary>Read-only runtime seat-frame/profile resolver; never bakes or rewrites an asset.</summary>
    internal sealed class NativeSeatEndpoint
    {
        public Vector3 Approach,Seat;
        public Quaternion ApproachYaw,SeatYaw;
        public PerformerSeatingProfile Profile;
        public PerformerSeatingMotion DefaultMotion;
        public Transform Hip;
        public AnimationClip Clip;
        public float Time;
        public bool AlignHip;
        public string Warning;
        public static NativeSeatEndpoint Resolve(NativeStageContext c,NativeSeatedProjection p)
        {
            if(p==null || p.Seat==null || p.Seat.gameObject.scene!=c.Scene)
                throw new InvalidOperationException("Assign Lounger's PerformerSeat in the host scene.");
            if(!Enum.IsDefined(typeof(NativeSeatCommand),p.Command))throw new InvalidOperationException("Unknown seated endpoint command.");
            var result=new NativeSeatEndpoint();
            if(!p.Seat.TryCaptureFrames(out result.Approach,out result.ApproachYaw,out result.Seat,out result.SeatYaw,out string reason))
                throw new InvalidOperationException(p.Command+"(Lounger): "+reason);
            result.Profile=p.Seat.SeatingProfile;
            NativeSeatingMath.RequireUsableScale(c.Lara.transform.lossyScale,"Lara");
            NativeSeatingMath.RequireUsableScale(p.Seat.SeatAnchor.lossyScale,"SeatAnchor");
            var hips=c.Lara.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="hip").ToArray();
            if(hips.Length!=1)throw new InvalidOperationException("Lara must have exactly one real 'hip' transform, matching PerformerSeating.SetPersistentPose (found "+hips.Length+").");
            result.Hip=hips[0];
            float disagreement=Quaternion.Angle(result.ApproachYaw,result.SeatYaw);
            result.Warning=disagreement>p.Seat.FacingDisagreementWarningDegrees
                ?"ApproachAnchor and SeatAnchor yaw differ by "+disagreement.ToString("0.0")+" degrees. Settled placement follows SeatAnchor; inspect the approach facing separately.":null;
            result.AlignHip=NativeSeatingMath.AlignsLiveHip(p.Command);
            if(result.AlignHip)
            {
                if(p.Posture!=PlayerPosture.Sitting)throw new InvalidOperationException(p.Command+" requires Sitting posture metadata on the source-bound pose.");
                if(p.Pose==null || p.Pose.Clip==null)throw new InvalidOperationException("Assign the source-bound Lounging PerformerPose/Clip.");
                result.Clip=p.Pose.Clip;result.Time=0;
            }
            else
            {
                switch(p.Seat.DefaultStyle)
                {
                    case PerformerSeatedStyle.CrossLegs:result.DefaultMotion=result.Profile.CrossLegsLoop;result.Time=NativeSeatingMath.BakedSampleSeconds(true,result.DefaultMotion.DurationSeconds);break;
                    case PerformerSeatedStyle.Basic:result.DefaultMotion=result.Profile.SitStart;result.Time=NativeSeatingMath.BakedSampleSeconds(false,result.DefaultMotion.DurationSeconds);break;
                    default:throw new InvalidOperationException("Unknown default seated style; choose Basic or CrossLegs on PerformerSeat.");
                }
                result.Clip=result.DefaultMotion.BodyClip;
            }
            if(result.Clip==null || result.Clip.empty || result.Clip.legacy)
                throw new InvalidOperationException(p.Command+" requires its actual nonempty Generic clip; no neutral substitute is available.");
            if(result.Time>result.Clip.length+.001f)throw new InvalidOperationException("Baked motion duration exceeds its clip length. Repair the baked seating data through the authorized content workflow.");
            return result;
        }
    }
}
