using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DazPose.Game;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor.Studio
{
    public sealed partial class SceneDirectorStudioWindow
    {
        RehearsalSession rehearsal;
        RehearsalScenario scenario=new RehearsalScenario();
        StoryLinkedContent rehearsalContent;
        readonly ConcurrentQueue<Action> rehearsalEvents=new ConcurrentQueue<Action>();
        readonly List<string> trace=new List<string>();
        Vector2 traceScroll;
        StoryInstructionSnapshot current;
        StoryBranchSnapshot branch;
        StoryDirectorSelectionSnapshot selection;
        DirectorSnapshot inspection;
        bool continuousTransport;
        int generation;
        bool transportBusy,showState;
        int clothingCount=6;
        string forceCard="";
        Exception previewFailure;
        void DrainRehearsal() {
            int budget=500;
            while(budget-->0&&rehearsalEvents.TryDequeue(out var action)) {
                try{action();}catch(Exception e){previewFailure=e;error="Preview effect: "+e.GetBaseException().Message;}
            }
            if(rehearsal!=null&&rehearsal.Failure!=null)error=rehearsal.Failure.Message;
            if(rehearsal!=null)Repaint();
        }
        void StopRehearsal() {
            generation++;previewFailure=null;stagedResult=null;if(rehearsal!=null)scenario=rehearsal.Scenario.Copy();rehearsal?.Dispose();rehearsal=null;current=null;selection=null;inspection=null;branch=null;continuousTransport=false;transportBusy=false;
            while(rehearsalEvents.TryDequeue(out _)){}
        }
        void StartRehearsal() {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Rehearsal is available only in Edit Mode.");
            if(source==null||!source.IsCompiled||content==null)throw new InvalidOperationException("Choose a valid imported story with reconciled bindings.");
            if(string.IsNullOrEmpty(sequence))throw new InvalidOperationException("Choose a sequence source, rather than a global-card-only file.");
            revisionDirty=true;CheckRevision();StopRehearsal();trace.Clear();
            BuildPreview();
            if(StoryDialogue.IdFor(JsonUtility.ToJson(source.program))!=StoryDialogue.IdFor(JsonUtility.ToJson(manifest.Analysis.Program)))throw new InvalidOperationException("Source has changed since its compiled import. Let Unity refresh it before rebuilding rehearsal.");
            rehearsalContent=StoryLinkedContent.Create(source);
            var catalogs=StoryCatalogs.Load();int owner=generation;
            scenario.initialClothing=Enumerable.Range(0,PlayerWorldState.ClothingSlots.Length).Select(i=>i<clothingCount).ToArray();
            // Defaults are isolated author test data. No persistentDataPath/profile file or GameApplication is read.
            var profile=scenario.initialProfile??new PlayerProfile();
            rehearsal=new RehearsalSession(rehearsalContent.Program,sequence,profile,catalogs.Kinks,catalogs.Equipment,
                content,rehearsalContent,host,scenario,command=>rehearsalEvents.Enqueue(()=>{
                    if(owner==generation)ApplyPreviewCommand(command);
                }));
            rehearsal.TraceLine+=(location,message)=>rehearsalEvents.Enqueue(()=>{
                if(owner!=generation)return;trace.Add((location==null?"":location+" · ")+message);if(trace.Count>1500)trace.RemoveRange(0,trace.Count-1500);
            });
            rehearsal.InstructionReady+=snapshot=>rehearsalEvents.Enqueue(()=>{
                if(owner!=generation)return;current=snapshot;inspection=null;
                if(!continuousTransport&&snapshot.Code==StoryCode.Call&&snapshot.Name.StartsWith("Director.",StringComparison.Ordinal))
                    try{inspection=rehearsal.Machine.InspectDirector(snapshot);}catch(Exception e){trace.Add("Director inspection: "+e.Message);}
            });
            rehearsal.BranchObserved+=snapshot=>rehearsalEvents.Enqueue(()=>{if(owner==generation)branch=snapshot;});
            rehearsal.DirectorObserved+=snapshot=>rehearsalEvents.Enqueue(()=>{if(owner==generation)selection=snapshot;});
            _=rehearsal.Start();
        }
        async void Transport(Func<Task> action) {
            if(transportBusy)return;int owner=generation;transportBusy=true;
            try{await action();}
            catch(OperationCanceledException){}
            catch(Exception e){if(owner==generation)error=e.GetBaseException().Message;}
            finally{if(owner==generation)transportBusy=false;Repaint();}
        }
        void DrawTransport() {
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                var entries=manifest?.Analysis.Program?.entries.Where(e=>e.kind=="sequence").Select(e=>e.name).ToArray()??Array.Empty<string>();
                if(entries.Length>0)sequence=entries[Mathf.Max(0,EditorGUILayout.Popup(Mathf.Max(0,Array.IndexOf(entries,sequence)),entries,GUILayout.Width(130)))];
                if(GUILayout.Button("Start / Reset",EditorStyles.toolbarButton))Try(StartRehearsal);
                using(new EditorGUI.DisabledScope(rehearsal==null||transportBusy)) {
                    if(GUILayout.Button("Step",EditorStyles.toolbarButton))Transport(async()=>{await rehearsal.StepAsync();});
                    if(GUILayout.Button("Continue",EditorStyles.toolbarButton)){continuousTransport=true;rehearsal.Continue();}
                    if(GUILayout.Button("Pause",EditorStyles.toolbarButton)){continuousTransport=false;rehearsal.Pause();}
                    if(GUILayout.Button("Back",EditorStyles.toolbarButton))BackRehearsal();
                    if(GUILayout.Button("Run to selected (before)",EditorStyles.toolbarButton))Transport(async()=>{
                        if(selectedPc<0||selectedEntry==null)throw new InvalidOperationException("Select a source instruction first.");
                        await rehearsal.RunToPCAsync(selectedEntry,selectedPc);
                        stagedResult="Before selected instruction · visit "+rehearsal.ReadySnapshot.Visit.Encounter;
                    });
                }
                if(GUILayout.Button("Stop",EditorStyles.toolbarButton)){StopRehearsal();StopAudio();stage.Dispose();}
                showState=GUILayout.Toggle(showState,"State","ToolbarButton");
            }
            using(new EditorGUILayout.HorizontalScope()) {
                int nextSeed=EditorGUILayout.IntField("Seed",scenario.seed,GUILayout.MinWidth(100));if(nextSeed!=scenario.seed){StopRehearsal();scenario.seed=nextSeed;}
                clothingCount=EditorGUILayout.IntSlider("Initial clothing",clothingCount,0,6);
                if(GUILayout.Button("Save scenario",GUILayout.Width(110)))Try(()=>{
                    string path=EditorUtility.SaveFilePanel("Save test-only rehearsal recipe","","rehearsal.json","json");
                    if(!string.IsNullOrEmpty(path))File.WriteAllText(path,rehearsal!=null?rehearsal.ExportScenarioJson():JsonUtility.ToJson(scenario,true));
                });
                if(GUILayout.Button("Load scenario",GUILayout.Width(110)))Try(()=>{
                    string path=EditorUtility.OpenFilePanel("Load test-only rehearsal recipe","","json");
                    if(!string.IsNullOrEmpty(path)){var loaded=RehearsalSession.ImportScenarioJson(File.ReadAllText(path));StopRehearsal();scenario=loaded;clothingCount=scenario.initialClothing.Count(x=>x);}
                });
            }
            if(GUILayout.Button("New scenario (clear recorded visit choices)")){StopRehearsal();scenario=new RehearsalScenario{seed=scenario.seed};}
            if(rehearsal?.Controller.PendingWait!=null) {
                EditorGUILayout.HelpBox("Waiting for a response at visit "+rehearsal.Controller.PendingWait.Visit.Encounter+". Choose an event to continue.",MessageType.Info);
                foreach(var response in rehearsal.Controller.PendingWait.Events)if(GUILayout.Button("Respond: "+response.String))Try(()=>rehearsal.SetEventResponse(current.Source,current.ProgramCounter,response.String));
            }
            if(current!=null) {
                EditorGUILayout.LabelField(current.EntryKind+" "+current.EntryName+" · PC "+current.ProgramCounter+" · visit "+current.Visit.Encounter+" · before · "+current.Source,EditorStyles.miniLabel);
                using(new EditorGUILayout.HorizontalScope()) {
                    if(current.Code==StoryCode.JumpFalse) {
                        EditorGUILayout.LabelField(current.Expression,GUILayout.MaxWidth(280));
                        if(GUILayout.Button("Force true"))rehearsal.SetBranchOverride(current.Source,current.ProgramCounter,true);
                        if(GUILayout.Button("Force false"))rehearsal.SetBranchOverride(current.Source,current.ProgramCounter,false);
                    }
                    if(current.Code==StoryCode.WaitAny&&rehearsalContent!=null) {
                        var instruction=rehearsalContent.Program.instructions[current.ProgramCounter];
                        foreach(var argument in instruction.arguments) {
                            var atoms=rehearsalContent.Program.expressions[argument.expression].atoms;
                            string eventId=atoms.Length==1?atoms[0].text:
                                atoms.Length==2&&atoms[1].text=="UI.Pressed"?"UI.Pressed:"+atoms[0].text:null;
                            if(!string.IsNullOrEmpty(eventId)&&GUILayout.Button("Race: "+eventId))rehearsal.SetEventResponse(current.Source,current.ProgramCounter,eventId);
                        }
                    }
                    if(current.Code==StoryCode.Call&&current.Name=="UI.WaitFor"&&rehearsalContent!=null) {
                        var instruction=rehearsalContent.Program.instructions[current.ProgramCounter];
                        var argument=instruction.arguments.FirstOrDefault(a=>a.name=="action");
                        var atoms=argument!=null?rehearsalContent.Program.expressions[argument.expression].atoms:Array.Empty<StoryAtom>();
                        if(atoms.Length==1&&!string.IsNullOrEmpty(atoms[0].text)&&GUILayout.Button("Simulate "+atoms[0].text))rehearsal.SimulateAction(atoms[0].text);
                    }
                    forceCard=EditorGUILayout.TextField("Force eligible card",forceCard);
                    using(new EditorGUI.DisabledScope(current.Code!=StoryCode.Call||!current.Name.StartsWith("Director.",StringComparison.Ordinal)))
                        if(GUILayout.Button("Force at this call"))Try(()=>rehearsal.ForceDirectorCard(current.Source,forceCard));
                }
            }
            traceScroll=EditorGUILayout.BeginScrollView(traceScroll);
            if(showState&&current!=null) {
                EditorGUILayout.LabelField("Detached rehearsal state",EditorStyles.boldLabel);
                foreach(var pair in current.HostState)EditorGUILayout.LabelField(pair.Key,pair.Value.ToString());
                foreach(var pair in current.Locals)EditorGUILayout.LabelField("local "+pair.Key,pair.Value.ToString());
                foreach(var frame in current.CallStack)EditorGUILayout.LabelField("stack",frame.EntryName+" · "+frame.CardId+" #"+frame.InvocationNumber);
            }
            if(inspection!=null) {
                EditorGUILayout.LabelField("Director · candidates for paused request",EditorStyles.boldLabel);
                EditorGUILayout.LabelField(inspection.Query??"",EditorStyles.wordWrappedMiniLabel);
                foreach(var candidate in inspection.Candidates)using(new EditorGUILayout.HorizontalScope()){
                    EditorGUILayout.LabelField(candidate.ToString(),EditorStyles.wordWrappedMiniLabel);
                    using(new EditorGUI.DisabledScope(!candidate.Eligible))
                        if(GUILayout.Button("Force",GUILayout.Width(50))){forceCard=candidate.Id;rehearsal.ForceDirectorCard(current.Source,forceCard);}
                }
            }
            if(selection!=null) {
                EditorGUILayout.LabelField(selection.Forced?"Director · FORCED":"Director · seeded selection",EditorStyles.boldLabel);
                foreach(var candidate in selection.Candidates) {
                    using(new EditorGUILayout.HorizontalScope()){
                        EditorGUILayout.LabelField(candidate.ToString(),EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }
            if(branch!=null)EditorGUILayout.LabelField((branch.Forced?"FORCED":"Computed")+" branch: "+branch.SelectedCondition);
            foreach(string line in trace)EditorGUILayout.LabelField(line,EditorStyles.wordWrappedMiniLabel);
            if(error!=null)EditorGUILayout.HelpBox(error,MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
        async Task FlushPreviewEvents(int owner) {
            do {if(owner!=generation)throw new OperationCanceledException();DrainRehearsal();if(!rehearsalEvents.IsEmpty)await Task.Yield();}while(!rehearsalEvents.IsEmpty);
            if(owner!=generation)throw new OperationCanceledException();
            if(previewFailure!=null)throw new InvalidOperationException("Logical execution completed, but the visual stage failed: "+previewFailure.Message,previewFailure);
        }
        void BackRehearsal() {
            int advances=Math.Max(0,rehearsal.History.Count-2);
            StopRehearsal();Try(StartRehearsal);if(rehearsal==null)return;
            var session=rehearsal;int owner=generation;
            Transport(async()=>{if(advances>session.Scenario.maxRunToSteps)throw new InvalidOperationException("Back replay exceeds the scenario step budget.");for(int i=0;i<advances;i++)if(!await session.StepAsync())break;await FlushPreviewEvents(owner);});
        }
        void NavigateSelected() {
            if(selectedPc<0||selectedSource==null)return;
            string owner=selectedSource.unit??selectedSource.file,entry=selectedEntry;int pc=selectedPc,visit=selectedVisit;
            StopRehearsal();Try(StartRehearsal);if(rehearsal==null)return;
            var session=rehearsal;int navigationGeneration=generation;
            Transport(async()=>{
                var completed=await session.RunThroughSelectedAsync(owner,entry,pc,visit);
                if(navigationGeneration!=generation)return;
                // Logical effects precede instruction completion; flush them on the Editor thread.
                await FlushPreviewEvents(navigationGeneration);stagedResult="After "+completed.Name+" · "+completed.Source+" · visit "+completed.Visit.Encounter;
            });
        }
        void ApplyPreviewCommand(StoryCommand command) {
            string Arg(string name)=>command.Require(name).String;
            var owner=rehearsalContent.ContentFor(command.Source);
            switch(command.Verb) {
                case "Lara.StartHiddenAt":case "Lara.DissolveIn":case "Lara.LocomoteTo":
                    stage.Arrival(host.RequireLocation(Arg("location")).placement);
                    if(command.Verb=="Lara.StartHiddenAt"&&stage.Lara!=null)stage.Lara.gameObject.SetActive(false);
                    break;
                case "Lara.DissolveTo":
                    var destination=host.RequireLocation(Arg("location"));
                    if(command.Arguments.ContainsKey("seated")){
                        var pose=owner.Pose(Arg("seated"));if(pose.posture!=PlayerPosture.Sitting)throw new InvalidOperationException("Seated arrival requires Sitting pose metadata.");
                        stage.SeatedArrival(destination.seat,pose.value.Clip);
                    }else stage.Arrival(destination.placement);break;
                case "PlayerBody.StartHiddenAt":
                    if(stage.PlayerBody!=null){var mark=host.RequireLocation(Arg("location")).placement;stage.PlayerBody.SetPositionAndRotation(mark.position,mark.rotation);stage.PlayerBody.gameObject.SetActive(false);}break;
                case "PlayerBody.DissolveIn":if(stage.PlayerBody!=null)stage.PlayerBody.gameObject.SetActive(true);break;
                case "PlayerBody.DissolveOut":if(stage.PlayerBody!=null)stage.PlayerBody.gameObject.SetActive(false);break;
                case "Lara.PersistentPose":case "Lara.PersistentSitPose":stage.Sample(owner.Pose(Arg("pose")).value.Clip,0);break;
                case "Lara.PlayOneShotGesture":sampledClip=owner.Gesture(Arg("gesture")).Clip;sampleTime=0;stage.Sample(sampledClip,0);break;
                case "Lara.MagicShader":stage.Magic(owner.Shader(Arg("profile")));break;
                case "Lara.Say":DrawSpeechPreview(owner.Dialogue(Arg("text")));break;
                case "Lara.SitAt":stage.SeatedArrival(host.RequireLocation(Arg("location")).seat);break;
                case "Camera.StartAt":
                    var placement=host.RequireLocation(Arg("location")).placement;stage.PreviewCameraAt(placement.position,placement.rotation);break;
                case "Camera.Track":if(Arg("performer")!="Lara")throw new InvalidOperationException("Camera tracking requires Lara.");stage.TrackLara();break;
                case "Lara.Grip":trace.Add("Grip is a logical contact indication; live solver animation requires the user's Unity play test.");break;
            }
        }
        void DrawSpeechPreview(AudioClip clip) {
            // Playback is explicit via the contextual audio preview, not automatic during fast logical replay.
            audioClip=clip;trace.Add("Speech preview available: "+clip.name+" · select its Audio requirement to hear it.");
        }
    }
}
