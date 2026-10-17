using System;
using System.Collections.Generic;
using UnityEngine;
namespace DazPose.Story {
 public static class StoryProgramSerializer {
  [Serializable] sealed class Header { public string schema, id; public int version; }
  public static StoryProgramDocument Parse(string json) {
   if (string.IsNullOrWhiteSpace(json)) throw new StoryProgramException(null, null, null, "JSON is blank.");
   StoryProgramDocument document;
   try {
    var header=JsonUtility.FromJson<Header>(json);
    if(header==null || header.schema!="dazpose.story" || header.version!=1)
     throw new StoryProgramException(header?.id,null,null,"Expected explicit schema dazpose.story and version 1.");
    StoryJsonNestingGuard.Validate(json,header.id);
    document = JsonUtility.FromJson<StoryProgramDocument>(json);
   }
   catch (StoryProgramException) { throw; }
   catch (Exception error) { throw new StoryProgramException(null, null, null, "Invalid JSON: " + error.Message, error); }
   Validate(document);
   return document;
  }
  public static string ToJson(StoryProgramDocument document, bool pretty = true)
   { Validate(document); return JsonUtility.ToJson(document, pretty); }
  public static void Validate(StoryProgramDocument document) {
   if (document == null || document.schema != "dazpose.story" || document.version != 1)
    throw new StoryProgramException(document?.id, null, null, "Expected schema dazpose.story version 1.");
   if (string.IsNullOrWhiteSpace(document.id)) throw new StoryProgramException(null, null, null, "Document ID is required.");
   Visit(document.steps, document.id, new HashSet<string>(StringComparer.Ordinal), 0);
  }
  static void Visit(StoryStep[] steps, string story, HashSet<string> ids, int depth) {
   if (steps == null || steps.Length == 0) return;
   if (depth > StoryJsonNestingGuard.MaximumNestedBlocks) throw new StoryProgramException(story, null, null, "Nesting exceeds 8 blocks supported by JsonUtility.");
   foreach (var step in steps ?? Array.Empty<StoryStep>()) {
    if (step == null || string.IsNullOrWhiteSpace(step.id) || !ids.Add(step.id))
     throw new StoryProgramException(story, step?.id, step?.op, "Step ID must be nonblank and globally unique.");
    if (string.IsNullOrWhiteSpace(step.op)) throw new StoryProgramException(story, step.id, step.op, "Opcode is required.");
    Visit(step.steps, story, ids, depth + 1); Visit(step.elseSteps, story, ids, depth + 1);
   }
  }
 }
}