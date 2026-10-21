using System;
using System.Collections.Generic;
using UnityEngine;
namespace DazPose.Player {
 /// <summary>
 /// On-screen test panel for the temporary Player Setup scene. Every button calls the same runtime methods the story
 /// verbs use (PlayerCameraController, PlayerBodyPresence, PlayerView). Not part of any production scene.
 /// </summary>
 [DisallowMultipleComponent]
 public sealed class PlayerSetupPanel : MonoBehaviour {
  [SerializeField] PlayerController player;
  [SerializeField] PlayerCameraController cameraController;
  [SerializeField] PlayerBodyPresence body;
  [SerializeField] Transform trackTarget;
  [SerializeField] PlayerBreathing breathing;
  string status="Ready.";
  Vector2 scroll;
  public void Configure(PlayerController controller,PlayerCameraController cameras,PlayerBodyPresence presence,Transform target,PlayerBreathing breath=null) {
   player=controller; cameraController=cameras; body=presence; trackTarget=target; breathing=breath;
  }
  /// <summary>Names of the references that are not assigned. Empty when the panel is fully wired.</summary>
  public IReadOnlyList<string> MissingReferences() {
   var missing=new List<string>();
   if(player==null) missing.Add("player");
   if(cameraController==null) missing.Add("cameraController");
   if(body==null) missing.Add("body");
   if(trackTarget==null) missing.Add("trackTarget");
   if(player!=null && body!=null && player.BodyPresence!=body) missing.Add("player.BodyPresence (must be the same body)");
   if(body!=null && body.GetComponent<PlayerCameraMounts>()==null) missing.Add("body PlayerCameraMounts");
   if(cameraController!=null && player!=null && cameraController.gameObject!=player.gameObject) missing.Add("cameraController (must be on the Player)");
   return missing;
  }
  async void Run(string label,Func<Awaitable> action) {
   status=label+"...";
   try { await action(); status=label+": done."; }
   catch(Exception error) { status=label+" failed: "+error.Message; Debug.LogException(error,this); }
  }
  void OnGUI() {
   var missing=MissingReferences();
   GUILayout.BeginArea(new Rect(16,16,320,Screen.height-32),GUI.skin.box);
   scroll=GUILayout.BeginScrollView(scroll);
   GUILayout.Label("Player Setup (temporary test panel)");
   if(missing.Count>0) { GUILayout.Label("Missing: "+string.Join(", ",missing)); GUILayout.EndScrollView(); GUILayout.EndArea(); return; }
   GUILayout.Label("Camera mount: "+(cameraController.CurrentHeight?.ToString() ?? "none yet")+(cameraController.IsChangingHeight ? " (changing)" : ""));
   var fade=cameraController.GetComponentInChildren<PlayerScreenFade>(true) ?? player.MainCamera.GetComponent<PlayerScreenFade>();
   GUILayout.Label("Fade alpha: "+(fade!=null ? fade.Alpha.ToString("F2") : "(none yet)"));
   GUILayout.Label("Tracking: "+(player.View.IsTracking ? "yes" : player.View.IsAcquiringTrack ? "acquiring" : "no")+"    Body: "+body.VisibilityState);
   GUILayout.Space(6);
   GUILayout.BeginHorizontal();
   if(GUILayout.Button("Start High")) Run("Start High",() => { cameraController.StartAt(PlayerCameraHeight.High); return Done(); });
   if(GUILayout.Button("Start Low")) Run("Start Low",() => { cameraController.StartAt(PlayerCameraHeight.Low); return Done(); });
   GUILayout.EndHorizontal();
   GUILayout.BeginHorizontal();
   if(GUILayout.Button("SetHeight High")) Run("SetHeight High",async () => await cameraController.SetHeightAsync(PlayerCameraHeight.High));
   if(GUILayout.Button("SetHeight Low")) Run("SetHeight Low",async () => await cameraController.SetHeightAsync(PlayerCameraHeight.Low));
   GUILayout.EndHorizontal();
   if(GUILayout.Button("Fade test: hold black 2 s")) Run("Fade test",async () => await FadeTest());
   GUILayout.Space(6);
   GUILayout.BeginHorizontal();
   if(GUILayout.Button("Body In")) Run("Body In",async () => await body.DissolveInAsync());
   if(GUILayout.Button("Body Out")) Run("Body Out",async () => await body.DissolveOutAsync());
   GUILayout.EndHorizontal();
   GUILayout.Space(6);
   GUILayout.BeginHorizontal();
   if(GUILayout.Button("Track target")) Run("Track target",() => { player.Track(trackTarget); return Done(); });
   if(GUILayout.Button("Release")) Run("Release",async () => await cameraController.ReleaseAsync());
   GUILayout.EndHorizontal();
   var position=trackTarget.position;
   GUILayout.Label("Target X "+position.x.ToString("F2")); position.x=GUILayout.HorizontalSlider(position.x,-3f,3f);
   GUILayout.Label("Target Y "+position.y.ToString("F2")); position.y=GUILayout.HorizontalSlider(position.y,0.2f,3f);
   GUILayout.Label("Target Z "+position.z.ToString("F2")); position.z=GUILayout.HorizontalSlider(position.z,-4f,4f);
   trackTarget.position=position;
   if(breathing!=null) {
    GUILayout.Space(6);
    breathing.enabled=GUILayout.Toggle(breathing.enabled,"Breathing");
   }
   GUILayout.Space(6);
   GUILayout.Label(status);
   GUILayout.EndScrollView();
   GUILayout.EndArea();
  }
  async Awaitable FadeTest() {
   var fade=PlayerScreenFade.For(player.MainCamera);
   for(float t=0f;t<0.5f;t+=Time.deltaTime){ fade.Alpha=t/0.5f; await Awaitable.NextFrameAsync(); }
   fade.Alpha=1f;
   await Awaitable.WaitForSecondsAsync(2f);
   for(float t=0f;t<0.5f;t+=Time.deltaTime){ fade.Alpha=1f-t/0.5f; await Awaitable.NextFrameAsync(); }
   fade.Alpha=0f;
  }
  static Awaitable Done() { var source=new AwaitableCompletionSource(); source.SetResult(); return source.Awaitable; }
  void OnDrawGizmos() {
   if(trackTarget==null) return;
   Gizmos.color=new Color(1f,.4f,.8f);
   Gizmos.DrawWireSphere(trackTarget.position,0.12f);
  }
 }
}
