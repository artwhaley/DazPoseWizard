using DazPose.Game;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation {
 public sealed class MainMenuController : MonoBehaviour {
  [SerializeField] Transform sceneList;
  [SerializeField] Button sceneButton, settingsButton, quitButton;
  [SerializeField] SettingsMenuController settings;
  public void Configure(Transform list,Button template,Button options,Button quit,SettingsMenuController panel) {
   sceneList=list; sceneButton=template; settingsButton=options; quitButton=quit; settings=panel;
  }
  void Start() {
   var app=GameApplication.EnsureStarted();
   foreach(var entry in app.Catalog.entries) {
    if(!entry.enabled) continue;
    var button=Instantiate(sceneButton,sceneList);
    button.GetComponentInChildren<Text>().text=entry.title;
    string id=entry.id; button.onClick.AddListener(()=>app.StartScene(id));
    button.gameObject.SetActive(true);
   }
   sceneButton.gameObject.SetActive(false);
   settingsButton.onClick.AddListener(()=>settings.gameObject.SetActive(true));
   quitButton.onClick.AddListener(Application.Quit);
  }
 }
}