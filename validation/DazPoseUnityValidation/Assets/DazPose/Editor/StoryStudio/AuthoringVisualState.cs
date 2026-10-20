using System;
using System.Linq;
using DazPose.Story.Language;
namespace DazPose.Editor.Studio {
 public enum AuthoringField { Placement, Pose, Hidden, BodyPlacement, BodyHidden, Camera, Tracking, Material }
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
  readonly AuthoringValue[] values;
  public AuthoringValue this[AuthoringField field]=>values[(int)field];
  public string Key {get;}
  public AuthoringVisualState(bool symbolic=false){values=Enumerable.Range(0,8).Select(i=>symbolic?new AuthoringValue(AuthoringKnowledge.Input,input:i):AuthoringValue.Baseline()).ToArray();Key=string.Join(";",values.Select(v=>v.Key));}
  AuthoringVisualState(AuthoringValue[] values){this.values=values;Key=string.Join(";",values.Select(v=>v.Key));}
  public AuthoringVisualState With(AuthoringField field,AuthoringValue value){if(this[field].Key==value.Key)return this;var copy=(AuthoringValue[])values.Clone();copy[(int)field]=value;return new AuthoringVisualState(copy);}
  public AuthoringVisualState Unknown(int mask,StorySource source,string reason){var state=this;for(int i=0;i<8;i++)if((mask&(1<<i))!=0)state=state.With((AuthoringField)i,AuthoringValue.Unknown(source,reason));return state;}
  public static AuthoringVisualState Join(AuthoringVisualState a,AuthoringVisualState b,StorySource source){var s=a;for(int i=0;i<8;i++)if(a.values[i].Key!=b.values[i].Key)s=s.With((AuthoringField)i,AuthoringValue.Unknown(source,"Different incoming card contexts"));return s;}
  public AuthoringVisualState Substitute(AuthoringVisualState input){
   AuthoringValue Resolve(AuthoringValue v){
    if(v.Knowledge==AuthoringKnowledge.Input)return input.values[v.InputField];
    var parent=v.Parent==null?null:Resolve(v.Parent);var pose=v.Pose==null?null:Resolve(v.Pose);
    if(parent==v.Parent&&pose==v.Pose)return v;
    if(v.Op=="move"&&parent?.Op=="move")parent=parent.Parent;
    if(v.Op=="sitpose"){
     if(parent==null||!parent.IsKnown||!(parent.Op=="sit"||parent.Op=="seatpose"))return AuthoringValue.Unknown(v.Source,"Seated pose needs an explicit known seat");
     return AuthoringValue.Known("seatpose",parent.Symbol,parent.Source,pose:pose);
    }
    return new AuthoringValue(v.Knowledge,v.Op,v.Symbol,v.Source,parent,pose,v.InputField,v.Reason);
   }
   return new AuthoringVisualState(values.Select(Resolve).ToArray());
  }
  public string[] Diagnostics=>Enumerable.Range(0,8).Where(i=>values[i].Knowledge==AuthoringKnowledge.Unknown).Select(i=>((AuthoringField)i)+" unknown: "+values[i].Reason+" · "+values[i].Source+". Saved baseline shown; assign an explicit "+(AuthoringField)i+" to resolve.").ToArray();
 }
 public static class AuthoringVisualReducer {
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
    case "Camera.Track":return Bit(AuthoringField.Tracking);
    case "Lara.MagicShader":return Bit(AuthoringField.Material);
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
    case "Camera.StartAt":Set(AuthoringField.Camera,V("full",Arg("location")));Set(AuthoringField.Tracking,V("false"));break;
    case "Camera.Track":Set(AuthoringField.Tracking,V(Arg("performer")=="Lara"?"true":"unsupported"));break;
    case "Lara.MagicShader":Set(AuthoringField.Material,V("material",Arg("profile")));break;
   }
   return s;
  }
 }
}
