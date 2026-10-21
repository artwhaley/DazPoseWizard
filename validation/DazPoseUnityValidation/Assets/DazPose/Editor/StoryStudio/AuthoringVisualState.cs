using System;
using System.Linq;
using DazPose.Story.Language;
using DazPose.Performer;
namespace DazPose.Editor.Studio {
 public enum AuthoringField { Placement, Pose, Hidden, BodyPlacement, BodyHidden, Camera, Tracking, Material, Outfit, Hair, Layers, Count }
 public enum AuthoringKnowledge { Baseline, Known, Unknown, Input }
 public sealed class AuthoringValue {
  public AuthoringKnowledge Knowledge {get;} public string Op {get;} public string Symbol {get;} public StorySource Source {get;}
  public AuthoringValue Parent {get;} public AuthoringValue Pose {get;} public int InputField {get;}
  public string Reason {get;} public string Key {get;}
  public AuthoringValue(AuthoringKnowledge knowledge,string op=null,string symbol=null,StorySource source=null,AuthoringValue parent=null,AuthoringValue pose=null,int input=-1,string reason=null){
   Knowledge=knowledge;Op=op;Symbol=symbol;Source=source;Parent=parent;Pose=pose;InputField=input;Reason=reason;
   string Part(string s)=>(s??"").Length+":"+(s??"");
   Key=knowledge+"|"+Part(op)+Part(symbol)+(knowledge==AuthoringKnowledge.Known?Part(source?.unit):"")+input+Part(parent?.Key)+Part(pose?.Key);
  }
  public static AuthoringValue Baseline()=>new AuthoringValue(AuthoringKnowledge.Baseline);
  public static AuthoringValue Unknown(StorySource source,string reason)=>new AuthoringValue(AuthoringKnowledge.Unknown,source:source,reason:reason);
  public static AuthoringValue Known(string op,string symbol,StorySource source,AuthoringValue parent=null,AuthoringValue pose=null)=>new AuthoringValue(AuthoringKnowledge.Known,op,symbol,source,parent,pose);
  public bool IsKnown=>Knowledge==AuthoringKnowledge.Known;
 }
 public sealed class AuthoringVisualState {
  public const int FieldCount=(int)AuthoringField.Count;
  public const int AllMask=(1<<FieldCount)-1;
  readonly AuthoringValue[] values;
  public AuthoringValue this[AuthoringField field]=>values[(int)field];
  public string Key {get;}
  public AuthoringVisualState(bool symbolic=false){values=Enumerable.Range(0,FieldCount).Select(i=>symbolic?new AuthoringValue(AuthoringKnowledge.Input,input:i):AuthoringValue.Baseline()).ToArray();Key=string.Join(";",values.Select(v=>v.Key));}
  AuthoringVisualState(AuthoringValue[] values){this.values=values;Key=string.Join(";",values.Select(v=>v.Key));}
  public AuthoringVisualState With(AuthoringField field,AuthoringValue value){if(this[field].Key==value.Key)return this;var copy=(AuthoringValue[])values.Clone();copy[(int)field]=value;return new AuthoringVisualState(copy);}
  public AuthoringVisualState Unknown(int mask,StorySource source,string reason){var state=this;for(int i=0;i<FieldCount;i++)if((mask&(1<<i))!=0)state=state.With((AuthoringField)i,AuthoringValue.Unknown(source,reason));return state;}
  public static AuthoringVisualState Join(AuthoringVisualState a,AuthoringVisualState b,StorySource source){var s=a;for(int i=0;i<FieldCount;i++)if(a.values[i].Key!=b.values[i].Key)s=s.With((AuthoringField)i,AuthoringValue.Unknown(source,"Different incoming card contexts"));return s;}
  public AuthoringVisualState Substitute(AuthoringVisualState input){
   AuthoringValue Resolve(AuthoringValue v){
    if(v.Knowledge==AuthoringKnowledge.Input)return input.values[v.InputField];
    var parent=v.Parent==null?null:Resolve(v.Parent);var pose=v.Pose==null?null:Resolve(v.Pose);
    if(parent==v.Parent&&pose==v.Pose)return v;
    if(v.Op=="move"&&parent?.Op=="move")parent=parent.Parent;
    if(v.Op=="layerops"&&parent?.Op=="layerops"){
     var table=new char[32];for(int index=0;index<table.Length;index++)table[index]=v.Symbol[index/4*4+parent.Symbol[index]-'0'];
     return AuthoringValue.Known("layerops",new string(table),v.Source,parent.Parent);
    }
    if(v.Op=="sitpose"){
     if(parent==null||!parent.IsKnown||!(parent.Op=="sit"||parent.Op=="seatpose"))return AuthoringValue.Unknown(v.Source,"Seated pose needs an explicit known seat");
     return AuthoringValue.Known("seatpose",parent.Symbol,parent.Source,pose:pose);
    }
    return new AuthoringValue(v.Knowledge,v.Op,v.Symbol,v.Source,parent,pose,v.InputField,v.Reason);
   }
   return new AuthoringVisualState(values.Select(Resolve).ToArray());
  }
  public string[] Diagnostics=>Enumerable.Range(0,FieldCount).Where(i=>values[i].Knowledge==AuthoringKnowledge.Unknown).Select(i=>((AuthoringField)i)+" unknown: "+values[i].Reason+" · "+values[i].Source+". Saved baseline shown; assign an explicit "+(AuthoringField)i+" to resolve.").ToArray();
 }
 public static class AuthoringVisualReducer {
  public static int LayerCeiling(AuthoringValue value,int mask,int baseline=2){
   if(value==null||value.Knowledge==AuthoringKnowledge.Baseline||value.Op=="full")return baseline;
   if(value.Op=="layerops")return value.Symbol[mask*4+LayerCeiling(value.Parent,mask,baseline)+1]-'0'-1;
   throw new InvalidOperationException("Layers need an explicit incoming wardrobe state.");
  }
  public static int Bit(AuthoringField f)=>1<<(int)f;
  public static int Writes(StoryAuthoringNode n){
   switch(n.Name){
    case "Lara.StartHiddenAt":case "Lara.DissolveIn":return Bit(AuthoringField.Placement)|Bit(AuthoringField.Hidden);
    case "Lara.DissolveTo":case "Lara.SitAt":return Bit(AuthoringField.Placement)|Bit(AuthoringField.Pose)|Bit(AuthoringField.Hidden);
    case "Lara.LocomoteTo":return Bit(AuthoringField.Placement);
    case "Lara.PersistentSitPose":return Bit(AuthoringField.Placement)|Bit(AuthoringField.Pose);
    case "Lara.PersistentPose":case "Lara.StartIdle":return Bit(AuthoringField.Pose);
    case "PlayerBody.StartHiddenAt":return Bit(AuthoringField.BodyPlacement)|Bit(AuthoringField.BodyHidden);
    case "PlayerBody.DissolveIn":case "PlayerBody.DissolveOut":return Bit(AuthoringField.BodyHidden);
    case "Camera.StartAt":return Bit(AuthoringField.Camera)|Bit(AuthoringField.Tracking);
    case "Camera.Track":case "Camera.Release":return Bit(AuthoringField.Tracking);
    case "Camera.SetHeight":return Bit(AuthoringField.Camera);
    case "Lara.MagicShader":return Bit(AuthoringField.Material);
    case "Lara.SetOutfit":return Bit(AuthoringField.Outfit)|Bit(AuthoringField.Layers);
    case "Lara.SetHair":case "Lara.ClearHair":return Bit(AuthoringField.Hair);
    case "Lara.Outfit.AddOneLayer":case "Lara.Outfit.RemoveOneLayer":return Bit(AuthoringField.Layers);
    default:return 0;
   }
  }
  public static AuthoringVisualState Apply(AuthoringVisualState s,StoryAuthoringNode n){
   string Arg(string name)=>n.Literals.TryGetValue(name,out var v)?v:null;
   AuthoringValue V(string op,string symbol=null,AuthoringValue parent=null,AuthoringValue pose=null)=>AuthoringValue.Known(op,symbol,n.Source,parent,pose);
   AuthoringVisualState Set(AuthoringField f,AuthoringValue v)=>s=s.With(f,v);
   switch(n.Name){
    case "Lara.StartHiddenAt":case "Lara.DissolveIn":Set(AuthoringField.Placement,V("full",Arg("location")));Set(AuthoringField.Hidden,V(n.Name=="Lara.StartHiddenAt"?"true":"false"));break;
    case "Lara.StartIdle":Set(AuthoringField.Pose,V("initial"));break;
    case "Lara.LocomoteTo":var before=s[AuthoringField.Placement];Set(AuthoringField.Placement,V("move",Arg("location"),before.Op=="move"?before.Parent:before));break;
    case "Lara.DissolveTo":
     var seated=Arg("seated");var pose=V(seated==null?"initial":"seated",seated);
     Set(AuthoringField.Placement,V(seated==null?"yaw":"seatpose",Arg("location"),pose:seated==null?null:pose));Set(AuthoringField.Pose,pose);Set(AuthoringField.Hidden,V("false"));break;
    case "Lara.SitAt":Set(AuthoringField.Placement,V("sit",Arg("location")));Set(AuthoringField.Pose,V("defaultseat",Arg("location")));Set(AuthoringField.Hidden,V("false"));break;
    case "Lara.PersistentPose":Set(AuthoringField.Pose,V("standing",Arg("pose")));break;
    case "Lara.PersistentSitPose":
     var seat=s[AuthoringField.Placement];var seatedPose=V("seated",Arg("pose"));
     Set(AuthoringField.Pose,seatedPose);
     if(seat.Knowledge==AuthoringKnowledge.Input)Set(AuthoringField.Placement,V("sitpose",parent:seat,pose:seatedPose));
     else if(seat.IsKnown&&(seat.Op=="sit"||seat.Op=="seatpose"))Set(AuthoringField.Placement,AuthoringValue.Known("seatpose",seat.Symbol,seat.Source,pose:seatedPose));
     else Set(AuthoringField.Placement,AuthoringValue.Unknown(n.Source,"Seated pose needs an explicit known seat"));break;
    case "PlayerBody.StartHiddenAt":Set(AuthoringField.BodyPlacement,V("full",Arg("location")));Set(AuthoringField.BodyHidden,V("true"));break;
    case "PlayerBody.DissolveIn":case "PlayerBody.DissolveOut":Set(AuthoringField.BodyHidden,V(n.Name.EndsWith("Out",StringComparison.Ordinal)?"true":"false"));break;
    case "Camera.StartAt":Set(AuthoringField.Camera,V("mount",Arg("height")));Set(AuthoringField.Tracking,V("false"));break;
    case "Camera.Track":Set(AuthoringField.Tracking,V(Arg("performer")=="Lara"?"true":"unsupported"));break;
    case "Camera.SetHeight":Set(AuthoringField.Camera,V("mount",Arg("height")));break;
    case "Camera.Release":Set(AuthoringField.Tracking,V("false"));break;
    case "Lara.MagicShader":Set(AuthoringField.Material,V("material",Arg("profile")));break;
    case "Lara.SetOutfit":Set(AuthoringField.Outfit,V("outfit",Arg("outfit")));Set(AuthoringField.Layers,V("full"));break;
    case "Lara.SetHair":Set(AuthoringField.Hair,V("hair",Arg("hair")));break;
    case "Lara.ClearHair":Set(AuthoringField.Hair,V("clear"));break;
    case "Lara.Outfit.AddOneLayer":case "Lara.Outfit.RemoveOneLayer":
     var prior=s[AuthoringField.Layers];var table=new char[32];bool add=n.Name.EndsWith("AddOneLayer",StringComparison.Ordinal);
     for(int mask=0;mask<=WardrobeLayerState.AllLayers;mask++)for(int ceiling=-1;ceiling<=2;ceiling++){
      int slot=mask*4+ceiling+1;int value=prior.Op=="layerops"?prior.Symbol[slot]-'0'-1:ceiling;
      table[slot]=(char)('0'+1+(add?WardrobeLayerState.AddLowestCeiling(mask,value):WardrobeLayerState.RemoveHighestCeiling(mask,value)));
     }
     Set(AuthoringField.Layers,V("layerops",new string(table),prior.Op=="layerops"?prior.Parent:prior));break;
   }
   return s;
  }
 }
}
