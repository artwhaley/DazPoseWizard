using DazPose.Editor.Studio;
using UnityEngine;
using NV=System.Numerics.Vector3;
using NQ=System.Numerics.Quaternion;
using M=System.Numerics.Matrix4x4;
namespace DazPose.Rehearsal.Tests;
public sealed class NativeSeatingTests
{
    static Vector3 V(NV v)=>new(v.X,v.Y,v.Z);
    static NV N(Vector3 v)=>new(v.x,v.y,v.z);
    [Theory]
    [InlineData(NativeSeatCommand.DefaultSitAt,false)]
    [InlineData(NativeSeatCommand.PersistentSitPose,true)]
    [InlineData(NativeSeatCommand.SeatedDissolve,true)]
    public void RuntimeEndpointFamiliesRemainDistinct(NativeSeatCommand command,bool correct)
        =>Assert.Equal(correct,NativeSeatingMath.AlignsLiveHip(command));
    [Fact] public void UnknownEndpointIsRejected()=>Assert.Throws<InvalidOperationException>(()=>NativeSeatingMath.AlignsLiveHip((NativeSeatCommand)99));
    [Fact] public void DefaultLoopAndBasicRestUseTheirOwnSampleTime()
    {Assert.Equal(0,NativeSeatingMath.BakedSampleSeconds(true,13));Assert.Equal(3,NativeSeatingMath.BakedSampleSeconds(false,3));}
    [Theory][InlineData(1,1,1,0)][InlineData(1.1f,.95f,1.2f,.8f)][InlineData(.7f,1.3f,1.1f,-1.1f)]
    public void DefaultBakedRootMatchesRuntimeScaleOffsetFormula(float sx,float sy,float sz,float yaw)
    {
        var seat=new Vector3(2,.6f,-1);var scale=new Vector3(sx,sy,sz);var offset=new Vector3(-.00019207368f,.9391111f,-.09671423f);
        var q=new Quaternion{value=NQ.CreateFromYawPitchRoll(yaw,0,0)};
        var actual=NativeSeatingMath.BakedRoot(seat,q,scale,offset);
        var expected=N(seat)-NV.Transform(N(offset)*new NV(sx,sy,sz),q.value);
        Assert.True(NV.Distance(N(actual),expected)<1e-6f);
    }
    [Fact] public void DefaultCrossLegsKeepsTheMeasuredBakedHipGapInsteadOfPretendingItIsPersistentAlignment()
    {
        var seat=new Vector3(0,.6f,0);var entry=new Vector3(-.00019207368f,.9391111f,-.09671423f);var loop=new Vector3(-.008643368f,.94614124f,-.12533292f);
        var root=NativeSeatingMath.BakedRoot(seat,Quaternion.identity,Vector3.one,entry);var actualHip=root+loop;
        Assert.InRange(NativeSeatingMath.Error(seat,actualHip),.03f,.031f);
        Assert.Equal(0,NativeSeatingMath.Error(root+loop,actualHip));
        Assert.NotEqual(root,NativeSeatingMath.AlignLiveHip(root,seat,actualHip));
    }
    [Theory][InlineData(1,1,1,0)][InlineData(1.1f,.95f,1.2f,.9f)][InlineData(.6f,1.4f,.8f,-1.2f)]
    public void LiveHipAlignmentUsesWorldPositionUnderScaledRotatedParent(float sx,float sy,float sz,float yaw)
    {
        var parent=M.CreateScale(sx,sy,sz)*M.CreateRotationY(yaw)*M.CreateTranslation(2,-.3f,1);
        var rootLocal=new NV(.7f,.2f,-.4f);var rootWorld=NV.Transform(rootLocal,parent);
        var boneChain=M.CreateScale(.9f,1.1f,.8f)*M.CreateRotationZ(.23f)*M.CreateTranslation(rootLocal);
        var hipWorld=NV.Transform(new NV(.2f,.8f,-.15f),boneChain*parent);var seat=new NV(-1,.6f,-2);
        var corrected=NativeSeatingMath.AlignLiveHip(V(rootWorld),V(seat),V(hipWorld));
        Assert.True(M.Invert(parent,out var inverse));var newRootLocal=NV.Transform(N(corrected),inverse);
        var correctedChain=M.CreateScale(.9f,1.1f,.8f)*M.CreateRotationZ(.23f)*M.CreateTranslation(newRootLocal);
        var finalHip=NV.Transform(new NV(.2f,.8f,-.15f),correctedChain*parent);
        Assert.True(NV.Distance(finalHip,seat)<2e-6f);
    }
    [Fact] public void RepeatedProjectionHasNoRootDriftAndDoesNotMoveSeatOrApproach()
    {
        var seat=new Vector3(1,.6f,2);var approach=new Vector3(4,0,3);var originalSeat=seat;var originalApproach=approach;
        var root=new Vector3(-1,0,1);var hip=new Vector3(-.8f,.9f,.7f);var first=NativeSeatingMath.AlignLiveHip(root,seat,hip);
        for(int i=0;i<100;i++)Assert.Equal(first,NativeSeatingMath.AlignLiveHip(root,seat,hip));
        Assert.Equal(originalSeat,seat);Assert.Equal(originalApproach,approach);
    }
    [Fact] public void ApproachOnlyShiftDoesNotEnterEitherSettledSolve()
    {
        var seat=new Vector3(1,.6f,2);var scale=Vector3.one;var offset=new Vector3(.1f,.9f,-.2f);
        var before=NativeSeatingMath.BakedRoot(seat,Quaternion.identity,scale,offset);var approach=new Vector3(0,0,0);approach+=new Vector3(3,1,-2);
        Assert.Equal(before,NativeSeatingMath.BakedRoot(seat,Quaternion.identity,scale,offset));
        var custom=NativeSeatingMath.AlignLiveHip(before,seat,before+offset);
        Assert.Equal(custom,NativeSeatingMath.AlignLiveHip(before,seat,before+offset));
    }
    [Theory][InlineData(0)][InlineData(float.NaN)][InlineData(float.PositiveInfinity)]
    public void SingularOrNonfiniteScaleFails(float bad)=>Assert.Throws<InvalidOperationException>(()=>NativeSeatingMath.RequireUsableScale(new Vector3(1,bad,1),"actor"));
    [Fact] public void InvalidResidualFailsInsteadOfStagingFloatingAvatar()
    {Assert.Throws<InvalidOperationException>(()=>NativeSeatingMath.RequireContact(.02f,.001f,"Lounging"));Assert.Throws<InvalidOperationException>(()=>NativeSeatingMath.RequireContact(float.NaN,.001f,"Lounging"));NativeSeatingMath.RequireContact(.00001f,.001f,"Lounging");}
    [Fact] public void PersistentCorrectionWorksFromDifferentInitialRootPlacements()
    {
        var seat=new Vector3(1,.6f,2);var offset=new Vector3(.2f,.8f,-.1f);
        var a=new Vector3(-1,1,4);var b=new Vector3(8,-3,-2);
        var solvedA=NativeSeatingMath.AlignLiveHip(a,seat,a+offset);var solvedB=NativeSeatingMath.AlignLiveHip(b,seat,b+offset);
        Assert.True(NativeSeatingMath.Error(solvedA,solvedB)<1e-6f);
    }
    [Fact] public void TemporaryActorScaleRestoresBeforeSaveAndDoesNotAccumulate()
    {
        var baseline=new Vector3(.9f,1.1f,.8f);var scale=baseline;var probe=new Vector3(1.1f,.95f,1.2f);var ledger=new PreviewPropertyLedger();
        ledger.Capture("actor.localScale",()=>scale,v=>scale=v);
        for(int i=0;i<100;i++)
        {
            scale=Vector3.Scale(scale,probe);ledger.RecordApplied();Assert.Equal(Vector3.Scale(baseline,probe),scale);
            ledger.PrepareRestore();ledger.Restore();Assert.Equal(baseline,scale);
        }
    }
    [Fact] public void ActualFirstContactKeepsFirstWarmupSitAndLaterMainSeatedDissolveOracles()
    {
        string source=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"FirstContact.dazstory"));
        string trace=NativeSeatingSourceOracle.Describe("FirstContact.dazstory",source);
        Assert.Contains("Warmup SitAt line",trace);Assert.Contains("Main Standing draw line",trace);Assert.Contains("Fixed endpoints, not replay",trace);
        Assert.Throws<InvalidOperationException>(()=>NativeSeatingSourceOracle.Describe("changed",source.Replace("Lara.PersistentSitPose(Lounging)","Lara.PersistentPose(Lounging)")));
        Assert.Throws<InvalidOperationException>(()=>NativeSeatingSourceOracle.Describe("changed",source.Replace("tags: [Main, Standing]","tags: [Warmup]")));
    }
}
