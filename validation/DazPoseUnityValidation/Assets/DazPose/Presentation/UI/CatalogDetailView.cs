using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace DazPose.Presentation {
 public sealed class CatalogDetailView : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler {
  public string Details;GameObject popup;
  public void OnPointerEnter(PointerEventData e)=>Show();
  public void OnSelect(BaseEventData e)=>Show();
  public void OnPointerExit(PointerEventData e)=>Hide();
  public void OnDeselect(BaseEventData e)=>Hide();
  void OnDisable()=>Hide();
  void Show(){
   Hide();if(string.IsNullOrWhiteSpace(Details))return;
   var library=ProductionUiLibrary.Load();popup=Instantiate(library.choiceButton,GetComponentInParent<Canvas>().transform,false);
   library.Prepare(popup);popup.GetComponent<UnityEngine.UI.Button>().enabled=false;
   var text=popup.GetComponentInChildren<TMP_Text>(true);text.text=Details;text.fontSize=18;text.raycastTarget=false;
   foreach(var graphic in popup.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))graphic.raycastTarget=false;
   var rect=(RectTransform)popup.transform;rect.anchorMin=new Vector2(.12f,.1f);rect.anchorMax=new Vector2(.88f,.2f);rect.offsetMin=rect.offsetMax=Vector2.zero;
   popup.SetActive(true);
  }
  void Hide(){if(popup!=null){popup.SetActive(false);Destroy(popup);}popup=null;}
 }
}
