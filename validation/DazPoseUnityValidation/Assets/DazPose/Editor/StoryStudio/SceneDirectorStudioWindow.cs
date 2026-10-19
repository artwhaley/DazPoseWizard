using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DazPose.Story;
using DazPose.Story.Language;
using DazPose.Performer;
using DazPose.Motion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor.Studio
{
    public sealed partial class SceneDirectorStudioWindow : EditorWindow
    {
        [SerializeField] CompiledStoryAsset source;
        [SerializeField] StorySceneBindings host;
        SourceBindingManifest manifest;
        StoryContentBindings content;
        readonly ScenePreviewSession stage=new ScenePreviewSession();
        Vector2 flowScroll,detailScroll;
        string search="",requirementFilter="All",error,sequence;
        int selectedPc=-1,selectedRequirement=-1;
        float leftWidth=280,rightWidth=330,bottomHeight=220;
        int dragging;
        bool viewportDrag;
        bool editLocation,showHandles;
        Vector3 ghostPosition,ghostRotation;
        string selectedKey;
        UnityEditor.Editor audioEditor;
        AudioClip audioClip;
        AnimationClip sampledClip;
        float sampleTime;
        DateTime sourceWrite;
        [MenuItem("DazPose/Story/Scene Director Studio")]
        public static void Open() {
            var window=GetWindow<SceneDirectorStudioWindow>();
            window.titleContent=new GUIContent("Scene Director Studio");window.minSize=new Vector2(700,480);
            if(Selection.activeObject is CompiledStoryAsset asset)window.SelectSource(asset);
            window.Show();
        }
        void OnEnable() {
            titleContent=new GUIContent("Scene Director Studio");
            EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Release;
            EditorSceneManager.activeSceneChangedInEditMode+=SceneChanged;SceneView.duringSceneGui+=DrawSceneHandles;
            if(source!=null)Refresh();
        }
        void OnDisable() {
            EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=Release;
            EditorSceneManager.activeSceneChangedInEditMode-=SceneChanged;SceneView.duringSceneGui-=DrawSceneHandles;Release();
        }
        void Release(){StopRehearsal();StopAudio();stage.Dispose();if(audioEditor!=null)DestroyImmediate(audioEditor);audioEditor=null;}
        void SceneChanged(Scene oldScene,Scene newScene){Release();host=null;editLocation=false;error="Scene changed. Select the saved scene host and rebuild the isolated preview.";Repaint();}
        void Tick() {
            DrainRehearsal();
            if(source!=null) {
                string path=AssetDatabase.GetAssetPath(source);
                if(File.Exists(path)&&File.GetLastWriteTimeUtc(path)!=sourceWrite) {Refresh();Repaint();}
            }
        }
        void SelectSource(CompiledStoryAsset value){Release();source=value;selectedPc=selectedRequirement=-1;selectedKey=null;Refresh();}
        void Refresh() {
            manifest=null;content=null;error=null;
            if(source==null)return;
            try {
                string path=AssetDatabase.GetAssetPath(source);
                if(!path.EndsWith(".dazstory",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Choose an imported .dazstory source.");
                sourceWrite=File.GetLastWriteTimeUtc(path);
                manifest=SourceBindingManifest.Analyze(path,AssetDatabase.AssetPathToGUID(path));
                content=SourceBindingReconciler.FindOwner(manifest.SourceGuid,SourceBindingReconciler.CompanionPath(path));
                var sequences=manifest.Analysis.Program?.entries.Where(e=>e.kind=="sequence").Select(e=>e.name).ToArray()??Array.Empty<string>();
                if(!sequences.Contains(sequence))sequence=sequences.FirstOrDefault();
                if(selectedKey!=null)selectedRequirement=Array.FindIndex(manifest.Requirements,r=>r.Id==selectedKey);
            }catch(Exception e){error=e.Message;}
        }
        void OnGUI() {
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                var next=(CompiledStoryAsset)EditorGUILayout.ObjectField(source,typeof(CompiledStoryAsset),false,GUILayout.MinWidth(120));
                if(next!=source)SelectSource(next);
                host=(StorySceneBindings)EditorGUILayout.ObjectField(host,typeof(StorySceneBindings),true,GUILayout.MinWidth(120));
                if(GUILayout.Button("Find scene host",EditorStyles.toolbarButton))host=UnityEngine.Object.FindObjectsByType<StorySceneBindings>(FindObjectsInactive.Include).FirstOrDefault(s=>s.gameObject.scene==SceneManager.GetActiveScene());
                if(GUILayout.Button("Refresh",EditorStyles.toolbarButton))Refresh();
                if(GUILayout.Button("Sync source bindings",EditorStyles.toolbarButton))Try(()=>{
                    if(manifest==null)throw new InvalidOperationException("Choose a valid source first.");
                    content=SourceBindingReconciler.ReconcileAsset(manifest.SourcePath,manifest.SourceGuid);AssetDatabase.ImportAsset(manifest.SourcePath);Refresh();
                });
            }
            float toolbar=24,height=position.height-toolbar;
            bool narrow=position.width<1050;
            leftWidth=Mathf.Clamp(leftWidth,180,Mathf.Max(181,position.width*.32f));
            rightWidth=Mathf.Clamp(rightWidth,240,Mathf.Max(241,position.width*.36f));
            bottomHeight=Mathf.Clamp(bottomHeight,120,Mathf.Max(121,height*.48f));
            Rect left=new Rect(0,toolbar,leftWidth,height-bottomHeight);
            Rect center=new Rect(left.xMax+4,toolbar,Mathf.Max(180,position.width-leftWidth-rightWidth-8),height-bottomHeight);
            Rect right=new Rect(center.xMax+4,toolbar,rightWidth,height-bottomHeight);
            if(narrow){left.width=200;center.x=204;center.width=position.width-204;right=new Rect(0,toolbar+height-bottomHeight,position.width*.48f,bottomHeight);}
            GUI.Box(left,GUIContent.none);GUI.Box(center,GUIContent.none);GUI.Box(right,GUIContent.none);
            GUILayout.BeginArea(left);DrawFlow();GUILayout.EndArea();
            GUILayout.BeginArea(center);DrawStage();GUILayout.EndArea();
            GUILayout.BeginArea(right);DrawDetails();GUILayout.EndArea();
            Rect bottom=narrow?new Rect(right.xMax+4,right.y,position.width-right.width-4,bottomHeight):
                new Rect(0,toolbar+height-bottomHeight,position.width,bottomHeight);
            GUI.Box(bottom,GUIContent.none);GUILayout.BeginArea(bottom);DrawTransport();GUILayout.EndArea();
            if(!narrow){Splitter(new Rect(left.xMax,toolbar,4,height-bottomHeight),1);Splitter(new Rect(center.xMax,toolbar,4,height-bottomHeight),2);}
            Splitter(new Rect(0,toolbar+height-bottomHeight-4,position.width,4),3);
        }
        void Splitter(Rect rect,int id) {
            EditorGUIUtility.AddCursorRect(rect,id==3?MouseCursor.ResizeVertical:MouseCursor.ResizeHorizontal);
            if(Event.current.type==EventType.MouseDown&&rect.Contains(Event.current.mousePosition)){dragging=id;Event.current.Use();}
            if(Event.current.type==EventType.MouseDrag&&dragging==id){if(id==1)leftWidth+=Event.current.delta.x;else if(id==2)rightWidth-=Event.current.delta.x;else bottomHeight-=Event.current.delta.y;Repaint();Event.current.Use();}
            if(Event.current.type==EventType.MouseUp)dragging=0;
        }
        void DrawFlow() {
            EditorGUILayout.LabelField("Source flow",EditorStyles.boldLabel);
            search=EditorGUILayout.TextField(search,EditorStyles.toolbarSearchField);
            flowScroll=EditorGUILayout.BeginScrollView(flowScroll);
            if(manifest?.Analysis.Program!=null) {
                var program=manifest.Analysis.Program;
                foreach(var entry in program.entries) {
                    EditorGUILayout.LabelField(entry.kind+" · "+entry.name,EditorStyles.boldLabel);
                    for(int pc=entry.start;pc<entry.end;pc++) {
                        var instruction=program.instructions[pc];string label=(instruction.source?.line.ToString()??"?")+"  "+instruction.code+" "+instruction.name;
                        if(search.Length>0&&label.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
                        bool selected=selectedPc==pc;
                        if(GUILayout.Toggle(selected,label,"Button")&&!selected) {
                            selectedPc=pc;var use=manifest.Requirements.FirstOrDefault(r=>r.Occurrences.Any(o=>o.line==instruction.source?.line&&o.declaration==instruction.source?.declaration));
                            if(use!=null)SelectRequirement(Array.IndexOf(manifest.Requirements,use));
                        }
                    }
                }
            }
            if(manifest!=null)foreach(string message in manifest.Analysis.SyntaxDiagnostics)EditorGUILayout.HelpBox(message,MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
        void DrawStage() {
            EditorGUILayout.LabelField("Isolated visual preview · Edit Mode",EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope()) {
                if(GUILayout.Button("Build preview"))Try(BuildPreview);
                if(GUILayout.Button("Frame Lara"))stage.Frame(stage.Lara);
                stage.PlayerCamera=GUILayout.Toggle(stage.PlayerCamera,"Player camera","Button");
            }
            if(sampledClip!=null) {
                float next=EditorGUILayout.Slider("Clip time",sampleTime,0,sampledClip.length);
                if(next!=sampleTime){sampleTime=next;Try(()=>stage.Sample(sampledClip,sampleTime));}
            }
            Rect rect=GUILayoutUtility.GetRect(100,100,GUILayout.ExpandHeight(true),GUILayout.ExpandWidth(true));
            // Shift reserves left-drag for draft placement/rotation; ordinary drag remains orbit.
            bool handleEvent=editLocation&&stage.Ready&&Event.current.shift&&rect.Contains(Event.current.mousePosition);
            if(handleEvent&&(Event.current.type==EventType.MouseDown||Event.current.type==EventType.MouseDrag)) {
                if(Event.current.control)ghostRotation.y+=Event.current.delta.x;
                else if(stage.DragOnGround(Event.current.mousePosition,rect,ghostPosition,out var next))ghostPosition=next;
                viewportDrag=true;Event.current.Use();Repaint();
            }
            if(Event.current.type==EventType.MouseUp)viewportDrag=false;
            stage.Draw(rect);
            if(editLocation&&stage.Ready) {
                var point=stage.Project(ghostPosition,rect);
                if(point.z>0)GUI.Label(new Rect(point.x-8,point.y-8,230,28),"+  draft · Shift-drag move / Ctrl+Shift rotate",EditorStyles.whiteMiniLabel);
                if(viewportDrag&&stage.Lara!=null)stage.Lara.SetPositionAndRotation(ghostPosition,Quaternion.Euler(ghostRotation));
            }
            if(Event.current.type==EventType.MouseDrag||Event.current.type==EventType.ScrollWheel)Repaint();
        }
        void BuildPreview() {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before opening the authoring preview.");
            if(host!=null&&string.IsNullOrEmpty(host.gameObject.scene.path))throw new InvalidOperationException("Save the source scene before staging it.");
            var lara=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Production/Characters/Lara/Lara.prefab");
            if(lara==null)lara=AssetDatabase.FindAssets("Lara t:Prefab").Select(g=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(g=>g!=null&&g.GetComponent<SuccubusPerformer>()!=null);
            stage.Build(host,lara,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Production/Player/PlayerBody.prefab"));
            if(!stage.Ready)throw new InvalidOperationException(stage.Diagnostic??"Preview could not be built.");
        }
        void DrawDetails() {
            detailScroll=EditorGUILayout.BeginScrollView(detailScroll);
            EditorGUILayout.LabelField("Source requirements",EditorStyles.boldLabel);
            string[] filters={"All","Unbound","Locations","Audio","Poses","Warnings"};
            int chosen=Array.IndexOf(filters,requirementFilter);requirementFilter=filters[EditorGUILayout.Popup(Mathf.Max(0,chosen),filters)];
            if(manifest!=null) {
                for(int i=0;i<manifest.Requirements.Length;i++) {
                    var r=manifest.Requirements[i];bool unbound=Value(r)==null;
                    if(requirementFilter=="Unbound"&&!unbound||requirementFilter=="Locations"&&r.Kind!=StoryBindingKind.Location||
                        requirementFilter=="Audio"&&!r.IsDialogue||requirementFilter=="Poses"&&r.Kind!=StoryBindingKind.Pose)continue;
                    if(GUILayout.Toggle(selectedRequirement==i,(unbound?"○ ":"● ")+r.ExpectedType+" · "+r.Alias,"Button")&&selectedRequirement!=i)SelectRequirement(i);
                }
                if(selectedRequirement>=0&&selectedRequirement<manifest.Requirements.Length)DrawRequirement(manifest.Requirements[selectedRequirement]);
            }
            if(error!=null)EditorGUILayout.HelpBox(error,MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
        void SelectRequirement(int index) {
            selectedRequirement=index;selectedKey=manifest.Requirements[index].Id;editLocation=false;sampledClip=null;sampleTime=0;StopAudio();
            var r=manifest.Requirements[index];var location=host?.locations.FirstOrDefault(l=>l.id==r.Alias);
            ghostPosition=location?.placement!=null?location.placement.position:Vector3.zero;
            ghostRotation=location?.placement!=null?location.placement.eulerAngles:Vector3.zero;
        }
        UnityEngine.Object Value(StoryBindingRequirement r) {
            if(r.Kind==StoryBindingKind.Location)return host?.locations.FirstOrDefault(l=>l.id==r.Alias)?.placement;
            if(content==null)return null;
            if(r.IsDialogue)return content.dialogue.FirstOrDefault(d=>d.id==StoryDialogue.IdFor(r.Transcript))?.clip;
            switch(r.Kind) {
                case StoryBindingKind.Pose:return content.poses.FirstOrDefault(d=>d.id==r.Alias)?.value;
                case StoryBindingKind.Gesture:return content.gestures.FirstOrDefault(d=>d.id==r.Alias)?.value;
                case StoryBindingKind.MagicShaderProfile:return content.shaders.FirstOrDefault(d=>d.id==r.Alias)?.value;
                case StoryBindingKind.Funscript:return content.motion.FirstOrDefault(d=>d.id==r.Alias)?.value;
                case StoryBindingKind.Performer:return host?.lara;
                case StoryBindingKind.PlayerBody:return host?.playerBody;
                default:return null;
            }
        }
        void DrawRequirement(StoryBindingRequirement r) {
            EditorGUILayout.Space();EditorGUILayout.LabelField(r.ExpectedType,EditorStyles.boldLabel);
            foreach(var occurrence in r.Occurrences)if(GUILayout.Button(occurrence.ToString(),EditorStyles.linkLabel))OpenLine(occurrence);
            if(r.Kind==StoryBindingKind.Location){DrawLocation(r);return;}
            if(r.Kind==StoryBindingKind.Performer||r.Kind==StoryBindingKind.PlayerBody||r.Kind==StoryBindingKind.ContactTarget){
                if(host==null)EditorGUILayout.HelpBox("Choose the saved scene host, then bind its existing production actor/contact roles.",MessageType.Warning);
                else if(GUILayout.Button("Inspect scene roles"))Selection.activeObject=host;
                return;
            }
            if(content==null){EditorGUILayout.HelpBox("Sync source bindings once to create the source-owned typed companion.",MessageType.Warning);return;}
            string array=r.IsDialogue?"dialogue":r.Kind==StoryBindingKind.Pose?"poses":r.Kind==StoryBindingKind.Gesture?"gestures":r.Kind==StoryBindingKind.MagicShaderProfile?"shaders":"motion";
            var serialized=new SerializedObject(content);serialized.Update();var rows=serialized.FindProperty(array);SerializedProperty row=null;
            string id=r.IsDialogue?StoryDialogue.IdFor(r.Transcript):r.Alias;
            for(int i=0;i<rows.arraySize;i++){var candidate=rows.GetArrayElementAtIndex(i);if(candidate.FindPropertyRelative("id").stringValue==id){row=candidate;break;}}
            if(row==null){EditorGUILayout.HelpBox("Source changed: Sync source bindings to add this typed row.",MessageType.Warning);return;}
            if(r.IsDialogue)EditorGUILayout.HelpBox(r.Transcript,MessageType.Info);
            var property=row.FindPropertyRelative(r.IsDialogue?"clip":"value");
            EditorGUILayout.PropertyField(property,new GUIContent("Assigned "+r.ExpectedType));
            if(r.Kind==StoryBindingKind.Pose) {
                EditorGUILayout.PropertyField(row.FindPropertyRelative("posture"),new GUIContent("World posture"));
                EditorGUILayout.HelpBox("Choose a generated PerformerPose wrapper. Import a DAZ pose with the normal importer; an .anim alone is not a pose binding.",MessageType.Info);
            }
            if(serialized.ApplyModifiedProperties())AssetDatabase.SaveAssetIfDirty(content);
            var value=Value(r);
            if(value==null){EditorGUILayout.HelpBox("Unbound. Drag the requested asset type from Project.",MessageType.Warning);return;}
            if(value is PerformerPose pose&&GUILayout.Button("Preview pose"))Try(()=>{sampledClip=pose.Clip;sampleTime=0;stage.Sample(sampledClip,0);});
            if(value is PerformerGesture gesture&&GUILayout.Button("Preview gesture"))Try(()=>{sampledClip=gesture.Clip;sampleTime=0;stage.Sample(sampledClip,0);});
            if(value is PerformerMagicShaderProfile magic&&GUILayout.Button("Preview visual profile"))Try(()=>stage.Magic(magic));
            if(value is AudioClip audio)DrawAudio(audio);
            if(value is FunscriptMotionProgram motion) {
                var curve=new AnimationCurve(motion.Actions.Select(a=>new Keyframe((float)(a.AtMilliseconds/1000d),motion.InterpretPosition(a))).ToArray());
                using(new EditorGUI.DisabledScope(true))EditorGUILayout.CurveField("Position over time",curve,GUILayout.Height(90));
                EditorGUILayout.LabelField("Preview only · no hardware output");
            }
        }
        void DrawLocation(StoryBindingRequirement requirement) {
            if(host==null){EditorGUILayout.HelpBox("Choose a host in an open saved scene. No marks are created automatically.",MessageType.Warning);return;}
            if(GUILayout.Button("Attach missing location records"))Try(()=>SceneMarkAuthoring.AttachRequirements(host,manifest.Analysis));
            var matches=host.locations.Where(l=>l.id==requirement.Alias).ToArray();
            if(matches.Length>1){EditorGUILayout.HelpBox("Duplicate location IDs. Resolve them before editing.",MessageType.Error);return;}
            var row=matches.FirstOrDefault();Transform mark=row?.placement;
            var pick=(Transform)EditorGUILayout.ObjectField("Pick existing",mark,typeof(Transform),true);
            if(pick!=mark&&row!=null)Try(()=>{
                if(pick!=null&&pick.gameObject.scene!=host.gameObject.scene)throw new InvalidOperationException("Choose a mark in this host's scene.");
                Undo.RecordObject(host,"Bind existing location");row.placement=pick;EditorUtility.SetDirty(host);EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
                ghostPosition=pick!=null?pick.position:Vector3.zero;ghostRotation=pick!=null?pick.eulerAngles:Vector3.zero;
            });
            editLocation=EditorGUILayout.Toggle("Edit location (preview draft)",editLocation);
            using(new EditorGUI.DisabledScope(!editLocation&&mark!=null)) {
                ghostPosition=EditorGUILayout.Vector3Field("Preview position",ghostPosition);
                ghostRotation=EditorGUILayout.Vector3Field("Preview rotation",ghostRotation);
                showHandles=EditorGUILayout.Toggle("Scene view draft handles",showHandles);
                if(GUILayout.Button("Align to preview view")){ghostPosition=stage.ViewPosition;ghostRotation=stage.ViewRotation.eulerAngles;}
            }
            if(GUILayout.Button("Preview arrival"))Try(()=>{
                if(stage.Lara==null)throw new InvalidOperationException("Build the preview first.");
                stage.Lara.SetPositionAndRotation(ghostPosition,Quaternion.Euler(ghostRotation));stage.Lara.gameObject.SetActive(true);stage.Frame(stage.Lara);
            });
            if(mark==null){if(GUILayout.Button("Create Mark at Preview Placement"))Try(()=>SceneMarkAuthoring.Create(host,requirement.Alias,ghostPosition,Quaternion.Euler(ghostRotation)));}
            else {
                if(GUILayout.Button("Frame mark")){stage.Frame(stage.CloneFor(mark));Selection.activeObject=mark;}
                using(new EditorGUI.DisabledScope(!editLocation))if(GUILayout.Button("Commit Mark"))Try(()=>SceneMarkAuthoring.Commit(host,mark,ghostPosition,Quaternion.Euler(ghostRotation)));
            }
            if(row!=null) {
                var seat=(PerformerSeat)EditorGUILayout.ObjectField("Seat",row.seat,typeof(PerformerSeat),true);
                var focus=(Transform)EditorGUILayout.ObjectField("Focus",row.focus,typeof(Transform),true);
                if(seat!=row.seat||focus!=row.focus)Try(()=>{
                    if(seat!=null&&seat.gameObject.scene!=host.gameObject.scene||focus!=null&&focus.gameObject.scene!=host.gameObject.scene)throw new InvalidOperationException("Choose seat and focus objects in this host's saved scene.");
                    Undo.RecordObject(host,"Bind seat/focus");row.seat=seat;row.focus=focus;EditorUtility.SetDirty(host);EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
                });
                if(row.seat!=null) {
                    using(new EditorGUI.DisabledScope(true)){EditorGUILayout.ObjectField("Approach",row.seat.ApproachAnchor,typeof(Transform),true);EditorGUILayout.ObjectField("Seat pelvis",row.seat.SeatAnchor,typeof(Transform),true);EditorGUILayout.ObjectField("Seating profile",row.seat.SeatingProfile,typeof(PerformerSeatingProfile),false);}
                    if(GUILayout.Button("Frame at seat pelvis (no pose)"))Try(()=>stage.Arrival(row.seat.SeatAnchor));
                }
            }
            EditorGUILayout.HelpBox("Draft handles and arrival affect preview only. Commit records Undo and marks the scene dirty. Save the scene yourself.",MessageType.Info);
        }
        void DrawSceneHandles(SceneView view) {
            if(!showHandles||!editLocation||selectedRequirement<0||manifest==null)return;
            Handles.color=Color.cyan;EditorGUI.BeginChangeCheck();
            ghostPosition=Handles.PositionHandle(ghostPosition,Quaternion.Euler(ghostRotation));
            var rotation=Handles.RotationHandle(Quaternion.Euler(ghostRotation),ghostPosition);
            Handles.ArrowHandleCap(0,ghostPosition,rotation,.5f,EventType.Repaint);
            Handles.Label(ghostPosition,manifest.Requirements[selectedRequirement].Alias+" (uncommitted preview)");
            if(EditorGUI.EndChangeCheck()){ghostRotation=rotation.eulerAngles;Repaint();}
        }
        static void OpenLine(StorySource location) {
            var asset=AssetDatabase.LoadMainAssetAtPath(location.file);
            if(asset!=null)AssetDatabase.OpenAsset(asset,location.line,location.column);
        }
        void DrawAudio(AudioClip clip) {
            if(audioClip!=clip){if(audioEditor!=null)DestroyImmediate(audioEditor);audioEditor=UnityEditor.Editor.CreateEditor(clip);audioClip=clip;}
            if(audioEditor!=null){audioEditor.OnPreviewSettings();audioEditor.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(100,90),EditorStyles.helpBox);}
            if(GUILayout.Button("Stop audio preview"))StopAudio();
        }
        void StopAudio() {
            if(audioEditor==null)return; // Do not stop another Inspector preview when this window owns no audio editor.
            var type=typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            type?.GetMethod("StopAllPreviewClips",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public)?.Invoke(null,null);
        }
        void Try(Action action){try{action();error=null;}catch(Exception e){error=e.GetBaseException().Message;}Repaint();}
    }
}
