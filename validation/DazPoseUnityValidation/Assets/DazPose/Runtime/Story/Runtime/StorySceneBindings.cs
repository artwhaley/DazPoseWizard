using System;
using UnityEngine;
using DazPose.Player;
using DazPose.Performer;
using DazPose.Story.Language;
using System.Linq;
namespace DazPose.Story {
 [Serializable] public sealed class StoryTransformBinding { public string id; public Transform value; }
 [Serializable] public sealed class StoryObjectBinding { public string id; public UnityEngine.Object value; }
 [Serializable] public sealed class StoryLocationBinding {
  public string id;
  public Transform placement;
  public PerformerSeat seat;
  public Transform focus;
 }
 [DisallowMultipleComponent]
 public sealed class StorySceneBindings : MonoBehaviour {
  [Header("Legacy JSON compatibility")]
  public StoryTransformBinding[] targets = Array.Empty<StoryTransformBinding>();
  public StoryObjectBinding[] assets = Array.Empty<StoryObjectBinding>();
  [Header("Authored locations and scene roles")]
  public StoryLocationBinding[] locations=Array.Empty<StoryLocationBinding>();
  public SuccubusPerformer lara;
  public Transform laraFocus;
  public StoryPromptService prompts;
  public PlayerBodyPresence playerBody;
  public StoryDebugPresenter debugPresenter;
  public StoryLocationBinding RequireLocation(string id) {
   var matches=locations.Where(l=>l!=null && l.id==id).ToArray();
   if(matches.Length!=1 || matches[0].placement==null) throw new InvalidOperationException("Assign the placement for Location '"+id+"'.");
   return matches[0];
  }
  public void ValidateSymbol(StoryBindingUse use) {
   switch(use.kind) {
    case StoryBindingKind.Location:RequireLocation(use.symbol);break;
    case StoryBindingKind.Performer:if(use.symbol!="Lara" || lara==null || laraFocus==null) throw new InvalidOperationException("Assign the speaking Lara performer.");break;
    case StoryBindingKind.PlayerBody:if(use.symbol!="PlayerBody" || playerBody==null) throw new InvalidOperationException("Assign the prepared male player body.");break;
    case StoryBindingKind.ContactTarget:if(use.symbol!="Penis" || GetComponent<PerformanceSceneBindings>()?.ContactTarget==null) throw new InvalidOperationException("Assign the prepared measured contact target.");break;
   }
  }
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