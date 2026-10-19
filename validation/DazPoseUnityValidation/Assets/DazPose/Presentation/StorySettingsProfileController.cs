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
   var library=ProductionUiLibrary.Load();
   AddProfileButton(library,"Interests",()=>Open(true),.56f,.75f);
   AddProfileButton(library,"Equipment",()=>Open(false),.77f,.96f);
  }
  void AddProfileButton(ProductionUiLibrary library,string label,UnityEngine.Events.UnityAction action,float left,float right) {
   var row=Instantiate(library.choiceButton,transform,false);library.Prepare(row);
   row.GetComponentInChildren<TMPro.TMP_Text>(true).text=label;
   var rect=(RectTransform)row.transform;rect.anchorMin=new Vector2(left,1);rect.anchorMax=new Vector2(right,1);
   rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(0,-74);rect.offsetMax=new Vector2(0,-28);
   row.GetComponent<Button>().onClick.AddListener(action);row.SetActive(true);
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