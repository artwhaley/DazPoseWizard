using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Presentation
{
    // Passive shared view; presenters own search/filter/draft behavior.
    public sealed class ProfileCatalogPanelView : MonoBehaviour
    {
        public TMP_InputField search;
        public TMP_Text summary, filterLabel;
        public Button filterButton;
        public GameObject filterPopup;
        public RecycledListView list, filterList;
    }
}
