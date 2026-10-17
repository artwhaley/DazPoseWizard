using System;
namespace DazPose.Story {
 public sealed class StoryProgramException : Exception {
  public string StoryId { get; }
  public string StepId { get; }
  public string Opcode { get; }
  public StoryProgramException(string story, string step, string opcode, string reason, Exception inner = null)
   : base($"Story '{story ?? "?"}', step '{step ?? "?"}' ({opcode ?? "?"}): {reason}", inner)
   { StoryId = story; StepId = step; Opcode = opcode; }
 }
}