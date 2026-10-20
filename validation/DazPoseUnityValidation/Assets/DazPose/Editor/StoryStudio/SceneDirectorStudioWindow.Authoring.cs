using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor.Studio {
 public sealed partial class SceneDirectorStudioWindow {
  readonly Dictionary<string,string[]> cueLines=new Dictionary<string,string[]>();
  readonly Dictionary<string,SourceBindingManifest> cueManifests=new Dictionary<string,SourceBindingManifest>();
  SourceBindingManifest inspectorManifest;
  StorySource inspectorOwner;
  bool contextNeedsSeat,showConnection,showPlaybackSettings,showTechnical,refreshCueQueued,draggingDivider;
  [SerializeField] float sidebarWidth=310;
  GUIStyle cueStyle,sectionStyle;
  CompiledStoryProgram CueProgram=>flowContent?.Program??manifest?.Analysis.Program;
  StoryInstruction SelectedInstruction=>selectedPc>=0&&selectedPc<(CueProgram?.instructions.Length??0)?CueProgram.instructions[selectedPc]:null;

  void DrawAuthoringWindow() {
   if(cueStyle==null){
    cueStyle=new GUIStyle(EditorStyles.label){alignment=TextAnchor.UpperLeft,wordWrap=true,padding=new RectOffset(12,12,9,9),richText=false};
    sectionStyle=new GUIStyle(EditorStyles.boldLabel){wordWrap=true,padding=new RectOffset(8,4,8,5)};
   }
   using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
    GUILayout.Label(source!=null?source.name:"Scene Director",EditorStyles.boldLabel);
    GUILayout.FlexibleSpace();
    GUILayout.Label(transportBusy?"Finding cue…":stage!=null&&stage.Ready?"Live preview":"Edit Mode",EditorStyles.miniLabel);
    using(new EditorGUI.DisabledScope(stage==null&&rehearsal==null))if(GUILayout.Button("Stop preview",EditorStyles.toolbarButton))Release();
    showConnection=GUILayout.Toggle(showConnection,"Scene / script",EditorStyles.toolbarButton);
   }
   if(showConnection||manifest==null)DrawConnection();
   if(manifest==null){EditorGUILayout.HelpBox(error??"Open a performance scene, then choose Find open scene above.",MessageType.Info);return;}
   using(new EditorGUILayout.HorizontalScope()){
    using(new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Clamp(sidebarWidth,220,Mathf.Max(220,position.width-310)))))DrawCueSidebar();
    var divider=GUILayoutUtility.GetRect(5,5,GUILayout.ExpandHeight(true));
    EditorGUI.DrawRect(divider,new Color(0,0,0,.25f));EditorGUIUtility.AddCursorRect(divider,MouseCursor.ResizeHorizontal);
    var ev=Event.current;
    if(ev.type==EventType.MouseDown&&divider.Contains(ev.mousePosition)){draggingDivider=true;ev.Use();}
    if(draggingDivider&&ev.type==EventType.MouseDrag){sidebarWidth=Mathf.Clamp(ev.mousePosition.x,220,Mathf.Max(220,position.width-310));Repaint();ev.Use();}
    if(ev.type==EventType.MouseUp)draggingDivider=false;
    using(new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))DrawCueInspector();
   }
  }
  void DrawConnection(){
   using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){
    var nextHost=(StorySceneBindings)EditorGUILayout.ObjectField("Performance scene",host,typeof(StorySceneBindings),true);
    if(nextHost!=host){nativeSequence=null;SelectHost(nextHost);}
    var nextSource=(CompiledStoryAsset)EditorGUILayout.ObjectField("Script",source,typeof(CompiledStoryAsset),false);
    if(nextSource!=source){nativeSequence=null;SelectSource(nextSource);}
    if(GUILayout.Button("Find open scene"))Try(ResolveNativeDefaults);
   }
  }
  string CueText(StoryInstruction instruction){
   var location=instruction.source;
   if(location!=null){
    if(!cueLines.TryGetValue(location.file,out var lines)){
     lines=File.Exists(location.file)?File.ReadAllLines(location.file):Array.Empty<string>();cueLines[location.file]=lines;
    }
    if(location.line>0&&location.line<=lines.Length)return lines[location.line-1].Trim();
   }
   return instruction.name??instruction.code.ToString();
  }
  bool VisibleCue(StoryInstruction instruction)=>instruction.code!=StoryCode.Return&&instruction.code!=StoryCode.LeavePhase&&instruction.code!=StoryCode.RepeatEnd&&instruction.code!=StoryCode.Jump;
  void DrawCueSidebar(){
   GUILayout.Label("Script cues",sectionStyle);
   search=EditorGUILayout.TextField(search,EditorStyles.toolbarSearchField);
   flowScroll=GUILayout.BeginScrollView(flowScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar,GUILayout.ExpandHeight(true));
   var program=CueProgram;
   if(program!=null)foreach(var entry in program.entries){
    bool header=false;
    for(int pc=entry.start;pc<entry.end;pc++){
     var instruction=program.instructions[pc];if(!VisibleCue(instruction))continue;
     string text=CueText(instruction);
     if(search.Length>0&&(entry.name+" "+text).IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
     if(!header){GUILayout.Label(entry.name+" · "+entry.kind,sectionStyle);header=true;}
     float width=Mathf.Max(150,Mathf.Min(sidebarWidth,position.width-310)-25);
     var label=new GUIContent((instruction.source?.line.ToString()??"?")+"   "+text,text);
     var rect=GUILayoutUtility.GetRect(label,cueStyle,GUILayout.Height(Mathf.Max(38,cueStyle.CalcHeight(label,width))),GUILayout.ExpandWidth(true));
     bool selected=pc==selectedPc;
     if(selected){EditorGUI.DrawRect(rect,EditorGUIUtility.isProSkin?new Color(.16f,.30f,.43f):new Color(.68f,.81f,.92f));EditorGUI.DrawRect(new Rect(rect.x,rect.y,3,rect.height),new Color(.28f,.68f,1));}
     else if(rect.Contains(Event.current.mousePosition))EditorGUI.DrawRect(rect,new Color(.5f,.5f,.5f,.12f));
     using(new EditorGUI.DisabledScope(transportBusy))if(GUI.Button(rect,label,cueStyle))SelectCue(pc,entry.name);
    }
   }
   if(manifest!=null)foreach(var message in manifest.Analysis.SyntaxDiagnostics)EditorGUILayout.HelpBox(message,MessageType.Error);
   GUILayout.EndScrollView();
  }
  SourceBindingManifest ManifestFor(StorySource owner){
   if(owner==null)return manifest;
   if(!cueManifests.TryGetValue(owner.file,out var found)){
    found=SourceBindingManifest.Analyze(owner.file,owner.unit);cueManifests[owner.file]=found;
   }
   return found;
  }
  StoryContentBindings ContentForInspector(StorySource owner){
   if(flowContent!=null)return flowContent.ContentFor(owner);
   var info=ManifestFor(owner);return SourceBindingReconciler.FindOwner(info.SourceGuid,SourceBindingReconciler.CompanionPath(info.SourcePath));
  }
  void SelectCue(int pc,string entry){
   if(transportBusy)return;
   var instruction=CueProgram.instructions[pc];selectedPc=pc;selectedSource=instruction.source;selectedEntry=entry;selectedVisit=1;
   selectedRequirement=-1;selectedKey=null;detailManifest=ManifestFor(selectedSource);StopAudio();error=null;
   NavigateSelected();
  }
  void AdjacentCue(int direction){
   var program=CueProgram;if(program==null)return;
   for(int pc=selectedPc+direction;pc>=0&&pc<program.instructions.Length;pc+=direction){
    if(!VisibleCue(program.instructions[pc]))continue;
    var entry=program.entries.FirstOrDefault(e=>pc>=e.start&&pc<e.end);if(entry!=null){SelectCue(pc,entry.name);return;}
   }
  }
  void DrawCueInspector(){
   detailScroll=GUILayout.BeginScrollView(detailScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar,GUILayout.ExpandHeight(true));
   float oldLabelWidth=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=125;
   try{
    var instruction=SelectedInstruction;
    if(instruction==null){
     GUILayout.Label("Choose a cue",sectionStyle);
     EditorGUILayout.HelpBox("Click any cue on the left. Its pose, performer marks and camera controls appear here. The Scene and Game views show the selected moment.",MessageType.Info);return;
    }
    GUILayout.Label(selectedEntry,EditorStyles.boldLabel);
    GUILayout.Label(CueText(instruction),new GUIStyle(EditorStyles.wordWrappedLabel){fontSize=14,padding=new RectOffset(0,0,8,10)});
    using(new EditorGUILayout.HorizontalScope())using(new EditorGUI.DisabledScope(transportBusy)){
     if(GUILayout.Button("← Previous cue"))AdjacentCue(-1);
     if(GUILayout.Button("Next cue →"))AdjacentCue(1);
    }
    using(new EditorGUILayout.HorizontalScope())using(new EditorGUI.DisabledScope(transportBusy)){
     int timing=GUILayout.Toolbar(stageAfter?1:0,new[]{"Before this cue","After this cue"});
     if((timing==1)!=stageAfter){stageAfter=timing==1;NavigateSelected();}
     if(GUILayout.Button("Preview",GUILayout.Width(70)))NavigateSelected();
    }
    if(error!=null)EditorGUILayout.HelpBox(error,MessageType.Error);
    if(transportBusy)EditorGUILayout.HelpBox(rehearsal?.Controller.PendingWait!=null?"Choose a response below to reach this cue.":"Finding this cue through the script…",MessageType.Info);
    else if(stage!=null&&stage.Ready)EditorGUILayout.LabelField(stage.Composition??"",EditorStyles.wordWrappedMiniLabel);
    var shown=new HashSet<string>(StringComparer.Ordinal);
    var own=ManifestFor(selectedSource);
    var direct=own.Requirements.Where(r=>r.Occurrences.Any(o=>o.line==selectedSource.line&&o.declaration==selectedSource.declaration)).ToArray();
    foreach(var r in direct)DrawCueBinding(r,selectedSource,"This cue",shown,instruction.name=="Lara.SitAt"||instruction.name=="Lara.DissolveTo"&&instruction.arguments.Any(a=>a.name=="seated"));
    if(direct.Length==0)EditorGUILayout.LabelField("This cue has no asset or mark assignments.",EditorStyles.wordWrappedMiniLabel);
    if(visualState.Effects.Count>0&&!transportBusy){
     // Bindings carried into this exact executed moment, including their original source owner.
     var effects=visualState.Effects;
     var location=effects.LastOrDefault(e=>e.Kind==VisualIntentKind.Hidden||e.Kind==VisualIntentKind.DissolveIn||e.Kind==VisualIntentKind.Move||e.Kind==VisualIntentKind.DissolveStanding||e.Kind==VisualIntentKind.Sit||e.Kind==VisualIntentKind.DissolveSeated);
     DrawEffectBinding(location,StoryBindingKind.Location,location?.Symbol,"Performer position · carried into this cue",shown,visualState.Seated);
     var pose=effects.LastOrDefault(e=>e.Kind==VisualIntentKind.Pose||e.Kind==VisualIntentKind.SitPose||e.Kind==VisualIntentKind.DissolveSeated||e.Kind==VisualIntentKind.Idle||e.Kind==VisualIntentKind.Sit||e.Kind==VisualIntentKind.DissolveStanding);
     if(pose?.Pose!=null)DrawEffectBinding(pose,StoryBindingKind.Pose,pose.Pose,"Pose · carried into this cue",shown,false);
     var camera=effects.LastOrDefault(e=>e.Kind==VisualIntentKind.CameraStart);
     DrawEffectBinding(camera,StoryBindingKind.Location,camera?.Symbol,"Camera position · carried into this cue",shown,false);
     var body=effects.LastOrDefault(e=>e.Kind==VisualIntentKind.BodyHidden);
     if(visualState.BodyHidden!=true)DrawEffectBinding(body,StoryBindingKind.Location,body?.Symbol,"Player body position",shown,false);
    }
    bool tracking=stage!=null&&stage.Ready&&visualState.TrackLara;
    if(instruction.name=="Camera.Track"||tracking){
     using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){
      EditorGUILayout.LabelField("Camera aim",EditorStyles.boldLabel);
      EditorGUILayout.LabelField(tracking?"Tracks Lara's head after the pose is applied.":"This cue enables tracking of Lara's head.",EditorStyles.wordWrappedMiniLabel);
      NativeMarkButton("tracking target offset",host?.laraFocus);
     }
    }
    if(visualState.Observation!=null&&stage!=null)EditorGUILayout.HelpBox(visualState.Observation,MessageType.Info);
    DrawCueChoices();
    showPlaybackSettings=EditorGUILayout.Foldout(showPlaybackSettings,"Preview path options",true);
    if(showPlaybackSettings){
     EditorGUILayout.HelpBox("For alternate branches, repeated cues and random card choices. These options affect only the preview path.",MessageType.Info);
     using(new EditorGUI.DisabledScope(transportBusy)){
      int visit=Mathf.Max(1,EditorGUILayout.IntField("Loop occurrence",selectedVisit));if(visit!=selectedVisit){selectedVisit=visit;NavigateSelected();}
      int seed=EditorGUILayout.IntField("Random seed",scenario.seed);if(seed!=scenario.seed){Release();scenario.seed=seed;NavigateSelected();}
     }
     showTechnical=EditorGUILayout.Foldout(showTechnical,"Advanced playback / diagnostics",true);
     if(showTechnical)DrawTransport();
    }
    EditorGUILayout.Space();
    if(GUILayout.Button("Open this line in script editor",EditorStyles.linkLabel))OpenLine(selectedSource);
   }finally{EditorGUIUtility.labelWidth=oldLabelWidth;GUILayout.EndScrollView();}
  }
  void DrawEffectBinding(VisualIntent effect,StoryBindingKind kind,string alias,string label,HashSet<string> shown,bool seated){
   if(effect==null||alias==null)return;
   var r=ManifestFor(effect.Source).Requirements.FirstOrDefault(x=>x.Kind==kind&&x.Alias==alias);
   if(r!=null)DrawCueBinding(r,effect.Source,label,shown,seated);
  }
  void DrawCueBinding(StoryBindingRequirement r,StorySource owner,string label,HashSet<string> shown,bool seated){
   string key=r.Kind==StoryBindingKind.Location?"scene:"+r.Alias:r.Id;
   if(!shown.Add(key))return;
   // Actor role bindings are part of scene setup, not a pose/camera editing slot.
   if(r.Kind==StoryBindingKind.Performer||r.Kind==StoryBindingKind.PlayerBody)return;
   var priorOwner=inspectorOwner;var priorManifest=inspectorManifest;bool priorSeat=contextNeedsSeat;
   inspectorOwner=owner;inspectorManifest=ManifestFor(owner);contextNeedsSeat=seated;
   try{using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){
    GUILayout.Label(r.IsDialogue?"Dialogue audio":r.Alias,EditorStyles.boldLabel);
    GUILayout.Label(label+" · "+(r.IsDialogue?"AudioClip":r.ExpectedType),EditorStyles.wordWrappedMiniLabel);
    DrawRequirement(r);
   }}finally{inspectorOwner=priorOwner;inspectorManifest=priorManifest;contextNeedsSeat=priorSeat;}
  }
  void DrawSeatMark(DazPose.Performer.PerformerSeat seat,string propertyName,string label){
   var serialized=new SerializedObject(seat);serialized.Update();var property=serialized.FindProperty(propertyName);
   var mark=(Transform)property.objectReferenceValue;
   var assigned=(Transform)EditorGUILayout.ObjectField(label,mark,typeof(Transform),true);
   if(assigned!=mark)Try(()=>{
    if(assigned!=null&&assigned.gameObject.scene!=host.gameObject.scene)throw new InvalidOperationException("Choose a mark in this scene.");
    property.objectReferenceValue=assigned;serialized.ApplyModifiedProperties();QueueCueRefresh();
   });
   if(mark==null&&GUILayout.Button("Create "+label.ToLowerInvariant()+" mark"))Try(()=>{
    var child=new GameObject(propertyName=="seatAnchor"?"SeatAnchor":"ApproachAnchor");
    Undo.RegisterCreatedObjectUndo(child,"Create "+label);Undo.SetTransformParent(child.transform,seat.transform,"Parent "+label);
    child.transform.localPosition=Vector3.zero;child.transform.localRotation=Quaternion.identity;
    property.objectReferenceValue=child.transform;serialized.ApplyModifiedProperties();
    Selection.activeGameObject=child;Tools.current=Tool.Move;QueueCueRefresh();
   });
   NativeMarkButton(label.ToLowerInvariant(),mark);
  }
  void DrawCreateBindingRows(){
   EditorGUILayout.HelpBox("This script needs an asset slot before you can assign it.",MessageType.Info);
   if(GUILayout.Button("Create missing asset slots")){
    var info=Details;Try(()=>{SourceBindingReconciler.ReconcileAsset(info.SourcePath,info.SourceGuid);QueueCueRefresh();});
   }
  }
  void QueueCueRefresh(){
   if(refreshCueQueued)return;refreshCueQueued=true;
   EditorApplication.delayCall+=()=>{
    if(this==null)return;refreshCueQueued=false;
    if(EditorApplication.isCompiling||EditorApplication.isUpdating){QueueCueRefresh();return;}
    var owner=selectedSource;string entry=selectedEntry;int visit=selectedVisit;bool resume=owner!=null;
    Release();sourceStamp=null;revisionDirty=true;Refresh();
    if(resume){
     selectedPc=Array.FindIndex(CueProgram?.instructions??Array.Empty<StoryInstruction>(),i=>i.source?.file==owner.file&&i.source?.line==owner.line&&i.source?.declaration==owner.declaration);
     selectedSource=selectedPc>=0?CueProgram.instructions[selectedPc].source:null;selectedEntry=entry;selectedVisit=visit;
     detailManifest=selectedSource!=null?ManifestFor(selectedSource):null;
     if(selectedPc>=0)NavigateSelected();
    }
    Repaint();
   };
  }
  void DrawCueChoices(){
   var pending=rehearsal?.Controller.PendingWait;
   if(pending!=null){
    EditorGUILayout.LabelField("Choose a response to continue",EditorStyles.boldLabel);
    foreach(var response in pending.Events)if(GUILayout.Button(response.String))Try(()=>rehearsal.SetEventResponse(pending.Source,pending.ProgramCounter,response.String));
   }
   if(current==null||rehearsal==null)return;
   if(!stageAfter&&current.Code==StoryCode.JumpFalse){
    EditorGUILayout.LabelField("Preview this branch",EditorStyles.boldLabel);
    using(new EditorGUILayout.HorizontalScope()){
     if(GUILayout.Button("Condition true"))Try(()=>rehearsal.SetBranchOverride(current.Source,current.ProgramCounter,true));
     if(GUILayout.Button("Condition false"))Try(()=>rehearsal.SetBranchOverride(current.Source,current.ProgramCounter,false));
    }
   }
   if(!stageAfter&&inspection!=null){
    EditorGUILayout.LabelField("Choose the next card",EditorStyles.boldLabel);
    foreach(var candidate in inspection.Candidates)using(new EditorGUI.DisabledScope(!candidate.Eligible)){
     if(GUILayout.Button(candidate.ToString(),new GUIStyle(EditorStyles.miniButton){alignment=TextAnchor.MiddleLeft,wordWrap=true}))Try(()=>rehearsal.ForceDirectorCard(current.Source,candidate.Id));
    }
   }
  }
 }
}
