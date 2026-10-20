using DazPose.Performer;
namespace DazPose.Wardrobe.Tests;
public sealed class PerformanceScopeTests {
 [Fact] public void ChildCleanupLeavesParentAndReplacedResourcesAlone() {
  using var root=new PerformanceOperationScope(0,null,0);using var child=new PerformanceOperationScope(1,"A",1,root);
  int parent=0,old=0,current=0;root.Register("pose",()=>parent++);child.Register("pose",()=>old++);child.Register("pose",()=>current++);
  child.ReleaseWriters();Assert.Equal(0,parent);Assert.Equal(0,old);Assert.Equal(1,current);Assert.Equal(1,root.WriterCount);
 }
 [Fact] public void RestoredGenerationTransfersOnceAndParentStopOwnsIt() {
  using var root=new PerformanceOperationScope(0,null,0);var child=new PerformanceOperationScope(1,"A",1,root);int old=0,restored=0;
  root.Register("pose",()=>old++);child.BeginReturn();child.Register("pose",()=>restored++);child.AdoptInto(root);child.Dispose();child.Dispose();
  Assert.Equal(0,restored);Assert.Throws<InvalidOperationException>(()=>child.BeginReturn());root.Cancel();Assert.Equal(0,old);Assert.Equal(1,restored);
 }
 [Fact] public void DelayedRegistrationStaysWithCapturedOwnerAndHistoryIsBounded() {
  using var root=new PerformanceOperationScope(0,null,0);using var child=new PerformanceOperationScope(1,"A",1,root);var captured=root;int released=0;
  captured.Register("pose",()=>released++);child.Dispose();Assert.Equal(0,released);
  for(int i=0;i<1000;i++)root.Register("pose",()=>released++);Assert.Equal(1,root.WriterCount);root.Cancel();Assert.Equal(1,released);
 }
 [Fact] public void ParentCancellationInvalidatesChildAndCannotAdoptAfterStop() {
  using var root=new PerformanceOperationScope(0,null,0);using var child=new PerformanceOperationScope(1,"A",1,root);
  child.BeginReturn();root.Cancel();Assert.True(child.Cancellation.IsCancellationRequested);Assert.Throws<OperationCanceledException>(()=>child.AdoptInto(root));
 }
 [Fact] public void ReturningNoChangeTransfersWritersWithoutRestartingThem() {
  using var root=new PerformanceOperationScope(0,null,0);using var card=new PerformanceOperationScope(1,"NoChange",1,root);int pose=0,action=0;
  card.Register("body-pose",()=>pose++);card.Register("action:ready",()=>action++);card.BeginReturn();card.ReleaseWritersExcept(key=>key=="body-pose");
  Assert.Equal(0,pose);Assert.Equal(1,action);card.AdoptInto(root);card.Dispose();Assert.Equal(0,pose);root.Cancel();Assert.Equal(1,pose);
 }
 [Fact] public void ReplacedControlCanRelinquishHandleWithoutSnappingItsValue() {
  using var root=new PerformanceOperationScope(0,null,0);using var card=new PerformanceOperationScope(1,"A",1,root);int gaze=0;
  card.Register("gaze",()=>gaze=0);gaze=1;card.Forget("gaze");card.ReleaseWriters();Assert.Equal(1,gaze);
 }
 [Fact] public void LateCompletionAfterDisposedOwnerCannotInstallWriter() {
  var owner=new PerformanceOperationScope(0,null,0);owner.Dispose();int cancelled=0;owner.Register("pose",()=>cancelled++);Assert.Equal(1,cancelled);Assert.Equal(0,owner.WriterCount);
 }
}
