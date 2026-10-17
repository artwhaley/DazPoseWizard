using System;
using System.Linq;
using DazPose.Game;
namespace DazPose.Story {
 public sealed partial class StoryValueAccessor {
  static string TypeFor(string scope,string key,bool writing=false) {
   if(string.IsNullOrWhiteSpace(key)) throw new ArgumentException("State key is required.");
   switch(scope) {
    case "profile":
     if(key=="submissionScore" || key=="favor") return "number";
     if(key.StartsWith("fact:",StringComparison.Ordinal) && key.Length>5) return null;
     break;
    case "contract":
     if(writing) throw new ArgumentException("Contract is read-only to state opcodes.");
     if(key=="pacingMultiplier" || key=="preparedEquipmentCount") return "number";
     if(key.StartsWith("preparedEquipment:",StringComparison.Ordinal) && key.Length>18) return "bool";
     break;
    case "session":
     if(key=="phase") return "string";
     if(key=="phaseCompletion" || key=="mood" || key=="intensityMultiplier") return "number";
     break;
    case "world":
     if(key=="posture" || key=="hands" || key=="ankles" || key=="oral" || key=="anal" || key=="genital") return "string";
     if(key=="clothing.count") { if(writing) throw new ArgumentException("clothing.count is derived and read-only."); return "number"; }
     if(key.StartsWith("clothing.",StringComparison.Ordinal) && PlayerWorldState.ClothingSlots.Contains(key.Substring(9))) return "bool";
     break;
    case "local": return null;
   }
   throw new ArgumentException("Unknown scope/key: "+scope+"."+key);
  }
  static void EnumValue<T>(string value) where T:struct,Enum {
   if(!Enum.TryParse<T>(value,false,out var result) || !Enum.IsDefined(typeof(T),result) || result.ToString()!=value)
    throw new ArgumentException("Invalid "+typeof(T).Name+": "+value);
  }
  static void ValidateWorldEnum(string key,string value) {
   switch(key) {
    case "posture": EnumValue<PlayerPosture>(value); break;
    case "hands": EnumValue<PlayerHands>(value); break;
    case "ankles": EnumValue<PlayerAnkles>(value); break;
    case "oral": case "anal": case "genital": EnumValue<PlayerOccupancy>(value); break;
   }
  }
  public static void ValidateCondition(StoryCondition condition) {
   if(condition==null) throw new ArgumentException("flow.if requires a condition.");
   StoryValue.ValidateType(condition.valueType);
   string expected=TypeFor(condition.scope,condition.key);
   if(expected!=null && expected!=condition.valueType) throw new ArgumentException("Condition type must be "+expected+".");
   if(!float.IsFinite(condition.numberValue)) throw new ArgumentException("Condition number must be finite.");
   switch(condition.comparison) {
    case "eq": case "neq": break;
    case "gt": case "gte": case "lt": case "lte":
     if(condition.valueType!="number") throw new ArgumentException("Ordered comparisons require numbers."); break;
    case "contains": case "notContains":
     if(condition.valueType!="string") throw new ArgumentException("contains requires strings."); break;
    default: throw new ArgumentException("Unknown comparison: "+condition.comparison);
   }
   if(condition.scope=="world" && expected=="string") ValidateWorldEnum(condition.key,condition.stringValue);
  }
  static void MutationKey(StoryStep s,out string scope,out string key) {
   if(s.op.StartsWith("local.",StringComparison.Ordinal)) { scope="local"; key=s.key; return; }
   int dot=s.key?.IndexOf('.') ?? -1;
   if(dot<=0) throw new ArgumentException("Use a scoped key, e.g. session.mood, world.clothing.shoes, profile.fact:met-lara.");
   scope=s.key.Substring(0,dot); key=s.key.Substring(dot+1);
   if(s.op.StartsWith("state.",StringComparison.Ordinal) && scope!="session" && scope!="world")
    throw new ArgumentException("state opcodes may write session/world only.");
   if(s.op.StartsWith("memory.",StringComparison.Ordinal) && scope!="profile")
    throw new ArgumentException("memory opcodes may write profile only.");
  }
  static StoryArgument ValueArgument(StoryStep s) => (s.args ?? Array.Empty<StoryArgument>()).SingleOrDefault(a=>a.name=="value");
  static StoryValue Literal(StoryStep s,string type) {
   var a=ValueArgument(s);
   if(a!=null) {
    StoryValue.ValidateType(a.valueType);
    if(type!=null && a.valueType!=type) throw new ArgumentException("Value argument must be "+type+".");
    return a.valueType=="number" ? new StoryValue(a.numberValue) : a.valueType=="bool" ? new StoryValue(a.boolValue) : new StoryValue(a.stringValue);
   }
   if(type==null) throw new ArgumentException("Facts/locals require args[{name:value,valueType:string|number|bool,...}] for set.");
   return type=="number" ? new StoryValue(s.number) : type=="bool" ? new StoryValue(s.flag) : new StoryValue(s.value);
  }
  public static void ValidateMutation(StoryStep s) {
   MutationKey(s,out string scope,out string key);
   string type=TypeFor(scope,key,true);
   if(s.op.EndsWith(".add",StringComparison.Ordinal)) {
    if(type!=null && type!="number") throw new ArgumentException("add requires a numeric key.");
    if(ValueArgument(s)!=null && ValueArgument(s).valueType!="number") throw new ArgumentException("add requires a numeric argument.");
   } else {
    var literal=Literal(s,type);
    if(scope=="world" && literal.Type=="string") ValidateWorldEnum(key,literal.Text);
    if(scope=="profile" && key=="submissionScore") ValidateScore(literal.Number);
   }
  }
  static void ValidateScore(float score) {
   if(score<0 || score>100 || score!=MathF.Floor(score)) throw new ArgumentException("submissionScore must be an integer from 0 through 100.");
  }
  public StoryValue Read(string scope,string key) {
   TypeFor(scope,key);
   var p=context.Application.Profile.Current; var s=context.Session; var w=s.world; var c=context.Contract;
   switch(scope) {
    case "profile":
     if(key=="submissionScore") return new StoryValue(p.submissionScore);
     if(key=="favor") return new StoryValue(p.favor);
     var fact=p.facts.SingleOrDefault(f=>f.key==key.Substring(5)) ?? throw new ArgumentException("Persistent fact is unset: "+key);
     return fact.valueType=="number" ? new StoryValue(fact.numberValue) : fact.valueType=="bool" ? new StoryValue(fact.boolValue) : new StoryValue(fact.stringValue);
    case "contract":
     if(key=="pacingMultiplier") return new StoryValue(c.pacingMultiplier);
     if(key=="preparedEquipmentCount") return new StoryValue(c.preparedEquipmentIds.Length);
     return new StoryValue(c.preparedEquipmentIds.Contains(key.Substring(18)));
    case "session":
     switch(key) { case "phase":return new StoryValue(s.phase); case "phaseCompletion":return new StoryValue(s.phaseCompletion);
      case "mood":return new StoryValue(s.mood); default:return new StoryValue(s.intensityMultiplier); }
    case "world":
     switch(key) {
      case "posture":return new StoryValue(w.posture.ToString()); case "hands":return new StoryValue(w.hands.ToString());
      case "ankles":return new StoryValue(w.ankles.ToString()); case "oral":return new StoryValue(w.oral.ToString());
      case "anal":return new StoryValue(w.anal.ToString()); case "genital":return new StoryValue(w.genital.ToString());
      case "clothing.count":return new StoryValue(w.ClothingCount);
      default:return new StoryValue(w.GetClothing(key.Substring(9)));
     }
    default:return context.Locals.Require(key);
   }
  }
  public bool Evaluate(StoryCondition condition) {
   ValidateCondition(condition);
   var value=Read(condition.scope,condition.key);
   if(value.Type!=condition.valueType) throw new ArgumentException("Condition value type differs from stored value.");
   bool equal=value.Type=="number" ? value.Number==condition.numberValue : value.Type=="bool" ? value.Flag==condition.boolValue : value.Text==(condition.stringValue ?? "");
   switch(condition.comparison) {
    case "eq":return equal; case "neq":return !equal;
    case "gt":return value.Number>condition.numberValue; case "gte":return value.Number>=condition.numberValue;
    case "lt":return value.Number<condition.numberValue; case "lte":return value.Number<=condition.numberValue;
    case "contains":return value.Text.Contains(condition.stringValue ?? "");
    default:return !value.Text.Contains(condition.stringValue ?? "");
   }
  }
  public void Mutate(StoryStep step) {
   context.CheckAuthority(); ValidateMutation(step);
   MutationKey(step,out string scope,out string key);
   bool add=step.op.EndsWith(".add",StringComparison.Ordinal);
   var value=Literal(step,add ? "number" : TypeFor(scope,key,true));
   if(add) {
    var current=Read(scope,key);
    if(current.Type!="number") throw new ArgumentException("Cannot add to a nonnumeric value.");
    value=new StoryValue(current.Number+value.Number);
   }
   if(scope=="profile") {
    context.Application.Profile.Update(p=> {
     if(key=="submissionScore") { ValidateScore(value.Number); p.submissionScore=(int)value.Number; }
     else if(key=="favor") p.favor=value.Number;
     else {
      string factKey=key.Substring(5); var fact=p.facts.SingleOrDefault(f=>f.key==factKey);
      if(fact==null) { fact=new PersistentStoryFact { key=factKey }; p.facts=p.facts.Concat(new[]{fact}).ToArray(); }
      fact.valueType=value.Type; fact.stringValue=value.Text; fact.numberValue=value.Number; fact.boolValue=value.Flag;
     }
    }); context.Log("Saved "+StoryFlowLog.Key(scope,key)+" as "+StoryFlowLog.Value(Read(scope,key))+"."); return;
   }
   if(scope=="local") { context.Locals.Set(key,value); context.Log("Set local "+key+" to "+StoryFlowLog.Value(value)+"."); return; }
   var s=context.Session; var w=s.world;
   if(scope=="session") {
    switch(key) { case "phase":s.phase=value.Text;break; case "phaseCompletion":s.phaseCompletion=value.Number;break;
     case "mood":s.mood=value.Number;break; case "intensityMultiplier":s.intensityMultiplier=value.Number;break; }
   } else {
    switch(key) {
     case "posture":w.posture=Enum.Parse<PlayerPosture>(value.Text);break;
     case "hands":w.hands=Enum.Parse<PlayerHands>(value.Text);break;
     case "ankles":w.ankles=Enum.Parse<PlayerAnkles>(value.Text);break;
     case "oral":w.oral=Enum.Parse<PlayerOccupancy>(value.Text);break;
     case "anal":w.anal=Enum.Parse<PlayerOccupancy>(value.Text);break;
     case "genital":w.genital=Enum.Parse<PlayerOccupancy>(value.Text);break;
     default:w.SetClothing(key.Substring(9),value.Flag);break;
    }
   }
   context.Log("Set "+StoryFlowLog.Key(scope,key)+" to "+StoryFlowLog.Value(Read(scope,key))+".");
  }
 }
}