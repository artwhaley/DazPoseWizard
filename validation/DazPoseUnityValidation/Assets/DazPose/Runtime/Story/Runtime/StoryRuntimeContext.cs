using System;
using System.Threading;
using DazPose.Game;
using DazPose.Performer;
namespace DazPose.Story {
 public sealed class StoryRuntimeContext : IDisposable {
  readonly CancellationTokenSource cancellation=new CancellationTokenSource();
  bool disposed;
  readonly string storyId;
  public PerformanceRun Run { get; }
  public StorySceneBindings Bindings { get; }
  public GameApplication Application { get; }
  public SessionContract Contract { get; }
  public SessionState Session { get; }=new SessionState();
  public StoryLocalState Locals { get; }=new StoryLocalState();
  public StoryCatalogs Catalogs { get; }
  public Random Random { get; }
  public Language.StorySessionRandom Randoms {get;}
  public StoryEventHub Events { get; }
  public StoryRuntimeSnapshot Snapshot { get; }
  public StoryValueAccessor Values { get; }
  public CancellationToken Cancellation => cancellation.Token;
  public StoryRuntimeContext(PerformanceRun run,StorySceneBindings bindings,GameApplication app,string story,int? seed=null,float pacing=1) {
   Run=run; Bindings=bindings; Application=app; storyId=story;
   Catalogs=StoryCatalogs.Load();
   Contract=SessionContract.Create(app.Profile.Current);
   if(seed.HasValue)Contract.randomSeed=seed.Value;
   if(!float.IsFinite(pacing) || pacing<=0)throw new ArgumentOutOfRangeException(nameof(pacing));
   Contract.pacingMultiplier=pacing;
   Randoms=new Language.StorySessionRandom(Contract.randomSeed);Random=Randoms.Setup;
   Snapshot=new StoryRuntimeSnapshot(story,Contract,Session);
   Events=new StoryEventHub(e=> { Snapshot.SetEvent(e); Log("Event received: "+e.Id+"; value: "+e.Value); });
   Values=new StoryValueAccessor(this);
   run.RegisterPersistentCleanup(Cancel);
  }
  public void Log(string message) => UnityEngine.Debug.Log("[Story "+storyId+"] "+message,Bindings);
  public void CheckAuthority() { cancellation.Token.ThrowIfCancellationRequested(); Run.ThrowIfActive(); }
  void Cancel() { if(!disposed) cancellation.Cancel(); }
  public void Dispose() { if(disposed) return; cancellation.Cancel(); disposed=true; cancellation.Dispose(); }
 }
}