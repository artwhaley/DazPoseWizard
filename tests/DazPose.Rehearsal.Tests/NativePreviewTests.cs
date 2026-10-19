using DazPose.Editor.Studio;
using DazPose.Player;
using UnityEngine;
using NV=System.Numerics.Vector3;
using NQ=System.Numerics.Quaternion;
namespace DazPose.Rehearsal.Tests;
public sealed class NativePreviewTests
{
    [Fact] public void OwnedPropertyRestoresAndUnownedAuthorEditRemains()
    {
        int actor=1,mark=2;var ledger=new PreviewPropertyLedger();
        ledger.Capture("actor",()=>actor,v=>actor=v);ledger.Capture("mark",()=>mark,v=>mark=v);
        actor=8;ledger.RecordApplied();mark=15;
        Assert.Empty(ledger.Conflicts());ledger.PrepareRestore();ledger.Restore();Assert.Equal(1,actor);Assert.Equal(15,mark);
    }
    [Fact] public void RepeatedBaselinePlusProjectionHasNoDrift()
    {
        int position=4;var ledger=new PreviewPropertyLedger();ledger.Capture("root",()=>position,v=>position=v);
        for(int i=0;i<100;i++){position+=3;ledger.RecordApplied();Assert.Equal(7,position);ledger.PrepareRestore();ledger.Restore();Assert.Equal(4,position);}
    }
    [Fact] public void ExternalOwnedEditIsReportedAndPreservedAcrossAnimationModeRestoration()
    {
        int value=1;var ledger=new PreviewPropertyLedger();ledger.Capture("bone",()=>value,v=>value=v);
        value=5;ledger.RecordApplied();value=9;Assert.Equal(new[]{"bone"},ledger.Conflicts());
        ledger.PrepareRestore();value=1; // Simulates StopAnimationMode restoring its own initial value.
        ledger.Restore();Assert.Equal(9,value);
    }
    [Fact] public void RestoreRunsAllSettersEvenWhenOneThrows()
    {
        int a=0,b=0;var ledger=new PreviewPropertyLedger();
        ledger.Capture("bad",()=>a,v=>throw new InvalidOperationException());ledger.Capture("good",()=>b,v=>b=v);
        a=b=10;ledger.RecordApplied();ledger.PrepareRestore();Assert.Throws<AggregateException>(()=>ledger.Restore());Assert.Equal(0,b);
    }
    [Fact] public void InterruptedPartialProjectionRestoresVisibilityMaterialAndBodyIndependently()
    {
        bool visible=true;object material=new(),original=material;int maleBody=12;
        var ledger=new PreviewPropertyLedger();ledger.Capture("visible",()=>visible,v=>visible=v);ledger.Capture("material",()=>material,v=>material=v);ledger.Capture("male",()=>maleBody,v=>maleBody=v);
        try{try{visible=false;material=new();throw new InvalidOperationException("sampling failed");}finally{ledger.RecordApplied();}}
        catch(InvalidOperationException){}
        ledger.PrepareRestore();ledger.Restore();Assert.True(visible);Assert.Same(original,material);Assert.Equal(12,maleBody);
    }
    [Fact] public void SeparateMaterialSlotsPreserveAnAuthorReplacement()
    {
        object a=new(),b=new(),originalA=a,authoredB=new();var ledger=new PreviewPropertyLedger();
        ledger.Capture("slot0",()=>a,v=>a=v);ledger.Capture("slot1",()=>b,v=>b=v);
        a=new();b=new();ledger.RecordApplied();b=authoredB;ledger.PrepareRestore();ledger.Restore();Assert.Same(originalA,a);Assert.Same(authoredB,b);
    }
    [Fact] public void SaveSuspendAndResumeAdoptNewAuthoredBaseline()
    {
        int root=1,mark=3;var ledger=new PreviewPropertyLedger();ledger.Capture("root",()=>root,v=>root=v);ledger.Capture("mark",()=>mark,v=>mark=v);
        root=10;ledger.RecordApplied();mark=7;ledger.PrepareRestore();ledger.Restore();Assert.Equal(1,root);Assert.Equal(7,mark);
        root=20;ledger.RecordApplied();ledger.PrepareRestore();ledger.Restore();Assert.Equal(1,root);Assert.Equal(7,mark);
    }
    [Fact] public void DeletedObjectCannotPreventSurvivingObjectCleanup()
    {
        bool deleted=false;int removed=1,survivor=2;var ledger=new PreviewPropertyLedger();
        ledger.Capture("deleted",()=>deleted?throw new InvalidOperationException("destroyed"):removed,v=>{if(!deleted)removed=v;});
        ledger.Capture("survivor",()=>survivor,v=>survivor=v);
        removed=survivor=8;ledger.RecordApplied();deleted=true;
        Assert.Contains("deleted",ledger.Conflicts());ledger.PrepareRestore();ledger.Restore();Assert.Equal(2,survivor);
    }
    [Fact] public void CameraDegenerateTargetRetainsRigRotation()
    {var q=new Quaternion{value=NQ.CreateFromYawPitchRoll(.3f,.1f,0)};var v=new Vector3(1,2,3);Assert.Equal(q.value,PlayerViewEndpoint.DesiredRotation(v,v,q).value);}
    [Theory]
    [InlineData(0,0,0,1,2,3)]
    [InlineData(2,1,-3,-1,4,8)]
    [InlineData(0,2,0,0,8,0)]
    [InlineData(0,2,0,0,-8,0)]
    public void SharedCalculatorMatchesOriginalRuntimeFormula(float x,float y,float z,float tx,float ty,float tz)
    {
        var origin=new Vector3(x,y,z);var target=new Vector3(tx,ty,tz);var direction=(target-origin).normalized;
        var up=MathF.Abs(Vector3.Dot(direction,Vector3.up))>.999f?Vector3.forward:Vector3.up;
        var expected=Quaternion.LookRotation(direction,up);var actual=PlayerViewEndpoint.DesiredRotation(target,origin,Quaternion.identity);
        Assert.True(MathF.Abs(NQ.Dot(expected.value,actual.value))>.999999f);
        var forward=NV.Transform(NV.UnitZ,actual.value);Assert.True(NV.Dot(forward,new NV(direction.x,direction.y,direction.z))>.99999f);
    }
    [Theory][InlineData(0,0,0)][InlineData(.2f,.1f,-.35f)]
    public void CameraAimUsesActualCameraOriginIncludingNonzeroChildOffsets(float x,float y,float z)
    {
        var root=new NV(2,1,-4);var rig=NQ.CreateFromYawPitchRoll(.5f,.2f,0);var camera=root+NV.Transform(new NV(x,y,z),rig);var target=new Vector3(-1,2,3);
        var result=PlayerViewEndpoint.DesiredRotation(target,new Vector3(camera.X,camera.Y,camera.Z),new Quaternion{value=rig});
        var expected=NV.Normalize(new NV(target.x,target.y,target.z)-camera);Assert.True(NV.Dot(expected,NV.Transform(NV.UnitZ,result.value))>.99999f);
    }
    [Fact] public void FullMarkRotationAndYawAreDistinctCommandPolicies()
    {
        var q=new Quaternion{value=NQ.CreateFromYawPitchRoll(.7f,.4f,.2f)};var f=NV.Transform(NV.UnitZ,q.value);var direction=new Vector3(f.X,f.Y,f.Z);
        Assert.Equal(q.value,NativeStandingRules.Facing(q,direction,false).value);
        var yaw=NativeStandingRules.Facing(q,direction,true);Assert.True(MathF.Abs(NV.Transform(NV.UnitZ,yaw.value).Y)<1e-6f);
        Assert.Throws<InvalidOperationException>(()=>NativeStandingRules.Facing(q,Vector3.up,true));
    }
}
