using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace DazPose.Performer {
 internal enum PerformanceScopeState { Open, Returning, Returned, Cancelled, Faulted, Disposed }
 // A run-local lifetime. Registrations are keyed by resource, never a history of completed commands.
 internal sealed class PerformanceOperationScope : IDisposable {
  readonly Dictionary<string,Action> writers=new Dictionary<string,Action>(StringComparer.Ordinal);
  readonly CancellationTokenSource lifetime;
  public long Id {get;} public string CardId {get;} public long InvocationId {get;}
  public PerformanceOperationScope Parent {get;}
  public PerformanceScopeState State {get;private set;}
  public CancellationToken Cancellation=>lifetime.Token;
  public int WriterCount=>writers.Count;
  public PerformanceOperationScope(long id,string cardId,long invocationId,PerformanceOperationScope parent=null) {
   Id=id;CardId=cardId;InvocationId=invocationId;Parent=parent;
   lifetime=parent==null ? new CancellationTokenSource() : CancellationTokenSource.CreateLinkedTokenSource(parent.Cancellation);
  }
  public bool Contains(PerformanceOperationScope owner) {for(var current=owner;current!=null;current=current.Parent)if(ReferenceEquals(current,this))return true;return false;}
  public void Register(string resource,Action cleanup) {
   if(cleanup==null)return;
   if(State!=PerformanceScopeState.Open && State!=PerformanceScopeState.Returning) {cleanup();return;}
   writers[resource]=cleanup;
  }
  public void BeginReturn() {if(State!=PerformanceScopeState.Open)throw new InvalidOperationException("Card scope may return exactly once.");Cancellation.ThrowIfCancellationRequested();State=PerformanceScopeState.Returning;}
  public void Forget(string resource){writers.Remove(resource);}
  public void ReleaseWriters() => ReleaseWritersExcept(_=>false);
  public void ReleaseWritersExcept(Func<string,bool> keep) {var keys=writers.Keys.Where(k=>!keep(k)).ToArray();var owned=keys.Select(k=>writers[k]).Reverse().ToArray();foreach(var key in keys)writers.Remove(key);Exception first=null;foreach(var cleanup in owned)try{cleanup();}catch(Exception error){if(first==null)first=error;}if(first!=null)throw first;}
  public void AdoptInto(PerformanceOperationScope parent) {
   if(State!=PerformanceScopeState.Returning || !ReferenceEquals(Parent,parent))throw new InvalidOperationException("Only returning children can transfer ownership to their parent.");
   Cancellation.ThrowIfCancellationRequested();parent.Cancellation.ThrowIfCancellationRequested();
   foreach(var row in writers)parent.Register(row.Key,row.Value);writers.Clear();State=PerformanceScopeState.Returned;
  }
  public void Cancel(bool faulted=false) {if(State==PerformanceScopeState.Disposed)return;State=faulted?PerformanceScopeState.Faulted:PerformanceScopeState.Cancelled;lifetime.Cancel();ReleaseWriters();}
  public void Dispose() {if(State==PerformanceScopeState.Disposed)return;try{lifetime.Cancel();ReleaseWriters();}finally{State=PerformanceScopeState.Disposed;lifetime.Dispose();}}
 }
}
