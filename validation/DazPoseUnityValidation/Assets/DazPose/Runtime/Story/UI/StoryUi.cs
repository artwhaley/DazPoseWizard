using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Story {
 // Uses the production Settings palette and the already installed uGUI package.
 public static class StoryUi {
  public static RectTransform Rect(string name,Transform parent) {
   var go=new GameObject(name,typeof(RectTransform)); var r=(RectTransform)go.transform;
   r.SetParent(parent,false); return r;
  }
  public static void Stretch(RectTransform rect,float left=0,float bottom=0,float right=0,float top=0) {
   rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
   rect.offsetMin=new Vector2(left,bottom); rect.offsetMax=new Vector2(-right,-top); rect.localScale=Vector3.one;
  }
  public static Image Background(RectTransform rect,Color color) { var image=rect.gameObject.AddComponent<Image>(); image.color=color; return image; }
  public static Text Label(Transform parent,string value,int size=20) {
   var r=Rect("Text",parent); var text=r.gameObject.AddComponent<Text>();
   text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=size;
   text.color=new Color(.92f,.93f,.97f); text.text=value; text.alignment=TextAnchor.MiddleLeft;
   text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Overflow;
   text.raycastTarget=false;
   var layout=r.gameObject.AddComponent<LayoutElement>(); layout.minHeight=size*2+12; layout.flexibleWidth=1;
   return text;
  }
  public static Button Button(Transform parent,string title,UnityEngine.Events.UnityAction action) {
   var r=Rect(title,parent); var bg=Background(r,new Color(.19f,.22f,.31f));
   r.gameObject.AddComponent<LayoutElement>().preferredHeight=46;
   var button=r.gameObject.AddComponent<Button>(); button.targetGraphic=bg; button.onClick.AddListener(action);
   var label=Label(r,title); Stretch(label.rectTransform,8,0,8,0); label.alignment=TextAnchor.MiddleCenter;
   return button;
  }
  public static Toggle Toggle(Transform parent,string title,bool value,UnityEngine.Events.UnityAction<bool> changed) {
   var r=Rect(title,parent); r.gameObject.AddComponent<LayoutElement>().preferredHeight=40;
   var box=Rect("Box",r); box.anchorMin=box.anchorMax=new Vector2(0,.5f); box.pivot=new Vector2(0,.5f); box.sizeDelta=new Vector2(28,28);
   var bg=Background(box,new Color(.15f,.18f,.26f));
   var check=Rect("Check",box); Stretch(check,5,5,5,5);
   var mark=Background(check,new Color(.55f,.65f,1));
   var toggle=r.gameObject.AddComponent<Toggle>(); toggle.targetGraphic=bg; toggle.graphic=mark;
   var label=Label(r,title,18); Stretch(label.rectTransform,44,0,0,0);
   toggle.SetIsOnWithoutNotify(value); toggle.onValueChanged.AddListener(changed); return toggle;
  }
  public static RectTransform Column(Transform parent,string name) {
   var r=Rect(name,parent); var group=r.gameObject.AddComponent<VerticalLayoutGroup>();
   group.spacing=10; group.childControlWidth=true; group.childForceExpandWidth=true;
   group.childControlHeight=true; group.childForceExpandHeight=false; return r;
  }
 }
}