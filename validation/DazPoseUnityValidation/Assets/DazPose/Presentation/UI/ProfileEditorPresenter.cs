using System;
using System.Linq;
using DazPose.Game;
using DazPose.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation
{
    public static class ProfileEditorPresenter
    {
        static ProfileCatalogPanelView Panel(Transform body)
        {
            var library=ProductionUiLibrary.Load();
            var rect=UnityEngine.Object.Instantiate(library.profilePanel,body,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            library.Prepare(rect.gameObject);rect.gameObject.SetActive(true);
            var view=rect.GetComponent<ProfileCatalogPanelView>();
            view.filterPopup.SetActive(false);
            view.filterButton.onClick.AddListener(()=>view.filterPopup.SetActive(!view.filterPopup.activeSelf));
            return view;
        }
        static string Category(string category,string[] tags)=>!string.IsNullOrWhiteSpace(category)?category:(tags??Array.Empty<string>()).FirstOrDefault()??"Other";
        static string Text(string id,string name,string description,params string[][] tags)=>
            id+" "+name+" "+description+" "+string.Join(" ",tags.SelectMany(t=>t??Array.Empty<string>()));
        static void Filters(ProfileCatalogPanelView view,string[] choices,Action<string> selected)
        {
            view.filterList.rowHeight=44;
            view.filterList.SetItems(choices.Length,ProductionUiLibrary.Load().choiceButton,(i,row)=>{
                string choice=choices[i];row.GetComponentInChildren<TMP_Text>(true).text=choice;
                var button=row.GetComponent<Button>();button.onClick.RemoveAllListeners();
                button.onClick.AddListener(()=>{view.filterLabel.text=choice;view.filterPopup.SetActive(false);selected(choice);});
            });
        }
        public static void Interests(Transform body,IProfileEditDraft draft,KinkCatalog catalog)
        {
            var view=Panel(body);var entries=catalog.entries??Array.Empty<KinkDefinition>();
            string filter="All";
            Action<bool> refresh=null;
            refresh=resetScroll=>{
                var visible=CatalogSearch.Find(entries,view.search.text,
                    d=>Text(d.id,d.displayName,d.description,d.themeTags,new[]{Category(d.displayCategory,d.themeTags)}),
                    d=>d.displayName,d=>d.id,d=>filter=="All" ||
                        (filter=="Enabled"&&draft.GetKink(d.id).frequency!=KinkFrequency.No) ||
                        (filter=="Disabled"&&draft.GetKink(d.id).frequency==KinkFrequency.No) ||
                        filter=="Category: "+Category(d.displayCategory,d.themeTags));
                view.summary.text=visible.Length+" of "+entries.Length+" interests · NO disables selection; sentiment is retained.";
                view.list.SetCatalogItems(visible.Select(i=>entries[i].id).ToArray(),ProductionUiLibrary.Load().interestRow,(i,row)=>{
                    var definition=entries[visible[i]];
                    row.GetComponent<InterestRowView>().Bind(definition,draft.GetKink(definition.id),
                        value=>{draft.SetFrequency(definition.id,value);refresh(false);},
                        value=>draft.SetSentiment(definition.id,value));
                },resetScroll);
            };
            Filters(view,new[]{"All","Enabled","Disabled"}.Concat(entries.Select(d=>"Category: "+Category(d.displayCategory,d.themeTags)).Distinct().OrderBy(x=>x,StringComparer.Ordinal)).ToArray(),
                value=>{filter=value;refresh(true);});
            view.search.onValueChanged.AddListener(_=>refresh());refresh(false);
        }
        public static void Equipment(Transform body,IProfileEditDraft draft,EquipmentCatalog catalog)
        {
            var view=Panel(body);var entries=catalog.entries??Array.Empty<EquipmentDefinition>();string filter="All";
            Action<bool> refresh=null;
            refresh=resetScroll=>{
                var visible=CatalogSearch.Find(entries,view.search.text,
                    d=>Text(d.id,d.displayName,d.description,d.identityTags,d.themeTags,new[]{Category(d.displayCategory,d.themeTags)}),
                    d=>d.displayName,d=>d.id,d=>{
                        var p=draft.GetEquipment(d.id);
                        return filter=="All" || filter=="Owned"&&p.ownedAndWilling || filter=="Unowned"&&!p.ownedAndWilling ||
                            filter=="Reward"&&p.useAsReward || filter=="Punishment"&&p.useAsPunishment ||
                            filter=="Category: "+Category(d.displayCategory,d.themeTags);
                    });
                view.summary.text=visible.Length+" of "+entries.Length+" items · Reward and punishment are independent.";
                view.list.SetCatalogItems(visible.Select(i=>entries[i].id).ToArray(),ProductionUiLibrary.Load().equipmentRow,(i,row)=>{
                    var definition=entries[visible[i]];
                    row.GetComponent<EquipmentRowView>().Bind(definition,draft.GetEquipment(definition.id),
                        value=>{draft.SetEquipmentOwnedAndWilling(definition.id,value);refresh(false);},
                        value=>{draft.SetReward(definition.id,value);refresh(false);},
                        value=>{draft.SetPunishment(definition.id,value);refresh(false);});
                },resetScroll);
            };
            Filters(view,new[]{"All","Owned","Unowned","Reward","Punishment"}.Concat(entries.Select(d=>"Category: "+Category(d.displayCategory,d.themeTags)).Distinct().OrderBy(x=>x,StringComparer.Ordinal)).ToArray(),
                value=>{filter=value;refresh(true);});
            view.search.onValueChanged.AddListener(_=>refresh());refresh(false);
        }
        public static void Clothing(Transform body,ClothingEditSession draft)
        {
            var view=Panel(body);view.search.gameObject.SetActive(false);view.filterButton.gameObject.SetActive(false);
            view.summary.text="What are you wearing right now?";
            view.list.SetItems(draft.Slots.Count,ProductionUiLibrary.Load().clothingRow,(i,row)=>{
                string slot=draft.Slots[i];row.GetComponent<LabeledToggleRowView>().Bind(slot,draft.GetClothing(slot),v=>draft.SetClothing(slot,v));
            });
        }
    }
}
