using UnityEngine;
namespace DazPose.Game {
 [DisallowMultipleComponent]
 public sealed class SceneActorIdentity : MonoBehaviour {
  [SerializeField] string actorId;
  public string ActorId => actorId;
  public void Configure(string id) => actorId=id;
 }
}