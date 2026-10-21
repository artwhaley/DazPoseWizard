using System;
using UnityEngine;
namespace DazPose.Player {
 /// <summary>
 /// The player camera verbs behind Camera.StartAt, Camera.SetHeight and Camera.Release, usable from a story run or
 /// from a test panel. Moves only the Player root and ViewRig; never writes HeadPose.
 /// </summary>
 [DisallowMultipleComponent]
 [RequireComponent(typeof(PlayerController))]
 public sealed class PlayerCameraController : MonoBehaviour {
  [SerializeField,Min(0f)] float fadeOutSeconds=0.35f;
  [SerializeField,Min(0f)] float fadeInSeconds=0.35f;
  PlayerController player;
  PlayerCameraHeightChange active;
  public float FadeOutSeconds { get => fadeOutSeconds; set => fadeOutSeconds=Mathf.Max(0f,value); }
  public float FadeInSeconds { get => fadeInSeconds; set => fadeInSeconds=Mathf.Max(0f,value); }
  public bool IsChangingHeight => active!=null;
  public PlayerCameraHeight? CurrentHeight => TryMounts(out var mounts) ? mounts.CurrentHeight : null;
  PlayerController Player => player!=null ? player : (player=GetComponent<PlayerController>());
  bool TryMounts(out PlayerCameraMounts mounts) {
   mounts=Player.BodyPresence!=null ? Player.BodyPresence.GetComponent<PlayerCameraMounts>() : null;
   return mounts!=null;
  }
  public PlayerCameraMounts RequireMounts() {
   if(Player.BodyPresence==null) throw new InvalidOperationException("Assign PlayerController.bodyPresence so the camera mounts can be found.");
   var mounts=Player.BodyPresence.GetComponent<PlayerCameraMounts>();
   if(mounts==null) throw new InvalidOperationException("Add the camera mounts to the player body (DazPose > Player > Add Camera Mounts to PlayerBody).");
   mounts.Validate();
   return mounts;
  }
  /// <summary>Instant, setup-time placement on a mount (Camera.StartAt).</summary>
  public void StartAt(PlayerCameraHeight height) {
   var mounts=RequireMounts();
   CancelHeightChange();
   mounts.PlaceView(Player.View,height);
  }
  /// <summary>Fade to black, move to the other mount, fade in (Camera.SetHeight). Immediate when already there.</summary>
  public Awaitable<PlayerCameraHeightResult> SetHeightAsync(PlayerCameraHeight height) {
   var mounts=RequireMounts();
   if(active==null && mounts.CurrentHeight==height) return Finished(PlayerCameraHeightResult.Completed);
   CancelHeightChange();
   var view=Player.View;
   var fade=PlayerScreenFade.For(Player.MainCamera);
   var source=new AwaitableCompletionSource<PlayerCameraHeightResult>();
   var change=new PlayerCameraHeightChange(fade,()=>mounts.MoveView(view,height),fadeOutSeconds,fadeInSeconds);
   change.Finished+=done => {
    if(ReferenceEquals(active,done)) active=null;
    if(done.Error!=null) { Debug.LogException(done.Error,this); source.TrySetException(done.Error); }
    else source.TrySetResult(done.Result);
   };
   active=change;
   change.Advance(0f); // a zero-length fade completes immediately
   return source.Awaitable;
  }
  /// <summary>Stops tracking and turns the view smoothly back to the current mount's authored direction (Camera.Release).</summary>
  public Awaitable<PlayerViewCompletion> ReleaseAsync() {
   var mounts=RequireMounts();
   var origin=Player.MainCamera.transform.position;
   return Player.LookAtAsync(mounts.AuthoredLookPoint(origin));
  }
  /// <summary>Returns an action that cancels the height change in progress now, if any, clearing the fade.</summary>
  public Action CaptureCancellation() {
   var captured=active;
   return () => captured?.Cancel();
  }
  public void CancelHeightChange() => active?.Cancel();
  static Awaitable<PlayerCameraHeightResult> Finished(PlayerCameraHeightResult result) {
   var source=new AwaitableCompletionSource<PlayerCameraHeightResult>();
   source.SetResult(result);
   return source.Awaitable;
  }
  void LateUpdate() => Tick(Time.deltaTime);
  /// <summary>Advances a height change in progress; called every frame, and directly by tests.</summary>
  public void Tick(float deltaTime) => active?.Advance(deltaTime);
  void OnDisable() => CancelHeightChange();
  void OnDestroy() => CancelHeightChange();
 }
}
