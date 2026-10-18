using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace DazPose.Editor {
  public static class AuthoringPointerCheck {
  static int stage; static double deadline,next;
  static readonly List<string> details=new List<string>();

  public static void RepairAndPlay() { GameUiAssetBuilder.RepairUiSizing(); Play(); }
  public static void Play() {
   EditorSceneManager.OpenScene("Assets/Production/Scenes/MainMenu.unity");
   SessionState.SetBool("DazPose.PointerCheck",true); deadline=EditorApplication.timeSinceStartup+60;
   EditorApplication.update+=Observe; EditorApplication.isPlaying=true;
  }
  static T Field<T>(UnityEngine.Object o,string name) where T:UnityEngine.Object => new SerializedObject(o).FindProperty(name).objectReferenceValue as T;
  public static bool Click(Button button) {
   Canvas.ForceUpdateCanvases();
   var rect=(RectTransform)button.transform;
   var corners=new Vector3[4]; rect.GetWorldCorners(corners);
   if((corners[2]-corners[0]).sqrMagnitude<100) throw new Exception(button.name+" has no visible screen area: "+rect.lossyScale);
   var center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
   details.Add(button.name+" screen="+center+" screenSize="+Screen.width+"/"+Screen.height);
   if(center.x<0 || center.y<0 || center.x>Screen.width || center.y>Screen.height) throw new Exception(button.name+" is off-screen");
   var pointer=new PointerEventData(EventSystem.current) { position=center,button=PointerEventData.InputButton.Left };
   var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
   details.Add("hits="+string.Join(",",hits.Select(h=>h.gameObject.name)));
   if(hits.Count==0) throw new Exception("No raycast hit for "+button.name);
   var receiver=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
   if(receiver!=button.gameObject) throw new Exception(button.name+" intercepted by "+(receiver!=null?receiver.name:hits[0].gameObject.name));
   ExecuteEvents.Execute(receiver,pointer,ExecuteEvents.pointerDownHandler);
   ExecuteEvents.Execute(receiver,pointer,ExecuteEvents.pointerUpHandler);
   ExecuteEvents.Execute(receiver,pointer,ExecuteEvents.pointerClickHandler);
   return true;
  }
  static void Observe() {
   if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next) return;
   try {
    if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Pointer check timed out");
    var roots=SceneManager.GetActiveScene().GetRootGameObjects();
    var menu=roots.SelectMany(r=>r.GetComponentsInChildren<MainMenuController>()).SingleOrDefault();
    if(menu==null) return;
    if(stage==0) { Click(Field<Button>(menu,"settingsButton")); stage=1; next=EditorApplication.timeSinceStartup+.5; }
    else {
     var settings=Field<SettingsMenuController>(menu,"settings");
     if(!settings.gameObject.activeInHierarchy) throw new Exception("Settings did not activate");
     Click(Field<Button>(settings,"close"));
     if(settings.gameObject.activeInHierarchy) throw new Exception("Close did not deactivate Settings");
     Finish(true,"Settings and Close were on-screen, unobstructed and accepted raycast-resolved pointer events.");
    }
   } catch(Exception e) { Finish(false,e.ToString()); }
  }
  static void Finish(bool pass,string reason) {
   SessionState.SetBool("DazPose.PointerCheck",false); EditorApplication.update-=Observe;
   Directory.CreateDirectory("TestOutput/Authoring");
   File.WriteAllText("TestOutput/Authoring/Pointer.txt",pass+"\n"+reason+"\n"+string.Join("\n",details));
   Debug.Log("AUTHORING_POINTER: "+pass+"; "+reason+"; "+string.Join("; ",details));
   EditorApplication.Exit(pass?0:1);
  }
 }
}