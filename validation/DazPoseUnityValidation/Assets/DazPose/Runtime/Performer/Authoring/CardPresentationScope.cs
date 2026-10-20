using System;
using System.Threading;
using System.Threading.Tasks;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEngine;
namespace DazPose.Performer {
 internal sealed class CardPresentationScope : IStoryCardPresentationScope {
  readonly PerformanceRun run;readonly PerformancePresentationService service;readonly PerformancePresentationSnapshot entrance;
  readonly PerformanceOperationScope owner;readonly CardPresentationRequest request;readonly Action<PlayerPosture> posture;
  readonly Action<CardPresentationObservation> observe;readonly CancellationTokenSource lifetime;IDisposable pin;bool disposed;
  CardPresentationScope(PerformanceRun run,PerformancePresentationService service,PerformancePresentationSnapshot entrance,PerformanceOperationScope owner,CardPresentationRequest request,IDisposable pin,CancellationToken cancellation,Action<PlayerPosture> posture,Action<CardPresentationObservation> observe){this.run=run;this.service=service;this.entrance=entrance;this.owner=owner;this.request=request;this.pin=pin;this.posture=posture;this.observe=observe;lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation,owner.Cancellation);}
  internal static async Task<IStoryCardPresentationScope> EnterAsync(PerformanceRun run,PerformancePresentationService service,CardPresentationRequest request,CancellationToken cancellation,Action<PlayerPosture> posture,Action<CardPresentationObservation> observe) {
   await run.WaitForPresentationIdleAsync(run.CurrentScope,cancellation);cancellation.ThrowIfCancellationRequested();run.ThrowIfActive();
   var snapshot=service.Capture();IDisposable pin=null;PerformanceOperationScope owner=null;CardPresentationScope result=null;
   try{pin=service.Pin(snapshot);owner=run.BeginCardScope(request.CardId,request.InvocationId);result=new CardPresentationScope(run,service,snapshot,owner,request,pin,cancellation,posture,observe);observe?.Invoke(new CardPresentationObservation("Entrance",request.CardId,request.InvocationId,owner,snapshot,null,0));return result;}
   catch{if(result!=null)result.Dispose();else{pin?.Dispose();if(owner!=null)run.EndCardScope(owner);}throw;}
  }
  public async Task ReturnAsync(CancellationToken cancellation) {
   if(disposed)throw new ObjectDisposedException(nameof(CardPresentationScope));owner.BeginReturn();
   using(var linked=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,cancellation)) {
    var token=linked.Token;token.ThrowIfCancellationRequested();await run.SettleCardBodyAsync(owner,token);await run.WaitForPresentationIdleAsync(owner,token);
    service.Checkpoint?.Invoke("BodySettled");token.ThrowIfCancellationRequested();run.ThrowIfActive();
    var plan=service.PrepareReturn(entrance);var outgoing=service.Capture();observe?.Invoke(new CardPresentationObservation("Outgoing",request.CardId,request.InvocationId,owner,outgoing,entrance,0,plan.Changed.ToString()));
    float started=Time.realtimeSinceStartup;await service.RestoreAsync(plan,owner,token);token.ThrowIfCancellationRequested();run.ThrowIfActive();
    posture(entrance.Posture);var returned=service.Capture();run.AdoptRestoredOwnership(owner.Parent,owner);
    observe?.Invoke(new CardPresentationObservation("Returned",request.CardId,request.InvocationId,owner,returned,entrance,Time.realtimeSinceStartup-started,plan.Changed.ToString()));
    pin?.Dispose();pin=null;
   }
  }
  public void Dispose(){if(disposed)return;disposed=true;try{pin?.Dispose();pin=null;}finally{try{lifetime.Dispose();}finally{run.EndCardScope(owner);}}}
 }
}
