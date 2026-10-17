using System;
using System.Collections.Generic;
namespace DazPose.Story {
 public sealed class StoryLocalState {
  readonly Dictionary<string,StoryValue> values=new Dictionary<string,StoryValue>(StringComparer.Ordinal);
  public StoryValue Require(string key) => values.TryGetValue(key,out var value) ? value : throw new ArgumentException("Local value is unset: "+key);
  public void Set(string key,StoryValue value) { if(string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Local key is blank."); values[key]=value; }
 }
}