using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation
{
    public sealed class SceneCardView : MonoBehaviour
    {
        public TMP_Text titleText, descriptionText;
        public Button selectButton;
        public GameObject selectedMark;
        public void Bind(string title,string description,bool selected,Action select)
        {
            titleText.text=title;descriptionText.text=description;
            if(selectedMark!=null)selectedMark.SetActive(selected);
            selectButton.onClick.RemoveAllListeners();selectButton.onClick.AddListener(()=>select());
        }
    }
}
