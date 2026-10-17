using System;
using System.Threading;
using DazPose.Game;
using DazPose.Story;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation {
 // Extends the existing Settings screen; it owns no scene/session lifecycle.
 [DisallowMultipleComponent]
 public sealed class StorySettingsProfileController : MonoBehaviour {
  StoryPromptService prompts;
  CancellationTokenSource cancellation;
  void Awake() {
   var interests=StoryUi.Button(transform,"Interests",()=>Open(true)); Place(interests,240);
   var equipment=StoryUi.Button(transform,"Equipment",()=>Open(false)); Place(equipment,470);
  }
  static void Place(Button button,float x) {
   var rect=(RectTransform)button.transform; rect.anchorMin=rect.anchorMax=new Vector2(.5f,1); rect.pivot=new Vector2(.5f,1);
   rect.anchoredPosition=new Vector2(x,-28); rect.sizeDelta=new Vector2(210,46);
  }
  void OnEnable() { cancellation=new CancellationTokenSource(); }
  async void Open(bool interests) {
   if(prompts==null) prompts=StoryPromptService.Create(null,"Settings profile prompts");
   if(prompts.IsOpen) return;
   try {
    var app=GameApplication.EnsureStarted(); var catalogs=StoryCatalogs.Load();
    if(interests) await prompts.ShowKinkEditorAsync(app.Profile,catalogs.Kinks,cancellation.Token);
    else await prompts.ShowEquipmentEditorAsync(app.Profile,catalogs.Equipment,cancellation.Token);
   } catch(OperationCanceledException) {}
   catch(Exception error) { Debug.LogError("Settings profile editor: "+error); }
  }
  void OnDisable() { cancellation?.Cancel(); cancellation?.Dispose(); cancellation=null; }
  void OnDestroy() { if(prompts!=null) Destroy(prompts.gameObject); }
 }
}