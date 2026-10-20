using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DazPose.Game;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEngine;
namespace DazPose.Editor {
 /// <summary>Runs and replays the canonical StoryMachine with a virtual host and deterministic scenario.</summary>
 public sealed class RehearsalSession : IDisposable {
  readonly CompiledStoryProgram sourceProgram;
  readonly string sequence;
  readonly PlayerProfile initialProfile;
  readonly KinkCatalog kinks;
  readonly EquipmentCatalog equipment;
  readonly StoryContentBindings sourceBindings;
  readonly StoryLinkedContent linked;
  readonly StorySceneBindings sceneBindings;
  readonly Action<StoryCommand> visualEffect;
  readonly Func<StoryCommand,Task> asyncVisualEffect;
  readonly object sync=new object();
  readonly SynchronizationContext editorContext;
  readonly List<StoryInstructionSnapshot> history=new List<StoryInstructionSnapshot>();
  TaskCompletionSource<StoryInstructionSnapshot> nextReady=NewReady();
  CancellationTokenSource cancellation;
  bool started,disposed;
  public RehearsalScenario Scenario {get;private set;}
  public RehearsalStoryHost Host {get;private set;}
  public StoryRehearsalController Controller {get;private set;}
  public StoryMachine Machine {get;private set;}
  public Task RunTask {get;private set;}
  public Exception Failure {get;private set;}
  public StoryInstructionSnapshot CompletedSnapshot {get;private set;}
  public StoryInstructionSnapshot ReadySnapshot {get;private set;}
  public IReadOnlyList<StoryInstructionSnapshot> History {get {lock(sync)return history.ToArray();}}
  public event Action<StorySource,string> TraceLine;
  public event Action<StoryInstructionSnapshot> InstructionReady;
  public event Action<StoryBranchSnapshot> BranchObserved;
  public event Action<StoryWaitSnapshot> WaitObserved;
  public event Action<StoryDirectorSelectionSnapshot> DirectorObserved;
  static TaskCompletionSource<StoryInstructionSnapshot> NewReady()=>new TaskCompletionSource<StoryInstructionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

