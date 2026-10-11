using System;
using System.Collections.Generic;
using System.IO;
using DazPose.Performer;
using DazPose.Toys;
using UnityEngine;
namespace DazPose.Game {
 public sealed class GameSettingsService {
  [Serializable] sealed class TuningFile { public int version=1; public SceneTuningSnapshot[] scenes=Array.Empty<SceneTuningSnapshot>(); }
  readonly ToyControlService toys;
  readonly string directory;
  string routeJson;
  PerformanceSceneBindings currentBindings;
  readonly Dictionary<string, SceneTuningSnapshot> cache=new Dictionary<string, SceneTuningSnapshot>();
  readonly Dictionary<string, SceneTuningSnapshot> saved=new Dictionary<string, SceneTuningSnapshot>();
  public GameSettings Values { get; private set; }
  public bool Connected => toys!=null && toys.IsConnected;
  public bool Scanning => toys!=null && toys.IsScanning;
  public IReadOnlyList<ToyDevice> Devices => toys!=null ? toys.ConnectedDevices : Array.AsReadOnly(Array.Empty<ToyDevice>());
  public GameSettingsService(ToyControlService service, string storageDirectory=null) {
   toys=service; directory=storageDirectory ?? Path.Combine(Application.persistentDataPath,"DazPose");
   Values=Read<GameSettings>("game-settings.json") ?? new GameSettings { endpoint=service!=null ? service.ServerAddress : ToyControlService.DefaultServerAddress };
   var tuning=Read<TuningFile>("scene-tuning.json");
   if(tuning!=null) foreach(var s in tuning.scenes ?? Array.Empty<SceneTuningSnapshot>()) saved[Key(s.sceneId,s.actorId)]=s;
   Values.routes=Values.routes ?? Array.Empty<ToyRouteSettings>();
   ApplySettings();
  }
  T Read<T>(string file) where T:class {
   try { var path=Path.Combine(directory,file); return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null; } catch { return null; }
  }
  void Write(string file, object value) {
   Directory.CreateDirectory(directory);
   var path=Path.Combine(directory,file); var temporary=path+".tmp";
   File.WriteAllText(temporary,JsonUtility.ToJson(value,true));
   if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
  }
  public void SaveSettings() { ApplySettings(); Write("game-settings.json",Values); }
  void ApplySettings() {
   AudioListener.volume=Mathf.Clamp01(Values.masterVolume);
   if(toys!=null) {
    toys.ServerAddress=Values.endpoint;
    string next=JsonUtility.ToJson(Values);
    // Only route changes replace output ownership. Audio/endpoint edits leave live choreography alone.
    var routeOnly=new GameSettings { routes=Values.routes };
    next=JsonUtility.ToJson(routeOnly)+"|"+string.Join(",",Array.ConvertAll(Values.routes,r=>r.sessionDeviceIndex?.ToString() ?? ""));
    if(next!=routeJson) { toys.ConfigureRoutes(Values.routes); routeJson=next; }
   }
   ApplyAudio(currentBindings);
  }
  public void Connect() {
   try {
    // Backend indices belong only to the current connection. Unique saved signatures
    // resolve again; ambiguous devices require a new explicit Settings selection.
    foreach(var route in Values.routes) route.sessionDeviceIndex=null;
    routeJson=null; ApplySettings(); toys?.Connect();
   } catch {}
  }
  public void Disconnect() { try { toys?.Disconnect(); } catch {} }
  public void Scan() { try { toys?.StartScanning(); } catch {} }
  public void StopAllOutputs() { try { toys?.StopAllOutput(); } catch {} }
  public void TestOutput(ToyRouteSettings route, float level=.25f) {
   try {
    toys?.TestConfiguredRoute(route,level);
   } catch {}
  }
  static string Key(string scene,string actor) => scene+"/"+actor;
  static bool HasIdentity(PerformanceSceneBindings b) => b!=null && b.Performer!=null && !string.IsNullOrEmpty(b.SceneId) && !string.IsNullOrEmpty(b.ActorId);
  public void BindAudio(PerformanceSceneBindings bindings) { currentBindings=bindings; ApplyAudio(bindings); }
  public void UnbindAudio(PerformanceSceneBindings bindings) { if(currentBindings==bindings) currentBindings=null; }
  void ApplyAudio(PerformanceSceneBindings b) { if(b!=null && b.Performer!=null && b.Performer.SpeechAudioSource!=null) b.Performer.SpeechAudioSource.volume=Mathf.Clamp01(Values.speechVolume); }
  public void CaptureSceneTuning(PerformanceSceneBindings bindings) {
   if(!HasIdentity(bindings)) return;
   var key=Key(bindings.SceneId,bindings.ActorId);
   var json=JsonUtility.ToJson(SceneTuningSnapshot.Capture(bindings));
   // Compare values, including curve keys, so unchanged frames never write to disk.
   if(saved.TryGetValue(key,out var previous) && JsonUtility.ToJson(previous)==json) {
    cache[key]=previous; return;
   }
   // Detach mutable curves from the live actor before retaining a baseline.
   var current=JsonUtility.FromJson<SceneTuningSnapshot>(json);
   cache[key]=current;
   var values=new Dictionary<string,SceneTuningSnapshot>(saved) { [key]=current };
   Write("scene-tuning.json",new TuningFile { scenes=new List<SceneTuningSnapshot>(values.Values).ToArray() });
   // Advance the saved baseline only after a successful atomic write.
   saved[key]=current;
  }
  public void ApplySceneTuning(PerformanceSceneBindings bindings) {
   if(!HasIdentity(bindings)) return;
   var key=Key(bindings.SceneId,bindings.ActorId);
   if(cache.TryGetValue(key,out var current)) JsonUtility.FromJson<SceneTuningSnapshot>(JsonUtility.ToJson(current)).Apply(bindings);
   else if(saved.TryGetValue(key,out var persisted)) JsonUtility.FromJson<SceneTuningSnapshot>(JsonUtility.ToJson(persisted)).Apply(bindings);
  }
  public void SaveCurrentTuning(PerformanceSceneBindings bindings) {
   if(!HasIdentity(bindings)) throw new InvalidOperationException("Assign stable scene and actor IDs before saving tuning.");
   CaptureSceneTuning(bindings);
  }
 }
}