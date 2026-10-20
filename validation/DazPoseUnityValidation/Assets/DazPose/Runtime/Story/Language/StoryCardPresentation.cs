using System;
using System.Threading;
using System.Threading.Tasks;
namespace DazPose.Story.Language {
 public sealed class CardPresentationRequest {
  public string CardId {get;} public long InvocationId {get;} public int NestingDepth {get;}
  public StorySource CardSource=>Copy(cardSource); public StorySource CallSite=>Copy(callSite);
  static StorySource Copy(StorySource s)=>s==null?null:new StorySource(s.file,s.line,s.column,s.declaration){unit=s.unit};
  readonly StorySource cardSource,callSite;
  public CardPresentationRequest(string cardId,long invocationId,int nestingDepth,StorySource cardSource,StorySource callSite){CardId=cardId;InvocationId=invocationId;NestingDepth=nestingDepth;this.cardSource=Copy(cardSource);this.callSite=Copy(callSite);}
  public StorySource Source=>Copy(cardSource);
  public StorySource InvocationSource=>Copy(callSite);
 }
 public interface IStoryCardPresentationHost {Task<IStoryCardPresentationScope> EnterCardAsync(CardPresentationRequest request,CancellationToken cancellation);}
 public interface IStoryCardPresentationScope : IDisposable {Task ReturnAsync(CancellationToken cancellation);}
}
