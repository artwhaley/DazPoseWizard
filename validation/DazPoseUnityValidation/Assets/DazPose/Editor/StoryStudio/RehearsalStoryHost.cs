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
 [Serializable] public sealed class RehearsalBranchChoice { public string key; public bool condition; }
 [Serializable] public sealed class RehearsalEventChoice { public string key,eventId; }
 [Serializable] public sealed class RehearsalCardChoice { public string invocationKey,cardId; }
 [Serializable] public sealed class RehearsalScenario {
  public int schemaVersion=2;
  public string programFingerprint;
  public bool reviewRequired;
  public int seed=1;
  public float pacingMultiplier=1;
  public float maxVirtualWaitSeconds=1800;
  public int maxRunToSteps=10000;
  public PlayerProfile initialProfile;
  public bool[] initialClothing={true,true,true,true,true,true};
  public KinkPreference[] popupKinks;
  public EquipmentPreference[] popupEquipment;
  public bool[] popupClothing;
  public string[] pressedActions=Array.Empty<string>();
  public RehearsalBranchChoice[] branches=Array.Empty<RehearsalBranchChoice>();
  public RehearsalEventChoice[] events=Array.Empty<RehearsalEventChoice>();
  public RehearsalCardChoice[] cards=Array.Empty<RehearsalCardChoice>();
  public RehearsalScenario Copy() {
   return new RehearsalScenario {schemaVersion=schemaVersion,programFingerprint=programFingerprint,reviewRequired=reviewRequired,seed=seed,pacingMultiplier=pacingMultiplier,maxVirtualWaitSeconds=maxVirtualWaitSeconds,maxRunToSteps=maxRunToSteps,
    initialProfile=initialProfile==null?null:PlayerProfileService.Clone(initialProfile),initialClothing=(bool[])initialClothing.Clone(),
    popupKinks=popupKinks?.Select(x=>new KinkPreference{id=x.id,frequency=x.frequency,sentiment=x.sentiment}).ToArray(),
    popupEquipment=popupEquipment?.Select(x=>new EquipmentPreference{id=x.id,ownedAndWilling=x.ownedAndWilling,useAsReward=x.useAsReward,useAsPunishment=x.useAsPunishment}).ToArray(),
    popupClothing=popupClothing==null?null:(bool[])popupClothing.Clone(),pressedActions=pressedActions?.ToArray()??Array.Empty<string>(),
    branches=branches?.Select(x=>new RehearsalBranchChoice{key=x.key,condition=x.condition}).ToArray()??Array.Empty<RehearsalBranchChoice>(),
    events=events?.Select(x=>new RehearsalEventChoice{key=x.key,eventId=x.eventId}).ToArray()??Array.Empty<RehearsalEventChoice>(),
    cards=cards?.Select(x=>new RehearsalCardChoice{invocationKey=x.invocationKey,cardId=x.cardId}).ToArray()??Array.Empty<RehearsalCardChoice>()};
  }
 }
 /// <summary>Pure virtual execution host for the canonical interpreter. It never owns a GameApplication or output service.</summary>
 public sealed class RehearsalStoryHost : IStoryExecutionHost,IStoryRehearsalStateReader,IStoryDirectorObserver,IStoryPhaseObserver,IStoryCardEligibility {
  sealed class ContentRow { public PlayerPosture posture=PlayerPosture.Standing;public bool ready;public string issue; }
  readonly Dictionary<string,ContentRow> bindings=new Dictionary<string,ContentRow>(StringComparer.Ordinal);
  readonly Dictionary<string,string> bindingIssues=new Dictionary<string,string>(StringComparer.Ordinal);
  readonly Dictionary<string,string> dialogueIssues=new Dictionary<string,string>(StringComparer.Ordinal);
  readonly HashSet<string> dialogueBindings=new HashSet<string>(StringComparer.Ordinal);
  readonly Dictionary<string,bool> locationSeats=new Dictionary<string,bool>(StringComparer.Ordinal);
  readonly HashSet<string> activeActions=new HashSet<string>(StringComparer.Ordinal);
  readonly HashSet<string> pressedActions;
  readonly StorySessionRandom random;
  readonly RehearsalScenario scenario;
  readonly KinkCatalog kinkCatalog;
  readonly EquipmentCatalog equipmentCatalog;
  readonly StoryLinkedContent linked;
  readonly CompiledStoryProgram canonicalProgram;
  readonly StorySceneBindings sceneBindings;
  readonly CancellationTokenSource lifetime=new CancellationTokenSource();
  bool cancelled;
  float virtualSeconds;
  public PlayerProfile Profile {get;private set;}
  public SessionState Session {get;}=new SessionState();
  public SessionContract Contract {get;}
  public StorySessionRandom Randoms=>random;
  public Func<StoryInstructionVisit,StoryData[],CancellationToken,Task<StoryData>> ChooseEvent {get;set;}
  StoryInstructionVisit waitingVisit;
  public Action<StoryCommand> VisualEffect {get;set;}
  public event Action<StorySource,string> TraceLine;
  public DirectorSnapshot LastDirectorSnapshot {get;private set;}
  public CardExecutionContext LastCardContext {get;private set;}
  public float VirtualSeconds=>virtualSeconds;
  public bool IsCancelled=>cancelled;

  public RehearsalStoryHost(CompiledStoryProgram program,PlayerProfile sourceProfile,KinkCatalog kinks,EquipmentCatalog equipment,int seed,
   StoryContentBindings sourceBindings=null,StoryLinkedContent linkedContent=null,StorySceneBindings readOnlySceneBindings=null,
   RehearsalScenario scenario=null,Action<StoryCommand> visualEffect=null) {
   if(program==null)throw new ArgumentNullException(nameof(program));if(sourceProfile==null)throw new ArgumentNullException(nameof(sourceProfile));if(kinks==null)throw new ArgumentNullException(nameof(kinks));if(equipment==null)throw new ArgumentNullException(nameof(equipment));
   this.scenario=scenario?.Copy()??new RehearsalScenario{seed=seed};this.scenario.seed=seed;this.kinkCatalog=kinks;equipmentCatalog=equipment;linked=linkedContent;canonicalProgram=linkedContent?.Program??program;sceneBindings=readOnlySceneBindings;VisualEffect=visualEffect;
   Profile=PlayerProfileService.Clone(this.scenario.initialProfile??sourceProfile);random=new StorySessionRandom(seed);
   Contract=new SessionContract {sessionId="rehearsal-"+seed.ToString(System.Globalization.CultureInfo.InvariantCulture),randomSeed=seed,pacingMultiplier=this.scenario.pacingMultiplier};Contract.RefreshKinks(Profile);
   ApplyClothing(this.scenario.initialClothing);pressedActions=new HashSet<string>(this.scenario.pressedActions??Array.Empty<string>(),StringComparer.Ordinal);
   SnapshotLinkedContent(sourceBindings);
   Trace(null,"Rehearsal host created from a detached PlayerProfile clone; profile persistence, live session services and hardware output are disabled.");
  }
  static string Owner(StorySource source)=>source?.unit??"";
  static string UseKey(StorySource source,StoryBindingKind kind,string alias)=>Owner(source)+"|"+kind+"|"+alias;
  static string DialogueKey(StorySource source,string id)=>Owner(source)+"|"+id;
  void SnapshotLinkedContent(StoryContentBindings root) {
   var program=canonicalProgram;
   if(program==null) {
    if(root!=null)SnapshotContent("",root,new CompiledStoryProgram{bindings=Array.Empty<StoryBindingUse>(),dialogue=Array.Empty<StoryDialogue>()});
    return;
   }
   var owners=program.bindings.Select(b=>Owner(b.source)).Concat(program.dialogue.Select(d=>Owner(d.source))).Distinct(StringComparer.Ordinal).ToArray();
   foreach(string owner in owners) {
    var source=program.bindings.Select(b=>b.source).Concat(program.dialogue.Select(d=>d.source)).FirstOrDefault(s=>Owner(s)==owner);
    try {var content=linked==null?root:linked.ContentFor(source);SnapshotContent(owner,content,new CompiledStoryProgram{bindings=program.bindings.Where(b=>Owner(b.source)==owner).ToArray(),dialogue=program.dialogue.Where(d=>Owner(d.source)==owner).ToArray()});}
    catch(Exception error) {Trace(null,"Content owner "+owner+" is unavailable: "+error.Message);}
   }
   if(root!=null&&!owners.Contains(""))SnapshotContent("",root,new CompiledStoryProgram{bindings=Array.Empty<StoryBindingUse>(),dialogue=Array.Empty<StoryDialogue>()});
  }
  void SnapshotContent(string owner,StoryContentBindings content,CompiledStoryProgram local) {
   if(content==null)return;
   foreach(var use in local.bindings) {
    string issue=null;var row=new ContentRow();
    try {switch(use.kind) {
     case StoryBindingKind.Pose:var pose=content.Pose(use.symbol);row.posture=pose.posture;row.ready=pose.value!=null&&pose.value.Clip!=null;if(!row.ready)issue="Assign the generated PerformerPose wrapper and clip.";break;
     case StoryBindingKind.Gesture:var gesture=content.Gesture(use.symbol);string reason=null;row.ready=gesture!=null&&gesture.IsReady(out reason);if(!row.ready)issue="Gesture binding is incomplete: "+(reason??"not assigned.");break;
     case StoryBindingKind.MagicShaderProfile:content.Shader(use.symbol).ValidateConfiguration();row.ready=true;break;
     case StoryBindingKind.Funscript:row.ready=content.Motion(use.symbol)!=null;if(!row.ready)issue="Assign the imported FunscriptMotionProgram.";break;
     case StoryBindingKind.Location:
      if(sceneBindings==null)issue="No read-only saved-scene resource context was supplied for Location '"+use.symbol+"'.";
      else {sceneBindings.ValidateSymbol(use);try{locationSeats[Owner(use.source)+"|"+use.symbol]=sceneBindings.RequireLocation(use.symbol).seat!=null;}catch{locationSeats[Owner(use.source)+"|"+use.symbol]=false;}}
      row.ready=issue==null;break;
     default:if(sceneBindings==null)issue="No read-only saved-scene resource context was supplied for "+use.kind+" '"+use.symbol+"'.";else sceneBindings.ValidateSymbol(use);row.ready=issue==null;break;
    }} catch(Exception error) {issue=error.Message;row.ready=false;}
    row.issue=issue;bindings[UseKey(use.source,use.kind,use.symbol)]=row;if(issue!=null)bindingIssues[UseKey(use.source,use.kind,use.symbol)]=use.source+"\n"+issue;
   }
   foreach(var line in local.dialogue) {
    string issue=null;string key=DialogueKey(line.source,line.id);dialogueBindings.Add(key);try{content.Dialogue(line.transcript);}catch(Exception e){issue=e.Message;}
    if(issue!=null)dialogueIssues[DialogueKey(line.source,line.id)]=line.source+"\n"+issue;
   }
  }
  void ApplyClothing(bool[] slots) {var values=slots??Array.Empty<bool>();for(int i=0;i<PlayerWorldState.ClothingSlots.Length;i++)Session.world.SetClothing(PlayerWorldState.ClothingSlots[i],i<values.Length&&values[i]);}
  public StoryData Read(string key) {
   switch(key) {
    case "Completion":return new StoryData((double)Session.phaseCompletion);
    case "Session.Phase":return new StoryData(Session.phase);
    case "Session.PhaseCompletion":return new StoryData((double)Session.phaseCompletion);
    case "Session.Mood":return new StoryData((double)Session.mood);
    case "Session.IntensityMultiplier":return new StoryData((double)Session.intensityMultiplier);
    case "Session.PreparedEquipment":return new StoryData(Contract.preparedEquipmentIds);
    case "Session.PreparedEquipment.Count":return new StoryData((double)Contract.preparedEquipmentIds.Length);
    case "Session.ActiveEquipment":return new StoryData(Session.activeEquipmentIds);
    case "Session.ActiveEquipmentTags":return new StoryData(Session.activeEquipmentIds.SelectMany(Tags).Distinct(StringComparer.Ordinal).ToArray());
    case "Contract.PacingMultiplier":return new StoryData((double)Contract.pacingMultiplier);
    case "Contract.SubmissionScore":case "Profile.SubmissionScore":return new StoryData((double)Contract.submissionScore);
    case "Contract.Favor":case "Profile.Favor":return new StoryData((double)Contract.favor);
    case "Card.Intensity":throw new InvalidOperationException("Card.Intensity is read by the canonical StoryMachine only inside a card.");
    case "Card.Invocation":case "Card.Id":throw new InvalidOperationException(key+" is read by the canonical StoryMachine only inside a card.");
    case "World.Posture":return new StoryData(Session.world.posture.ToString());case "World.Hands":return new StoryData(Session.world.hands.ToString());
    case "World.Ankles":return new StoryData(Session.world.ankles.ToString());case "World.Oral":return new StoryData(Session.world.oral.ToString());case "World.Anal":return new StoryData(Session.world.anal.ToString());case "World.Genital":return new StoryData(Session.world.genital.ToString());
    case "World.Clothing.Count":return new StoryData((double)Session.world.ClothingCount);
   }
   if(key.StartsWith("World.Clothing.",StringComparison.Ordinal))return new StoryData(Session.world.GetClothing(key.Substring(15).ToLowerInvariant()));
   throw new KeyNotFoundException("Unknown rehearsal state '"+key+"'.");
  }
  public void Write(string key,StoryData value) {
   if(cancelled)throw new OperationCanceledException("Rehearsal host has stopped.");
   switch(key) {
    case "Session.Phase":Session.phase=value.String;Trace(null,"Phase entered: "+Session.phase+".");return;
    case "Session.PhaseCompletion":Session.phaseCompletion=(float)value.Numeric;return;
    case "Session.Mood":Session.mood=(float)value.Numeric;return;
    case "Session.IntensityMultiplier":if(value.Numeric<=0)throw new ArgumentOutOfRangeException(nameof(value));Session.intensityMultiplier=(float)value.Numeric;return;
    case "World.Posture":Session.world.posture=EnumValue<PlayerPosture>(value.String);return;
    case "World.Hands":Session.world.hands=EnumValue<PlayerHands>(value.String);return;
    case "World.Ankles":Session.world.ankles=EnumValue<PlayerAnkles>(value.String);return;
    case "World.Oral":Session.world.oral=EnumValue<PlayerOccupancy>(value.String);return;
    case "World.Anal":Session.world.anal=EnumValue<PlayerOccupancy>(value.String);return;
    case "World.Genital":Session.world.genital=EnumValue<PlayerOccupancy>(value.String);return;
   }
   if(key.StartsWith("World.Clothing.",StringComparison.Ordinal)){Session.world.SetClothing(key.Substring(15).ToLowerInvariant(),value.Boolean);return;}
   throw new ArgumentException("Unknown/read-only rehearsal state '"+key+"'.");
  }
  static T EnumValue<T>(string text) where T:struct,Enum {if(!Enum.TryParse<T>(text,false,out var value)||!Enum.IsDefined(typeof(T),value)||value.ToString()!=text)throw new ArgumentException("Invalid "+typeof(T).Name+": "+text);return value;}
  public StoryData Function(string name,StoryData[] args) {
   switch(name) {
    case "round":if(args.Length!=1)throw new ArgumentException("round takes one value.");return new StoryData(Math.Round(args[0].Numeric,MidpointRounding.AwayFromZero));
    case "Equipment.Tags":return new StoryData(Tags(args.Single().String));
    case "Equipment.ActiveHasTag":return new StoryData(Session.activeEquipmentIds.Any(id=>Tags(id).Contains(args.Single().String)));
    case "Equipment.PreparedHasTag":return new StoryData(Contract.preparedEquipmentIds.Any(id=>Tags(id).Contains(args.Single().String)));
    case "Equipment.IsPrepared":return new StoryData(Contract.preparedEquipmentIds.Contains(args.Single().String));
    case "Contract.KinkEnabled":return new StoryData(Contract.kinks.Any(k=>k.id==args.Single().String&&k.frequency!=KinkFrequency.No));
    case "World.Clothing.LeastRisqueWorn":return new StoryData(PlayerWorldState.ClothingSlots.FirstOrDefault(Session.world.GetClothing)??throw new InvalidOperationException("No clothing item is worn."));
    case "UI.Pressed":return new StoryData("UI.Pressed:"+args.Single().String);
    default:throw new ArgumentException("Unknown rehearsal function '"+name+"'.");
   }
  }
  string[] Tags(string id){var item=equipmentCatalog.Require(id);return item.identityTags.Concat(item.themeTags).Distinct(StringComparer.Ordinal).ToArray();}
  public async Task<StoryData> InvokeAsync(StoryCommand command,CancellationToken cancellation) {
   cancellation.ThrowIfCancellationRequested();if(cancelled)throw new OperationCanceledException();var spec=StoryVocabulary.Require(command.Verb,command.Source);
   if(spec.setupOnly&&command.Verb.StartsWith("Lara.",StringComparison.Ordinal)&&command.Verb!="Lara.StartIdle"){} // the canonical compiler/runtime own setup semantics.
   string Arg(string name)=>command.Require(name).String;
   switch(command.Verb) {
    case "Lara.StartHiddenAt":case "PlayerBody.StartHiddenAt":case "Camera.StartAt":case "Camera.Track":case "Lara.DissolveIn":case "Lara.DissolveTo":case "Lara.LocomoteTo":case "Lara.SitAt":case "Lara.Grip": {
     StoryBindingKind kind=command.Verb=="PlayerBody.StartHiddenAt"?StoryBindingKind.Location:command.Verb=="Camera.Track"?StoryBindingKind.Performer:command.Verb=="Lara.Grip"?StoryBindingKind.ContactTarget:StoryBindingKind.Location;
     string alias=command.Verb=="Camera.Track"?Arg("performer"):command.Verb=="Lara.Grip"?Arg("target"):Arg("location");RequireReady(command.Source,kind,alias);
     if(command.Verb=="Lara.DissolveTo"&&command.Arguments.TryGetValue("seated",out var seated)){var pose=RequireBinding(command.Source,StoryBindingKind.Pose,seated.String);RequireReady(command.Source,StoryBindingKind.Pose,seated.String);Session.world.posture=pose.posture;}else if(command.Verb=="Lara.DissolveIn"||command.Verb=="Lara.DissolveTo")Session.world.posture=PlayerPosture.Standing;
     if(command.Verb=="Lara.SitAt"||command.Verb=="Lara.DissolveTo"&&command.Arguments.ContainsKey("seated")) {
      if(!locationSeats.TryGetValue(Owner(command.Source)+"|"+alias,out bool hasSeat)||!hasSeat)throw new StorySourceException(command.Source,"The selected Location requires its PerformerSeat binding.");
      if(command.Verb=="Lara.SitAt")Session.world.posture=PlayerPosture.Sitting;
     }
     Stage(command);break;
    }
    case "PlayerBody.DissolveIn":case "PlayerBody.DissolveOut":RequireReady(command.Source,StoryBindingKind.PlayerBody,"PlayerBody");Stage(command);break;
    case "Lara.StartIdle":Session.world.posture=PlayerPosture.Standing;Stage(command);break;
    case "Lara.PersistentPose":case "Lara.PersistentSitPose": {
     string id=Arg("pose");var binding=RequireBinding(command.Source,StoryBindingKind.Pose,id);if(!binding.ready)throw new StorySourceException(command.Source,binding.issue);
     if(command.Verb=="Lara.PersistentSitPose"&&binding.posture!=PlayerPosture.Sitting)throw new StorySourceException(command.Source,"PersistentSitPose requires Sitting posture metadata.");Session.world.posture=binding.posture;Stage(command);break;
    }
    case "Lara.Say": {
     string transcript=Arg("text"),id=StoryDialogue.IdFor(transcript),dialogueKey=DialogueKey(command.Source,id);if(dialogueIssues.TryGetValue(dialogueKey,out var issue))throw new StorySourceException(command.Source,issue);if(!dialogueBindings.Contains(dialogueKey))throw new StorySourceException(command.Source,"No source-owned AudioClip assignment snapshot exists for this transcript.");
     Stage(command);Trace(command.Source,"Audio preview is virtual; real speech playback is not started.");break;
    }
    case "Lara.MagicShader":case "Lara.PlayOneShotGesture": {
     var kind=command.Verb=="Lara.MagicShader"?StoryBindingKind.MagicShaderProfile:StoryBindingKind.Gesture;string alias=Arg(kind==StoryBindingKind.Gesture?"gesture":"profile");var binding=RequireBinding(command.Source,kind,alias);if(!binding.ready)throw new StorySourceException(command.Source,binding.issue);Stage(command);break;
    }
    case "SubmissionScore.Calculate":Profile.submissionScore=PlayerProfileService.CalculateSubmissionScore(Profile,kinkCatalog);Contract.RefreshKinks(Profile);Trace(command.Source,"Calculated canonical submission score on the detached rehearsal profile: "+Profile.submissionScore+".");break;
    case "Session.SelectPreparedEquipment": {
     double count=command.Require("count").Numeric;if(count<0||count>3||count!=Math.Floor(count))throw new ArgumentException("Prepared equipment count must be a whole number from 0 through 3.");
     Contract.preparedEquipmentIds=EquipmentCompatibility.Select(equipmentCatalog,Profile.equipment,(int)count,random.Setup);Trace(command.Source,"Prepared equipment (canonical compatibility): "+new StoryData(Contract.preparedEquipmentIds)+".");break;
    }
    case "Popup.Show":HandlePopup(Arg("panel"),command.Source);break;
    case "Popup.ShowNeededEquipment":Trace(command.Source,"Simulated equipment confirmation: "+new StoryData(command.Require("items").Items??Array.Empty<string>())+". No user prompt was opened.");break;
    case "Popup.RemoveClothingItem":Trace(command.Source,"Simulated clothing-removal instruction for "+Arg("item")+"; no physical action is requested.");break;
    case "Popup.Ready":Trace(command.Source,"Simulated Ready confirmation: "+Arg("label")+".");break;
    case "World.Clothing.MarkRemoved":Session.world.SetClothing(Arg("item").ToLowerInvariant(),false);Trace(command.Source,"Virtual clothing state: marked "+Arg("item")+" removed.");break;
    case "World.Clothing.Clear":foreach(string slot in PlayerWorldState.ClothingSlots)Session.world.SetClothing(slot,false);Trace(command.Source,"Virtual clothing state cleared.");break;
    case "MotionDriver.PlayFunscript": {
     string id=Arg("program");var binding=RequireBinding(command.Source,StoryBindingKind.Funscript,id);if(!binding.ready)throw new StorySourceException(command.Source,binding.issue);bool loop=command.Arguments.TryGetValue("loop",out var loopValue)&&loopValue.Boolean;
     Stage(command);Trace(command.Source,"Motion curve is preview-only; no playback driver or device output is started (loop="+loop+"). Natural-end event can be selected in the rehearsal scenario.");break;
    }
    case "MotionDriver.Stop":Trace(command.Source,"Virtual motion driver stopped; no hardware output was started.");break;
    case "UI.ShowAction":activeActions.Add(Arg("action"));Trace(command.Source,"Virtual action shown: "+Arg("action")+" — "+Arg("label")+".");break;
    case "UI.HideAction":activeActions.Remove(Arg("action"));Trace(command.Source,"Virtual action hidden: "+Arg("action")+".");break;
    case "UI.WaitFor": {
     string action=Arg("action");if(!pressedActions.Remove(action)){if(ChooseEvent==null)throw new InvalidOperationException("A simulated press is required.");await ChooseEvent(command.Visit,new[]{new StoryData("UI.Pressed:"+action)},cancellation);}
     Trace(command.Source,"Simulated player pressed "+action+".");break;
    }
    case "Equipment.Activate": {
     string id=Arg("equipment");equipmentCatalog.Require(id);if(!Contract.preparedEquipmentIds.Contains(id))throw new InvalidOperationException("Only prepared equipment may activate: "+id);
     Session.activeEquipmentIds=Session.activeEquipmentIds.Concat(new[]{id}).Distinct(StringComparer.Ordinal).ToArray();Trace(command.Source,"Virtual equipment active: "+id+"; tags ["+string.Join(", ",Tags(id))+"]. Physical state effects remain authored by the card.");break;
    }
    case "Toy.Vibrate":var intensity=Math.Min(1f,Math.Max(0f,(float)command.Require("intensity").Numeric));Trace(command.Source,"Virtual toy vibration requested at "+intensity.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"; output intentionally disabled.");break;
    case "Wait": {
     double seconds=command.Require("seconds").Numeric;if(seconds<0||double.IsNaN(seconds)||double.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
     virtualSeconds+=(float)seconds;if(virtualSeconds>scenario.maxVirtualWaitSeconds)throw new InvalidOperationException("Virtual wait budget exceeded ("+scenario.maxVirtualWaitSeconds+" seconds). No wall-clock wait occurred.");
     Trace(command.Source,"Virtual wait advanced "+seconds.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" seconds; elapsed "+virtualSeconds.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+".");break;
    }
    default:throw new StorySourceException(command.Source,"No typed rehearsal effect is registered for vocabulary command '"+command.Verb+"'.");
   }
   return default;
  }
  void RequireReady(StorySource source,StoryBindingKind kind,string alias){var row=RequireBinding(source,kind,alias);if(!row.ready)throw new StorySourceException(source,row.issue??("No ready "+kind+" assignment for "+alias+"."));}
  ContentRow RequireBinding(StorySource source,StoryBindingKind kind,string alias) {
   string key=UseKey(source,kind,alias);if(bindings.TryGetValue(key,out var row))return row;
   if(bindingIssues.TryGetValue(key,out var issue))return new ContentRow{ready=false,issue=issue};
   throw new StorySourceException(source,"No source-owned "+kind+" assignment snapshot exists for '"+alias+"'.");
  }
  void Stage(StoryCommand command) {if(VisualEffect==null){Trace(command.Source,"logical only; real playback required for this visual effect.");return;}VisualEffect(Copy(command));Trace(command.Source,"Queued isolated preview effect: "+command.Verb+" (no authored scene object was changed).");}
  static StoryCommand Copy(StoryCommand source) {var copy=new StoryCommand{Visit=source.Visit,Verb=source.Verb,Awaited=source.Awaited,Source=source.Source==null?null:new StorySource(source.Source.file,source.Source.line,source.Source.column,source.Source.declaration){unit=source.Source.unit}};foreach(var p in source.Arguments)copy.Arguments[p.Key]=p.Value.Kind==StoryDataKind.List?new StoryData(p.Value.Items.ToArray()):p.Value;return copy;}
  void HandlePopup(string panel,StorySource source) {
   switch(panel) {
    case "Kinks":if(scenario.popupKinks!=null){Profile.kinks=scenario.popupKinks.Select(x=>new KinkPreference{id=x.id,frequency=x.frequency,sentiment=x.sentiment}).ToArray();Contract.RefreshKinks(Profile);}Trace(source,"Simulated Kinks popup applied to detached profile draft: "+(scenario.popupKinks==null?"unchanged":"updated preferences")+".");break;
    case "Equipment":if(scenario.popupEquipment!=null)Profile.equipment=scenario.popupEquipment.Select(x=>new EquipmentPreference{id=x.id,ownedAndWilling=x.ownedAndWilling,useAsReward=x.useAsReward,useAsPunishment=x.useAsPunishment}).ToArray();Trace(source,"Simulated Equipment popup applied to detached profile draft: "+(scenario.popupEquipment==null?"unchanged":"updated preferences")+".");break;
    case "Clothing":if(scenario.popupClothing!=null)ApplyClothing(scenario.popupClothing);Trace(source,"Simulated Clothing popup applied to virtual world state; worn count "+Session.world.ClothingCount+".");break;
    default:throw new ArgumentException("Unknown popup panel '"+panel+"'.");
   }
  }
  public void SetWaitingVisit(StoryInstructionVisit visit)=>waitingVisit=visit;
  public async Task<StoryData> WaitAnyAsync(StoryData[] events,CancellationToken cancellation) {
   cancellation.ThrowIfCancellationRequested();var accepted=events.Select(e=>e.String).ToArray();
   string selected=null; // Source-keyed event overrides are injected by the canonical controller, never borrowed from another race.
   if(selected==null)selected=accepted.FirstOrDefault(id=>pressedActions.Contains(id.StartsWith("UI.Pressed:",StringComparison.Ordinal)?id.Substring(11):id));
   if(selected==null){if(ChooseEvent==null)throw new InvalidOperationException("A simulated response is required for this event race.");return await ChooseEvent(waitingVisit,events,cancellation);}
   pressedActions.Remove(selected.StartsWith("UI.Pressed:",StringComparison.Ordinal)?selected.Substring(11):selected);
   Trace(null,"Simulated event race selected "+selected+"; no live events or listeners were installed.");return new StoryData(selected);
  }
  public void SimulateAction(string action) {
   if(cancelled)throw new OperationCanceledException();
   if(!activeActions.Contains(action))throw new InvalidOperationException("The current phase has no active action '"+action+"'.");
   pressedActions.Add(action);Trace(null,"Synthetic player press: "+action+".");
  }
  public async Task YieldAsync(CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();await Task.Yield();cancellation.ThrowIfCancellationRequested();}
  public void CancelOperations(){cancelled=true;if(!lifetime.IsCancellationRequested)lifetime.Cancel();activeActions.Clear();}
  public void Trace(StorySource source,string message)=>TraceLine?.Invoke(source,message);
  public IDictionary<string,StoryData> SnapshotState()=>stateSnapshot();
  Dictionary<string,StoryData> stateSnapshot()=>new Dictionary<string,StoryData>(StringComparer.Ordinal) {
   ["Session.Phase"]=new StoryData(Session.phase),["Session.PhaseCompletion"]=new StoryData((double)Session.phaseCompletion),["Session.Mood"]=new StoryData((double)Session.mood),["Session.IntensityMultiplier"]=new StoryData((double)Session.intensityMultiplier),
   ["Session.PreparedEquipment"]=new StoryData(Contract.preparedEquipmentIds),["Session.PreparedEquipment.Count"]=new StoryData((double)Contract.preparedEquipmentIds.Length),["Session.ActiveEquipment"]=new StoryData(Session.activeEquipmentIds),["Contract.PacingMultiplier"]=new StoryData((double)Contract.pacingMultiplier),
   ["Profile.SubmissionScore"]=new StoryData((double)Contract.submissionScore),["Profile.Favor"]=new StoryData((double)Contract.favor),["World.Posture"]=new StoryData(Session.world.posture.ToString()),["World.Hands"]=new StoryData(Session.world.hands.ToString()),["World.Ankles"]=new StoryData(Session.world.ankles.ToString()),["World.Oral"]=new StoryData(Session.world.oral.ToString()),["World.Anal"]=new StoryData(Session.world.anal.ToString()),["World.Genital"]=new StoryData(Session.world.genital.ToString()),["World.Clothing.Count"]=new StoryData((double)Session.world.ClothingCount)
  };
  public void ObserveDirector(DirectorSnapshot snapshot,CardExecutionContext context) {
   LastDirectorSnapshot=snapshot==null?null:new DirectorSnapshot{Query=snapshot.Query,Selected=snapshot.Selected,Outcome=snapshot.Outcome,Candidates=snapshot.Candidates.Select(c=>new CardCandidate{Id=c.Id,Reason=c.Reason,Eligible=c.Eligible,FinalWeight=c.FinalWeight}).ToArray(),Recent=snapshot.Recent.ToArray()};
   LastCardContext=context==null?null:new CardExecutionContext{CardId=context.CardId,Phase=context.Phase,SessionSeed=context.SessionSeed,InvocationNumber=context.InvocationNumber,IntensityMultiplier=context.IntensityMultiplier,PreparedEquipment=context.PreparedEquipment?.ToArray(),ActiveEquipment=context.ActiveEquipment?.ToArray(),Random=context.Random};
  }
  public void LeavePhase(string phase){activeActions.Clear();Trace(null,"Virtual phase scope closed: "+phase+"; simulated actions/events were cleared.");}
  public string ResourceIssue(CardDefinition card) {
   if(sceneBindings==null&&linked!=null) {var spatial=StoryProgramReachability.ForCard(linked.Program,card).bindings.FirstOrDefault(b=>b.kind==StoryBindingKind.Location||b.kind==StoryBindingKind.Performer||b.kind==StoryBindingKind.PlayerBody||b.kind==StoryBindingKind.ContactTarget);if(spatial!=null)return spatial.source+"\nUnavailable scene resource '"+spatial.symbol+"': no read-only saved-scene bindings were supplied.";}
   var view=StoryProgramReachability.ForCard(canonicalProgram,card);
   foreach(var use in view.bindings)if(bindingIssues.TryGetValue(UseKey(use.source,use.kind,use.symbol),out var issue))return issue;
   foreach(var line in view.dialogue)if(dialogueIssues.TryGetValue(DialogueKey(line.source,line.id),out var issue))return issue;
   return null;
  }
 }
}
