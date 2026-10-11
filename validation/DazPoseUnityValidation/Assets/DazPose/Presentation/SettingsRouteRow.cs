using System;
using System.Linq;
using DazPose.Game;
using DazPose.Toys;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation {
 public sealed class SettingsRouteRow : MonoBehaviour {
  [SerializeField] Text label;
  [SerializeField] Toggle enabledRoute,inverted;
  [SerializeField] InputField channel;
  [SerializeField] Slider minimum,maximum;
  [SerializeField] Button test;
  public void Bind(Text title,Toggle enable,Toggle invert,InputField mapping,Slider low,Slider high,Button output) {
   label=title; enabledRoute=enable; inverted=invert; channel=mapping; minimum=low; maximum=high; test=output;
  }
  public void Configure(GameSettingsService settings,ToyDevice device,ToyFeature feature,ToyOutputRange output) {
   string signature=ToyControlService.DeviceSignature(device);
   var route=settings.Values.routes.FirstOrDefault(r=>r.deviceName==device.Name && r.signature==signature && r.featureIndex==feature.FeatureIndex && r.capability==output.Capability);
   if(route==null) route=new ToyRouteSettings { deviceName=device.Name,signature=signature,featureIndex=feature.FeatureIndex,capability=output.Capability,channel=(int)feature.FeatureIndex };
   var selected=route;
   label.text=device.DisplayName+" · "+feature.Description+" · "+output.Capability;
   enabledRoute.SetIsOnWithoutNotify(route.enabled); inverted.SetIsOnWithoutNotify(route.inverted);
   channel.SetTextWithoutNotify(route.channel.ToString());
   minimum.SetValueWithoutNotify(route.minimum); maximum.SetValueWithoutNotify(route.maximum);
   void Save() {
    selected.enabled=enabledRoute.isOn; selected.inverted=inverted.isOn;
    if(int.TryParse(channel.text,out int value)) selected.channel=Mathf.Max(0,value);
    selected.minimum=Mathf.Min(minimum.value,maximum.value); selected.maximum=Mathf.Max(minimum.value,maximum.value);
    selected.sessionDeviceIndex=device.DeviceIndex;
    if(!settings.Values.routes.Contains(selected)) settings.Values.routes=settings.Values.routes.Concat(new[]{selected}).ToArray();
    settings.SaveSettings();
   }
   enabledRoute.onValueChanged.AddListener(_=>Save()); inverted.onValueChanged.AddListener(_=>Save());
   channel.onEndEdit.AddListener(_=>Save()); minimum.onValueChanged.AddListener(_=>Save()); maximum.onValueChanged.AddListener(_=>Save());
   test.onClick.AddListener(()=>settings.TestOutput(selected));
  }
 }
}