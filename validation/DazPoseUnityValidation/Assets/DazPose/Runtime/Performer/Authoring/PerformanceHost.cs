using System;
using DazPose.Game;
using UnityEngine;
namespace DazPose.Performer {
 public enum PerformanceHostState { Idle, Running, Stopped, Completed, Failed }
 [DisallowMultipleComponent]
 public sealed class PerformanceHost : MonoBehaviour {
  [SerializeField] PerformanceSceneBindings bindings;
  [SerializeField] PerformanceSequence sequence;
  [SerializeField] bool playOnStart=true;
  PerformanceRun run;
  GameApplication app;
  int generation;
  bool appliedTuning;
  public PerformanceHostState State { get; private set; }
  public Exception Failure { get; private set; }
  public PerformanceSceneBindings Bindings => bindings;
  public PerformanceSequence Sequence => sequence;
  public bool PlayOnStart { get=>playOnStart; set=>playOnStart=value; }
  public void Configure(PerformanceSceneBindings references, PerformanceSequence content) { bindings=references; sequence=content; }
  void Start() { if(playOnStart) StartPerformance(); }
  public void StartPerformance() {
   if(State==PerformanceHostState.Running || !isActiveAndEnabled) return;
   int id=++generation;
   Failure=null;
   try {
    app=GameApplication.EnsureStarted();
    if(bindings==null || sequence==null) throw new InvalidOperationException("Assign saved bindings and sequence on PerformanceHost.");
    if(!appliedTuning) { app.Settings.ApplySceneTuning(bindings); appliedTuning=true; }
    app.RegisterSession(this,StopOwned);
    app.Settings.BindAudio(bindings);
    run=PerformanceRun.Begin(bindings);
    State=PerformanceHostState.Running;
    Debug.Log("DazPose performance started: "+gameObject.scene.path+"; "+sequence.GetType().Name,this);
    Execute(id,run);
   } catch(Exception error) {
    Failure=error; State=PerformanceHostState.Failed; Cleanup(id,run);
   }
  }
  async void Execute(int id, PerformanceRun owned) {
   try {
    await sequence.ExecuteAsync(owned);
    if(id==generation) State=owned.Failure!=null ? PerformanceHostState.Failed : owned.IsStopped ? PerformanceHostState.Stopped : PerformanceHostState.Completed;
   } catch(OperationCanceledException error) {
    if(id==generation) { Failure=owned.Failure; State=Failure!=null ? PerformanceHostState.Failed : PerformanceHostState.Stopped; }
   } catch(Exception error) {
    if(id==generation) { Failure=owned.Failure ?? error; State=PerformanceHostState.Failed; }
   } finally { if(id==generation) Debug.Log("DazPose performance ended: "+gameObject.scene.path+"; "+State+(Failure!=null ? "; "+Failure : ""),this); Cleanup(id,owned); }
  }
  // Observe live Inspector and script edits even after completion/Stop and while paused.
  void LateUpdate() { Capture(); }
  void Capture() { if(appliedTuning && app!=null) app.Settings.CaptureSceneTuning(bindings); }
  void Cleanup(int id, PerformanceRun owned) {
   if(id==generation) { Capture(); run=null; app?.ReleaseSession(this); }
   owned?.Dispose();
  }
  public void StopPerformance() {
   if(app!=null && app.OwnsSession(this)) app.StopSession(); else StopOwned();
  }
  void StopOwned() {
   Capture();
   ++generation;
   if(State==PerformanceHostState.Running) State=PerformanceHostState.Stopped;
   var owned=run; run=null;
   owned?.Stop(); owned?.Dispose();
   app?.ReleaseSession(this);
  }
  void OnDisable() { StopPerformance(); app?.Settings.UnbindAudio(bindings); }
 }
}