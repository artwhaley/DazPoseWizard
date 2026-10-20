using System;
using System.Linq;
using System.Text;
using DazPose.Story;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor.Studio {
 public sealed partial class SceneDirectorStudioWindow {
  [SerializeField] StoryProgramSequence nativeSequence;
  NativeStageContext nativeContext;
  bool stageAfter=true;
  [SerializeField] bool updateWhileDragging;
  string anchorStamp;
  double nextAnchorPoll;
  void AutoResolve(){if(this!=null&&host==null)try{ResolveNativeDefaults();}catch(Exception e){error=e.Message;}}
  void ResolveNativeDefaults() {
   if(host==null)SelectHost(NativeStageContext.FindHost());
   if(nativeSequence==null){var matches=host.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StoryProgramSequence>(true)).Where(s=>s.Bindings==host).ToArray();if(matches.Length==1)nativeSequence=matches[0];}
   if(source==null&&nativeSequence!=null&&nativeSequence.CompiledSource!=null)SelectSource(nativeSequence.CompiledSource);
  }
  void BuildPreview() {
   if(host==null||source==null)ResolveNativeDefaults();
   if(nativeSequence!=null&&(nativeSequence.Bindings!=host||nativeSequence.CompiledSource!=source))throw new InvalidOperationException("Saved story sequence, source and host disagree. Select matching references.");
   var resolved=NativeStageContext.Resolve(host,false);
   if(stage!=null&&(stage.Context.Lara!=resolved.Lara||stage.Context.Body!=resolved.Body||stage.Context.PlayerRoot!=resolved.PlayerRoot||stage.Context.ViewRig!=resolved.ViewRig||stage.Context.Camera!=resolved.Camera)){stage.Dispose();stage=null;}
   if(stage==null||!stage.Ready){stage?.Dispose();nativeContext=resolved;stage=new NativeSceneStage(nativeContext);anchorStamp=null;}
  }
  void NativeMarkButton(string label,Transform mark) {
   if(mark==null)return;
   using(new EditorGUILayout.HorizontalScope()){
    if(GUILayout.Button("Select "+label)){Selection.activeGameObject=mark.gameObject;Tools.current=Tool.Move;EditorGUIUtility.PingObject(mark);}
    if(GUILayout.Button("Frame",GUILayout.Width(60))){Selection.activeGameObject=mark.gameObject;SceneView.lastActiveSceneView?.FrameSelected();}
   }
  }
  string CaptureAnchorStamp() {
   var text=new StringBuilder();
   var seen=new System.Collections.Generic.HashSet<Transform>();
   void Mark(Transform t){if(t!=null&&seen.Add(t))text.Append(t.GetEntityId().ToString()).Append(t.localToWorldMatrix.ToString("R"));}
   foreach(var dependency in activeDependencies){if(dependency is Transform transform){if(transform!=host.laraFocus)Mark(transform);}else if(dependency is DazPose.Performer.PerformerSeat seat){Mark(seat.SeatAnchor);Mark(seat.ApproachAnchor);}}
   foreach(var root in new[]{nativeContext.Lara.transform,nativeContext.Body.transform,nativeContext.PlayerRoot})Mark(root.parent);
   var focus=host.laraFocus;if(focus!=null)text.Append(focus.localPosition.ToString("R")).Append(focus.localRotation.ToString("R")).Append(focus.localScale.ToString("R"));
   return text.ToString();
  }
  void PollNativeAnchors() {
   if(stage==null||projectionPending||indexStale)return;
   if(!stage.Ready){string message=stage.Failure??stage.Status;stage.Dispose();stage=null;nativeContext=null;error=message;return;}
   if(stage.IsSaveSuspended||EditorApplication.isUpdating||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<nextAnchorPoll)return;
   nextAnchorPoll=EditorApplication.timeSinceStartup+.04;
   try{string next=CaptureAnchorStamp();if(next!=anchorStamp){stage.ReanchorAuthoring();anchorStamp=next;ObserveNative("reanchor");Repaint();}}
   catch(Exception e){HaltVisual(e);}
  }
  void OnGUI() => DrawAuthoringWindow();
 }
}
