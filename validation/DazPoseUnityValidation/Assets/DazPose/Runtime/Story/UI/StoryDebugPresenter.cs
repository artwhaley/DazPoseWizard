using UnityEngine;
namespace DazPose.Story {
 [DisallowMultipleComponent] public sealed class StoryDebugPresenter : MonoBehaviour {
  [SerializeField] bool visible;StoryRuntimeSnapshot snapshot;Vector2 scroll;
  public void Observe(StoryRuntimeSnapshot value) {snapshot=value;}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  void Update() {if(Input.GetKeyDown(KeyCode.F8))visible=!visible;}
  void OnGUI() {
   if(GUI.Button(new Rect(Screen.width-180,66,170,28),"Story debug (F8)"))visible=!visible;
   if(!visible || snapshot==null)return;
   var s=snapshot;
   string text="Story: "+s.StoryId+"\nStep: "+s.StepId+" / "+s.Opcode+"\nPhase: "+s.Phase+"; completion "+s.PhaseCompletion
    +"\nPacing: "+s.Pacing+"\n"+s.World+"\nMood: "+s.Mood+"; intensity "+s.Intensity+"\nSeed: "+s.Seed
    +"\nPrepared: "+s.PreparedEquipment+"\nActive: "+s.ActiveEquipment+"\nClothing ("+s.ClothingCount+"): "+s.Clothing
    +"\nLast event: "+s.LastEvent+"\nCheckpoint: "+s.Checkpoint+"\nCard: "+s.Card?.CardId+" / invocation "+s.Card?.InvocationNumber+"\n"+s.Director;
   float width=Mathf.Min(660,Screen.width-30),height=Screen.height-125;
   GUILayout.BeginArea(new Rect(Screen.width-width-10,100,width,height),GUI.skin.box);
   scroll=GUILayout.BeginScrollView(scroll);GUILayout.Label(text);GUILayout.EndScrollView();GUILayout.EndArea();
  }
#endif
 }
}
