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
        SourceBindingManifest detailManifest;
        StorySource selectedSource;
        string selectedEntry;
        SourceBindingManifest Details=>inspectorManifest??detailManifest??manifest;
        StoryContentBindings DetailContent=>inspectorOwner!=null?ContentForInspector(inspectorOwner):selectedSource!=null?ContentForInspector(selectedSource):content;
        NativeSceneStage stage;
        AuthoringVisualState visualState=new AuthoringVisualState();
        Vector2 flowScroll,detailScroll;
        string search="",error,sequence;
        int selectedPc=-1,selectedRequirement=-1;
        string selectedKey;
        UnityEditor.Editor audioEditor;
        AudioClip audioClip;


        [MenuItem("DazPose/Story/Scene Director Studio")]
        public static void Open() {
            var window=GetWindow<SceneDirectorStudioWindow>();
            window.titleContent=new GUIContent("Scene Director Studio");window.minSize=new Vector2(640,420);
            if(Selection.activeObject is CompiledStoryAsset asset)window.SelectSource(asset);
            window.Show();
        }
        void OnEnable() {
            titleContent=new GUIContent("Scene Director Studio");minSize=new Vector2(640,420);
            StudioAssetChanges.Changed+=ImportedAssets;
            EditorApplication.projectChanged+=RevisionChanged;EditorApplication.hierarchyChanged+=RevisionChanged;Undo.undoRedoPerformed+=RevisionChanged;Undo.postprocessModifications+=Modified;
            EditorApplication.playModeStateChanged+=PlayStateChanged;EditorSceneManager.sceneClosed+=ClosedScene;
            EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Release;
            EditorSceneManager.activeSceneChangedInEditMode+=SceneChanged;
            if(source!=null)Refresh();
            else EditorApplication.delayCall+=AutoResolve;
        }
        void OnDisable() {
            StudioAssetChanges.Changed-=ImportedAssets;
            EditorApplication.projectChanged-=RevisionChanged;EditorApplication.hierarchyChanged-=RevisionChanged;Undo.undoRedoPerformed-=RevisionChanged;Undo.postprocessModifications-=Modified;
            EditorApplication.playModeStateChanged-=PlayStateChanged;EditorSceneManager.sceneClosed-=ClosedScene;
            EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=Release;
            EditorSceneManager.activeSceneChangedInEditMode-=SceneChanged;Release();CancelIndex();
        }
        void Release(){projectionQueue.Cancel();nativeContext=null;StopAudio();stage?.Dispose();stage=null;if(audioEditor!=null)DestroyImmediate(audioEditor);audioEditor=null;ObserveNative("stop");}
        void SceneChanged(Scene oldScene,Scene newScene){Release();CancelIndex();host=null;error="Scene changed. Choose Find open scene to reconnect.";Repaint();}
        void Tick(){
            CompleteIndex();
            if(NativePreviewInteraction.IsDragging){if(updateWhileDragging)PollNativeAnchors();return;}
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||stage?.IsSaveSuspended==true)return;
            CheckRevision();
            if(projectionPending)ProjectSelected();
            PollNativeAnchors();
        }
        void SelectSource(CompiledStoryAsset value){Release();CancelIndex();source=value;selectedSource=null;detailManifest=null;selectedPc=selectedRequirement=-1;selectedKey=null;selectedCue=null;Refresh();}
        void Refresh()=>BeginAuthoringIndex();
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
                case StoryBindingKind.WardrobePreset:return content.wardrobe.FirstOrDefault(d=>d.id==r.Alias)?.value;
                case StoryBindingKind.Hair:return content.hair.FirstOrDefault(d=>d.id==r.Alias)?.value;
                case StoryBindingKind.Funscript:return content.motion.FirstOrDefault(d=>d.id==r.Alias)?.value;
                case StoryBindingKind.Performer:return host?.lara;
                case StoryBindingKind.PlayerBody:return host?.playerBody;
                default:return null;
            }
        }
        void DrawRequirement(StoryBindingRequirement r) {
            var content=DetailContent;

            if(r.Kind==StoryBindingKind.Location){DrawLocation(r);return;}
            if(r.Kind==StoryBindingKind.Performer||r.Kind==StoryBindingKind.PlayerBody||r.Kind==StoryBindingKind.ContactTarget){
                if(host==null)EditorGUILayout.HelpBox("Choose the saved scene host, then bind its existing production actor/contact roles.",MessageType.Warning);
                else if(GUILayout.Button("Inspect scene roles"))Selection.activeObject=host;
                return;
            }
            if(content==null){DrawCreateBindingRows();return;}
            string array=r.IsDialogue?"dialogue":r.Kind==StoryBindingKind.Pose?"poses":r.Kind==StoryBindingKind.Gesture?"gestures":r.Kind==StoryBindingKind.MagicShaderProfile?"shaders":r.Kind==StoryBindingKind.WardrobePreset?"wardrobe":r.Kind==StoryBindingKind.Hair?"hair":"motion";
            var serialized=new SerializedObject(content);serialized.Update();var rows=serialized.FindProperty(array);SerializedProperty row=null;
            string id=r.IsDialogue?StoryDialogue.IdFor(r.Transcript):r.Alias;
            for(int i=0;i<rows.arraySize;i++){var candidate=rows.GetArrayElementAtIndex(i);if(candidate.FindPropertyRelative("id").stringValue==id){row=candidate;break;}}
            if(row==null){DrawCreateBindingRows();return;}
            if(r.IsDialogue)EditorGUILayout.HelpBox(r.Transcript,MessageType.Info);
            var property=row.FindPropertyRelative(r.IsDialogue?"clip":"value");
            EditorGUILayout.LabelField(r.Kind==StoryBindingKind.Pose?"Drop a PerformerPose from Project here":"Drag an asset from Project",EditorStyles.wordWrappedMiniLabel);
            if(r.Kind==StoryBindingKind.Hair) {
                var assigned=(GameObject)EditorGUILayout.ObjectField("Hair prefab",property.objectReferenceValue,typeof(GameObject),false);
                if(assigned!=property.objectReferenceValue)Try(()=>{
                    if(assigned!=null) {
                        var d=assigned.GetComponent<WardrobeAttachmentDescriptor>();
                        if(!PrefabUtility.IsPartOfPrefabAsset(assigned)||assigned.transform.parent!=null||d==null||d.AttachmentRole!=WardrobeAttachmentRole.Hair)throw new InvalidOperationException("Drop a generated Hair prefab root from Project.");
                        if(!d.Validate(out var issue))throw new InvalidOperationException(issue);
                        var rig=host?.lara?.GetComponent<PerformerWardrobe>()?.RigBinding?.Manifest;
                        if(rig!=null && (d.GenerationId!=rig.GenerationId||d.CharacterSignature!=rig.CharacterSignature||!rig.TryGetHair(d.StableKey,out var expected)||expected!=d))throw new InvalidOperationException("Hair does not match this actor's manifest/generation/signature.");
                    }
                    property.objectReferenceValue=assigned;
                });
            } else EditorGUILayout.PropertyField(property,GUIContent.none,GUILayout.Height(r.Kind==StoryBindingKind.Pose?40:22));
            if(r.Kind==StoryBindingKind.Pose) {
                EditorGUILayout.PropertyField(row.FindPropertyRelative("posture"),new GUIContent("World posture"));
                EditorGUILayout.LabelField("Shared pose slot: "+r.Alias+" · "+r.Occurrences.Length+" cue(s) in this source",EditorStyles.wordWrappedMiniLabel);
            }
            if(serialized.ApplyModifiedProperties()){NativePreviewLease.SaveBindings(content);QueueBindingRefresh();}
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
            if(host==null){EditorGUILayout.HelpBox("Open a saved performance scene to assign its marks.",MessageType.Warning);return;}
            var rows=(host.locations??Array.Empty<StoryLocationBinding>()).Where(l=>l!=null&&l.id==requirement.Alias).ToArray();
            if(rows.Length>1){EditorGUILayout.HelpBox("Duplicate location binding: "+requirement.Alias,MessageType.Error);return;}
            var row=rows.FirstOrDefault();
            var mark=(Transform)EditorGUILayout.ObjectField("Scene mark",row?.placement,typeof(Transform),true);
            if(mark!=row?.placement)Try(()=>{
                if(mark!=null&&mark.gameObject.scene!=host.gameObject.scene)throw new InvalidOperationException("Drag a mark from this scene's Hierarchy.");
                Undo.RecordObject(host,"Bind "+requirement.Alias);
                if(row==null){row=new StoryLocationBinding{id=requirement.Alias};host.locations=(host.locations??Array.Empty<StoryLocationBinding>()).Concat(new[]{row}).ToArray();}
                row.placement=mark;EditorUtility.SetDirty(host);EditorSceneManager.MarkSceneDirty(host.gameObject.scene);QueueCueRefresh();
            });
            if(row?.placement==null&&GUILayout.Button("Create "+requirement.Alias+" mark"))Try(()=>{
                var created=SceneMarkAuthoring.Create(host,requirement.Alias,SceneView.lastActiveSceneView!=null?SceneView.lastActiveSceneView.pivot:Vector3.zero,Quaternion.identity);
                Selection.activeGameObject=created.gameObject;Tools.current=Tool.Move;QueueCueRefresh();
            });
            NativeMarkButton(requirement.Alias,row?.placement);
            bool seated=contextNeedsSeat||row?.seat!=null;
            if(!seated)return;
            var seat=(PerformerSeat)EditorGUILayout.ObjectField("Seat / chair",row?.seat,typeof(PerformerSeat),true);
            if(seat!=row?.seat)Try(()=>{
                if(seat!=null&&seat.gameObject.scene!=host.gameObject.scene)throw new InvalidOperationException("Drag a seat from this scene's Hierarchy.");
                Undo.RecordObject(host,"Bind seat "+requirement.Alias);
                if(row==null){row=new StoryLocationBinding{id=requirement.Alias};host.locations=(host.locations??Array.Empty<StoryLocationBinding>()).Concat(new[]{row}).ToArray();}
                row.seat=seat;EditorUtility.SetDirty(host);EditorSceneManager.MarkSceneDirty(host.gameObject.scene);QueueCueRefresh();
            });
            if(row?.seat!=null){DrawSeatMark(row.seat,"seatAnchor","Seat position");DrawSeatMark(row.seat,"approachAnchor","Approach position");NativeMarkButton("chair",row.seat.transform);}
            else EditorGUILayout.HelpBox("Assign the chair's PerformerSeat component to use its seating and approach marks.",MessageType.Info);
        }
        static void OpenLine(StorySource location) {
            var asset=AssetDatabase.LoadMainAssetAtPath(location.file);
            if(asset!=null)AssetDatabase.OpenAsset(asset,location.line,location.column);
        }
        void DrawAudio(AudioClip clip) {
            if(audioEditor==null||audioClip!=clip){if(audioEditor!=null)DestroyImmediate(audioEditor);audioEditor=UnityEditor.Editor.CreateEditor(clip);audioClip=clip;}
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
