using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace DazPose.Performer {
 public sealed partial class PerformanceRun {
  internal PerformanceOperationScope BeginCardScope(string cardId,long invocationId) {
   ThrowIfStopped();int depth=0;for(var s=CurrentScope;s!=null;s=s.Parent)depth++;
   if(depth>128)throw new InvalidOperationException("Card scope depth exceeds 128.");
   return CurrentScope=new PerformanceOperationScope(++nextScopeId,cardId,invocationId,CurrentScope);
  }
  internal async Task WaitForPresentationIdleAsync(PerformanceOperationScope owner,CancellationToken cancellation) {
   while(true) {
    ThrowIfStopped();cancellation.ThrowIfCancellationRequested();Task[] pending;
    lock(gate)pending=operations.Where(o=>!o.Settled && IsPresentation(o.Verb) && o.Owner.Contains(owner)).Select(o=>o.Completion.Task).ToArray();
    if(pending.Length==0) {
     bool actorBusy=actor.IsTransitioning||actor.IsLocomoting||actor.IsTeleporting||actor.IsDissolving||actor.IsExpressionTransitioning||actor.IsPerforming||actor.IsPresentationSeatingTransitioning||actor.IsPresentationMotionTransitioning||actor.IsGesturing||(actor.CurrentWardrobe?.IsChanging??false);
     bool viewBusy=player!=null && (player.View.IsMoving||player.View.IsLooking||player.View.IsAcquiringTrack);
     bool bodyBusy=player!=null && player.BodyPresence!=null && (player.BodyPresence.VisibilityState==DazPose.Player.PlayerBodyVisibilityState.DissolvingIn || player.BodyPresence.VisibilityState==DazPose.Player.PlayerBodyVisibilityState.DissolvingOut);
     if(!actorBusy && !viewBusy && !bodyBusy)return;
     await UnityEngine.Awaitable.NextFrameAsync();continue;
    }
    await AwaitScopeTasks(pending,cancellation);
   }
  }
  static string PresentationResource(string verb) {
   switch(verb) {
    case "Pose":return "body-pose";
    case "WalkTo":case "TurnTo":case "ArrivalHold":return "arrival";
    case "PersistentSitPose":return "seated-pose";
    case "Expression":return "expression";
    case "LookAt":return "gaze";
    case "MagicShader":return "material";
    case "Grip":case "ReleaseGrip":return "grip";
    case "Player.LookAt":return "view-orientation";
    case "Player.MoveTo":return "view-position";
    case "Outfit":case "SetOutfit":case "SetHair":case "TryRemoveLayer":case "TryAddLayer":return "wardrobe";
    default:return verb;
   }
  }
  static bool IsPresentation(string verb)=>verb!="Delay" && verb!="Say" && !verb.StartsWith("Toy",StringComparison.Ordinal);
  internal async Task SettleCardBodyAsync(PerformanceOperationScope card,CancellationToken cancellation) {
   Task[] pending;lock(gate)pending=operations.Where(o=>!o.Settled && card.Contains(o.Owner)).Select(o=>o.Completion.Task).ToArray();
   await AwaitScopeTasks(pending,cancellation);ThrowIfStopped();
  }
  static async Task AwaitScopeTasks(Task[] tasks,CancellationToken cancellation) {
   cancellation.ThrowIfCancellationRequested();var cancelled=new TaskCompletionSource<bool>();
   using(cancellation.Register(()=>cancelled.TrySetCanceled(cancellation))) {
    var all=Task.WhenAll(tasks);if(await Task.WhenAny(all,cancelled.Task)!=all)await cancelled.Task;await all;
   }
  }
  internal void ReleaseCardWriters(PerformanceOperationScope card) {ThrowIfStopped();card.ReleaseWriters();}
  internal void AdoptRestoredOwnership(PerformanceOperationScope parent,PerformanceOperationScope child) {
   ThrowIfStopped();if(!ReferenceEquals(CurrentScope,child))throw new InvalidOperationException("Only the current card can return.");
   child.AdoptInto(parent);CurrentScope=parent;
  }
  internal void EndCardScope(PerformanceOperationScope card) {
   if(ReferenceEquals(CurrentScope,card))CurrentScope=card.Parent??RootScope;
   card.Dispose();
  }
  void CloseScopes() {var scope=CurrentScope;CurrentScope=RootScope;Exception first=null;
   while(scope!=null){var parent=scope.Parent;try{scope.Cancel();}catch(Exception e){if(first==null)first=e;}finally{try{scope.Dispose();}catch(Exception e){if(first==null)first=e;}}scope=parent;}
   if(first!=null)throw first;
  }
 }
}
