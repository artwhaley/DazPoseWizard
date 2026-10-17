using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace DazPose.Story.Language {
 // Unbuffered, invocation-owned events. A race removes every subscription before publishing its result.
 public sealed class StorySignalHub {
  sealed class Subscription : IDisposable {
   readonly StorySignalHub hub;public readonly string Id;public readonly Action<string> Callback;
   public Subscription(StorySignalHub hub,string id,Action<string> callback) {this.hub=hub;Id=id;Callback=callback;}
   public void Dispose() {lock(hub.gate) {if(hub.listeners.TryGetValue(Id,out var list)) {list.Remove(this);if(list.Count==0)hub.listeners.Remove(Id);}}}
  }
  readonly object gate=new object();
  readonly Dictionary<string,List<Subscription>> listeners=new Dictionary<string,List<Subscription>>(StringComparer.Ordinal);
  public int SubscriptionCount {get {lock(gate)return listeners.Values.Sum(l=>l.Count);}}
  public void Emit(string id) {Subscription[] copy;lock(gate)copy=listeners.TryGetValue(id,out var list) ? list.ToArray() : Array.Empty<Subscription>();foreach(var item in copy)item.Callback(id);}
  public async Task<string> WaitAnyAsync(string[] ids,CancellationToken cancellation) {
   cancellation.ThrowIfCancellationRequested();
   if(ids.Length==0 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)throw new ArgumentException("An event race requires distinct nonempty events.");
   var source=new TaskCompletionSource<string>();var subscriptions=new List<Subscription>();int winner=0;
   void Finish(string id,bool cancelled) {
    if(Interlocked.CompareExchange(ref winner,1,0)!=0)return;
    foreach(var subscription in subscriptions)subscription.Dispose();
    if(cancelled)source.TrySetCanceled(cancellation);else source.TrySetResult(id);
   }
   lock(gate)foreach(string id in ids) {
    var subscription=new Subscription(this,id,value=>Finish(value,false));subscriptions.Add(subscription);
    if(!listeners.TryGetValue(id,out var list))listeners.Add(id,list=new List<Subscription>());list.Add(subscription);
   }
   using(cancellation.Register(()=>Finish(null,true))) {
    try{return await source.Task;}finally {foreach(var subscription in subscriptions)subscription.Dispose();}
   }
  }
 }
}
