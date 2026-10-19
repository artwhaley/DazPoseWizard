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
 public sealed class StoryCallFrameSnapshot {
  public string EntryName {get;} public string EntryKind {get;} public string CardId {get;} public int InvocationNumber {get;}
  internal StoryCallFrameSnapshot(string entryName,string entryKind,string cardId,int invocationNumber) {EntryName=entryName;EntryKind=entryKind;CardId=cardId;InvocationNumber=invocationNumber;}
 }
 public sealed class StoryInstructionSnapshot {
  public string EntryName {get;} public string EntryKind {get;} public int ProgramCounter {get;} public StorySource Source {get;} public StoryCode Code {get;} public string Name {get;} public bool Awaited {get;} public string Expression {get;} public int JumpTarget {get;}
  public IReadOnlyList<StoryCallFrameSnapshot> CallStack {get;} public IReadOnlyDictionary<string,StoryData> Locals {get;} public IReadOnlyDictionary<string,StoryData> HostState {get;} public IReadOnlyDictionary<int,int> RepeatRemaining {get;}
  internal StoryInstructionSnapshot(string entryName,string entryKind,int pc,StoryInstruction instruction,string expression,IEnumerable<StoryCallFrameSnapshot> frames,IDictionary<string,StoryData> locals,IDictionary<string,StoryData> state,IDictionary<int,int> repeats) {
   EntryName=entryName;EntryKind=entryKind;ProgramCounter=pc;Source=StoryRehearsalCopy.Source(instruction.source);Code=instruction.code;Name=instruction.name;Awaited=instruction.awaited;Expression=expression;JumpTarget=instruction.jump;
   CallStack=Array.AsReadOnly(frames.ToArray());Locals=StoryRehearsalCopy.DataMap(locals);HostState=StoryRehearsalCopy.DataMap(state);RepeatRemaining=new ReadOnlyDictionary<int,int>(new Dictionary<int,int>(repeats));
  }
 }
 public sealed class StoryBranchSnapshot {
  public string EntryName {get;} public int ProgramCounter {get;} public StorySource Source {get;} public string Expression {get;} public bool EvaluatedCondition {get;} public bool SelectedCondition {get;} public bool Forced {get;}
  internal StoryBranchSnapshot(string entry,int pc,StorySource source,string expression,bool evaluated,bool selected,bool forced) {EntryName=entry;ProgramCounter=pc;Source=StoryRehearsalCopy.Source(source);Expression=expression;EvaluatedCondition=evaluated;SelectedCondition=selected;Forced=forced;}
 }
 public sealed class StoryWaitSnapshot {
  public string EntryName {get;} public int ProgramCounter {get;} public StorySource Source {get;} public IReadOnlyList<StoryData> Events {get;}
  internal StoryWaitSnapshot(string entry,int pc,StorySource source,IEnumerable<StoryData> events) {EntryName=entry;ProgramCounter=pc;Source=StoryRehearsalCopy.Source(source);Events=Array.AsReadOnly(events.Select(StoryRehearsalCopy.Data).ToArray());}
 }
 public sealed class StoryDirectorCallSnapshot {
  public StorySource Source {get;} public string InvocationKey {get;} public CardSelectionRequest Request {get;}
  internal StoryDirectorCallSnapshot(StorySource source,string invocationKey,CardSelectionRequest request) {Source=StoryRehearsalCopy.Source(source);InvocationKey=invocationKey;Request=StoryRehearsalCopy.Request(request);}
 }
 public sealed class StoryDirectorSelectionSnapshot {
  public string SelectedCardId {get;} public bool Forced {get;} public string ForcedCardId {get;} public IReadOnlyList<CardCandidate> Candidates {get;}
  internal StoryDirectorSelectionSnapshot(string selected,bool forced,string forcedId,IEnumerable<CardCandidate> candidates) {SelectedCardId=selected;Forced=forced;ForcedCardId=forcedId;Candidates=Array.AsReadOnly(candidates.Select(StoryRehearsalCopy.Candidate).ToArray());}
 }
 internal static class StoryRehearsalCopy {
  internal static StorySource Source(StorySource value)=>value==null ? null : new StorySource(value.file,value.line,value.column,value.declaration) {unit=value.unit};
  internal static StoryData Data(StoryData value)=>value.Kind==StoryDataKind.List ? new StoryData(value.Items.ToArray()) : value;
  internal static IReadOnlyDictionary<string,StoryData> DataMap(IDictionary<string,StoryData> values) {var copy=new Dictionary<string,StoryData>(StringComparer.Ordinal);foreach(var pair in values)copy.Add(pair.Key,Data(pair.Value));return new ReadOnlyDictionary<string,StoryData>(copy);}
  internal static CardCandidate Candidate(CardCandidate value)=>new CardCandidate {Id=value.Id,Reason=value.Reason,Eligible=value.Eligible,FinalWeight=value.FinalWeight};
  internal static CardSelectionRequest Request(CardSelectionRequest value)=>new CardSelectionRequest {RequiredTags=value.RequiredTags.ToArray(),EquipmentIds=value.EquipmentIds.ToArray(),EquipmentTags=value.EquipmentTags.ToArray(),ExplicitCardId=value.ExplicitCardId,Intensity=value.Intensity,Completion=value.Completion,CompletionTarget=value.CompletionTarget,RequireProgress=value.RequireProgress,EquipmentRequested=value.EquipmentRequested,Phase=value.Phase};
 }
 public sealed class StoryRehearsalController : IStoryRehearsalObserver,IDisposable {
  readonly object sync=new object(); TaskCompletionSource<bool> gate=NewGate(); bool continuous,disposed; string forcedDirectorCardId;
  readonly Dictionary<string,string> directorOverrides=new Dictionary<string,string>(StringComparer.Ordinal); readonly Dictionary<string,StoryData> eventResponses=new Dictionary<string,StoryData>(StringComparer.Ordinal);
  readonly Dictionary<string,bool> branchOverrides=new Dictionary<string,bool>(StringComparer.Ordinal);
  public event Action<StoryInstructionSnapshot> InstructionReady; public event Action<StoryInstructionSnapshot> InstructionCompleted; public event Action<StoryBranchSnapshot> BranchObserved; public event Action<StoryWaitSnapshot> WaitObserved; public event Action<StoryDirectorSelectionSnapshot> DirectorObserved;
  static TaskCompletionSource<bool> NewGate()=>new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  public void SetForcedDirectorCard(string cardId) {lock(sync)forcedDirectorCardId=cardId;}
  public void SetForcedDirectorCard(string invocationKey,string cardId) {if(string.IsNullOrWhiteSpace(invocationKey))throw new ArgumentException("An invocation key is required.",nameof(invocationKey));lock(sync)directorOverrides[invocationKey]=cardId;}
  public void SetEventResponse(string stableKey,StoryData response) {if(string.IsNullOrWhiteSpace(stableKey))throw new ArgumentException("A source key is required.",nameof(stableKey));lock(sync)eventResponses[stableKey]=StoryRehearsalCopy.Data(response);}
  public static string DirectorKey(StorySource source)=>((source?.unit ?? source?.file ?? "")+"|"+(source?.declaration ?? "")+"|"+(source?.line ?? 0));
  public static string SourceKey(StorySource source,int pc)=>((source?.unit ?? source?.file ?? "")+":"+(source?.line ?? 0)+":"+pc);
  public void SetBranchOverride(string stableKey,bool condition) {if(string.IsNullOrWhiteSpace(stableKey))throw new ArgumentException("A stable source key is required.",nameof(stableKey));lock(sync)branchOverrides[stableKey]=condition;}
  public static string BranchKey(StorySource source,int pc)=>((source?.unit ?? source?.file ?? "")+"|"+(source?.declaration ?? "")+"|"+(source?.line ?? 0)+"|"+pc);
  public void Continue() {lock(sync){ThrowIfDisposed();continuous=true;gate.TrySetResult(true);}}
  public void Pause() {lock(sync){ThrowIfDisposed();continuous=false;if(gate.Task.IsCompleted)gate=NewGate();}}
  public void Step() {lock(sync){ThrowIfDisposed();continuous=false;var previous=gate;gate=NewGate();previous.TrySetResult(true);}}
  public async Task BeforeInstructionAsync(StoryInstructionSnapshot snapshot,CancellationToken cancellation) {
   Task wait;bool pause;lock(sync){ThrowIfDisposed();pause=!continuous;wait=gate.Task;}
   InstructionReady?.Invoke(snapshot);if(pause){using(cancellation.Register(()=> {lock(sync)gate.TrySetCanceled();}))await wait.ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();}
  }
  public void AfterInstruction(StoryInstructionSnapshot snapshot)=>InstructionCompleted?.Invoke(snapshot);
  public bool TryOverrideBranch(StoryBranchSnapshot branch,out bool condition) {lock(sync)return branchOverrides.TryGetValue(BranchKey(branch.Source,branch.ProgramCounter),out condition);}
  public void ObserveBranch(StoryBranchSnapshot branch)=>BranchObserved?.Invoke(branch);
  public void ObserveWaitAny(StoryWaitSnapshot wait)=>WaitObserved?.Invoke(wait);
  public bool TryInjectEvent(StoryWaitSnapshot wait,out StoryData response) {lock(sync){if(eventResponses.TryGetValue(SourceKey(wait.Source,wait.ProgramCounter),out response)){response=StoryRehearsalCopy.Data(response);return true;}response=default;return false;}}
  public string ForcedDirectorCardId(StoryDirectorCallSnapshot call) {lock(sync)return directorOverrides.TryGetValue(call.InvocationKey,out var selected) ? selected : forcedDirectorCardId;}
  public void ObserveDirectorSelection(StoryDirectorSelectionSnapshot selection)=>DirectorObserved?.Invoke(selection);
  void ThrowIfDisposed(){if(disposed)throw new ObjectDisposedException(nameof(StoryRehearsalController));}
  public void Dispose(){lock(sync){if(disposed)return;disposed=true;continuous=false;gate.TrySetCanceled();}}
 }
}
