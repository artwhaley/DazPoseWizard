using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DazPose.Story.Language {
 public interface IStoryRehearsalStateReader { IDictionary<string,StoryData> SnapshotState(); }
 public interface IStoryRehearsalObserver {
  Task BeforeInstructionAsync(StoryInstructionSnapshot snapshot,CancellationToken cancellation);
  void AfterInstruction(StoryInstructionSnapshot snapshot);
  bool TryOverrideBranch(StoryBranchSnapshot branch,out bool condition);
  void ObserveBranch(StoryBranchSnapshot branch);
  void ObserveWaitAny(StoryWaitSnapshot wait);
  bool TryInjectEvent(StoryWaitSnapshot wait,out StoryData response);
  string ForcedDirectorCardId(StoryDirectorCallSnapshot call);
  void ObserveDirectorSelection(StoryDirectorSelectionSnapshot selection);
 }
 // Created exactly once when the canonical machine reaches an instruction.
 public sealed class StoryInstructionVisit {
  public string SourceIdentity {get;} public string Declaration {get;} public int ProgramCounter {get;} public int Encounter {get;}
  public string Key=>SiteKey(SourceIdentity,Declaration,ProgramCounter)+"#"+Encounter;
  public StoryInstructionVisit(StorySource source,int pc,int encounter) {SourceIdentity=source?.unit??source?.file??"";Declaration=source?.declaration??"";ProgramCounter=pc;Encounter=encounter;}
  public static string SiteKey(string owner,string declaration,int pc)=>owner.Length+":"+owner+"|"+declaration.Length+":"+declaration+"|"+pc;
 }
 public sealed class StoryCallFrameSnapshot {
  public string EntryName {get;} public string EntryKind {get;} public string CardId {get;} public int InvocationNumber {get;}
  internal StoryCallFrameSnapshot(string entryName,string entryKind,string cardId,int invocationNumber) {EntryName=entryName;EntryKind=entryKind;CardId=cardId;InvocationNumber=invocationNumber;}
 }
 public sealed class StoryInstructionSnapshot {
  public StoryInstructionVisit Visit {get;}
  public string EntryName {get;} public string EntryKind {get;} public int ProgramCounter {get;} public StorySource Source {get;} public StoryCode Code {get;} public string Name {get;} public bool Awaited {get;} public string Expression {get;} public int JumpTarget {get;}
  public IReadOnlyList<StoryCallFrameSnapshot> CallStack {get;} public IReadOnlyDictionary<string,StoryData> Locals {get;} public IReadOnlyDictionary<string,StoryData> HostState {get;} public IReadOnlyDictionary<int,int> RepeatRemaining {get;}
  internal StoryInstructionSnapshot(string entryName,string entryKind,int pc,StoryInstruction instruction,string expression,IEnumerable<StoryCallFrameSnapshot> frames,IDictionary<string,StoryData> locals,IDictionary<string,StoryData> state,IDictionary<int,int> repeats,StoryInstructionVisit visit=null) {
   Visit=visit;
   EntryName=entryName;EntryKind=entryKind;ProgramCounter=pc;Source=StoryRehearsalCopy.Source(instruction.source);Code=instruction.code;Name=instruction.name;Awaited=instruction.awaited;Expression=expression;JumpTarget=instruction.jump;
   CallStack=Array.AsReadOnly(frames.ToArray());Locals=StoryRehearsalCopy.DataMap(locals);HostState=StoryRehearsalCopy.DataMap(state);RepeatRemaining=new ReadOnlyDictionary<int,int>(new Dictionary<int,int>(repeats));
  }
 }
 public sealed class StoryBranchSnapshot {
  public StoryInstructionVisit Visit {get;}
  public string EntryName {get;} public int ProgramCounter {get;} public StorySource Source {get;} public string Expression {get;} public bool EvaluatedCondition {get;} public bool SelectedCondition {get;} public bool Forced {get;}
  internal StoryBranchSnapshot(string entry,int pc,StorySource source,string expression,bool evaluated,bool selected,bool forced,StoryInstructionVisit visit=null) {Visit=visit;EntryName=entry;ProgramCounter=pc;Source=StoryRehearsalCopy.Source(source);Expression=expression;EvaluatedCondition=evaluated;SelectedCondition=selected;Forced=forced;}
 }
 public sealed class StoryWaitSnapshot {
  public StoryInstructionVisit Visit {get;}
  public string EntryName {get;} public int ProgramCounter {get;} public StorySource Source {get;} public IReadOnlyList<StoryData> Events {get;}
  internal StoryWaitSnapshot(string entry,int pc,StorySource source,IEnumerable<StoryData> events,StoryInstructionVisit visit=null) {Visit=visit;EntryName=entry;ProgramCounter=pc;Source=StoryRehearsalCopy.Source(source);Events=Array.AsReadOnly(events.Select(StoryRehearsalCopy.Data).ToArray());}
 }
 public sealed class StoryDirectorCallSnapshot {
  public StoryInstructionVisit Visit {get;}
  public StorySource Source {get;} public string InvocationKey {get;} public CardSelectionRequest Request {get;}
  internal StoryDirectorCallSnapshot(StorySource source,string invocationKey,CardSelectionRequest request,StoryInstructionVisit visit=null) {Visit=visit;Source=StoryRehearsalCopy.Source(source);InvocationKey=invocationKey;Request=StoryRehearsalCopy.Request(request);}
 }
 public sealed class StoryDirectorSelectionSnapshot {
  public StoryInstructionVisit Visit {get;}
  public string SelectedCardId {get;} public bool Forced {get;} public string ForcedCardId {get;} public IReadOnlyList<CardCandidate> Candidates {get;}
  internal StoryDirectorSelectionSnapshot(string selected,bool forced,string forcedId,IEnumerable<CardCandidate> candidates,StoryInstructionVisit visit=null) {Visit=visit;SelectedCardId=selected;Forced=forced;ForcedCardId=forcedId;Candidates=Array.AsReadOnly(candidates.Select(StoryRehearsalCopy.Candidate).ToArray());}
 }
 internal static class StoryRehearsalCopy {
  internal static StorySource Source(StorySource value)=>value==null ? null : new StorySource(value.file,value.line,value.column,value.declaration) {unit=value.unit};
  internal static StoryData Data(StoryData value)=>value.Kind==StoryDataKind.List ? new StoryData(value.Items.ToArray()) : value;
  internal static IReadOnlyDictionary<string,StoryData> DataMap(IDictionary<string,StoryData> values) {var copy=new Dictionary<string,StoryData>(StringComparer.Ordinal);foreach(var pair in values)copy.Add(pair.Key,Data(pair.Value));return new ReadOnlyDictionary<string,StoryData>(copy);}
  internal static CardCandidate Candidate(CardCandidate value)=>new CardCandidate {Id=value.Id,Reason=value.Reason,Eligible=value.Eligible,FinalWeight=value.FinalWeight};
  internal static CardSelectionRequest Request(CardSelectionRequest value)=>new CardSelectionRequest {RequiredTags=value.RequiredTags.ToArray(),EquipmentIds=value.EquipmentIds.ToArray(),EquipmentTags=value.EquipmentTags.ToArray(),ExplicitCardId=value.ExplicitCardId,Intensity=value.Intensity,Completion=value.Completion,CompletionTarget=value.CompletionTarget,RequireProgress=value.RequireProgress,EquipmentRequested=value.EquipmentRequested,Phase=value.Phase};
 }
 public sealed class StoryRehearsalController : IStoryRehearsalObserver,IDisposable {
  readonly object sync=new object(); TaskCompletionSource<bool> gate=NewGate(); bool continuous,disposed; StoryInstructionSnapshot paused;
  readonly Dictionary<string,string> directorOverrides=new Dictionary<string,string>(StringComparer.Ordinal); readonly Dictionary<string,StoryData> eventResponses=new Dictionary<string,StoryData>(StringComparer.Ordinal);
  readonly Dictionary<string,bool> branchOverrides=new Dictionary<string,bool>(StringComparer.Ordinal);
  StoryWaitSnapshot pendingWait; TaskCompletionSource<StoryData> pendingResponse;
  public StoryWaitSnapshot PendingWait {get {lock(sync)return pendingWait;}}
  public bool IsPausedAt(StoryInstructionVisit visit) {lock(sync)return visit!=null&&((paused?.Visit.Key==visit.Key&&!continuous)||(pendingWait?.Visit.Key==visit.Key));}
  public async Task<StoryData> WaitForEventAsync(StoryWaitSnapshot wait,CancellationToken cancellation) {
   Task<StoryData> response;
   lock(sync){ThrowIfDisposed();pendingWait=wait;pendingResponse=new TaskCompletionSource<StoryData>(TaskCreationOptions.RunContinuationsAsynchronously);response=pendingResponse.Task;
    if(eventResponses.TryGetValue(wait.Visit.Key,out var configured)){eventResponses.Remove(wait.Visit.Key);if(!wait.Events.Any(e=>e.Kind==StoryDataKind.Text&&configured.Kind==StoryDataKind.Text&&e.String==configured.String)){pendingWait=null;pendingResponse=null;throw new InvalidOperationException("Scenario event is not offered by this instruction visit.");}pendingResponse.TrySetResult(configured);}}
   ObserveWaitAny(wait);
   try {using(cancellation.Register(()=>{lock(sync)pendingResponse?.TrySetCanceled();}))return await response.ConfigureAwait(false);}
   finally {lock(sync){pendingWait=null;pendingResponse=null;}}
  }
  public event Action<StoryInstructionSnapshot> InstructionReady; public event Action<StoryInstructionSnapshot> InstructionCompleted; public event Action<StoryBranchSnapshot> BranchObserved; public event Action<StoryWaitSnapshot> WaitObserved; public event Action<StoryDirectorSelectionSnapshot> DirectorObserved;
  static TaskCompletionSource<bool> NewGate()=>new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  public void SetForcedDirectorCard(string cardId) {lock(sync){if(paused==null)throw new InvalidOperationException("Pause at a Director visit first.");directorOverrides[paused.Visit.Key]=cardId;}}
  public void SetForcedDirectorCard(string invocationKey,string cardId) {if(string.IsNullOrWhiteSpace(invocationKey))throw new ArgumentException("An invocation key is required.",nameof(invocationKey));lock(sync)directorOverrides[invocationKey]=cardId;}
  public void SetEventResponse(string stableKey,StoryData response) {if(string.IsNullOrWhiteSpace(stableKey))throw new ArgumentException("A source key is required.",nameof(stableKey));lock(sync){
   if(pendingWait?.Visit.Key==stableKey){if(!pendingWait.Events.Any(e=>e.Kind==StoryDataKind.Text&&response.Kind==StoryDataKind.Text&&e.String==response.String))throw new InvalidOperationException("Choose an event offered by this visit.");pendingResponse.TrySetResult(response);}
   else eventResponses[stableKey]=StoryRehearsalCopy.Data(response);
  }}
  public static string SourceKey(StorySource source,int pc)=>new StoryInstructionVisit(source,pc,1).Key;
  public void SetBranchOverride(string stableKey,bool condition) {if(string.IsNullOrWhiteSpace(stableKey))throw new ArgumentException("A stable source key is required.",nameof(stableKey));lock(sync)branchOverrides[stableKey]=condition;}
  public static string BranchKey(StorySource source,int pc)=>SourceKey(source,pc);
  public void Continue() {lock(sync){ThrowIfDisposed();continuous=true;paused=null;gate.TrySetResult(true);}}
  public void Pause() {lock(sync){ThrowIfDisposed();continuous=false;if(gate.Task.IsCompleted)gate=NewGate();}}
  public void Step() {lock(sync){ThrowIfDisposed();continuous=false;paused=null;var previous=gate;gate=NewGate();previous.TrySetResult(true);}}
  public async Task BeforeInstructionAsync(StoryInstructionSnapshot snapshot,CancellationToken cancellation) {
   Task wait;bool pause;lock(sync){ThrowIfDisposed();pause=!continuous;paused=pause?snapshot:null;wait=gate.Task;}
   InstructionReady?.Invoke(snapshot);if(pause){using(cancellation.Register(()=> {lock(sync)gate.TrySetCanceled();}))await wait.ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();}
  }
  public void AfterInstruction(StoryInstructionSnapshot snapshot)=>InstructionCompleted?.Invoke(snapshot);
  public bool TryOverrideBranch(StoryBranchSnapshot branch,out bool condition) {lock(sync){if(branchOverrides.TryGetValue(branch.Visit.Key,out condition)){branchOverrides.Remove(branch.Visit.Key);return true;}return false;}}
  public void ObserveBranch(StoryBranchSnapshot branch)=>BranchObserved?.Invoke(branch);
  public void ObserveWaitAny(StoryWaitSnapshot wait)=>WaitObserved?.Invoke(wait);
  public bool TryInjectEvent(StoryWaitSnapshot wait,out StoryData response) {lock(sync){if(eventResponses.TryGetValue(wait.Visit.Key,out response)){eventResponses.Remove(wait.Visit.Key);response=StoryRehearsalCopy.Data(response);return true;}response=default;return false;}}
  public string ForcedDirectorCardId(StoryDirectorCallSnapshot call) {lock(sync){if(directorOverrides.TryGetValue(call.Visit.Key,out var selected)){directorOverrides.Remove(call.Visit.Key);return selected;}return null;}}
  public void ObserveDirectorSelection(StoryDirectorSelectionSnapshot selection)=>DirectorObserved?.Invoke(selection);
  void ThrowIfDisposed(){if(disposed)throw new ObjectDisposedException(nameof(StoryRehearsalController));}
  public void Dispose(){lock(sync){if(disposed)return;disposed=true;continuous=false;paused=null;pendingResponse?.TrySetCanceled();directorOverrides.Clear();branchOverrides.Clear();eventResponses.Clear();gate.TrySetCanceled();}}
 }
}
