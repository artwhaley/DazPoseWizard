using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
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
        bool deferPresentation;
        string presentedKey;
        int generation;
        bool transportBusy,showState;
        int clothingCount=6;
        string forceCard="";
        Exception previewFailure;
        CancellationTokenSource visualCancellation;
        int visualThread;
        Task DispatchVisual(StoryCommand command,int owner) {
            if(owner!=generation||visualCancellation==null)return Task.FromCanceled(new CancellationToken(true));
            // StoryMachine resumes on Unity's captured editor context. Complete synchronous endpoint
            // effects immediately so unawaited virtual pose/gesture calls release their channels.
            if(Environment.CurrentManagedThreadId==visualThread){ApplyPreviewCommand(command);return Task.CompletedTask;}
            var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var token=visualCancellation.Token;var registration=token.Register(()=>completion.TrySetCanceled());
            rehearsalEvents.Enqueue(()=>{
                if(owner!=generation||token.IsCancellationRequested){completion.TrySetCanceled();return;}
                try{ApplyPreviewCommand(command);completion.TrySetResult(true);}
                catch(Exception e){HaltVisual(e);completion.TrySetException(e);}
                finally{registration.Dispose();}
            });
            return completion.Task;
        }
        void HaltVisual(Exception e) {
            ObserveNative("failure",e.ToString());previewFailure=e;error="INCOMPLETE — "+e.Message;
            continuousTransport=false;rehearsal?.Pause();stage?.Dispose();stage=null;nativeContext=null;
            while(rehearsalEvents.TryDequeue(out _)){}
            Repaint();
        }
        void DrainRehearsal() {
            int budget=500;bool changed=false;
            while(budget-->0&&rehearsalEvents.TryDequeue(out var action)) {
                changed=true;try{action();}catch(Exception e){HaltVisual(e);break;}
                if(previewFailure!=null)break;
            }
            if(rehearsal!=null&&rehearsal.Failure!=null&&previewFailure==null)HaltVisual(rehearsal.Failure);
            if(changed)Repaint();
        }
        void StopRehearsal() {
            visualCancellation?.Cancel();visualCancellation?.Dispose();visualCancellation=null;
            ObserveNative("stop");generation++;deferPresentation=false;previewFailure=null;stagedResult=null;if(rehearsal!=null)scenario=rehearsal.Scenario.Copy();rehearsal?.Dispose();rehearsal=null;current=null;selection=null;inspection=null;branch=null;continuousTransport=false;transportBusy=false;
            while(rehearsalEvents.TryDequeue(out _)){}
        }
        void StartRehearsal() { StartRehearsalCore(true); }
        void StartRehearsalCore(bool firstStep) {
            try{StartNativeSession(firstStep);}catch{Release();throw;}
        }
        void StartNativeSession(bool firstStep) {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Rehearsal is available only in Edit Mode.");
            if(source==null||!source.IsCompiled||content==null)throw new InvalidOperationException("Choose a valid imported story with reconciled bindings.");
            if(string.IsNullOrEmpty(sequence))throw new InvalidOperationException("Choose a sequence source, rather than a global-card-only file.");
            if(host==null||source==null)ResolveNativeDefaults();
            revisionDirty=true;CheckRevision();StopRehearsal();trace.Clear();directorSelections=0;nativeRestages=0;visualState=new RehearsalVisualState();visualCancellation=new CancellationTokenSource();visualThread=Environment.CurrentManagedThreadId;
            BuildPreview();
            if(StoryDialogue.IdFor(JsonUtility.ToJson(source.program))!=StoryDialogue.IdFor(JsonUtility.ToJson(manifest.Analysis.Program)))throw new InvalidOperationException("Source has changed since its compiled import. Let Unity refresh it before rebuilding rehearsal.");
            rehearsalContent=flowContent??StoryLinkedContent.Create(source);
            var catalogs=StoryCatalogs.Load();int owner=generation;
            scenario.initialClothing=Enumerable.Range(0,PlayerWorldState.ClothingSlots.Length).Select(i=>i<clothingCount).ToArray();
            // Defaults are isolated author test data. No persistentDataPath/profile file or GameApplication is read.
            var profile=scenario.initialProfile??new PlayerProfile();
            rehearsal=new RehearsalSession(rehearsalContent.Program,sequence,profile,catalogs.Kinks,catalogs.Equipment,
                content,rehearsalContent,host,scenario,asyncVisualEffect:command=>DispatchVisual(command,owner));
            rehearsal.TraceLine+=(location,message)=>rehearsalEvents.Enqueue(()=>{
                if(owner!=generation)return;trace.Add((location==null?"":location+" · ")+message);if(trace.Count>1500)trace.RemoveRange(0,trace.Count-1500);
            });
            rehearsal.InstructionReady+=snapshot=>rehearsalEvents.Enqueue(()=>{
                if(owner!=generation)return;current=snapshot;inspection=null;
                if(!continuousTransport&&snapshot.Code==StoryCode.Call&&snapshot.Name.StartsWith("Director.",StringComparison.Ordinal))
                    try{inspection=rehearsal.Machine.InspectDirector(snapshot);}catch(Exception e){trace.Add("Director inspection: "+e.Message);}
            });
            rehearsal.BranchObserved+=snapshot=>rehearsalEvents.Enqueue(()=>{if(owner==generation)branch=snapshot;});
            rehearsal.DirectorObserved+=snapshot=>rehearsalEvents.Enqueue(()=>{if(owner==generation){selection=snapshot;directorSelections++;ObserveNative("director");}});
            _=rehearsal.Start();
            var initialSession=rehearsal;if(firstStep)Transport(async()=>{await initialSession.StepAsync();await FlushPreviewEvents(owner);});
        }
        async void Transport(Func<Task> action) {
            if(transportBusy)return;int owner=generation;transportBusy=true;
            try{await action();}
            catch(OperationCanceledException){}
            catch(Exception e){if(owner==generation)HaltVisual(e);}
            finally{if(owner==generation)transportBusy=false;Repaint();}
        }
        void DrawTransport() {
            var entries=manifest?.Analysis.Program?.entries.Where(e=>e.kind=="sequence").Select(e=>e.name).ToArray()??Array.Empty<string>();
            if(entries.Length>0){var nextEntry=entries[Mathf.Max(0,EditorGUILayout.Popup("Sequence",Mathf.Max(0,Array.IndexOf(entries,sequence)),entries))];if(nextEntry!=sequence){Release();sequence=nextEntry;}}
            using(new EditorGUILayout.HorizontalScope()) {
                if(GUILayout.Button("Start / Reset"))Try(StartRehearsal);
                using(new EditorGUI.DisabledScope(rehearsal==null||transportBusy||previewFailure!=null)) {
                    if(GUILayout.Button("Run")){continuousTransport=true;rehearsal.Continue();}
                    if(GUILayout.Button("Pause")){continuousTransport=false;rehearsal.Pause();}
                }
                if(GUILayout.Button("Stop"))Release();
                showState=GUILayout.Toggle(showState,"State","Button");
            }
            using(new EditorGUILayout.HorizontalScope()) {
                int nextSeed=EditorGUILayout.IntField("Seed",scenario.seed,GUILayout.MinWidth(100));if(nextSeed!=scenario.seed){Release();scenario.seed=nextSeed;}

                if(GUILayout.Button("Save scenario",GUILayout.Width(110)))Try(()=>{
                    string path=EditorUtility.SaveFilePanel("Save test-only rehearsal recipe","","rehearsal.json","json");
                    if(!string.IsNullOrEmpty(path))File.WriteAllText(path,rehearsal!=null?rehearsal.ExportScenarioJson():JsonUtility.ToJson(scenario,true));
                });
                if(GUILayout.Button("Load scenario",GUILayout.Width(110)))Try(()=>{
                    string path=EditorUtility.OpenFilePanel("Load test-only rehearsal recipe","","json");
                    if(!string.IsNullOrEmpty(path)){var loaded=RehearsalSession.ImportScenarioJson(File.ReadAllText(path));Release();scenario=loaded;clothingCount=scenario.initialClothing.Count(x=>x);}
                });
            }
            int nextClothing=EditorGUILayout.IntSlider("Initial clothing",clothingCount,0,6);if(nextClothing!=clothingCount){Release();clothingCount=nextClothing;}
            if(GUILayout.Button("New scenario (clear recorded visit choices)")){Release();scenario=new RehearsalScenario{seed=scenario.seed};}
            var pendingWait=rehearsal?.Controller.PendingWait;
            if(pendingWait!=null) {
                EditorGUILayout.HelpBox("Waiting for a response at visit "+pendingWait.Visit.Encounter+". Choose an event to continue.",MessageType.Info);
                foreach(var response in pendingWait.Events)if(GUILayout.Button("Respond: "+response.String))Try(()=>rehearsal.SetEventResponse(pendingWait.Source,pendingWait.ProgramCounter,response.String));
            }
            if(current!=null) {
                EditorGUILayout.LabelField(current.EntryKind+" "+current.EntryName+" · PC "+current.ProgramCounter+" · visit "+current.Visit.Encounter+" · before · "+current.Source,EditorStyles.miniLabel);
                using(new EditorGUILayout.VerticalScope()) {
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
            StopRehearsal();Try(()=>StartRehearsalCore(false));if(rehearsal==null)return;
            var session=rehearsal;int owner=generation;
            Transport(async()=>{if(advances>session.Scenario.maxRunToSteps)throw new InvalidOperationException("Back replay exceeds the scenario step budget.");for(int i=0;i<advances;i++)if(!await session.StepAsync())break;await FlushPreviewEvents(owner);});
        }
        void NavigateSelected() {
            if(selectedPc<0||selectedSource==null||transportBusy)return;
            string owner=selectedSource.unit??selectedSource.file,entry=selectedEntry;int pc=selectedPc,visit=selectedVisit;bool after=stageAfter;
            error=null;
            // Keep the actual scene lease and sampling graph alive across logical navigation.
            if(rehearsal==null||stage==null||!stage.Ready||previewFailure!=null){Try(()=>StartRehearsalCore(false));if(rehearsal==null)return;}
            var session=rehearsal;int navigationGeneration=generation;
            Transport(async()=>{
                bool Match(StoryInstructionSnapshot item)=>item!=null&&item.EntryName==entry&&item.ProgramCounter==pc&&item.Visit.Encounter==visit&&(item.Source?.unit??item.Source?.file??"")==owner;
                var history=session.History;
                bool restart=Match(session.CompletedSnapshot)||history.Take(Math.Max(0,history.Count-1)).Any(Match)||session.RunTask?.IsCompleted==true;
                deferPresentation=true;if(restart)visualState=new RehearsalVisualState();
                try{
                    var completed=after?await session.RunThroughSelectedAsync(owner,entry,pc,visit,restart:restart):await session.RunBeforeSelectedAsync(owner,entry,pc,visit,restart:restart);
                    if(navigationGeneration!=generation)return;
                    await FlushPreviewEvents(navigationGeneration);
                    PresentCurrentCue();
                    stagedResult=(after?"After ":"Before ")+completed.Name+" · "+completed.Source+" · visit "+completed.Visit.Encounter;
                    ObserveNative("cue");
                }finally{if(navigationGeneration==generation)deferPresentation=false;}
            });
        }
        void PresentCurrentCue(){
            if(stage==null||!stage.Ready)throw new InvalidOperationException("Preview stopped. Select a cue to reconnect.");
            string nextAnchor=CaptureAnchorStamp();
            // Dialogue, waits and other nonvisual changes leave the native shot untouched.
            if(presentedKey!=visualState.PresentationKey||anchorStamp!=nextAnchor||!stage.IsStaged){
                stage.Apply(visualState,rehearsalContent);presentedKey=visualState.PresentationKey;anchorStamp=nextAnchor;
            }
        }
        void ApplyPreviewCommand(StoryCommand command) {
            if(stage==null||!stage.Ready)throw new InvalidOperationException("Select a cue to connect the scene preview.");
            var next=visualState.After(command);
            if(deferPresentation)stage.ValidateIntent(next,rehearsalContent);
            visualState=next;
            if(!deferPresentation){PresentCurrentCue();ObserveNative("cue");}
            if(command.Verb=="Lara.Say")DrawSpeechPreview(rehearsalContent.ContentFor(command.Source).Dialogue(command.Require("text").String));
        }
        void DrawSpeechPreview(AudioClip clip) {
            // Playback is explicit via the contextual audio preview, not automatic during fast logical replay.
            audioClip=clip;trace.Add("Speech preview available: "+clip.name+" · use this cue’s Dialogue audio control to hear it.");
        }
    }
}
