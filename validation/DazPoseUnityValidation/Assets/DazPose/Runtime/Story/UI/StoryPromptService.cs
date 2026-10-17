using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Story {
 [DisallowMultipleComponent]
 public sealed partial class StoryPromptService : MonoBehaviour {
  AwaitableCompletionSource<StoryPromptResult> pending;
  GameObject modal;
  Text checkpoint;
  public bool IsOpen => pending!=null;
  public static StoryPromptService Create(Transform parent,string name="Story prompts") {
   var rect=StoryUi.Rect(name,parent);
   var canvas=rect.gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=200;
   var scaler=rect.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
   scaler.referenceResolution=new Vector2(1280,800); scaler.matchWidthOrHeight=1;
   rect.gameObject.AddComponent<GraphicRaycaster>();
   return rect.gameObject.AddComponent<StoryPromptService>();
  }
  async Awaitable<StoryPromptResult> Show(string title,string confirm,Action<Transform> build,Action commit,CancellationToken cancellation) {
   cancellation.ThrowIfCancellationRequested();
   if(IsOpen) throw new InvalidOperationException("A story prompt is already active.");
   var source=new AwaitableCompletionSource<StoryPromptResult>(); pending=source;
   try {
    var panel=StoryUi.Rect("Modal",transform); modal=panel.gameObject;
    // Keep the saved session control strip reachable so Stop can always cancel a prompt.
    StoryUi.Stretch(panel,0,0,0,64); StoryUi.Background(panel,new Color(.055f,.065f,.095f,.99f));
    var heading=StoryUi.Label(panel,title,30); heading.rectTransform.anchorMin=new Vector2(.08f,1); heading.rectTransform.anchorMax=new Vector2(.92f,1);
    heading.rectTransform.pivot=new Vector2(.5f,1); heading.rectTransform.sizeDelta=new Vector2(0,60); heading.rectTransform.anchoredPosition=new Vector2(0,-16);
    var viewport=StoryUi.Rect("Scroll viewport",panel); StoryUi.Stretch(viewport,90,150,90,90);
    StoryUi.Background(viewport,new Color(.075f,.085f,.12f)); viewport.gameObject.AddComponent<RectMask2D>();
    var content=StoryUi.Column(viewport,"Content"); content.anchorMin=new Vector2(0,1); content.anchorMax=new Vector2(1,1); content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
    var layout=content.GetComponent<VerticalLayoutGroup>(); layout.padding=new RectOffset(20,20,12,12);
    var fitter=content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
    var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.viewport=viewport; scroll.content=content;
    build(content);
    var footer=StoryUi.Column(panel,"Confirm"); footer.anchorMin=new Vector2(.08f,0); footer.anchorMax=new Vector2(.92f,0); footer.pivot=new Vector2(.5f,0); footer.sizeDelta=new Vector2(0,130); footer.anchoredPosition=new Vector2(0,10);
    var error=StoryUi.Label(footer,"",16);
    StoryUi.Button(footer,confirm,()=>{
     if(pending!=source || cancellation.IsCancellationRequested) return;
     try { commit?.Invoke(); Close(source); source.TrySetResult(new StoryPromptResult()); }
     catch(Exception failure) { error.text=failure.Message; Debug.LogError("Story prompt commit failed: "+failure); }
    });
    using(var registration=cancellation.Register(()=>Cancel(source))) { return await source.Awaitable; }
   } finally { Close(source); }
  }
  void Close(AwaitableCompletionSource<StoryPromptResult> source) {
   if(pending!=source) return;
   pending=null;
   if(modal!=null) { modal.SetActive(false); Destroy(modal); modal=null; }
  }
  void Cancel(AwaitableCompletionSource<StoryPromptResult> source) {
   if(source==null || pending!=source) return;
   Close(source); source.TrySetException(new OperationCanceledException("Story prompt cancelled."));
  }
  void OnDisable() { Cancel(pending); }
  public Awaitable<StoryPromptResult> ShowMessageAsync(string title,string body,string confirmLabel="Continue",CancellationToken cancellation=default) =>
   Show(title,confirmLabel,parent=>StoryUi.Label(parent,body,22),null,cancellation);
  public void ShowCheckpoint(string message) {
   if(checkpoint==null) {
    var panel=StoryUi.Rect("Checkpoint",transform); panel.anchorMin=new Vector2(.05f,0); panel.anchorMax=new Vector2(.95f,0); panel.pivot=new Vector2(.5f,0);
    panel.sizeDelta=new Vector2(0,130); panel.anchoredPosition=new Vector2(0,20);
    StoryUi.Background(panel,new Color(.055f,.065f,.095f,.96f));
    checkpoint=StoryUi.Label(panel,message,22); StoryUi.Stretch(checkpoint.rectTransform,20,10,20,10);
   }
   checkpoint.text=message;
  }
 }
}