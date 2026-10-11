using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Game;
using DazPose.Toys;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation {
 public sealed class SettingsMenuController : MonoBehaviour {
  [SerializeField] InputField endpoint;
  [SerializeField] Button connect,disconnect,scan,stop,close;
  [SerializeField] Slider master,speech;
  [SerializeField] Text state;
  [SerializeField] Transform routes;
  [SerializeField] SettingsRouteRow routeTemplate;
  GameSettingsService settings;
  string deviceSignature;
  readonly List<SettingsRouteRow> rows=new List<SettingsRouteRow>();
  public void Configure(InputField address,Button open,Button leave,Button discover,Button halt,Button done,Slider volume,Slider voice,Text status,Transform list,SettingsRouteRow row) {
   endpoint=address; connect=open; disconnect=leave; scan=discover; stop=halt; close=done;
   master=volume; speech=voice; state=status; routes=list; routeTemplate=row;
  }
  void Awake() {
   settings=GameApplication.EnsureStarted().Settings;
   endpoint.onEndEdit.AddListener(value=>{ settings.Values.endpoint=value; settings.SaveSettings(); });
   master.onValueChanged.AddListener(value=>{ settings.Values.masterVolume=value; settings.SaveSettings(); });
   speech.onValueChanged.AddListener(value=>{ settings.Values.speechVolume=value; settings.SaveSettings(); });
   connect.onClick.AddListener(settings.Connect); disconnect.onClick.AddListener(settings.Disconnect);
   scan.onClick.AddListener(settings.Scan); stop.onClick.AddListener(settings.StopAllOutputs);
   close.onClick.AddListener(()=>gameObject.SetActive(false));
  }
  public void FitToParentCanvas() {
   if(transform.parent==null) return;
   var rect=(RectTransform)transform;
   rect.localScale=Vector3.one;
   rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
   rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=Vector2.zero; rect.sizeDelta=Vector2.zero;
  }
  void OnEnable() {
   FitToParentCanvas();
   if(settings==null) settings=GameApplication.EnsureStarted().Settings;
   endpoint.SetTextWithoutNotify(settings.Values.endpoint);
   master.SetValueWithoutNotify(settings.Values.masterVolume); speech.SetValueWithoutNotify(settings.Values.speechVolume);
   RefreshRoutes(true);
  }
  void Update() {
   state.text=settings.Connected ? settings.Scanning ? "Connected · scanning" : "Connected" : "Disconnected";
   RefreshRoutes(false);
  }
  void RefreshRoutes(bool force) {
   string signature=string.Join("|",settings.Devices.Select(d=>d.DeviceIndex+":"+ToyControlService.DeviceSignature(d)));
   if(!force && signature==deviceSignature) return;
   deviceSignature=signature;
   foreach(var row in rows) Destroy(row.gameObject); rows.Clear();
   foreach(var device in settings.Devices)
   foreach(var feature in device.Features)
   foreach(var output in feature.Outputs) {
    var row=Instantiate(routeTemplate,routes); rows.Add(row);
    row.Configure(settings,device,feature,output);
    row.gameObject.SetActive(true);
   }
   routeTemplate.gameObject.SetActive(false);
  }
 }
}