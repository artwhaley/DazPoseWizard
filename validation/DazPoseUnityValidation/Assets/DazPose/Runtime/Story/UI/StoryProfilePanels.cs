using System;
using System.Linq;
using DazPose.Game;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Story {
 public static class StoryInterestsPanel {
  public static void Build(Transform parent,PlayerProfile draft,KinkCatalog catalog) {
   StoryUi.Label(parent,"Choose frequency (NO / 1x / 2x / 4x) and sentiment. NO disables future selection.",18);
   foreach(var definition in catalog.entries) {
    var preference=draft.kinks.SingleOrDefault(p=>p.id==definition.id);
    if(preference==null) { preference=new KinkPreference { id=definition.id }; draft.kinks=draft.kinks.Concat(new[]{preference}).ToArray(); }
    var row=StoryUi.Column(parent,definition.id); row.gameObject.AddComponent<LayoutElement>().preferredHeight=120;
    StoryUi.Label(row,definition.displayName,20);
    var choices=StoryUi.Rect("Choices",row); choices.gameObject.AddComponent<LayoutElement>().preferredHeight=46;
    var layout=choices.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing=16; layout.childForceExpandWidth=true;
    var frequencies=new[]{KinkFrequency.No,KinkFrequency.Normal,KinkFrequency.Frequent,KinkFrequency.VeryFrequent};
    var names=new[]{"NO","1x","2x","4x"};
    Button frequency=null;
    frequency=StoryUi.Button(choices,"Frequency: "+names[Array.IndexOf(frequencies,preference.frequency)],()=>{
     int index=(Array.IndexOf(frequencies,preference.frequency)+1)%frequencies.Length;
     preference.frequency=frequencies[index]; frequency.GetComponentInChildren<Text>().text="Frequency: "+names[index];
    });
    var sentiments=new[]{KinkSentiment.Loved,KinkSentiment.Neutral,KinkSentiment.Punishment};
    var labels=new[]{"🥳 Loved","🙂 Neutral","😨 Punishment"};
    Button sentiment=null;
    sentiment=StoryUi.Button(choices,labels[Array.IndexOf(sentiments,preference.sentiment)],()=>{
     int index=(Array.IndexOf(sentiments,preference.sentiment)+1)%sentiments.Length;
     preference.sentiment=sentiments[index]; sentiment.GetComponentInChildren<Text>().text=labels[index];
    });
   }
  }
 }
 public static class StoryEquipmentPanel {
  public static void Build(Transform parent,PlayerProfile draft,EquipmentCatalog catalog) {
   StoryUi.Label(parent,"Only owned / willing items can be prepared. Reward and punishment can both be enabled.",18);
   foreach(var definition in catalog.entries) {
    var preference=draft.equipment.SingleOrDefault(p=>p.id==definition.id);
    if(preference==null) { preference=new EquipmentPreference { id=definition.id }; draft.equipment=draft.equipment.Concat(new[]{preference}).ToArray(); }
    var row=StoryUi.Column(parent,definition.id); row.gameObject.AddComponent<LayoutElement>().preferredHeight=220;
    StoryUi.Label(row,definition.displayName,20);
    StoryUi.Toggle(row,"Owned / willing to use",preference.ownedAndWilling,v=>preference.ownedAndWilling=v);
    StoryUi.Toggle(row,"Use as reward",preference.useAsReward,v=>preference.useAsReward=v);
    StoryUi.Toggle(row,"Use as punishment",preference.useAsPunishment,v=>preference.useAsPunishment=v);
   }
  }
 }
 public static class StoryClothingPanel {
  public static void Build(Transform parent,PlayerWorldState draft) {
   StoryUi.Label(parent,"What are you wearing right now?",22);
   foreach(var slot in PlayerWorldState.ClothingSlots) StoryUi.Toggle(parent,slot,draft.GetClothing(slot),v=>draft.SetClothing(slot,v));
  }
 }
}