using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DazPose.Story.Language;
namespace DazPose.Tests {
 public sealed class RecordingCardPresentation {
  public readonly List<string> Events=new List<string>();
  public Func<CardPresentationRequest,CancellationToken,Task> BeforeReturn;
  public Task<IStoryCardPresentationScope> EnterAsync(CardPresentationRequest request,CancellationToken cancellation) {cancellation.ThrowIfCancellationRequested();Events.Add("Enter:"+request.CardId+":"+request.InvocationId);return Task.FromResult<IStoryCardPresentationScope>(new Scope(this,request));}
  sealed class Scope : IStoryCardPresentationScope {
   readonly RecordingCardPresentation owner;readonly CardPresentationRequest request;bool returned,disposed;
   public Scope(RecordingCardPresentation owner,CardPresentationRequest request){this.owner=owner;this.request=request;}
   public async Task ReturnAsync(CancellationToken cancellation){if(returned||disposed)throw new InvalidOperationException("Duplicate return.");cancellation.ThrowIfCancellationRequested();if(owner.BeforeReturn!=null)await owner.BeforeReturn(request,cancellation);cancellation.ThrowIfCancellationRequested();returned=true;owner.Events.Add("Return:"+request.CardId+":"+request.InvocationId);}
   public void Dispose(){if(disposed)return;disposed=true;owner.Events.Add("Dispose:"+request.CardId+":"+request.InvocationId);}
  }
 }
}
