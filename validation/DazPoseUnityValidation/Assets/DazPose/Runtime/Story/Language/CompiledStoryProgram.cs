using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace DazPose.Story.Language {
 // Storage-independent executable data. No Unity objects, parser nodes, or nested instructions.
 public enum StoryCode { Call, Store, Jump, JumpFalse, RepeatBegin, RepeatEnd, Play, EnterPhase, LeavePhase, Return, WaitAny }
 public enum StoryAtomCode { Number, String, Bool, Symbol, Read, Add, Subtract, Multiply, Divide, Range, Negate, Not, Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual, AndSkip, OrSkip, Function, List }
 public enum StoryBindingKind { Location, Pose, Gesture, MagicShaderProfile, Performer, PlayerBody, ContactTarget, Funscript }
 [Serializable] public sealed class StorySource {
  public string file, declaration, unit;
  public int line,column;
  public StorySource() { }
  public StorySource(string file,int line,int column,string declaration=null) { this.file=file; this.line=line; this.column=column; this.declaration=declaration; }
  public override string ToString() => file+":"+line+":"+column+(string.IsNullOrEmpty(declaration) ? "" : " ("+declaration+")");
 }
 public sealed class StorySourceException : Exception {
  public StorySource SourceLocation { get; }
  public StorySourceException(StorySource source,string message,Exception inner=null) : base(source+"\n"+message,inner) { SourceLocation=source; }
 }
 [Serializable] public sealed class StoryAtom {
  public StoryAtomCode code;
  public string text;
  public double number;
  public int count;
 }
 [Serializable] public sealed class StoryExpression { public StoryAtom[] atoms=Array.Empty<StoryAtom>(); }
 [Serializable] public sealed class StoryCallArgument { public string name; public int expression; }
 [Serializable] public sealed class StoryInstruction {
  public StoryCode code;
  public string name;
  public bool awaited,completionLoop;
  public int completionTarget=-1;
  public int expression=-1,jump=-1,loop=-1;
  public StoryCallArgument[] arguments=Array.Empty<StoryCallArgument>();
  public StorySource source;
 }
 [Serializable] public sealed class StoryEntry {
  public string name,kind;
  public int start,end;
 }
 [Serializable] public sealed class StoryBindingUse {
  public int instruction;
  public string symbol;
  public StoryBindingKind kind;
  public StorySource source;
 }
 [Serializable] public sealed class StoryDialogue {
  public string id,transcript;
  public StorySource source;
  public static string IdFor(string transcript) {
   using(var sha=SHA256.Create()) return "dialogue:"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(transcript ?? ""))).Replace("-","").ToLowerInvariant();
  }
 }
 [Serializable] public sealed class CompiledStoryProgram {
  public int version=1;
  public string name;
  public bool linked;
  public StoryInstruction[] instructions=Array.Empty<StoryInstruction>();
  public StoryExpression[] expressions=Array.Empty<StoryExpression>();
  public StoryEntry[] entries=Array.Empty<StoryEntry>();
  public CardDefinition[] cards=Array.Empty<CardDefinition>();
  public StoryBindingUse[] bindings=Array.Empty<StoryBindingUse>();
  public StoryDialogue[] dialogue=Array.Empty<StoryDialogue>();
  public StoryEntry RequireEntry(string id,string kind="sequence") => entries.SingleOrDefault(e=>e.name==id && e.kind==kind) ?? throw new InvalidOperationException("Unknown story entry '"+id+"'.");
 }
 public enum StoryDataKind { None, Number, Text, Bool, List, Range }
 public readonly struct StoryData {
  public readonly StoryDataKind Kind;
  public readonly double Number,Upper;
  public readonly string Text;
  public readonly string[] Items;
  public StoryData(double number) { if(double.IsNaN(number)||double.IsInfinity(number)) throw new ArgumentException("Story numbers must be finite."); Kind=StoryDataKind.Number; Number=number; Upper=0; Text=null; Items=null; }
  public StoryData(string text) { Kind=StoryDataKind.Text; Text=text ?? ""; Number=Upper=0; Items=null; }
  public StoryData(bool value) { Kind=StoryDataKind.Bool; Number=value ? 1 : 0; Upper=0; Text=null; Items=null; }
  public StoryData(string[] items) { Kind=StoryDataKind.List; Items=items ?? Array.Empty<string>(); Number=Upper=0; Text=null; }
  public StoryData(double from,double to) { Kind=StoryDataKind.Range; Number=from; Upper=to; Text=null; Items=null; }
  public bool Boolean => Kind==StoryDataKind.Bool ? Number!=0 : throw new InvalidOperationException("Expected a boolean, got "+Kind+".");
  public double Numeric => Kind==StoryDataKind.Number ? Number : throw new InvalidOperationException("Expected a number, got "+Kind+".");
  public string String => Kind==StoryDataKind.Text ? Text : throw new InvalidOperationException("Expected text or a symbol, got "+Kind+".");
  public override string ToString() => Kind==StoryDataKind.Bool ? (Boolean ? "yes" : "no") : Kind==StoryDataKind.List ? "["+string.Join(", ",Items)+"]" : Kind==StoryDataKind.Text ? Text : Number.ToString("0.###",CultureInfo.InvariantCulture);
 }
 public sealed class StoryCommand {
  public string Verb;
  public bool Awaited;
  public StorySource Source;
  public readonly Dictionary<string,StoryData> Arguments=new Dictionary<string,StoryData>(StringComparer.Ordinal);
  public StoryData Require(string name) => Arguments.TryGetValue(name,out var value) ? value : throw new StorySourceException(Source,"Missing argument '"+name+"' for "+Verb+".");
  public double Number(string name,double fallback) => Arguments.TryGetValue(name,out var value) ? value.Numeric : fallback;
 }
 public sealed class StoryVerb {
  public string name,channel;
  public bool canRunUnawaited,instant,setupOnly;
  public string[] parameters;
  public int required;
  public Dictionary<string,StoryBindingKind> bindingParameters=new Dictionary<string,StoryBindingKind>(StringComparer.Ordinal);
 }
 public static class StoryVocabulary {
  static readonly Dictionary<string,StoryVerb> verbs=Build();
  public static StoryVerb Require(string name,StorySource source) => verbs.TryGetValue(name,out var value) ? value : throw new StorySourceException(source,"Unknown performance command '"+name+"'.");
  static Dictionary<string,StoryVerb> Build() {
   var result=new Dictionary<string,StoryVerb>(StringComparer.Ordinal);
   Action<string,string,int,bool,bool,string,bool> add=(name,args,required,instant,unawaited,channel,setup)=>result.Add(name,new StoryVerb { name=name,parameters=args.Length==0 ? Array.Empty<string>() : args.Split(','),required=required,instant=instant,canRunUnawaited=unawaited,channel=channel,setupOnly=setup });
   Action<string,string,StoryBindingKind> bind=(name,arg,kind)=>result[name].bindingParameters.Add(arg,kind);
   add("Lara.StartHiddenAt","location",1,true,true,null,true); bind("Lara.StartHiddenAt","location",StoryBindingKind.Location);
   add("PlayerBody.StartHiddenAt","location",1,true,true,null,true); bind("PlayerBody.StartHiddenAt","location",StoryBindingKind.Location);
   add("Camera.StartAt","location",1,true,true,null,true); bind("Camera.StartAt","location",StoryBindingKind.Location);
   add("Lara.StartIdle","",0,true,true,null,true);
   add("Lara.DissolveIn","location,seconds",1,false,false,"Body",false); bind("Lara.DissolveIn","location",StoryBindingKind.Location);
   add("Lara.DissolveTo","location,seated,seconds",1,false,false,"Body",false); bind("Lara.DissolveTo","location",StoryBindingKind.Location); bind("Lara.DissolveTo","seated",StoryBindingKind.Pose);
   add("Lara.LocomoteTo","location",1,false,false,"Body",false); bind("Lara.LocomoteTo","location",StoryBindingKind.Location);
   add("Lara.PersistentPose","pose",1,false,true,"Body",false); bind("Lara.PersistentPose","pose",StoryBindingKind.Pose);
   add("Lara.PersistentSitPose","pose",1,false,true,"Body",false); bind("Lara.PersistentSitPose","pose",StoryBindingKind.Pose);
   add("Lara.SitAt","location",1,false,false,"Body",false); bind("Lara.SitAt","location",StoryBindingKind.Location);
   add("Lara.Say","text",1,false,false,"Speech",false);
   add("Lara.MagicShader","profile",1,false,false,"Visual",false); bind("Lara.MagicShader","profile",StoryBindingKind.MagicShaderProfile);
   add("Lara.PlayOneShotGesture","gesture",1,false,true,"Gesture",false); bind("Lara.PlayOneShotGesture","gesture",StoryBindingKind.Gesture);
   add("Camera.Track","performer",1,true,true,null,false); bind("Camera.Track","performer",StoryBindingKind.Performer);
   add("Popup.Show","panel",1,false,false,"Popup",false);
   add("Popup.ShowNeededEquipment","items",1,false,false,"Popup",false);
   add("Popup.RemoveClothingItem","item",1,false,false,"Popup",false);
   add("Popup.Ready","label",1,false,false,"Popup",false);
   add("SubmissionScore.Calculate","",0,true,true,null,false);
   add("Session.SelectPreparedEquipment","count",1,true,true,null,false);
   add("World.Clothing.MarkRemoved","item",1,true,true,null,false);
   add("World.Clothing.Clear","",0,true,true,null,false);
   add("Wait","seconds",1,false,false,null,false);
   // Director, player action and motion commands share this typed vocabulary.
   add("Director.DrawAndExecute","tags,equipment,intensity",0,false,false,null,false);
   add("Director.Execute","card",1,false,false,null,false);
   add("PlayerBody.DissolveIn","seconds",0,false,false,"PlayerBody",false);
   add("PlayerBody.DissolveOut","seconds",0,false,false,"PlayerBody",false);
   add("Lara.Grip","target",1,false,false,"Grip",false); bind("Lara.Grip","target",StoryBindingKind.ContactTarget);
   add("MotionDriver.PlayFunscript","program,loop",1,true,true,null,false); bind("MotionDriver.PlayFunscript","program",StoryBindingKind.Funscript);
   add("MotionDriver.Stop","",0,true,true,null,false);
   add("UI.ShowAction","action,label",2,true,true,null,false);
   add("UI.HideAction","action",1,true,true,null,false);
   add("UI.WaitFor","action",1,false,false,null,false);
   add("Equipment.Activate","equipment",1,true,true,null,false);
   add("Toy.Vibrate","intensity",1,true,true,null,false);
   return result;
  }
 }
}