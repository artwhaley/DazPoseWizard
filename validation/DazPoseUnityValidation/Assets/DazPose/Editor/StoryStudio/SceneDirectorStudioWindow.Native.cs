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
  int nativeTab;
  bool stageAfter=true;
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
   stage?.Dispose();nativeContext=NativeStageContext.Resolve(host);stage=new NativeSceneStage(nativeContext);visualState=new RehearsalVisualState();anchorStamp=null;
  }
  void NativeMarkButton(string label,Transform mark) {
   if(mark==null)return;
   using(new EditorGUILayout.HorizontalScope()){
    if(GUILayout.Button("Select "+label)){Selection.activeGameObject=mark.gameObject;EditorGUIUtility.PingObject(mark);}
    if(GUILayout.Button("Frame",GUILayout.Width(60))){Selection.activeGameObject=mark.gameObject;SceneView.lastActiveSceneView?.FrameSelected();}
   }
  }
  string CaptureAnchorStamp() {
   var text=new StringBuilder();
   void Mark(Transform t){if(t!=null)text.Append(t.GetEntityId().ToString()).Append(t.localToWorldMatrix.ToString("R"));}
   foreach(var effect in visualState.Effects) {
    switch(effect.Kind){
     case VisualIntentKind.Hidden:case VisualIntentKind.DissolveIn:case VisualIntentKind.Move:case VisualIntentKind.DissolveStanding:case VisualIntentKind.BodyHidden:case VisualIntentKind.CameraStart:Mark(host.RequireLocation(effect.Symbol).placement);break;
     case VisualIntentKind.Sit:case VisualIntentKind.DissolveSeated:
      var seat=host.RequireLocation(effect.Symbol).seat;
      if(seat!=null){Mark(seat.SeatAnchor);Mark(seat.ApproachAnchor);text.Append(EditorJsonUtility.ToJson(seat));}break;
    }
   }
   foreach(var root in new[]{nativeContext.Lara.transform,nativeContext.Body.transform,nativeContext.PlayerRoot})Mark(root.parent);
   var focus=host.laraFocus;if(focus!=null)text.Append(focus.localPosition.ToString("R")).Append(focus.localRotation.ToString("R")).Append(focus.localScale.ToString("R"));
   return text.ToString();
  }
  void PollNativeAnchors() {
   if(stage==null)return;
   if(!stage.Ready){string message=stage.Failure??stage.Status;StopRehearsal();stage.Dispose();stage=null;nativeContext=null;error=message;return;}
   if(stage.IsSaveSuspended||EditorApplication.isUpdating||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<nextAnchorPoll)return;
   nextAnchorPoll=EditorApplication.timeSinceStartup+.04;
   try{string next=CaptureAnchorStamp();if(next!=anchorStamp){stage.Apply(visualState,rehearsalContent);anchorStamp=next;nativeRestages++;ObserveNative("reanchor");Repaint();}}
   catch(Exception e){HaltVisual(e);}
  }
  void OnGUI() {
   using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
    EditorGUILayout.LabelField("Scene Director Studio",EditorStyles.boldLabel);
    if(GUILayout.Button("Find scene/source",EditorStyles.toolbarButton))Try(ResolveNativeDefaults);
   }
   var nextHost=(StorySceneBindings)EditorGUILayout.ObjectField("Scene host",host,typeof(StorySceneBindings),true);if(nextHost!=host){nativeSequence=null;SelectHost(nextHost);}
   var nextSource=(CompiledStoryAsset)EditorGUILayout.ObjectField("Story source",source,typeof(CompiledStoryAsset),false);if(nextSource!=source){nativeSequence=null;SelectSource(nextSource);}
   if(manifest==null){EditorGUILayout.HelpBox(error??"Open the saved FirstContact scene and click Find scene/source.",MessageType.Info);return;}
   using(new EditorGUILayout.HorizontalScope()){
    if(GUILayout.Button("Start directing"))Try(StartRehearsal);
    if(GUILayout.Button("Stop"))Release();
    stageAfter=GUILayout.Toggle(stageAfter,stageAfter?"After cue":"Before cue","Button");
   }
   EditorGUILayout.LabelField(stage!=null&&stage.Ready?"EDIT MODE — LIVE STAGING · G4 acceptance pending":"EDIT MODE — STOPPED",EditorStyles.boldLabel);
   EditorGUILayout.LabelField(stage?.Composition??"Use Unity Scene and Game views. Start directing stages the first script cue.",EditorStyles.wordWrappedMiniLabel);
   if(visualState.Observation!=null)EditorGUILayout.HelpBox(visualState.Observation,MessageType.Info);
   if(current!=null)EditorGUILayout.LabelField(current.Source+" · PC "+current.ProgramCounter+" · visit "+current.Visit.Encounter,EditorStyles.wordWrappedMiniLabel);
   using(new EditorGUI.DisabledScope(rehearsal==null||transportBusy||previewFailure!=null))using(new EditorGUILayout.HorizontalScope()){
    if(GUILayout.Button("Previous"))BackRehearsal();
    if(GUILayout.Button("Next"))Transport(async()=>{await rehearsal.StepAsync();await FlushPreviewEvents(generation);});
   }
   if(host!=null)NativeMarkButton("Lara camera focus (head-local offset)",host.laraFocus);
   nativeTab=GUILayout.Toolbar(nativeTab,new[]{"Cues","Bindings","Scenario / Director"});
   if(nativeTab==0)DrawFlow();else if(nativeTab==1)DrawDetails();else DrawTransport();
   if(error!=null)EditorGUILayout.HelpBox(error,MessageType.Error);
  }
 }
}
