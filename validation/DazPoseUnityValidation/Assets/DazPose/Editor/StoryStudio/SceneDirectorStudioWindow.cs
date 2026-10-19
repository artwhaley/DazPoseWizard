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
        StoryLinkedContent flowContent;
        SourceBindingManifest detailManifest;
        StorySource selectedSource;
        string selectedEntry;
        int selectedVisit=1;
        string stagedResult;
        SourceBindingManifest Details=>detailManifest??manifest;
        StoryContentBindings DetailContent=>selectedSource!=null&&flowContent!=null?flowContent.ContentFor(selectedSource):content;
        NativeSceneStage stage;
        RehearsalVisualState visualState=new RehearsalVisualState();
        Vector2 flowScroll,detailScroll;
        string search="",requirementFilter="All",error,sequence;
        int selectedPc=-1,selectedRequirement=-1;
        string selectedKey;
        UnityEditor.Editor audioEditor;
        AudioClip audioClip;

        DateTime sourceWrite;
        [MenuItem("DazPose/Story/Scene Director Studio")]
        public static void Open() {
            var window=GetWindow<SceneDirectorStudioWindow>();
            window.titleContent=new GUIContent("Scene Director Studio");window.minSize=new Vector2(460,450);
            if(Selection.activeObject is CompiledStoryAsset asset)window.SelectSource(asset);
            window.Show();
        }
        void OnEnable() {
            titleContent=new GUIContent("Scene Director Studio");
            EditorApplication.projectChanged+=RevisionChanged;EditorApplication.hierarchyChanged+=RevisionChanged;Undo.undoRedoPerformed+=RevisionChanged;Undo.postprocessModifications+=Modified;
            EditorApplication.playModeStateChanged+=PlayStateChanged;EditorSceneManager.sceneClosed+=ClosedScene;
            EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Release;
            EditorSceneManager.activeSceneChangedInEditMode+=SceneChanged;
            if(source!=null)Refresh();
            else EditorApplication.delayCall+=AutoResolve;
        }
        void OnDisable() {
            EditorApplication.projectChanged-=RevisionChanged;EditorApplication.hierarchyChanged-=RevisionChanged;Undo.undoRedoPerformed-=RevisionChanged;Undo.postprocessModifications-=Modified;
            EditorApplication.playModeStateChanged-=PlayStateChanged;EditorSceneManager.sceneClosed-=ClosedScene;
            EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=Release;
            EditorSceneManager.activeSceneChangedInEditMode-=SceneChanged;Release();
        }
        void Release(){nativeContext=null;StopRehearsal();StopAudio();stage?.Dispose();stage=null;if(audioEditor!=null)DestroyImmediate(audioEditor);audioEditor=null;}
        void SceneChanged(Scene oldScene,Scene newScene){Release();host=null;error="Scene changed. Select the saved scene host and reconnect native directing.";Repaint();}
        void Tick() {
            if(rehearsal!=null||stage!=null){CheckRevision();DrainRehearsal();PollNativeAnchors();}
            if(source!=null) {
                string path=AssetDatabase.GetAssetPath(source);
                if(File.Exists(path)&&File.GetLastWriteTimeUtc(path)!=sourceWrite) {revisionDirty=true;CheckRevision();Repaint();}
            }
        }
        void SelectSource(CompiledStoryAsset value){Release();sourceStamp=null;revisionDirty=true;source=value;selectedSource=null;detailManifest=null;stagedResult=null;selectedPc=selectedRequirement=-1;selectedKey=null;Refresh();}
        void Refresh() {
            manifest=null;content=null;flowContent=null;detailManifest=null;error=null;
            if(source==null)return;
            try {
                string path=AssetDatabase.GetAssetPath(source);
                if(!path.EndsWith(".dazstory",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Choose an imported .dazstory source.");
                sourceWrite=File.GetLastWriteTimeUtc(path);
                manifest=SourceBindingManifest.Analyze(path,AssetDatabase.AssetPathToGUID(path));
                content=SourceBindingReconciler.FindOwner(manifest.SourceGuid,SourceBindingReconciler.CompanionPath(path));
                try{if(source.IsCompiled&&content!=null)flowContent=StoryLinkedContent.Create(source);}catch(Exception e){error="Linked flow: "+e.Message;}
                var sequences=manifest.Analysis.Program?.entries.Where(e=>e.kind=="sequence").Select(e=>e.name).ToArray()??Array.Empty<string>();
                if(!sequences.Contains(sequence))sequence=sequences.FirstOrDefault();
                if(selectedKey!=null)selectedRequirement=Array.FindIndex(Details.Requirements,r=>r.Id==selectedKey);
            }catch(Exception e){error=e.Message;}
        }
        void DrawFlow() {
            EditorGUILayout.LabelField("Source flow",EditorStyles.boldLabel);
            search=EditorGUILayout.TextField(search,EditorStyles.toolbarSearchField);
            selectedVisit=Mathf.Max(1,EditorGUILayout.IntField("Instruction visit",selectedVisit));
            if(selectedSource!=null&&GUILayout.Button("Open selected source line"))OpenLine(selectedSource);
            if(selectedSource!=null&&GUILayout.Button(stageAfter?"Stage selected visit (after)":"Stage selected visit (before)"))NavigateSelected();
            if(stagedResult!=null)EditorGUILayout.HelpBox(stagedResult,MessageType.Info);
            flowScroll=EditorGUILayout.BeginScrollView(flowScroll);
            if(manifest?.Analysis.Program!=null) {
                var program=flowContent?.Program??manifest.Analysis.Program;
                var sourceLines=new Dictionary<string,string[]>();
                foreach(var entry in program.entries) {
                    EditorGUILayout.LabelField(entry.kind+" · "+entry.name+" · "+(entry.start<program.instructions.Length?program.instructions[entry.start].source?.file:""),EditorStyles.boldLabel);
                    for(int pc=entry.start;pc<entry.end;pc++) {
                        var instruction=program.instructions[pc];string cue=instruction.name;
                        if(instruction.source!=null&&File.Exists(instruction.source.file)){
                            if(!sourceLines.TryGetValue(instruction.source.file,out var lines)){lines=File.ReadAllLines(instruction.source.file);sourceLines[instruction.source.file]=lines;}
                            if(instruction.source.line>0&&instruction.source.line<=lines.Length)cue=lines[instruction.source.line-1].Trim();
                        }
                        string label=(instruction.source?.line.ToString()??"?")+"  "+cue;
                        if(search.Length>0&&label.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
                        bool selected=selectedPc==pc;
                        if(GUILayout.Toggle(selected,label,"Button")&&!selected) {
                            selectedPc=pc;selectedSource=instruction.source;selectedEntry=entry.name;selectedRequirement=-1;selectedKey=null;
                            detailManifest=instruction.source!=null&&File.Exists(instruction.source.file)?SourceBindingManifest.Analyze(instruction.source.file,instruction.source.unit):null;
                            var use=Details.Requirements.FirstOrDefault(r=>r.Occurrences.Any(o=>o.line==instruction.source?.line&&o.declaration==instruction.source?.declaration));
                            if(use!=null)SelectRequirement(Array.IndexOf(Details.Requirements,use));
                            NavigateSelected();
                        }
                    }
                }
            }
            if(manifest!=null)foreach(string message in manifest.Analysis.SyntaxDiagnostics)EditorGUILayout.HelpBox(message,MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
        void DrawDetails() {
            detailScroll=EditorGUILayout.BeginScrollView(detailScroll);
            EditorGUILayout.LabelField("Source requirements",EditorStyles.boldLabel);
            if(GUILayout.Button("Sync typed source bindings"))Try(()=>{
                Release();content=SourceBindingReconciler.ReconcileAsset(manifest.SourcePath,manifest.SourceGuid);AssetDatabase.ImportAsset(manifest.SourcePath);Refresh();sourceStamp=null;revisionDirty=true;
            });
            string[] filters={"All","Unbound","Locations","Audio","Poses","Warnings"};
            int chosen=Array.IndexOf(filters,requirementFilter);requirementFilter=filters[EditorGUILayout.Popup(Mathf.Max(0,chosen),filters)];
            if(manifest!=null) {
                for(int i=0;i<Details.Requirements.Length;i++) {
                    var r=Details.Requirements[i];bool unbound=Value(r)==null;
                    if(requirementFilter=="Unbound"&&!unbound||requirementFilter=="Locations"&&r.Kind!=StoryBindingKind.Location||
                        requirementFilter=="Audio"&&!r.IsDialogue||requirementFilter=="Poses"&&r.Kind!=StoryBindingKind.Pose)continue;
                    if(GUILayout.Toggle(selectedRequirement==i,(unbound?"○ ":"● ")+r.ExpectedType+" · "+r.Alias,"Button")&&selectedRequirement!=i)SelectRequirement(i);
                }
                if(selectedRequirement>=0&&selectedRequirement<Details.Requirements.Length)DrawRequirement(Details.Requirements[selectedRequirement]);
            }
            if(error!=null)EditorGUILayout.HelpBox(error,MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
        void SelectRequirement(int index) {
            selectedRequirement=index;selectedKey=Details.Requirements[index].Id;StopAudio();

        }
        UnityEngine.Object Value(StoryBindingRequirement r) {
            var content=DetailContent;
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
            var content=DetailContent;
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
            if(serialized.ApplyModifiedProperties()){AssetDatabase.SaveAssetIfDirty(content);RevisionChanged();}
            var value=Value(r);
            if(value==null){EditorGUILayout.HelpBox("Unbound. Drag the requested asset type from Project.",MessageType.Warning);return;}
            if(value is AudioClip audio)DrawAudio(audio);
            if(value is FunscriptMotionProgram motion) {
                var curve=new AnimationCurve(motion.Actions.Select(a=>new Keyframe((float)(a.AtMilliseconds/1000d),motion.InterpretPosition(a))).ToArray());
                using(new EditorGUI.DisabledScope(true))EditorGUILayout.CurveField("Position over time",curve,GUILayout.Height(90));
                EditorGUILayout.LabelField("Preview only · no hardware output");
            }
        }
        void DrawLocation(StoryBindingRequirement requirement) {
            if(host==null){EditorGUILayout.HelpBox("Choose the saved scene host.",MessageType.Warning);return;}
            var rows=host.locations.Where(l=>l.id==requirement.Alias).ToArray();
            if(rows.Length!=1){EditorGUILayout.HelpBox("Location requires exactly one binding: "+requirement.Alias,MessageType.Error);if(GUILayout.Button("Attach missing location records"))Try(()=>SceneMarkAuthoring.AttachRequirements(host,Details.Analysis));return;}
            var row=rows[0];
            var mark=(Transform)EditorGUILayout.ObjectField("Placement",row.placement,typeof(Transform),true);
            var seat=(PerformerSeat)EditorGUILayout.ObjectField("Seat",row.seat,typeof(PerformerSeat),true);
            var focus=(Transform)EditorGUILayout.ObjectField("Location focus binding",row.focus,typeof(Transform),true);
            if(mark!=row.placement||seat!=row.seat||focus!=row.focus)Try(()=>{
                foreach(var t in new[]{mark,seat!=null?seat.transform:null,focus})if(t!=null&&t.gameObject.scene!=host.gameObject.scene)throw new InvalidOperationException("Bind objects in this host's saved scene.");
                Release();Undo.RecordObject(host,"Bind story location");row.placement=mark;row.seat=seat;row.focus=focus;EditorUtility.SetDirty(host);EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
            });
            if(row.placement==null&&GUILayout.Button("Create placement mark at Scene view pivot"))Try(()=>SceneMarkAuthoring.Create(host,requirement.Alias,SceneView.lastActiveSceneView!=null?SceneView.lastActiveSceneView.pivot:Vector3.zero,Quaternion.identity));
            NativeMarkButton("placement",row.placement);
            if(row.seat!=null){NativeMarkButton("SeatAnchor",row.seat.SeatAnchor);NativeMarkButton("ApproachAnchor",row.seat.ApproachAnchor);NativeMarkButton("chair",row.seat.transform);}
            EditorGUILayout.HelpBox("Select a real mark, then use Unity's Move/Rotate/Scale tools or Inspector. Native Undo and scene Save apply. Location focus is not Camera.Track(Lara)'s target.",MessageType.Info);
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
        void Try(Action action){try{error=null;action();}catch(Exception e){error=e.GetBaseException().Message;}Repaint();}
    }
}
