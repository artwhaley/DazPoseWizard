using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DazPose.Story {
 public sealed partial class StoryPromptService {
  sealed class SessionAction {public long Owner;public GameObject Object;public Button Button;public bool Pressed;}
  readonly Dictionary<string,SessionAction> actions=new Dictionary<string,SessionAction>(StringComparer.Ordinal);
  int actionScope;
  public int ActionScope=>actionScope;
  public bool ActionWasPressed(string id)=>actions.TryGetValue(id,out var action) && action.Pressed;
  public bool HasAction(string id)=>actions.TryGetValue(id,out var action) && action.Object!=null;
  public void BeginActionScope(int scope) {ClearActions();actionScope=scope;}
  sealed class ActionLease : IDisposable {Action release;public ActionLease(Action release){this.release=release;}public void Dispose(){var action=release;release=null;action?.Invoke();}}
  public void ShowAction(string id,string label,Action pressed) {ShowOwnedAction(id,label,pressed,0);}
  public IDisposable ShowOwnedAction(string id,string label,Action pressed,long owner) {
   if(string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label))throw new ArgumentException("An action ID and label are required.");
   if(actions.TryGetValue(id,out var old)) {if(old.Owner!=owner)throw new InvalidOperationException("Action "+id+" belongs to an ancestor card/phase; use a distinct action ID.");if(old.Pressed || old.Object!=null)return new ActionLease(()=>RemoveOwnedAction(id,old));}
   var state=old ?? new SessionAction {Owner=owner};actions[id]=state;int scope=actionScope;
   var panel=StoryUi.Rect("Session action "+id,transform);panel.anchorMin=new Vector2(1,0);panel.anchorMax=new Vector2(1,0);panel.pivot=new Vector2(1,0);panel.sizeDelta=new Vector2(220,70);panel.anchoredPosition=new Vector2(-24,80);
   var layout=panel.gameObject.AddComponent<VerticalLayoutGroup>();layout.childForceExpandHeight=true;state.Object=panel.gameObject;
   state.Button=StoryUi.Button(panel,label,()=> {
    if(scope!=actionScope || state.Pressed || state.Object==null)return;
    state.Pressed=true;state.Button.interactable=false;pressed?.Invoke();
   });
   return new ActionLease(()=>RemoveOwnedAction(id,state));
  }
  void RemoveOwnedAction(string id,SessionAction state) {if(!actions.TryGetValue(id,out var current)||!ReferenceEquals(state,current))return;HideAction(id);actions.Remove(id);}
  public void HideOwnedAction(string id,long owner) {if(actions.TryGetValue(id,out var state)){if(state.Owner!=owner)throw new InvalidOperationException("Action "+id+" belongs to an ancestor card/phase.");RemoveOwnedAction(id,state);}}
  public void HideAction(string id) {if(!actions.TryGetValue(id,out var action))return;if(action.Object!=null) {action.Object.SetActive(false);Destroy(action.Object);}action.Object=null;action.Button=null;}
  public void ClearActions() {foreach(string id in new List<string>(actions.Keys))HideAction(id);actions.Clear();}
 }
}
