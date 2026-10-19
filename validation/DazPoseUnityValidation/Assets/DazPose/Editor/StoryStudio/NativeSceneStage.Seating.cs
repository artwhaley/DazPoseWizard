using System;
using UnityEngine;
namespace DazPose.Editor.Studio
{
    public sealed partial class NativeSceneStage
    {
        public NativeSeatObservation LastSeating {get;private set;}
        public void Apply(NativeSeatedProjection projection)
        {
            if(projection==null)throw new ArgumentNullException(nameof(projection));
            LastSeating=null;lease.Apply(()=>StageSeated(projection));
        }
        void StageSeated(NativeSeatedProjection p)
        {
            RequireAnchor(p.CameraMark,"Camera.StartAt placement");
            if(p.Seat==null)throw new InvalidOperationException("Assign Lounger's PerformerSeat.");
            RequireAnchor(p.Seat.SeatAnchor,"Lounger.SeatAnchor");
            RequireAnchor(p.Seat.ApproachAnchor,"Lounger.ApproachAnchor");
            var frame=NativeSeatEndpoint.Resolve(Context,p);
            var root=Context.Lara.transform;
            NativeSeatingMath.RequireUsableScale(p.ActorScale,"Temporary actor scale probe");
            root.localScale=Vector3.Scale(root.localScale,p.ActorScale); // Baseline restored first: never accumulate.
            lease.Sample(frame.Clip,frame.Time);
            root.rotation=frame.SeatYaw;
            root.position=NativeSeatingMath.BakedRoot(frame.Seat,frame.SeatYaw,root.lossyScale,frame.Profile.SitStart.PelvisOffsetAt(1));
            Vector3 expected;
            if(frame.AlignHip)
            {
                root.position=NativeSeatingMath.AlignLiveHip(root.position,frame.Seat,frame.Hip.position);
                expected=frame.Seat;
            }
            else
            {
                // Default sitting keeps the baked SitStart landing root. Do not silently hip-correct its loop.
                float phase=p.Seat.DefaultStyle==DazPose.Performer.PerformerSeatedStyle.Basic?1:0;
                expected=root.TransformPoint(frame.DefaultMotion.PelvisOffsetAt(phase));
            }
            LastSeating=new NativeSeatObservation{command=p.Command.ToString(),style=p.Seat.DefaultStyle.ToString(),clip=frame.Clip.name,
                sampleSeconds=frame.Time,hipPath=NativeStageContext.ObjectPath(frame.Hip),seatPath=NativeStageContext.ObjectPath(p.Seat.SeatAnchor),
                actorRoot=root.position,actorWorldScale=root.lossyScale,actorRotation=root.rotation,hip=frame.Hip.position,seat=frame.Seat,expectedHip=expected,approach=frame.Approach,
                hipToSeatMeters=NativeSeatingMath.Error(frame.Seat,frame.Hip.position),expectedHipResidualMeters=NativeSeatingMath.Error(expected,frame.Hip.position),
                toleranceMeters=frame.AlignHip?NativeSeatingMath.PersistentContactTolerance:NativeSeatingMath.BakedResidualTolerance,
                facingDisagreementDegrees=Quaternion.Angle(frame.ApproachYaw,frame.SeatYaw),warning=frame.Warning,liveHipAligned=frame.AlignHip,
                rule=frame.AlignHip?"PerformerSeating.AnchorPersistentPose: sampled hip world correction":"PerformerSeating.BeginSitStart landing root + default baked rest (no persistent hip correction)"};
            NativeSeatingMath.RequireContact(LastSeating.expectedHipResidualMeters,LastSeating.toleranceMeters,p.Command.ToString());
            Present(false,p.HideBody,null,p.CameraMark,p.TrackLara);
        }
    }
}
