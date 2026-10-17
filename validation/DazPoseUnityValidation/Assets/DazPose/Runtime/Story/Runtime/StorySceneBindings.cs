using System;
using UnityEngine;
using DazPose.Player;
namespace DazPose.Story {
 [Serializable] public sealed class StoryTransformBinding { public string id; public Transform value; }
 [Serializable] public sealed class StoryObjectBinding { public string id; public UnityEngine.Object value; }
 [DisallowMultipleComponent]
 public sealed class StorySceneBindings : MonoBehaviour {
  public StoryTransformBinding[] targets = Array.Empty<StoryTransformBinding>();
  public StoryObjectBinding[] assets = Array.Empty<StoryObjectBinding>();
  public StoryPromptService prompts;
  public PlayerBodyPresence playerBody;
  public StoryDebugPresenter debugPresenter;
  public Transform RequireTarget(string id) => Require(targets, id);
  static Transform Require(StoryTransformBinding[] entries, string id) {
   if(string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Target ID is required.");
   Transform result=null; int count=0;
   foreach(var e in entries ?? Array.Empty<StoryTransformBinding>()) if(e!=null && e.id==id) { result=e.value; count++; }
   if(count!=1 || result==null) throw new InvalidOperationException($"Target '{id}' must have exactly one non-null binding (found {count}).");
   return result;
  }
  public T RequireAsset<T>(string id) where T:UnityEngine.Object {
   if(string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Asset ID is required.");
   UnityEngine.Object result=null; int count=0;
   foreach(var e in assets ?? Array.Empty<StoryObjectBinding>()) if(e!=null && e.id==id) { result=e.value; count++; }
   if(count!=1 || !(result is T typed)) throw new InvalidOperationException($"Asset '{id}' must have exactly one {typeof(T).Name} binding (found {count}).");
   return typed;
  }
  public void ValidateBindings() {
   var ids=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
   foreach(var e in targets ?? Array.Empty<StoryTransformBinding>())
    if(e==null || string.IsNullOrWhiteSpace(e.id) || !ids.Add(e.id) || e.value==null)
     throw new InvalidOperationException("Target bindings require unique IDs and assigned values: "+e?.id);
   ids.Clear();
   foreach(var e in assets ?? Array.Empty<StoryObjectBinding>())
    if(e==null || string.IsNullOrWhiteSpace(e.id) || !ids.Add(e.id) || e.value==null)
     throw new InvalidOperationException("Asset bindings require unique IDs and assigned values: "+e?.id);
  }
 }
}