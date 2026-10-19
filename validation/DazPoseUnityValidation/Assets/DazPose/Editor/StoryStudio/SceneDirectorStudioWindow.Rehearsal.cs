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
        void DrainRehearsal() {
            int budget=500;
            while(budget-->0&&rehearsalEvents.TryDequeue(out var action)) {
                try{action();}catch(Exception e){error="Preview effect: "+e.GetBaseException().Message;}
            }
            if(rehearsal!=null&&rehearsal.Failure!=null)error=rehearsal.Failure.Message;
            if(rehearsal!=null)Repaint();
        }
        void StopRehearsal() {
            generation++;rehearsal?.Dispose();rehearsal=null;current=null;selection=null;inspection=null;branch=null;continuousTransport=false;transportBusy=false;
            while(rehearsalEvents.TryDequeue(out _)){}
        }
        void StartRehearsal() {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Rehearsal is available only in Edit Mode.");
            if(source==null||!source.IsCompiled||content==null)throw new InvalidOperationException("Choose a valid imported story with reconciled bindings.");
            if(string.IsNullOrEmpty(sequence))throw new InvalidOperationException("Choose a sequence source, rather than a global-card-only file.");
            StopRehearsal();trace.Clear();
            if(!stage.Ready)BuildPreview();
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
            if(transportBusy)return;transportBusy=true;
            try{await action();}
            catch(OperationCanceledException){}
            catch(Exception e){error=e.GetBaseException().Message;}
            finally{transportBusy=false;Repaint();}
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
                    if(GUILayout.Button("Back",EditorStyles.toolbarButton))Transport(async()=>{BuildPreview();await rehearsal.BackAsync();});
                    if(GUILayout.Button("Run to selected",EditorStyles.toolbarButton))Transport(async()=>{
                        if(selectedPc<0)throw new InvalidOperationException("Select a source instruction first.");
                        var entry=manifest.Analysis.Program.entries.First(e=>selectedPc>=e.start&&selectedPc<e.end);
                        await rehearsal.RunToPCAsync(entry.name,selectedPc);
                    });
                }
                if(GUILayout.Button("Stop",EditorStyles.toolbarButton)){StopRehearsal();StopAudio();stage.Dispose();}
                showState=GUILayout.Toggle(showState,"State","ToolbarButton");
            }
            using(new EditorGUILayout.HorizontalScope()) {
                scenario.seed=EditorGUILayout.IntField("Seed",scenario.seed,GUILayout.MinWidth(100));
                clothingCount=EditorGUILayout.IntSlider("Initial clothing",clothingCount,0,6);
                if(GUILayout.Button("Save scenario",GUILayout.Width(110)))Try(()=>{
                    string path=EditorUtility.SaveFilePanel("Save test-only rehearsal recipe","","rehearsal.json","json");
                    if(!string.IsNullOrEmpty(path))File.WriteAllText(path,rehearsal!=null?rehearsal.ExportScenarioJson():JsonUtility.ToJson(scenario,true));
                });
                if(GUILayout.Button("Load scenario",GUILayout.Width(110)))Try(()=>{
                    string path=EditorUtility.OpenFilePanel("Load test-only rehearsal recipe","","json");
                    if(!string.IsNullOrEmpty(path)){scenario=JsonUtility.FromJson<RehearsalScenario>(File.ReadAllText(path))??throw new InvalidOperationException("Invalid scenario.");clothingCount=scenario.initialClothing.Count(x=>x);}
                });
            }
            if(current!=null) {
                EditorGUILayout.LabelField(current.EntryKind+" "+current.EntryName+" · PC "+current.ProgramCounter+" · "+current.Source,EditorStyles.miniLabel);
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
        void ApplyPreviewCommand(StoryCommand command) {
            string Arg(string name)=>command.Require(name).String;
            var owner=rehearsalContent.ContentFor(command.Source);
            switch(command.Verb) {
                case "Lara.StartHiddenAt":case "Lara.DissolveIn":case "Lara.DissolveTo":case "Lara.LocomoteTo":
                    stage.Arrival(host.RequireLocation(Arg("location")).placement);
                    if(command.Verb=="Lara.StartHiddenAt"&&stage.Lara!=null)stage.Lara.gameObject.SetActive(false);
                    if(command.Arguments.ContainsKey("seated"))stage.Sample(owner.Pose(Arg("seated")).value.Clip,0);break;
                case "PlayerBody.StartHiddenAt":
                    if(stage.PlayerBody!=null){var mark=host.RequireLocation(Arg("location")).placement;stage.PlayerBody.SetPositionAndRotation(mark.position,mark.rotation);stage.PlayerBody.gameObject.SetActive(false);}break;
                case "PlayerBody.DissolveIn":if(stage.PlayerBody!=null)stage.PlayerBody.gameObject.SetActive(true);break;
                case "PlayerBody.DissolveOut":if(stage.PlayerBody!=null)stage.PlayerBody.gameObject.SetActive(false);break;
                case "Lara.PersistentPose":case "Lara.PersistentSitPose":stage.Sample(owner.Pose(Arg("pose")).value.Clip,0);break;
                case "Lara.PlayOneShotGesture":sampledClip=owner.Gesture(Arg("gesture")).Clip;sampleTime=0;stage.Sample(sampledClip,0);break;
                case "Lara.MagicShader":stage.Magic(owner.Shader(Arg("profile")));break;
                case "Lara.Say":DrawSpeechPreview(owner.Dialogue(Arg("text")));break;
                case "Lara.SitAt":stage.Arrival(host.RequireLocation(Arg("location")).seat.SeatAnchor);break;
                case "Camera.StartAt":
                    var placement=host.RequireLocation(Arg("location")).placement;stage.PreviewCameraAt(placement.position,placement.rotation);break;
                case "Camera.Track":stage.TrackLara();break;
                case "Lara.Grip":trace.Add("Grip is a logical contact indication; live solver animation requires the user's Unity play test.");break;
            }
        }
        void DrawSpeechPreview(AudioClip clip) {
            // Playback is explicit via the contextual audio preview, not automatic during fast logical replay.
            audioClip=clip;trace.Add("Speech preview available: "+clip.name+" · select its Audio requirement to hear it.");
        }
    }
}
