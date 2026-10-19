using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Story {
 [DisallowMultipleComponent]
 public sealed partial class StoryPromptService : MonoBehaviour {
  AwaitableCompletionSource<StoryPromptResult> pending;
  GameObject modal;
  IStoryModalView view;
  Text checkpoint;
  public bool IsOpen => pending!=null;
  public static StoryPromptService Create(Transform parent,string name="Story prompts") {
   var rect=StoryUi.Rect(name,parent);
   var canvas=rect.gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=200;
   var scaler=rect.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
   scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=1;
   rect.gameObject.AddComponent<GraphicRaycaster>();
   return rect.gameObject.AddComponent<StoryPromptService>();
  }
  async Awaitable<StoryPromptResult> Show(string title,string confirm,Action<Transform> build,Action commit,CancellationToken cancellation,bool allowCancel=false) {
   cancellation.ThrowIfCancellationRequested();
   if(IsOpen) throw new InvalidOperationException("A story prompt is already active.");
   var source=new AwaitableCompletionSource<StoryPromptResult>(); pending=source;
   try {
    var factory=StoryModalPresentation.Factory??throw new InvalidOperationException("The shared production UI provider is unavailable.");
    view=factory.Create(transform,title,confirm,allowCancel);
    modal=view.Root;build(view.Body);
    view.Accepted+=()=>{
     if(pending!=source || cancellation.IsCancellationRequested)return;
     try {view.SetBusy(true);commit?.Invoke();Close(source);source.TrySetResult(new StoryPromptResult());}
     catch(Exception failure) {view.SetBusy(false);view.SetError(failure.Message);Debug.LogError("Could not apply choices: "+failure.Message);}
    };
    view.Cancelled+=()=>{if(pending==source){Close(source);source.TrySetResult(new StoryPromptResult("cancelled"));}};
    using(var registration=cancellation.Register(()=>Cancel(source))) { return await source.Awaitable; }
   } finally { Close(source); }
  }
  void Close(AwaitableCompletionSource<StoryPromptResult> source) {
   if(pending!=source) return;
   pending=null;
   view?.Dispose();view=null;modal=null;
  }
  void Cancel(AwaitableCompletionSource<StoryPromptResult> source) {
   if(source==null || pending!=source) return;
   Close(source); source.TrySetException(new OperationCanceledException("Story prompt cancelled."));
  }
  void OnDisable() { Cancel(pending);ClearActions(); }
  public Awaitable<StoryPromptResult> ShowMessageAsync(string title,string body,string confirmLabel="Continue",CancellationToken cancellation=default) =>
   Show(title,confirmLabel,parent=>StoryModalPresentation.Factory.Message(parent,body),null,cancellation);
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