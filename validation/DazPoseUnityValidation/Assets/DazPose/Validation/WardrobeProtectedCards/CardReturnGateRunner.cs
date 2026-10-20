using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DazPose.Game;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEngine;
namespace DazPose.Validation.WardrobeProtectedCards {
 public sealed class CardReturnGateRunner : MonoBehaviour {
  public const string SetupVersion="W07-GateB-v1";
  public string setupVersion,sourceFingerprint;
  public PerformanceSceneBindings performance;public StorySceneBindings scene;
  public CompiledStoryAsset root,cards;public WardrobePreset openingOutfit;public WardrobeAttachmentDescriptor openingHair;
  public FunscriptMotionProgram replacementMotion;public PerformerPose seatedPose;public PerformerExpression changedExpression;public PerformerAura entranceAura;
  public Transform entranceMark,cameraMark,shiftCamera;public PerformerSeat seat;
  [Serializable] public sealed class Stamp {public string phase;public float seconds;}
  [Serializable] public sealed class Result {public string id,status,error,entry,posture,camera,stopAt,fault;public bool hidden,trailing,driverRetained,outputAuthority,interactiveStopped;public double progress;public List<string> phases=new List<string>();public List<Stamp> timings=new List<Stamp>();public List<CardPresentationObservation> observations=new List<CardPresentationObservation>();}
  [Serializable] sealed class Report {public int schemaVersion=1;public string gate="B",revision,scenePath,sceneGuid,generation,timestampUtc;public string[] requiredCases;public List<Result> cases;public bool toyOutputAllowed=false;public string nativeResult;}
  public string sceneGuid;string runningFingerprint;
  float PanelWidth=>Mathf.Min(570,Screen.width*.45f);
  float ContentWidth=>Mathf.Max(80,PanelWidth-38);
  GUIStyle wrappedLabel,wrappedButton,wrappedToggle;
  void Awake(){runningFingerprint=CurrentFingerprint();}
  void ConfigureGateViewport(){var camera=performance.Player.MainCamera;float edge=(PanelWidth+24)/Mathf.Max(1,Screen.width);camera.rect=new Rect(edge,0,1-edge,1);}
  async Task AimGateCamera(PerformanceRun run,int mode){
   if(mode==1){run.Player.Track(scene.laraFocus,.25f);await Wait(.3f,lifetime.Token);}
   else{await run.Player.LookAtAsync(scene.laraFocus,0f);performance.Player.MainCamera.transform.Rotate(0,0,13,Space.Self);}
  }
  readonly List<Result> results=new List<Result>();
  bool busy;string status="Start with Run direct card. Watch the changed look/place/camera return to its entrance. If PASS, run all technical checks. A FAIL stops the suite; write report.";int posture;int camera=1;bool hidden;Vector2 scroll;
  PerformanceRun active;CancellationTokenSource lifetime;Result current;Transform disposableTarget;string invalidFitJson;WardrobeFitState invalidFit;
  void OnDisable(){StopCurrent();RestoreFaultFit();}
  void StopCurrent(){lifetime?.Cancel();active?.Stop();}
  void InteractiveStop(){if(current!=null){current.id="interactive-stop";current.interactiveStopped=true;current.stopAt="InteractiveStop";}active?.Stop();}
  void OnGUI(){
   if(wrappedLabel==null){
    wrappedLabel=new GUIStyle(GUI.skin.label){wordWrap=true,alignment=TextAnchor.MiddleLeft};
    wrappedButton=new GUIStyle(GUI.skin.button){wordWrap=true,alignment=TextAnchor.MiddleLeft};
    wrappedToggle=new GUIStyle(GUI.skin.toggle){wordWrap=true,alignment=TextAnchor.MiddleLeft};
   }
   GUILayout.BeginArea(new Rect(12,12,PanelWidth,Mathf.Max(100,Screen.height-24)),GUI.skin.box);
   scroll.x=0;
   scroll=GUILayout.BeginScrollView(scroll,false,true,GUIStyle.none,GUI.skin.verticalScrollbar);
   GUILayout.BeginVertical(GUILayout.Width(ContentWidth));
   Label("Card Return Gate - one Lara / original player body");Label("Direct card: opening normal look -> changed outfit/wireframe -> opening look restored. Ending in wireframe is a failure.");Label(status);
   GUI.enabled=!busy;
   posture=GUILayout.SelectionGrid(posture,new[]{"Standing","Default seated","Persistent seated"},1,wrappedButton,GUILayout.Width(ContentWidth));
   camera=GUILayout.SelectionGrid(camera,new[]{"Fixed (pitch + roll)","Track Lara"},1,wrappedButton,GUILayout.Width(ContentWidth));
   hidden=GUILayout.Toggle(hidden,"Hidden entrance (must remain hidden on return)",wrappedToggle,GUILayout.Width(ContentWidth));
   Button("Run all Gate B technical checks",AllCases);
   Button("Run direct card",()=>Case("direct","Direct",posture,camera,hidden));Button("Run explicit Director card",()=>Case("explicit","Explicit",posture,camera,hidden));Button("Run drawn card",()=>Case("draw","Draw",posture,camera,hidden));
   Button("Run nested cards",()=>Case("nested","Nested",posture,camera,hidden));Button("Run persistent sequence",()=>Case("persistent","Persistent",posture,camera,hidden));Button("Run sequence inside card",()=>Case("contained","Contained",posture,camera,hidden));Button("Run no-change card",()=>Case("unchanged","Unchanged",posture,camera,hidden));
   Button("Run parent motion ownership",()=>Case("parent-motion","Motion",0,1,false));Button("Run Stop checkpoints",StopMatrix);Button("Run fault cases",FaultMatrix);Button("Run remaining combinations",Combinations);
   if(Press("Write report"))WriteReport();GUI.enabled=busy;if(Press("Stop current run"))InteractiveStop();GUI.enabled=true;
   Label("Report: docs/wardrobe-directing/evidence/gate-b-native.json");
   foreach(var result in results.TakeLast(8))Label(result.id+": "+result.status);
   GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndArea();
  }
  void Label(string text)=>GUILayout.Label(text,wrappedLabel,GUILayout.Width(ContentWidth));
  bool Press(string text)=>GUILayout.Button(text,wrappedButton,GUILayout.Width(ContentWidth),GUILayout.Height(Mathf.Max(32,wrappedButton.CalcHeight(new GUIContent(text),ContentWidth))));
  void Button(string label,Func<Task> action){if(Press(label))StartCase(action);}
  async void StartCase(Func<Task> action){if(busy)return;busy=true;lifetime=new CancellationTokenSource();try{await action();status="Finished. Inspect the case result, then choose the next check.";}catch(Exception error){status=error.Message+" Write report and send the failed case label; do not continue testing.";}finally{busy=false;lifetime.Dispose();lifetime=null;}}
  async Task AllCases(){await Case("direct","Direct",0,1,false);await Case("explicit","Explicit",0,1,false);await Case("draw","Draw",0,1,false);await Case("nested","Nested",0,1,false);await Case("persistent","Persistent",0,1,false);await Case("contained","Contained",0,1,false);await Case("unchanged","Unchanged",0,1,false);await Case("parent-motion","Motion",0,1,false);await Combinations();await StopMatrix();await FaultMatrix();}
  async Task Combinations(){for(int p=0;p<3;p++)for(int c=0;c<2;c++){lifetime.Token.ThrowIfCancellationRequested();await Case("combination-"+p+"-"+c,"Direct",p,c,false);}await Case("hidden-fixed","Direct",0,0,true);await Case("hidden-track","Direct",0,1,true);}
  async Task StopMatrix(){foreach(string phase in new[]{"BodyHold","BodySettled","Prepared","ConcealLara","LaraConcealed","WardrobeCommit","RelocationCommit","PoseBlend","RevealLara","ViewBlend","Verify","Verified"}){lifetime.Token.ThrowIfCancellationRequested();await Case("stop-"+phase,"Direct",0,0,false,phase);}}
  async Task FaultMatrix(){foreach(string fault in new[]{"body","target","fit","return"}){lifetime.Token.ThrowIfCancellationRequested();await Case("fault-"+fault,"Explicit",0,1,false,null,fault);}}
  async Task PrepareEntry(PerformanceRun run,int posture,int camera,bool hidden,CancellationToken token){
   var actor=scene.lara;actor.GetComponent<PerformerMagicShaderPresentation>()?.RestoreRegular();actor.ResetPerformance(entranceMark.position,entranceMark.rotation);performance.Player.View.ResetPerformanceView(cameraMark.position,cameraMark.rotation);
   performance.Player.BodyPresence.StartHiddenAt(scene.RequireLocation("player_start").placement);await run.Performer.SetOutfitAsync(openingOutfit,WardrobeTransition.Cut);await run.Performer.SetHairAsync(openingHair.StableKey,WardrobeTransition.Cut);
   if(actor.IsHidden)await run.Performer.DissolveInAsync(.25f);if(posture>0){await run.Performer.SitAtAsync(seat,seat.DefaultStyle);if(posture==2)await run.Performer.PersistentSitPoseAsync(seatedPose);}
   if(entranceAura!=null)run.Performer.Aura(entranceAura,actor.transform);
   await run.Performer.LookAtAsync(actor.transform.position+actor.transform.forward*3+Vector3.up*1.6f);
   if(!hidden)await run.Player.DissolveBodyInAsync(.25f);else await run.Performer.DissolveOutAsync(.25f);
   ConfigureGateViewport();await AimGateCamera(run,camera);
   await Wait(2,token);
  }
  async Task Case(string id,string entry,int posture,int camera,bool hidden,string stopAt=null,string fault=null){
   var result=new Result{id=id,status="RUNNING",entry=entry,posture=posture.ToString(),camera=camera.ToString(),hidden=hidden,stopAt=stopAt,fault=fault};results.Add(result);current=result;
   CancellationToken token=lifetime.Token;using var caseLifetime=CancellationTokenSource.CreateLinkedTokenSource(token);var run=PerformanceRun.Begin(performance,allowToyOutput:false);active=run;
   result.outputAuthority=run.HasOutputAuthority;
   StoryRuntimeContext context=null;PerformanceStoryHost production=null;long generation=0;double clock=0;bool stopReached=false,faultReached=false;
   try {
    if(result.outputAuthority)throw new InvalidOperationException("Diagnostic run unexpectedly acquired output authority.");
    status="Establishing "+id+" entrance. This is setup, before the card snapshot.";await PrepareEntry(run,posture,camera,hidden,token);
    var linked=StoryLinkedContent.Create(root.program,root.content,new[]{cards},root.sourceIdentity);var missing=linked.MandatoryDiagnostics(scene,entry);if(missing.Length>0)throw new InvalidOperationException(string.Join("\n",missing));
    context=new StoryRuntimeContext(run,scene,GameApplication.EnsureStarted(),"GateB",771);production=new PerformanceStoryHost(context,root.content,linked);
    context.Session.world.posture=posture==0?PlayerPosture.Standing:PlayerPosture.Sitting;
    var adapter=new GateHost(production);StoryMachine machine=null;var injected=new HashSet<string>();
    if(entry=="Motion"){
     var driver=performance.Playback;if(driver==null || scene.lara.MotionSource!=driver || run.ContactTarget==null)throw new InvalidOperationException("Parent-motion case requires the saved local driver and measured contact target.");
     driver.StopMotion();driver.SourceMode=MotionSourceMode.Sine;driver.RestartMotion();generation=driver.CommandGeneration;
     await run.Performer.GripAsync(run.ContactTarget);run.Performer.FollowGripMotion();clock=driver.CurrentSample.TimeSeconds;
    }
    production.CardPresentationObserved+=observation=>{result.observations.Add(observation);status=id+" — "+observation.phase+" "+observation.card;};
    production.CardReturnCheckpoint+=phase=>{
     result.phases.Add(phase);result.timings.Add(new Stamp{phase=phase,seconds=Time.realtimeSinceStartup});
     if(phase==stopAt){stopReached=true;run.Stop();return;}
     if(fault=="return"&&phase=="PoseBlend"){faultReached=true;throw new InvalidOperationException("GateB intentional return fault");}
     if(phase!="BodySettled")return;
     if(fault=="target"){faultReached=true;if(disposableTarget==null)throw new InvalidOperationException("Disposable camera target was not prepared.");DestroyImmediate(disposableTarget.gameObject);disposableTarget=null;}
#if UNITY_EDITOR
     if(fault=="fit"){
      invalidFit=openingOutfit.FitStates.Single(f=>f.VisibleMask==openingOutfit.PopulatedMask);
      if(!UnityEditor.AssetDatabase.GetAssetPath(invalidFit).StartsWith("Assets/__WardrobeProtectedCards/",StringComparison.Ordinal))throw new InvalidOperationException("Fault injection refused a shared fit asset.");
      invalidFitJson=JsonUtility.ToJson(invalidFit);faultReached=true;invalidFit.ConfigureGenerated(invalidFit.VisibleMask,invalidFit.CharacterSignature,WardrobeFitStatus.Invalid,null,invalidFit.AttachmentMeshes,invalidFit.SurfaceBindings,invalidFit.DissolveProfile,Array.Empty<string>(),false,null);
     }
#endif
    };
    adapter.Before=async command=>{
     string key=(machine.CardContext?.InvocationNumber??0)+":"+command.Source?.declaration;
     bool disruptive=command.Source?.declaration=="card Disruptive"||command.Source?.declaration=="sequence PersistentDemo";
     if(disruptive && command.Verb=="Lara.SetOutfit" && scene.lara.IsHidden)await run.Performer.DissolveInAsync(.25f);
     if(command.Verb!="Wait" || command.Require("seconds").Numeric!=2 || !injected.Add(key))return;
     if(entry=="Motion"){
      var driver=performance.Playback;bool stopRejected=false,replaceRejected=false;
      try{run.StopPlayback();}catch(PerformanceCommandException error){stopRejected=error.OriginalStatus=="ParentMotionOwned";}
      try{run.StartPlayback(replacementMotion);}catch(PerformanceCommandException error){replaceRejected=error.OriginalStatus=="AlreadyRunning";}
      if(!stopRejected||!replaceRejected||!driver.IsRunning||driver.CommandGeneration!=generation)throw new InvalidOperationException("Parent driver ownership guard failed.");
      return;
     }
     if(!disruptive)return;
     performance.Player.View.ResetPerformanceView(shiftCamera.position,shiftCamera.rotation);
     await AimGateCamera(run,camera);
     if(scene.playerBody.IsStablyHidden)await run.Player.DissolveBodyInAsync(.25f);else await run.Player.DissolveBodyOutAsync(.25f);
     await run.Performer.ExpressionAsync(changedExpression,.35f,.25f);await run.Performer.LookAtAsync(scene.lara.transform.position+scene.lara.transform.forward*3+Vector3.up*1.6f+scene.lara.transform.right*.5f);run.Performer.ClearAura();
     status=id+" — outgoing state, held for two seconds before return.";
     if(stopAt=="BodyHold"){stopReached=true;run.Stop();}
     if(fault=="body"){faultReached=true;throw new InvalidOperationException("GateB intentional body fault");}
    };
    if(fault=="target"){
     var target=new GameObject("Gate B disposable captured target");disposableTarget=target.transform;disposableTarget.position=scene.laraFocus.position;run.Player.Track(disposableTarget,.1f);await Wait(.2f,token);
    }
    machine=new StoryMachine(linked.Program,adapter,context.Randoms);double mood=production.Read("Session.Mood").Numeric;
    using var machineLifetime=CancellationTokenSource.CreateLinkedTokenSource(caseLifetime.Token,context.Cancellation);
    try{await machine.RunAsync(entry,machineLifetime.Token);}catch(Exception error)when(stopAt!=null||fault!=null||result.interactiveStopped){result.error=error.Message;}
    result.trailing=production.Read("Session.Mood").Numeric==mood+1;result.progress=production.Read("Session.PhaseCompletion").Numeric;
    if(stopAt!=null||fault!=null||result.interactiveStopped){
     if(!(stopReached||faultReached||result.interactiveStopped)||result.trailing||result.progress!=0||result.observations.Any(o=>o.phase=="Returned"))throw new InvalidOperationException("Terminal case did not stop at its named failure without trailing cue/progress.");
     if(fault!=null){string expected=fault=="body"?"GateB intentional body fault":fault=="return"?"GateB intentional return fault":fault=="fit"?"Wardrobe":"target";if(result.error==null||result.error.IndexOf(expected,StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Wrong terminal failure for "+fault+": "+result.error);}
     int phases=result.phases.Count;await Wait(.75f,token);if(result.phases.Count!=phases)throw new InvalidOperationException("Return continued after terminal Stop/fault.");
    } else {
     if(!result.trailing)throw new InvalidOperationException("Trailing cue did not execute after return.");
     var entered=result.observations.Count(o=>o.phase=="Entrance");var returned=result.observations.Count(o=>o.phase=="Returned");
     if(entry=="Persistent"){if(entered!=0 || scene.lara.CurrentWardrobe.OutfitId==openingOutfit.PresetId)throw new InvalidOperationException("Sequence did not retain its outfit change.");}
     else if(entered==0||entered!=returned)throw new InvalidOperationException("Card entrance/verified return counts differ.");
     if(new[]{"Direct","Explicit","Draw","Contained","Nested"}.Contains(entry)&&!result.observations.Any(o=>o.phase=="Outgoing"&&o.changedDomains.Contains("Wardrobe")&&o.changedDomains.Contains("View")))throw new InvalidOperationException("No nontrivial wardrobe/view disruption was observed.");
     if(entry=="Unchanged"&&result.phases.Any(p=>p=="ConcealLara"||p=="ViewBlend"||p=="WardrobeCommit"||p=="PoseBlend"))throw new InvalidOperationException("No-change card restarted presentation.");
     if((entry=="Explicit"||entry=="Draw")&&result.progress!=10)throw new InvalidOperationException("Director credit was not granted after verified return.");
     if(entry=="Motion"){var driver=performance.Playback;result.driverRetained=driver.IsRunning&&driver.CommandGeneration==generation&&driver.CurrentSample.TimeSeconds>clock&&scene.lara.IsGripping;if(!result.driverRetained)throw new InvalidOperationException("Parent driver/grip was restarted or not retained.");}
     await Wait(2,token);
    }
    result.status="PASS";
   }catch(Exception error){result.status="FAIL";result.error=error.ToString();status=result.error;Debug.LogError("Gate B failed "+result.id+": "+result.error,this);}
   finally{RestoreFaultFit();if(disposableTarget!=null)Destroy(disposableTarget.gameObject);disposableTarget=null;context?.Dispose();run.Dispose();performance.Playback?.StopMotion();active=null;current=null;}
   if(result.status=="FAIL")throw new InvalidOperationException("Gate B stopped at failed case "+result.id+".");
  }
  void RestoreFaultFit(){if(invalidFit!=null && invalidFitJson!=null)JsonUtility.FromJsonOverwrite(invalidFitJson,invalidFit);invalidFit=null;invalidFitJson=null;}
  static async Task Wait(float seconds,CancellationToken token){float elapsed=0;while(elapsed<seconds){token.ThrowIfCancellationRequested();await Awaitable.NextFrameAsync(token);elapsed+=Mathf.Max(0,Time.deltaTime);}}
  static string CurrentFingerprint(){string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"DazPose"));using(var hash=SHA256.Create()){string data=string.Join("\n",Directory.GetFiles(folder,"*.cs",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(f=>Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(f)))));return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data))).Replace("-","").ToLowerInvariant();}}
  void WriteReport(){var required=new[]{"direct","explicit","draw","nested","persistent","contained","unchanged","parent-motion","interactive-stop","hidden-fixed","hidden-track"}.Concat(Enumerable.Range(0,3).SelectMany(p=>Enumerable.Range(0,2).Select(c=>"combination-"+p+"-"+c))).Concat(new[]{"body","target","fit","return"}.Select(f=>"fault-"+f)).Concat(new[]{"BodyHold","BodySettled","Prepared","ConcealLara","LaraConcealed","WardrobeCommit","RelocationCommit","PoseBlend","RevealLara","ViewBlend","Verify","Verified"}.Select(p=>"stop-"+p)).ToArray();
   var report=new Report{revision=runningFingerprint,scenePath=gameObject.scene.path,sceneGuid=sceneGuid,generation=openingHair.GenerationId,timestampUtc=DateTime.UtcNow.ToString("O"),requiredCases=required,cases=results,toyOutputAllowed=results.Any(r=>r.outputAuthority),nativeResult=required.All(id=>results.LastOrDefault(r=>r.id==id)?.status=="PASS")?"PASS":"INCOMPLETE_OR_FAILED"};
   string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/wardrobe-directing/evidence/gate-b-native.json"));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));status="Wrote "+path+" — "+report.nativeResult;
  }
  sealed class GateHost : IStoryExecutionHost,IStoryCardPresentationHost,IStoryCardEligibility,IStoryDirectorObserver,IStoryPhaseObserver {
   readonly PerformanceStoryHost host;public Func<StoryCommand,Task> Before;public GateHost(PerformanceStoryHost host){this.host=host;}
   public StoryData Read(string key)=>host.Read(key);public void Write(string key,StoryData value)=>host.Write(key,value);public async Task<StoryData> InvokeAsync(StoryCommand c,CancellationToken t){if(Before!=null)await Before(c);return await host.InvokeAsync(c,t);}public Task<StoryData> WaitAnyAsync(StoryData[] e,CancellationToken t)=>host.WaitAnyAsync(e,t);public StoryData Function(string n,StoryData[] a)=>host.Function(n,a);public Task YieldAsync(CancellationToken t)=>host.YieldAsync(t);public void CancelOperations()=>host.CancelOperations();public void Trace(StorySource s,string m)=>host.Trace(s,m);public Task<IStoryCardPresentationScope> EnterCardAsync(CardPresentationRequest r,CancellationToken t)=>host.EnterCardAsync(r,t);public string ResourceIssue(CardDefinition c)=>host.ResourceIssue(c);public void ObserveDirector(DirectorSnapshot s,CardExecutionContext c)=>host.ObserveDirector(s,c);public void LeavePhase(string p)=>host.LeavePhase(p);
  }
 }
}
