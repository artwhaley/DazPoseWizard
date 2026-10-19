using System;
using DazPose.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation
{
    public sealed class EquipmentRowView : MonoBehaviour
    {
        public TMP_Text nameText, detailText;
        public Toggle owned, reward, punishment;
        public void Bind(EquipmentDefinition definition,EquipmentPreference value,Action<bool> setOwned,Action<bool> setReward,Action<bool> setPunishment)
        {
            var detail=GetComponent<CatalogDetailView>()??gameObject.AddComponent<CatalogDetailView>();
            detail.Details=definition.displayName+" ("+definition.id+")\n"+definition.description;
            nameText.text=definition.displayName;detailText.text=string.Join(" · ",definition.identityTags??Array.Empty<string>());
            BindToggle(owned,value.ownedAndWilling,setOwned);
            BindToggle(reward,value.useAsReward,setReward);BindToggle(punishment,value.useAsPunishment,setPunishment);
        }
        static void BindToggle(Toggle toggle,bool value,Action<bool> changed)
        {
            toggle.onValueChanged.RemoveAllListeners();toggle.SetIsOnWithoutNotify(value);toggle.onValueChanged.AddListener(v=>changed(v));
        }
    }
}
