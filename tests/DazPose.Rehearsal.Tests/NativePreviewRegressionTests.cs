using DazPose.Editor.Studio;
using DazPose.Player;
using UnityEngine;
using NQ=System.Numerics.Quaternion;
namespace DazPose.Rehearsal.Tests;
public sealed class NativePreviewRegressionTests
{
    sealed class Graph : IDisposable
    {
        public int Samples,Suspensions,Destructions;
        public void Sample()=>Samples++;
        public void Suspend()=>Suspensions++;
        public void Dispose()=>Destructions++;
    }
    [Fact] public void ReprojectionRetainsOwnedGraphDespiteStaleNativeBoundFlag()
    {
        bool nativeBound=false;int creations=0;var owner=new PreviewResourceOwner<Graph>();
        Graph Create(){if(nativeBound)throw new InvalidOperationException("Another graph owns Animator");creations++;nativeBound=true;return new Graph();}
        Graph graph=owner.GetOrCreate(Create);
        for(int i=0;i<100;i++)
        {
            owner.WithExisting(g=>g.Suspend()); // Native flag deliberately stays set until a later editor update.
            Assert.Same(graph,owner.GetOrCreate(Create));owner.GetOrCreate(Create).Sample();
        }
        Assert.Equal(1,creations);Assert.Equal(100,graph.Samples);Assert.Equal(0,graph.Destructions);
        owner.Dispose();owner.Dispose();Assert.Equal(1,graph.Destructions);
    }
    [Fact] public void NormalAndForcedSaveCanSuspendThenResumeSameProjectionWithoutRestart()
    {
        int actor=1,mark=3,creations=0;var owner=new PreviewResourceOwner<Graph>();var ledger=new PreviewPropertyLedger();
        ledger.Capture("actor",()=>actor,v=>actor=v);
        Graph Create(){creations++;return new Graph();}
        void Stage(){owner.GetOrCreate(Create).Sample();actor=mark;ledger.RecordApplied();}
        Stage();Graph graph=owner.GetOrCreate(Create);
        for(int save=0;save<2;save++)
        {
            ledger.PrepareRestore();owner.WithExisting(g=>g.Suspend());ledger.Restore();Assert.Equal(1,actor);
            mark+=2;Stage();Assert.Equal(mark,actor);Assert.Same(graph,owner.GetOrCreate(Create));
        }
        Assert.Equal(1,creations);Assert.Equal(3,graph.Samples);owner.Dispose();
    }
    [Fact] public void SuspensionBeforeFirstSampleDoesNotAllocateGraph()
    {var owner=new PreviewResourceOwner<Graph>();bool invoked=false;owner.WithExisting(g=>invoked=true);owner.Dispose();Assert.False(invoked);}
    [Fact] public void ForeignGraphIsStillRefusedAtInitialAcquisition()
    {
        var owner=new PreviewResourceOwner<Graph>();bool foreign=true;
        Graph Create()=>foreign?throw new InvalidOperationException("foreign graph"):new Graph();
        Assert.Throws<InvalidOperationException>(()=>owner.GetOrCreate(Create));foreign=false;
        Assert.NotNull(owner.GetOrCreate(Create));owner.Dispose();
    }
    [Fact] public void StoppedLeaseCannotReacquireGraph()
    {var owner=new PreviewResourceOwner<Graph>();owner.Dispose();Assert.Throws<ObjectDisposedException>(()=>owner.GetOrCreate(()=>new Graph()));}
    [Fact] public void UnityEmptyStringNormalizationDoesNotStopAnOtherwiseUnchangedSave()
    {
        string original="%YAML 1.1\r\nMonoBehaviour:\r\n  stopBeforePhase: ''\r\n  value: 4\r\n";
        string saved="%YAML 1.1\nMonoBehaviour:\n  stopBeforePhase: \n  value: 4\n";
        Assert.True(NativeSceneSaveComparison.Equivalent(original,saved));
    }
    [Fact] public void PreviewOnlyPrefabOverrideIsNotIgnoredBySaveComparison()
    {
        string before="m_Modifications: []\n";
        string after="m_Modifications:\n- propertyPath: m_UpdateWhenOffscreen\n  value: 0\n";
        Assert.False(NativeSceneSaveComparison.Equivalent(before,after));
    }
    [Fact] public void SceneTransformMaterialAndReferenceChangesStillFailComparison()
    {
        foreach(string field in new[]{"m_LocalPosition: {x: 1}","material: {fileID: 4}","m_LocalRotation: {w: 0.5}"})
            Assert.False(NativeSceneSaveComparison.Equivalent("field: old\n",field+"\n"));
    }
    [Fact] public void NonemptyStringAndBlockScalarWhitespaceArePreserved()
    {
        Assert.False(NativeSceneSaveComparison.Equivalent("name: 'x'\n","name: \n"));
        Assert.False(NativeSceneSaveComparison.Equivalent("text: |\n  x \n","text: |\n  x\n"));
    }
    [Fact] public void CameraStartAtPreservesNewAnchorPitchYawAndRoll()
    {
        var oldMark=new Quaternion{value=NQ.CreateFromYawPitchRoll(.2f,.1f,.3f)};
        var newMark=new Quaternion{value=NQ.CreateFromYawPitchRoll(1.1f,-.5f,-.3f)};
        var target=new Vector3(1,2,3);var origin=new Vector3(0,0,0);
        var oldShot=PlayerViewEndpoint.ProjectionRotation(false,oldMark,target,origin);
        var newShot=PlayerViewEndpoint.ProjectionRotation(false,newMark,target,origin);
        Assert.Equal(oldMark.value,oldShot.value);Assert.Equal(newMark.value,newShot.value);Assert.NotEqual(oldShot.value,newShot.value);
    }
    [Fact] public void TrackingExplicitlyOverridesMarkRotationAtFixedOrigin()
    {
        var a=new Quaternion{value=NQ.CreateFromYawPitchRoll(.2f,.1f,.3f)};
        var b=new Quaternion{value=NQ.CreateFromYawPitchRoll(1.1f,-.5f,-.3f)};
        var target=new Vector3(1,2,3);var origin=new Vector3(0,0,0);
        Assert.Equal(PlayerViewEndpoint.ProjectionRotation(true,a,target,origin).value,PlayerViewEndpoint.ProjectionRotation(true,b,target,origin).value);
        Assert.NotEqual(b.value,PlayerViewEndpoint.ProjectionRotation(true,b,target,origin).value);
    }
}
