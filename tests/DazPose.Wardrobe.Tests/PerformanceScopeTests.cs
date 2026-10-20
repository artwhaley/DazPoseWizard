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
}
