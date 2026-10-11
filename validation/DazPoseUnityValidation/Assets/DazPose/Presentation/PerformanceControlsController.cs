using DazPose.Game;
using DazPose.Performer;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation {
 public sealed class PerformanceControlsController : MonoBehaviour {
  [SerializeField] PerformanceHost host;
  [SerializeField] Button start,stop,restart,options,menu;
  [SerializeField] Text state;
  [SerializeField] SettingsMenuController settings;
  public void BindHost(PerformanceHost value) => host=value;
  public void Configure(Button play,Button halt,Button reload,Button config,Button home,Text label,SettingsMenuController panel) {
   start=play; stop=halt; restart=reload; options=config; menu=home; state=label; settings=panel;
  }
  void Start() {
   var app=GameApplication.EnsureStarted();
   start.onClick.AddListener(()=>host.StartPerformance());
   stop.onClick.AddListener(app.StopSession);
   restart.onClick.AddListener(app.RestartCurrentScene);
   options.onClick.AddListener(()=>settings.gameObject.SetActive(true));
   menu.onClick.AddListener(app.ReturnToMenu);
  }
  void Update() { if(state!=null && host!=null) state.text=host.State.ToString(); }
 }
}