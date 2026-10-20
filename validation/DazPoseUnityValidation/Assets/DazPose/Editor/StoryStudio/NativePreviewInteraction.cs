using UnityEditor;
using UnityEngine;
namespace DazPose.Editor.Studio {
 [InitializeOnLoad]
 internal static class NativePreviewInteraction {
  static bool dragging;
  public static bool IsDragging=>dragging&&EditorWindow.focusedWindow is SceneView;
  static NativePreviewInteraction(){SceneView.duringSceneGui+=Observe;}
  static void Observe(SceneView view){
   var e=Event.current;if(e==null)return;
   if(e.rawType==EventType.MouseDrag&&e.button==0)dragging=true;
   if(e.rawType==EventType.MouseUp||e.rawType==EventType.Ignore||GUIUtility.hotControl==0&&e.type==EventType.Repaint)dragging=false;
  }
 }
}
