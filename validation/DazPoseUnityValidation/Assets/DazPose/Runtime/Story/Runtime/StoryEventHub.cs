using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
namespace DazPose.Story {
 // Main-thread, invocation-owned rendezvous. Events without a current waiter are not queued.
 public sealed class StoryEventHub {
  readonly Dictionary<string,List<AwaitableCompletionSource<StoryEvent>>> waiters=new Dictionary<string,List<AwaitableCompletionSource<StoryEvent>>>(StringComparer.Ordinal);
  readonly Action<StoryEvent> observed;
  public StoryEventHub(Action<StoryEvent> observed=null) { this.observed=observed; }
  public void Emit(string eventId,string value=null) {
   if(string.IsNullOrWhiteSpace(eventId)) throw new ArgumentException("Event ID is required.");
   var result=new StoryEvent(eventId,value); observed?.Invoke(result);
   if(!waiters.TryGetValue(eventId,out var list)) return;
   waiters.Remove(eventId);
   foreach(var waiter in list.ToArray()) waiter.TrySetResult(result);
  }
  public async Awaitable<StoryEvent> WaitAsync(string eventId,CancellationToken cancellation) {
   cancellation.ThrowIfCancellationRequested();
   if(string.IsNullOrWhiteSpace(eventId)) throw new ArgumentException("Event ID is required.");
   var source=new AwaitableCompletionSource<StoryEvent>();
   if(!waiters.TryGetValue(eventId,out var list)) waiters.Add(eventId,list=new List<AwaitableCompletionSource<StoryEvent>>());
   list.Add(source);
   using(var registration=cancellation.Register(()=>source.TrySetException(new OperationCanceledException(cancellation)))) {
    try { var result=await source.Awaitable; return result; }
    finally { list.Remove(source); if(list.Count==0 && waiters.TryGetValue(eventId,out var current) && current==list) waiters.Remove(eventId); }
   }
  }
 }
}