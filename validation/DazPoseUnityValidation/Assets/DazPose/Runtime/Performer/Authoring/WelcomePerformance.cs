using UnityEngine;
namespace DazPose.Performer {
 public sealed class WelcomePerformance : PerformanceSequence {
  [SerializeField] PerformerPose pose;
  public PerformerPose Pose => pose;
  public void Configure(PerformerPose value) => pose=value;
  public override async Awaitable ExecuteAsync(PerformanceRun run) { await run.Performer.PoseAsync(pose); }
 }
}