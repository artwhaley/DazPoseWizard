using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation
{
    public sealed class LabeledToggleRowView : MonoBehaviour
    {
        public TMP_Text label;
        public Toggle choice;
        public void Bind(string text,bool value,Action<bool> changed)
        {
            label.text=text;choice.onValueChanged.RemoveAllListeners();choice.SetIsOnWithoutNotify(value);
            choice.onValueChanged.AddListener(v=>changed(v));
        }
    }
}
