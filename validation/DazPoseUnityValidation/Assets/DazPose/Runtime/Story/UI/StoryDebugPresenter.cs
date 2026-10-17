using UnityEngine;
namespace DazPose.Story {
 [DisallowMultipleComponent]
 public sealed class StoryDebugPresenter : MonoBehaviour {
  [SerializeField] bool visible;
  StoryRuntimeSnapshot snapshot;
  public void Observe(StoryRuntimeSnapshot value) { snapshot=value; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  void Update() { if(Input.GetKeyDown(KeyCode.F8)) visible=!visible; }
  void OnGUI() {
   if(GUI.Button(new Rect(Screen.width-180,66,170,28),"Story debug (F8)")) visible=!visible;
   if(!visible || snapshot==null) return;
   var s=snapshot;
   var text="Story: "+s.StoryId+"\nStep: "+s.StepId+" / "+s.Opcode
    +"\nPhase: "+s.Phase+" · completion "+s.PhaseCompletion
    +"\nMood: "+s.Mood+" · intensity "+s.Intensity+"\nSeed: "+s.Seed
    +"\nPrepared: "+s.PreparedEquipment+"\nActive: "+s.ActiveEquipment
    +"\nClothing ("+s.ClothingCount+"): "+s.Clothing+"\nLast event: "+s.LastEvent+"\nCheckpoint: "+s.Checkpoint;
   GUI.Box(new Rect(Screen.width-460,100,450,340),GUIContent.none);
   GUI.Label(new Rect(Screen.width-448,112,426,316),text);
  }
#endif
 }
}