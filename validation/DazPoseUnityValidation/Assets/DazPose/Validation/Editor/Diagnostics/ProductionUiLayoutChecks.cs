using System;
using System.Linq;
using DazPose.Game;
using DazPose.Presentation;
using DazPose.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace DazPose.Validation.Editor {
 public static class ProductionUiLayoutChecks {
  [MenuItem("DazPose/Validation/UI/Check disposable 100 and 300 item layouts")]
  public static void Run() {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Run UI layout checks in Edit Mode.");
   var library=ProductionUiLibrary.Load();int checkedPanels=0;
   foreach(int count in new[]{100,300})foreach(var size in new[]{new Vector2(1280,720),new Vector2(1920,1080),new Vector2(2560,1440),new Vector2(1280,800)})
   foreach(bool interests in new[]{true,false}) {
    var scene=EditorSceneManager.NewPreviewScene();GameObject canvasObject=null;
    try {
     canvasObject=new GameObject("Disposable catalog layout check",typeof(RectTransform),typeof(Canvas));
     canvasObject.hideFlags=HideFlags.HideAndDontSave;SceneManager.MoveGameObjectToScene(canvasObject,scene);
     var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
     var rect=(RectTransform)canvasObject.transform;rect.sizeDelta=new Vector2(1920,size.y*1920/size.x);
     var modal=UnityEngine.Object.Instantiate(library.modalShell,rect,false);library.Prepare(modal.gameObject);
     modal.Initialize("Synthetic catalog","Apply",true);modal.gameObject.SetActive(true);
     using(var draft=new PlayerProfileEditSession(new PlayerProfileService(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"DazPose-readonly-fixture-"+Guid.NewGuid().ToString("N"))))) {
      if(interests)ProfileEditorPresenter.Interests(modal.Body,draft,new KinkCatalog{entries=Enumerable.Range(0,count).Select(i=>new KinkDefinition{id="synthetic-"+i,displayName="A long synthetic interest name "+i,description="Generated test data; never a persistent catalog.",themeTags=new[]{"theme:fixture"}}).ToArray()});
      else ProfileEditorPresenter.Equipment(modal.Body,draft,new EquipmentCatalog{entries=Enumerable.Range(0,count).Select(i=>new EquipmentDefinition{id="synthetic-"+i,displayName="A long synthetic equipment name "+i,identityTags=new[]{"equipment:fixture"}}).ToArray()});
      Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
      var panel=modal.GetComponentInChildren<ProfileCatalogPanelView>(true);panel.list.RefreshBindings();
      Canvas.ForceUpdateCanvases();
      if(panel.list.LiveRowCount>Mathf.CeilToInt(panel.list.scroll.viewport.rect.height/panel.list.rowHeight)+2)throw new InvalidOperationException("List created more than its viewport row budget.");
      if(panel.list.LiveRowCount==0)throw new InvalidOperationException("The synthetic catalog produced no visible rows.");
      if(panel.list.content.rect.height<count*panel.list.rowHeight-.5f)throw new InvalidOperationException("Scroll content does not represent the entire catalog.");
      var clip=panel.list.scroll.viewport;var corners=new Vector3[4];modal.applyButton.GetComponent<RectTransform>().GetWorldCorners(corners);
      if(corners.Any(c=>clip.rect.Contains(clip.InverseTransformPoint(c))))throw new InvalidOperationException("Apply footer overlaps the list viewport.");
      panel.search.SetTextWithoutNotify("no matching synthetic item");panel.search.onValueChanged.Invoke(panel.search.text);
      if(panel.list.content.rect.height>.5f)throw new InvalidOperationException("Search with no matches retained scroll items.");
      if(draft.IsCommitted)throw new InvalidOperationException("Layout inspection committed a profile.");
      checkedPanels++;
     }
    } finally {if(canvasObject!=null)UnityEngine.Object.DestroyImmediate(canvasObject);EditorSceneManager.ClosePreviewScene(scene);}
   }
   Debug.Log("Shared catalog layout checks passed for "+checkedPanels+" disposable panels. No profile/catalog/source scene was saved. Rendered keyboard and visual review remain manual.");
  }
 }
}
