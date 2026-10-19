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
   RehearsalScenario scenario=null,Action<StoryCommand> visualEffect=null) {
   sourceProgram=linkedContent?.Program??program??throw new ArgumentNullException(nameof(program));this.sequence=sequence??throw new ArgumentNullException(nameof(sequence));
   if(profile==null)throw new ArgumentNullException(nameof(profile));initialProfile=PlayerProfileService.Clone(profile);kinks=kinkCatalog??throw new ArgumentNullException(nameof(kinkCatalog));equipment=equipmentCatalog??throw new ArgumentNullException(nameof(equipmentCatalog));
   this.sourceBindings=sourceBindings;linked=linkedContent;sceneBindings=readOnlySceneBindings;this.visualEffect=visualEffect;editorContext=SynchronizationContext.Current;
   Scenario=scenario?.Copy()??new RehearsalScenario();Initialize();
  }
  void Initialize() {
   lock(sync){history.Clear();nextReady=NewReady();ReadySnapshot=null;Failure=null;RunTask=null;started=false;}
   cancellation=new CancellationTokenSource();Host=new RehearsalStoryHost(sourceProgram,initialProfile,kinks,equipment,Scenario.seed,sourceBindings,linked,sceneBindings,Scenario,visualEffect);
   Controller=new StoryRehearsalController();Controller.InstructionReady+=OnInstructionReady;Controller.BranchObserved+=OnBranch;Controller.WaitObserved+=OnWait;Controller.DirectorObserved+=OnDirector;
   foreach(var item in Scenario.branches??Array.Empty<RehearsalBranchChoice>())Controller.SetBranchOverride(item.key,item.condition);
   foreach(var item in Scenario.events??Array.Empty<RehearsalEventChoice>())Controller.SetEventResponse(item.key,new StoryData(item.eventId));
   foreach(var item in Scenario.cards??Array.Empty<RehearsalCardChoice>())Controller.SetForcedDirectorCard(item.invocationKey,item.cardId);
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
  public async Task BackAsync(CancellationToken token=default) {
   token.ThrowIfCancellationRequested();ThrowIfDisposed();int priorCount;lock(sync)priorCount=history.Count;
   await ResetAsync(false,token);_=Start();await ReadyOrFinished(token);
   int advances=Math.Max(0,priorCount-2);
   for(int i=0;i<advances;i++)if(!await StepAsync(token))break;
  }
  public async Task ResetAsync(bool start=true,CancellationToken token=default) {
   token.ThrowIfCancellationRequested();ThrowIfDisposed();await StopCoreAsync(token);Unsubscribe();Controller?.Dispose();Host?.CancelOperations();cancellation?.Dispose();await OnEditorContextAsync(Initialize,token);if(start)_=Start();
  }
  public void SetBranchOverride(StorySource source,int pc,bool condition) {
   ThrowIfDisposed();string key=StoryRehearsalController.BranchKey(source,pc);var rows=(Scenario.branches??Array.Empty<RehearsalBranchChoice>()).ToList();int i=rows.FindIndex(x=>x.key==key);
   if(i<0)rows.Add(new RehearsalBranchChoice{key=key,condition=condition});else rows[i].condition=condition;Scenario.branches=rows.ToArray();Controller.SetBranchOverride(key,condition);
  }
  public void SetEventResponse(StorySource source,int pc,string eventId) {
   ThrowIfDisposed();string key=StoryRehearsalController.SourceKey(source,pc);var rows=(Scenario.events??Array.Empty<RehearsalEventChoice>()).ToList();int i=rows.FindIndex(x=>x.key==key);
   if(i<0)rows.Add(new RehearsalEventChoice{key=key,eventId=eventId});else rows[i].eventId=eventId;Scenario.events=rows.ToArray();Controller.SetEventResponse(key,new StoryData(eventId));
  }
  public void ForceDirectorCard(StorySource source,string cardId) {
   ThrowIfDisposed();string key=StoryRehearsalController.DirectorKey(source);var rows=(Scenario.cards??Array.Empty<RehearsalCardChoice>()).ToList();int i=rows.FindIndex(x=>x.invocationKey==key);
   if(i<0)rows.Add(new RehearsalCardChoice{invocationKey=key,cardId=cardId});else rows[i].cardId=cardId;Scenario.cards=rows.ToArray();Controller.SetForcedDirectorCard(key,cardId);
  }
  public void SimulateAction(string action) {
   ThrowIfDisposed();Host.SimulateAction(action);Scenario.pressedActions=(Scenario.pressedActions??Array.Empty<string>()).Concat(new[]{action}).Distinct(StringComparer.Ordinal).ToArray();
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
  void OnInstructionReady(StoryInstructionSnapshot snapshot) {
   lock(sync){ReadySnapshot=snapshot;history.Add(snapshot);nextReady.TrySetResult(snapshot);}
   InstructionReady?.Invoke(snapshot);
  }
  void OnTrace(StorySource source,string text)=>TraceLine?.Invoke(source,text);
  void OnBranch(StoryBranchSnapshot value)=>BranchObserved?.Invoke(value);
  void OnWait(StoryWaitSnapshot value)=>WaitObserved?.Invoke(value);
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
   if(Controller!=null){Controller.InstructionReady-=OnInstructionReady;Controller.BranchObserved-=OnBranch;Controller.WaitObserved-=OnWait;Controller.DirectorObserved-=OnDirector;}
   if(Host!=null)Host.TraceLine-=OnTrace;
  }
  void ThrowIfDisposed(){if(disposed)throw new ObjectDisposedException(nameof(RehearsalSession));}
  public async Task StopAsync(CancellationToken token=default){ThrowIfDisposed();await StopCoreAsync(token);}
  public void Dispose(){if(disposed)return;disposed=true;if(cancellation!=null&&!cancellation.IsCancellationRequested)cancellation.Cancel();Controller?.Dispose();Host?.CancelOperations();Unsubscribe();var task=RunTask;if(task!=null)_=task.ContinueWith(_=>{cancellation?.Dispose();},TaskScheduler.Default);else cancellation?.Dispose();}
 }
}
