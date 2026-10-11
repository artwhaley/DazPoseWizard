using System;
using DazPose.Toys;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Game {
 [DisallowMultipleComponent]
 public sealed class GameApplication : MonoBehaviour {
  static GameApplication instance;
  [SerializeField] ToyControlService toys;
  [SerializeField] SceneCatalog catalog;
  Action stopSession;
  object session;
  bool navigating;
  public static GameApplication Current => instance;
  public ToyControlService Toys => toys;
  public SceneCatalog Catalog => catalog;
  public GameSettingsService Settings { get; private set; }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStatics() => instance = null;
  public static GameApplication EnsureStarted() {
   if(instance != null) return instance;
   var existing = FindObjectsByType<GameApplication>(FindObjectsSortMode.None);
   if(existing.Length > 0) { existing[0].Initialize(); return instance; }
   var prefab = Resources.Load<GameObject>("DazPose/GameApplication");
   if(prefab == null) throw new InvalidOperationException("Install the DazPose application prefab using Initialize Game Session.");
   return Instantiate(prefab).GetComponent<GameApplication>();
  }
  void Awake() => Initialize();
  void Initialize() {
   if(instance != null && instance != this) { Destroy(gameObject); return; }
   if(Settings != null) { instance=this; return; }
   instance=this; DontDestroyOnLoad(gameObject);
   if(toys == null) toys=GetComponent<ToyControlService>();
   Settings=new GameSettingsService(toys);
  }
  public void RegisterSession(object owner, Action stop) {
   if(session != owner) StopSession();
   session=owner; stopSession=stop;
  }
  public bool OwnsSession(object owner) => session==owner;
  public void ReleaseSession(object owner) { if(session == owner) { session=null; stopSession=null; } }
  public void StopSession() {
   var stop=stopSession; session=null; stopSession=null;
   stop?.Invoke();
   try { toys?.StopAllOutput(); } catch {}
  }
  public void StartScene(string id) {
   foreach(var entry in catalog.entries)
    if(entry.enabled && entry.id==id) { Navigate(entry.scenePath); return; }
  }
  public void RestartCurrentScene() => Navigate(SceneManager.GetActiveScene().path);
  public void ReturnToMenu() => Navigate(catalog.mainMenuPath);
  async void Navigate(string path) {
   if(navigating || string.IsNullOrEmpty(path)) return;
   navigating=true;
   try { StopSession(); Debug.Log("DazPose scene load started: "+path); var load=SceneManager.LoadSceneAsync(path);
    while(!load.isDone) await Awaitable.NextFrameAsync();
    Debug.Log("DazPose scene load completed: "+path);
   } finally { navigating=false; }
  }
  public void Configure(ToyControlService service, SceneCatalog scenes) { toys=service; catalog=scenes; }
  void OnDestroy() { if(instance==this) { StopSession(); instance=null; } }
 }
}