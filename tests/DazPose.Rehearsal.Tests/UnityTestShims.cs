// Test-only Unity/content adapters. They supply asset readiness and scene resource presence only; command behavior, interpreter semantics, virtual state and eligibility are linked from production source.
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
namespace UnityEngine {
 public class Object { }
 public class AudioClip : Object { }
 public static class Application { public static string persistentDataPath => throw new InvalidOperationException("Rehearsal tests must not access Unity player storage."); }
 public static class Debug { public static void LogWarning(object message) { } public static void LogError(object message) { } }
 public static class JsonUtility {
  sealed class PublicFieldResolver : DefaultContractResolver {
   protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization) => type.GetFields(BindingFlags.Public|BindingFlags.Instance).Select(field=>CreateProperty(field,MemberSerialization.Fields)).ToList();
  }
  static readonly JsonSerializerSettings Settings=new JsonSerializerSettings {ContractResolver=new PublicFieldResolver()};
  public static T FromJson<T>(string json)=>JsonConvert.DeserializeObject<T>(json,Settings);
  public static string ToJson(object value,bool prettyPrint=false)=>JsonConvert.SerializeObject(value,prettyPrint?Formatting.Indented:Formatting.None,Settings);
 }
 public class ScriptableObject : Object { }
 public class TextAsset : Object { public string text; public TextAsset(string value){text=value;} }
 public static class Resources { public static T Load<T>(string path) where T:class => throw new InvalidOperationException("Tests must not load Unity Resources."); }
}
namespace DazPose.Performer {
 public sealed class PerformerPose { public object Clip {get;set;} }
 public sealed class PerformerGesture { public bool IsReady(out string reason){reason=null;return true;} }
}
namespace DazPose.Motion { public sealed class FunscriptMotionProgram { } }
namespace DazPose.Performer { public sealed class PerformerMagicShaderProfile { public void ValidateConfiguration(){} } }
namespace DazPose.Story {
 using DazPose.Motion;
 using DazPose.Performer;
 using DazPose.Story.Language;
 public sealed class StoryContentBindings {
  public readonly Dictionary<string,StoryPoseContent> poses=new(StringComparer.Ordinal);
  public readonly Dictionary<string,PerformerGesture> gestures=new(StringComparer.Ordinal);
  public readonly Dictionary<string,PerformerMagicShaderProfile> shaders=new(StringComparer.Ordinal);
  public readonly Dictionary<string,FunscriptMotionProgram> motion=new(StringComparer.Ordinal);
  public readonly Dictionary<string,(string transcript,UnityEngine.AudioClip clip)> dialogue=new(StringComparer.Ordinal);
  public StoryPoseContent Pose(string id)=>poses.TryGetValue(id,out var value)?value:throw new InvalidOperationException("fixture missing pose "+id);
  public PerformerGesture Gesture(string id)=>gestures.TryGetValue(id,out var value)?value:throw new InvalidOperationException("fixture missing gesture "+id);
  public PerformerMagicShaderProfile Shader(string id)=>shaders.TryGetValue(id,out var value)?value:throw new InvalidOperationException("fixture missing shader "+id);
  public FunscriptMotionProgram Motion(string id)=>motion.TryGetValue(id,out var value)?value:throw new InvalidOperationException("fixture missing motion "+id);
  public UnityEngine.AudioClip Dialogue(string transcript) {var row=dialogue.Values.SingleOrDefault(x=>x.transcript==transcript);return row.clip??throw new InvalidOperationException("fixture missing transcript "+transcript);}
 }
 public sealed class StoryPoseContent { public PerformerPose value; public PlayerPosture posture; }
 public sealed class StorySceneBindings {
  readonly HashSet<string> locations;
  public StorySceneBindings(IEnumerable<string> names){locations=new HashSet<string>(names,StringComparer.Ordinal);}
  public StoryLocationBinding RequireLocation(string id)=>locations.Contains(id)?new StoryLocationBinding{seat=new object()}:throw new InvalidOperationException("fixture missing location "+id);
  public void ValidateSymbol(StoryBindingUse use) {if(use.kind==StoryBindingKind.Location && !locations.Contains(use.symbol))throw new InvalidOperationException("fixture missing Location "+use.symbol); if(use.kind==StoryBindingKind.Performer&&use.symbol!="Lara")throw new InvalidOperationException("unknown performer");if(use.kind==StoryBindingKind.PlayerBody&&use.symbol!="PlayerBody")throw new InvalidOperationException("unknown playerbody");if(use.kind==StoryBindingKind.ContactTarget&&use.symbol!="Penis")throw new InvalidOperationException("unknown contact target");}
 }
 public sealed class StoryLocationBinding {public object seat;}
 public sealed class StoryLinkedContent { readonly Dictionary<string,StoryContentBindings> owners; public CompiledStoryProgram Program {get;} public StoryContentBindings RootContent {get;} public StoryLinkedContent(CompiledStoryProgram program,StoryContentBindings root,Dictionary<string,StoryContentBindings> owners=null){Program=program;RootContent=root;this.owners=owners;} public StoryContentBindings ContentFor(StorySource source)=>owners!=null&&source?.unit!=null?owners[source.unit]:RootContent; }
}
