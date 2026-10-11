using UnityEngine;
namespace DazPose.Performer {
 public abstract class PerformanceSequence : MonoBehaviour {
  public abstract Awaitable ExecuteAsync(PerformanceRun run);
 }
}