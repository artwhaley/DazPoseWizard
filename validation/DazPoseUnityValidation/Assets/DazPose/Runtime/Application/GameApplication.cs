using System;
using System.Linq;
using System.Threading.Tasks;
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
  SceneNavigationCoordinator navigation;
  public bool IsNavigating => navigation != null && navigation.IsNavigating;
  public string NavigationError => navigation?.LastError;
  public static GameApplication Current => instance;
  public ToyControlService Toys => toys;
  public SceneCatalog Catalog => catalog;
  public GameSettingsService Settings { get; private set; }
  public PlayerProfileService Profile { get; private set; }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStatics() => instance = null;
  public static GameApplication EnsureStarted() {
   if(instance != null) return instance;
   var existing = FindObjectsByType<GameApplication>();
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
   Profile=new PlayerProfileService();
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
  SceneNavigationCoordinator Navigation => navigation ?? (navigation =
   new SceneNavigationCoordinator(StopSession, LoadSceneAsync, path => Application.CanStreamedLevelBeLoaded(path)));
  public void StartScene(string id) { _ = StartSceneAsync(id); }
  public Task<bool> StartSceneAsync(string id) {
   var entry = catalog?.entries?.FirstOrDefault(e => e != null && e.enabled && e.id == id);
   if(entry == null) return Task.FromResult(Navigation.Reject("That scene is not published or is no longer available."));
   return Navigation.NavigateAsync(entry.scenePath);
  }
  public void RestartCurrentScene() { _ = Navigation.NavigateAsync(SceneManager.GetActiveScene().path); }
  public void ReturnToMenu() { _ = ReturnToMenuAsync(); }
  public Task<bool> ReturnToMenuAsync() => catalog == null ?
   Task.FromResult(Navigation.Reject("The application scene catalog is unavailable.")) :
   Navigation.NavigateAsync(catalog.mainMenuPath);
  static async Task LoadSceneAsync(string path) {
   Debug.Log("DazPose scene load started: " + path);
   var load = SceneManager.LoadSceneAsync(path);
   if(load == null) throw new InvalidOperationException("Unity did not create a scene load operation.");
   while(!load.isDone) await Awaitable.NextFrameAsync();
   Debug.Log("DazPose scene load completed: " + path);
  }
  public void Configure(ToyControlService service, SceneCatalog scenes) { toys=service; catalog=scenes; }
  void OnDestroy() { if(instance==this) { StopSession(); instance=null; } }
 }
}