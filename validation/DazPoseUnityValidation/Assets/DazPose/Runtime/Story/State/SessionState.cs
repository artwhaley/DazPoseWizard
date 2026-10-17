using System;
namespace DazPose.Story {
 [Serializable] public sealed class SessionState {
  public string phase="Setup";
  public float phaseCompletion=0, mood=50, intensityMultiplier=1;
  public string[] activeEquipmentIds=Array.Empty<string>();
  public PlayerWorldState world=new PlayerWorldState();
 }
}