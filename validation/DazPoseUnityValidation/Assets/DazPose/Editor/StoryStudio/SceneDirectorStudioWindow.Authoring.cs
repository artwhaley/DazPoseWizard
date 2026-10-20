using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor.Studio {
 public sealed partial class SceneDirectorStudioWindow {
  readonly Dictionary<string,SourceBindingManifest> cueManifests=new Dictionary<string,SourceBindingManifest>();
  SourceBindingManifest inspectorManifest;StorySource inspectorOwner;
  bool contextNeedsSeat,showConnection,draggingDivider;
  [SerializeField] float sidebarWidth=310;
  GUIStyle cueStyle,sectionStyle;
  sealed class CueRow {public int Index;public float Y,Height;public string Header,Text;}
  readonly List<CueRow> cueRows=new List<CueRow>();
  string layoutSearch;float layoutWidth,layoutHeight;AuthoringCueIndex layoutIndex;
  void DrawAuthoringWindow(){
   if(cueStyle==null){cueStyle=new GUIStyle(EditorStyles.label){alignment=TextAnchor.UpperLeft,wordWrap=true,padding=new RectOffset(12,12,9,9)};sectionStyle=new GUIStyle(EditorStyles.boldLabel){wordWrap=true,padding=new RectOffset(8,4,8,5)};}
   using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
    GUILayout.Label(source!=null?source.name:"Scene Director",EditorStyles.boldLabel);GUILayout.FlexibleSpace();
    updateWhileDragging=GUILayout.Toggle(updateWhileDragging,new GUIContent("Live drag","Off: update the shot on handle release. On: update placement while dragging."),EditorStyles.toolbarButton);
    GUILayout.Label(indexStale?"Indexing source…":stage!=null&&stage.Ready?"Live preview":"Edit Mode",EditorStyles.miniLabel);
    using(new EditorGUI.DisabledScope(stage==null))if(GUILayout.Button("Stop preview",EditorStyles.toolbarButton))Release();
    showConnection=GUILayout.Toggle(showConnection,"Scene / script",EditorStyles.toolbarButton);
   }
   if(showConnection||source==null)DrawConnection();
   if(cueIndex==null){EditorGUILayout.HelpBox(error??(indexStale?"Reading script cues…":"Open a saved performance scene and choose Find open scene."),MessageType.Info);return;}
   using(new EditorGUILayout.HorizontalScope()){
    float width=Mathf.Clamp(sidebarWidth,220,Mathf.Max(220,position.width-310));
    using(new EditorGUILayout.VerticalScope(GUILayout.Width(width)))DrawCueSidebar();
    var divider=GUILayoutUtility.GetRect(5,5,GUILayout.ExpandHeight(true));EditorGUI.DrawRect(divider,new Color(0,0,0,.25f));EditorGUIUtility.AddCursorRect(divider,MouseCursor.ResizeHorizontal);
    var ev=Event.current;if(ev.type==EventType.MouseDown&&divider.Contains(ev.mousePosition)){draggingDivider=true;ev.Use();}
    if(draggingDivider&&ev.type==EventType.MouseDrag){sidebarWidth=Mathf.Clamp(ev.mousePosition.x,220,Mathf.Max(220,position.width-310));Repaint();ev.Use();}if(ev.type==EventType.MouseUp)draggingDivider=false;
    using(new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))DrawCueInspector();
   }
  }
  void DrawConnection(){using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){
   var next=(StorySceneBindings)EditorGUILayout.ObjectField("Performance scene",host,typeof(StorySceneBindings),true);if(next!=host){nativeSequence=null;SelectHost(next);}
   var script=(CompiledStoryAsset)EditorGUILayout.ObjectField("Script",source,typeof(CompiledStoryAsset),false);if(script!=source){nativeSequence=null;SelectSource(script);}
   if(GUILayout.Button("Find open scene"))Try(ResolveNativeDefaults);
  }}
  string CueText(AuthoringCueState cue)=>string.IsNullOrEmpty(cue.Node.Text)?(authorText.TryGetValue(cue.Node.Id,out var label)?label:cue.Node.Name??cue.Node.Kind):cue.Node.Text;
  void DrawCueSidebar(){
   GUILayout.Label("Script cues",sectionStyle);search=EditorGUILayout.TextField(search,EditorStyles.toolbarSearchField);
   float width=Mathf.Max(150,Mathf.Min(sidebarWidth,position.width-310)-25);
   if(layoutIndex!=cueIndex||layoutSearch!=search||Mathf.Abs(layoutWidth-width)>1){
    cueRows.Clear();float y=0;string header=null;
    for(int i=0;i<cueIndex.Cues.Count;i++){var cue=cueIndex.Cues[i];string text=CueText(cue),section=cue.Node.Source.declaration+" · "+System.IO.Path.GetFileNameWithoutExtension(cue.Node.Source.file);
     if(search.Length>0&&(section+" "+text).IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
     if(header!=section){header=section;cueRows.Add(new CueRow{Index=-1,Y=y,Height=42,Header=header});y+=42;}
     string label=(i+1)+"  "+text;float height=Mathf.Max(38,cueStyle.CalcHeight(new GUIContent(label),width));cueRows.Add(new CueRow{Index=i,Y=y,Height=height,Text=label});y+=height;
    }
    layoutIndex=cueIndex;layoutSearch=search;layoutWidth=width;layoutHeight=y;
   }
   flowScroll=GUILayout.BeginScrollView(flowScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar,GUILayout.ExpandHeight(true));
   var canvas=GUILayoutUtility.GetRect(width,layoutHeight,GUILayout.ExpandWidth(true));
   foreach(var row in cueRows){if(row.Y+row.Height<flowScroll.y||row.Y>flowScroll.y+position.height)continue;var rect=new Rect(canvas.x,canvas.y+row.Y,canvas.width,row.Height);
    if(row.Index<0){GUI.Label(rect,row.Header,sectionStyle);continue;}
    if(row.Index==selectedPc)EditorGUI.DrawRect(rect,EditorGUIUtility.isProSkin?new Color(.16f,.30f,.43f):new Color(.68f,.81f,.92f));
    if(GUI.Button(rect,row.Text,cueStyle))SelectCue(row.Index);
   }
   GUILayout.EndScrollView();
  }
  SourceBindingManifest ManifestFor(StorySource owner)=>owner!=null&&cueManifests.TryGetValue(owner.file,out var found)?found:manifest;
  StoryContentBindings ContentForInspector(StorySource owner){try{return authorCatalog?.ContentFor(owner);}catch{return null;}}
  void SelectCue(int index){
   var watch=System.Diagnostics.Stopwatch.StartNew();selectedPc=index;selectedCue=cueIndex.Cues[index];selectedSource=selectedCue.Node.Source;selectedEntry=selectedCue.Declaration;detailManifest=ManifestFor(selectedSource);selectedRequirement=-1;selectedKey=null;StopAudio();error=null;
   lookupCount++;lookupMilliseconds=watch.Elapsed.TotalMilliseconds;RequestProjection();
  }
  void AdjacentCue(int direction){int next=selectedPc+direction;if(next>=0&&next<cueIndex.Cues.Count)SelectCue(next);}
  void DrawCueInspector(){
   detailScroll=GUILayout.BeginScrollView(detailScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar,GUILayout.ExpandHeight(true));float prior=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=125;
   try{
    if(selectedCue==null){GUILayout.Label("Choose a cue",sectionStyle);EditorGUILayout.HelpBox(error??"Click a cue to edit its pose, performer marks and camera. Scene and Game show the resolved visual state.",MessageType.Info);return;}
    GUILayout.Label(selectedEntry,EditorStyles.boldLabel);GUILayout.Label(CueText(selectedCue),EditorStyles.wordWrappedLabel);
    using(new EditorGUILayout.HorizontalScope()){if(GUILayout.Button("← Previous cue"))AdjacentCue(-1);if(GUILayout.Button("Next cue →"))AdjacentCue(1);}
    using(new EditorGUILayout.HorizontalScope()){int side=GUILayout.Toolbar(stageAfter?1:0,new[]{"Before this cue","After this cue"});if((side==1)!=stageAfter){stageAfter=side==1;RequestProjection();}if(GUILayout.Button("Preview",GUILayout.Width(70)))RequestProjection();}
    if(indexStale)EditorGUILayout.HelpBox("Script is updating. These controls show the previous source until indexing completes.",MessageType.Warning);
    if(!string.IsNullOrEmpty(selectedCue.Block))EditorGUILayout.HelpBox(selectedCue.Block+". Local editing context only; this block is skipped for following cues.",MessageType.Info);
    if(error!=null)EditorGUILayout.HelpBox(error,MessageType.Warning);
    var state=stageAfter?selectedCue.After:selectedCue.Before;
    foreach(var warning in state.Diagnostics)EditorGUILayout.HelpBox(warning,MessageType.Warning);
    if(stage!=null){foreach(var warning in stage.AuthoringWarnings.Except(state.Diagnostics))EditorGUILayout.HelpBox(warning,MessageType.Warning);EditorGUILayout.LabelField(stage.Composition??"",EditorStyles.wordWrappedMiniLabel);}
    var shown=new HashSet<string>(StringComparer.Ordinal);bool seated=selectedCue.Node.Name=="Lara.SitAt"||selectedCue.Node.Literals.ContainsKey("seated");
    foreach(var r in directRequirements[selectedCue.Node.Id])DrawCueBinding(r,selectedSource,"This cue",shown,seated);
    void Inherited(AuthoringValue v,StoryBindingKind kind,string label,bool seat=false){if(v?.IsKnown!=true||v.Symbol==null)return;var r=ManifestFor(v.Source)?.Requirements.FirstOrDefault(x=>x.Kind==kind&&x.Alias==v.Symbol);if(r!=null)DrawCueBinding(r,v.Source,label,shown,seat);}
    var placement=state[AuthoringField.Placement];Inherited(placement,StoryBindingKind.Location,"Performer position · carried into this cue",placement.Op=="sit"||placement.Op=="seatpose");
    Inherited(placement.Parent,StoryBindingKind.Location,"Position height inherited from this mark",placement.Parent?.Op=="sit"||placement.Parent?.Op=="seatpose");
    Inherited(state[AuthoringField.Pose],StoryBindingKind.Pose,"Pose · carried into this cue");Inherited(state[AuthoringField.Camera],StoryBindingKind.Location,"Camera position · carried into this cue");Inherited(state[AuthoringField.BodyPlacement],StoryBindingKind.Location,"Player body position");
    if(state[AuthoringField.Tracking].Op=="true"){GUILayout.Label("Camera aim",EditorStyles.boldLabel);NativeMarkButton("tracking target offset",host?.laraFocus);}
    if(selectedCue.Node.Name=="Lara.PlayOneShotGesture"||selectedCue.Node.Name=="Lara.Grip"||selectedCue.Node.Name=="MotionDriver.PlayFunscript")EditorGUILayout.HelpBox("Bindings can be edited here. Transient gesture, dynamic contact and timed motion require the later Play check; the retained base pose is shown.",MessageType.Info);
    if(GUILayout.Button("Open this line in script editor",EditorStyles.linkLabel))OpenLine(selectedSource);
   }finally{EditorGUIUtility.labelWidth=prior;GUILayout.EndScrollView();}
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
 }
}