  public RehearsalSession(CompiledStoryProgram program,string sequence,PlayerProfile profile,KinkCatalog kinkCatalog,EquipmentCatalog equipmentCatalog,
   StoryContentBindings sourceBindings=null,StoryLinkedContent linkedContent=null,StorySceneBindings readOnlySceneBindings=null,
   RehearsalScenario scenario=null,Action<StoryCommand> visualEffect=null,Func<StoryCommand,Task> asyncVisualEffect=null) {
   sourceProgram=linkedContent?.Program??program??throw new ArgumentNullException(nameof(program));this.sequence=sequence??throw new ArgumentNullException(nameof(sequence));
   if(profile==null)throw new ArgumentNullException(nameof(profile));initialProfile=PlayerProfileService.Clone(profile);kinks=kinkCatalog??throw new ArgumentNullException(nameof(kinkCatalog));equipment=equipmentCatalog??throw new ArgumentNullException(nameof(equipmentCatalog));
   this.sourceBindings=sourceBindings;linked=linkedContent;sceneBindings=readOnlySceneBindings;this.visualEffect=visualEffect;this.asyncVisualEffect=asyncVisualEffect;editorContext=SynchronizationContext.Current;
   Scenario=scenario?.Copy()??new RehearsalScenario();
   string fingerprint=StoryDialogue.IdFor(JsonUtility.ToJson(sourceProgram));
   if(Scenario.schemaVersion!=2||Scenario.reviewRequired||(!string.IsNullOrEmpty(Scenario.programFingerprint)&&Scenario.programFingerprint!=fingerprint))throw new InvalidOperationException("Scenario is legacy or belongs to a changed program. Review its choices and create a new visit-scoped scenario.");
   Scenario.programFingerprint=fingerprint;Initialize();
  }
  void Initialize() {
   lock(sync){history.Clear();nextReady=NewReady();ReadySnapshot=null;CompletedSnapshot=null;Failure=null;RunTask=null;started=false;}
   cancellation=new CancellationTokenSource();Host=new RehearsalStoryHost(sourceProgram,initialProfile,kinks,equipment,Scenario.seed,sourceBindings,linked,sceneBindings,Scenario,visualEffect);Host.AsyncVisualEffect=asyncVisualEffect;
   Controller=new StoryRehearsalController();Controller.InstructionReady+=OnInstructionReady;Controller.InstructionCompleted+=OnInstructionCompleted;Controller.BranchObserved+=OnBranch;Controller.WaitObserved+=OnWait;Controller.DirectorObserved+=OnDirector;
   foreach(var item in Scenario.branches??Array.Empty<RehearsalBranchChoice>())Controller.SetBranchOverride(item.key,item.condition);
   foreach(var item in Scenario.events??Array.Empty<RehearsalEventChoice>())Controller.SetEventResponse(item.key,new StoryData(item.eventId));
   foreach(var item in Scenario.cards??Array.Empty<RehearsalCardChoice>())Controller.SetForcedDirectorCard(item.invocationKey,item.cardId);
   Host.ChooseEvent=(visit,events,token)=>Controller.WaitForEventAsync(new StoryWaitSnapshot(ReadySnapshot?.EntryName,visit.ProgramCounter,ReadySnapshot?.Source,events,visit),token);
   Machine=new StoryMachine(sourceProgram,Host,Host.Randoms,Controller);Host.TraceLine+=OnTrace;
  }
  public Task Start() {
   ThrowIfDisposed();if(started)return RunTask;started=true;
   RunTask=Machine.RunAsync(sequence,cancellation.Token);
   _=RunTask.ContinueWith(t=>{if(t.IsFaulted){var error=t.Exception?.GetBaseException();lock(sync)if(ReferenceEquals(RunTask,t))Failure=error;}},TaskScheduler.Default);
   return RunTask;
  }
  public void Continue(){ThrowIfDisposed();if(!started)_=Start();Controller.Continue();}
  public void Pause(){ThrowIfDisposed();if(started)Controller.Pause();}
  public async Task<bool> StepAsync(CancellationToken token=default) {
   token.ThrowIfCancellationRequested();ThrowIfDisposed();if(!started)_=Start();if(await ReadyOrFinished(token)==null)return false;
   var next=NewReady();lock(sync){nextReady=next;}
   Controller.Step();return await WaitNextOrFinished(next.Task,token)!=null;
  }
  public async Task<StoryInstructionSnapshot> RunToPCAsync(string entryName,int programCounter,CancellationToken token=default) {
   token.ThrowIfCancellationRequested();ThrowIfDisposed();if(!started)_=Start();int count=0;
   while(true) {
    var current=await ReadyOrFinished(token);if(current==null)throw new InvalidOperationException("The story completed before reaching the requested source instruction.");
    if(current.EntryName==entryName&&current.ProgramCounter==programCounter)return current;
    if(++count>Scenario.maxRunToSteps)throw new InvalidOperationException("Run To PC exceeded the scenario step budget of "+Scenario.maxRunToSteps+".");
    if(!await StepAsync(token))throw new InvalidOperationException("The story completed before reaching the requested source instruction.");
   }
  }
  public async Task<StoryInstructionSnapshot> RunThroughSelectedAsync(string owner,string entryName,int pc,int encounter=1,CancellationToken token=default,bool restart=true) {
   if(encounter<1)throw new ArgumentOutOfRangeException(nameof(encounter));token.ThrowIfCancellationRequested();
   if(!sourceProgram.entries.Any(e=>e.name==entryName&&pc>=e.start&&pc<e.end)||pc<0||pc>=sourceProgram.instructions.Length||(sourceProgram.instructions[pc].source?.unit??sourceProgram.instructions[pc].source?.file??"")!=owner)throw new InvalidOperationException("Selected source owner/entry is no longer in the linked program.");
   if(restart)await ResetAsync(false,token);_=Start();string chosen=null;
   for(int steps=0;steps<=Scenario.maxRunToSteps;steps++) {
    var current=await ReadyOrFinished(token);
    if(chosen!=null&&CompletedSnapshot?.Visit.Key==chosen)return CompletedSnapshot;
    if(current==null)throw new InvalidOperationException("Selected instruction visit is unreachable on this seed/scenario path. Review branch, card and event choices.");
    if(current.EntryName==entryName&&current.ProgramCounter==pc&&current.Visit.Encounter==encounter)chosen=current.Visit.Key;
    if(steps==Scenario.maxRunToSteps)break;
    bool more=await StepAsync(token);if(chosen!=null&&CompletedSnapshot?.Visit.Key==chosen)return CompletedSnapshot;
    if(!more)throw new InvalidOperationException("Story completed before the selected instruction visit; choose another path or visit.");
   }
   throw new InvalidOperationException("Source navigation exceeded the scenario instruction budget. Choose a reachable visit or revise the path.");
  }
  public async Task<StoryInstructionSnapshot> RunBeforeSelectedAsync(string owner,string entryName,int pc,int encounter=1,CancellationToken token=default,bool restart=true) {
   if(encounter<1)throw new ArgumentOutOfRangeException(nameof(encounter));
   if(restart)await ResetAsync(false,token);_=Start();
   for(int steps=0;steps<=Scenario.maxRunToSteps;steps++) {
    var current=await ReadyOrFinished(token);
    if(current==null)throw new InvalidOperationException("Selected instruction visit is unreachable on this scenario path.");
    if(current.EntryName==entryName&&current.ProgramCounter==pc&&current.Visit.Encounter==encounter&&(current.Source?.unit??current.Source?.file??"")==owner)return current;
    if(steps==Scenario.maxRunToSteps)break;
    if(!await StepAsync(token))throw new InvalidOperationException("Story completed before the selected instruction visit.");
   }
   throw new InvalidOperationException("Source navigation exceeded the scenario instruction budget.");
  }
  public async Task BackAsync(CancellationToken token=default) {
   token.ThrowIfCancellationRequested();ThrowIfDisposed();int priorCount;lock(sync)priorCount=history.Count;
   await ResetAsync(false,token);_=Start();await ReadyOrFinished(token);
   int advances=Math.Max(0,priorCount-2);
   for(int i=0;i<advances;i++)if(!await StepAsync(token))break;
  }
  public async Task ResetAsync(bool start=true,CancellationToken token=default) {
   token.ThrowIfCancellationRequested();ThrowIfDisposed();await StopCoreAsync(token);ThrowIfDisposed();Unsubscribe();Controller?.Dispose();Host?.CancelOperations();cancellation?.Dispose();await OnEditorContextAsync(()=>{ThrowIfDisposed();Initialize();},token);if(start)_=Start();
  }
  public void SetBranchOverride(StorySource source,int pc,bool condition) {
   ThrowIfDisposed();if(ReadySnapshot?.Code!=StoryCode.JumpFalse)throw new InvalidOperationException("Pause at a branch visit first.");string key=RequireVisit(source,pc).Key;var rows=(Scenario.branches??Array.Empty<RehearsalBranchChoice>()).ToList();int i=rows.FindIndex(x=>x.key==key);
   if(i<0)rows.Add(new RehearsalBranchChoice{key=key,condition=condition});else rows[i].condition=condition;Scenario.branches=rows.ToArray();Controller.SetBranchOverride(key,condition);
  }
  public void SetEventResponse(StorySource source,int pc,string eventId) {
   ThrowIfDisposed();string key=RequireVisit(source,pc).Key;
   var accepted=Controller.PendingWait?.Events??(ReadySnapshot.Code==StoryCode.WaitAny?Machine.InspectWait(ReadySnapshot):Array.Empty<StoryData>());
   if(!accepted.Any(e=>e.Kind==StoryDataKind.Text&&e.String==eventId))throw new InvalidOperationException("Choose an event offered by this instruction visit.");
   var rows=(Scenario.events??Array.Empty<RehearsalEventChoice>()).ToList();int i=rows.FindIndex(x=>x.key==key);
   if(i<0)rows.Add(new RehearsalEventChoice{key=key,eventId=eventId});else rows[i].eventId=eventId;Scenario.events=rows.ToArray();Controller.SetEventResponse(key,new StoryData(eventId));
  }
  public void ForceDirectorCard(StorySource source,string cardId) {
   ThrowIfDisposed();string key=RequireVisit(source,ReadySnapshot?.ProgramCounter??-1).Key;
   var candidate=Machine.InspectDirector(ReadySnapshot).Candidates.FirstOrDefault(c=>c.Id==cardId);
   if(candidate==null||!candidate.Eligible||candidate.FinalWeight<=0)throw new InvalidOperationException("Cannot force this card: "+(candidate?.Reason??"not a candidate for this visit"));
   var rows=(Scenario.cards??Array.Empty<RehearsalCardChoice>()).ToList();int i=rows.FindIndex(x=>x.invocationKey==key);
   if(i<0)rows.Add(new RehearsalCardChoice{invocationKey=key,cardId=cardId});else rows[i].cardId=cardId;Scenario.cards=rows.ToArray();Controller.SetForcedDirectorCard(key,cardId);
  }
  StoryInstructionVisit RequireVisit(StorySource source,int pc) {
   var current=ReadySnapshot;if(current==null||current.ProgramCounter!=pc||current.Source?.unit!=source?.unit||current.Source?.file!=source?.file||!Controller.IsPausedAt(current.Visit))throw new InvalidOperationException("Pause at the chosen instruction visit before assigning a response.");return current.Visit;
  }
  [Serializable] sealed class ScenarioHeader {public int schemaVersion=0;}
  public static RehearsalScenario ImportScenarioJson(string json) {
   var result=JsonUtility.FromJson<RehearsalScenario>(json)??throw new InvalidOperationException("Invalid scenario.");
   if(JsonUtility.FromJson<ScenarioHeader>(json)?.schemaVersion!=2||result.schemaVersion!=2){result.reviewRequired=true;throw new InvalidOperationException("Legacy scenario uses source-site choices. Review and recreate it with visit-scoped decisions.");}
   return result;
  }
  public void SimulateAction(string action) {
   ThrowIfDisposed();if(Controller.PendingWait!=null)SetEventResponse(ReadySnapshot.Source,ReadySnapshot.ProgramCounter,"UI.Pressed:"+action);else Host.SimulateAction(action);
  }
  public string ExportScenarioJson()=>JsonUtility.ToJson(Scenario.Copy(),true);
  Task<StoryInstructionSnapshot> ReadyOrFinished(CancellationToken token) {
   token.ThrowIfCancellationRequested();
   lock(sync)if(ReadySnapshot!=null&&nextReady.Task.IsCompleted)return Task.FromResult(ReadySnapshot);
   return WaitNextOrFinished(nextReady.Task,token);
  }
  async Task<StoryInstructionSnapshot> WaitNextOrFinished(Task<StoryInstructionSnapshot> readyTask,CancellationToken token) {
   token.ThrowIfCancellationRequested();var completed=RunTask;
   if(completed==null)return await AwaitWithCancellation(readyTask,token);
   var cancelled=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   using(token.Register(()=>cancelled.TrySetResult(true))) {
    var winner=await Task.WhenAny(readyTask,completed,cancelled.Task);
    if(winner==cancelled.Task)throw new OperationCanceledException(token);
    if(winner==readyTask)return await readyTask;
    await completed;return null;
   }
  }
  void OnInstructionCompleted(StoryInstructionSnapshot snapshot){lock(sync)CompletedSnapshot=snapshot;}
  void OnInstructionReady(StoryInstructionSnapshot snapshot) {
   lock(sync){ReadySnapshot=snapshot;history.Add(snapshot);nextReady.TrySetResult(snapshot);}
   InstructionReady?.Invoke(snapshot);
  }
  void OnTrace(StorySource source,string text)=>TraceLine?.Invoke(source,text);
  void OnBranch(StoryBranchSnapshot value)=>BranchObserved?.Invoke(value);
  void OnWait(StoryWaitSnapshot value){Host.SetWaitingVisit(value.Visit);WaitObserved?.Invoke(value);}
  void OnDirector(StoryDirectorSelectionSnapshot value)=>DirectorObserved?.Invoke(value);
  static async Task<T> AwaitWithCancellation<T>(Task<T> task,CancellationToken token) {
   if(!token.CanBeCanceled)return await task;var cancelled=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   using(token.Register(()=>cancelled.TrySetResult(true))){if(await Task.WhenAny(task,cancelled.Task)!=task)throw new OperationCanceledException(token);return await task;}
  }
  Task OnEditorContextAsync(Action action,CancellationToken token) {
   if(editorContext==null||SynchronizationContext.Current==editorContext){token.ThrowIfCancellationRequested();action();return Task.CompletedTask;}
   var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   editorContext.Post(_=>{try{token.ThrowIfCancellationRequested();action();completion.TrySetResult(true);}catch(Exception e){completion.TrySetException(e);}},null);
   return AwaitWithCancellation(completion.Task,token);
  }
  static async Task AwaitWithCancellation(Task task,CancellationToken token) {
   if(!token.CanBeCanceled){await task;return;}var cancelled=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   using(token.Register(()=>cancelled.TrySetResult(true))){if(await Task.WhenAny(task,cancelled.Task)!=task)throw new OperationCanceledException(token);await task;}
  }
  async Task StopCoreAsync(CancellationToken token) {
   if(!started){Host?.CancelOperations();return;}if(!cancellation.IsCancellationRequested)cancellation.Cancel();Controller?.Dispose();Host?.CancelOperations();
   var task=RunTask;if(task!=null)try{await AwaitWithCancellation(task,token);}catch(OperationCanceledException){}catch(Exception error){lock(sync)Failure=error;}
   started=false;
  }
  void Unsubscribe() {
   if(Controller!=null){Controller.InstructionReady-=OnInstructionReady;Controller.InstructionCompleted-=OnInstructionCompleted;Controller.BranchObserved-=OnBranch;Controller.WaitObserved-=OnWait;Controller.DirectorObserved-=OnDirector;}
   if(Host!=null)Host.TraceLine-=OnTrace;
  }
  void ThrowIfDisposed(){if(disposed)throw new ObjectDisposedException(nameof(RehearsalSession));}
  public async Task StopAsync(CancellationToken token=default){ThrowIfDisposed();await StopCoreAsync(token);}
  public void Dispose(){if(disposed)return;disposed=true;if(cancellation!=null&&!cancellation.IsCancellationRequested)cancellation.Cancel();Controller?.Dispose();Host?.CancelOperations();Unsubscribe();var task=RunTask;if(task!=null)_=task.ContinueWith(_=>{cancellation?.Dispose();},TaskScheduler.Default);else cancellation?.Dispose();}
 }
}
