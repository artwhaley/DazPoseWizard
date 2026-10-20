using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Story.Language;
using UnityEngine;

namespace DazPose.Story {
 // Generated application content, never a scene-authored membership list.
 public sealed class GlobalStoryCardCatalog : ScriptableObject {
  public string fingerprint;
  public CompiledStoryAsset[] sources=Array.Empty<CompiledStoryAsset>();
  public string[] diagnostics=Array.Empty<string>();
  public static GlobalStoryCardCatalog Require() {
   var catalog=Resources.Load<GlobalStoryCardCatalog>("DazPose/GlobalStoryCards");
   if(catalog==null)throw new InvalidOperationException("The generated global card index is missing. Reindex published cards under DazPose > Content > Advanced.");
   if(catalog.diagnostics.Length>0)throw new InvalidOperationException("Published global cards need attention:\n"+string.Join("\n",catalog.diagnostics));
   return catalog;
  }
 }
 public sealed class StoryLinkedContent {
  public CompiledStoryProgram Program {get;}
  public StoryContentBindings RootContent {get;}
  readonly Dictionary<string,StoryContentBindings> content;
  StoryLinkedContent(CompiledStoryProgram program,StoryContentBindings root,Dictionary<string,StoryContentBindings> content) {Program=program;RootContent=root;this.content=content;}
  public static StoryLinkedContent Create(CompiledStoryAsset root) {
   if(root==null || !root.IsCompiled)throw new InvalidOperationException("Assign a compiled story source with no import diagnostics.");
   return Create(root.program,root.content,string.IsNullOrEmpty(root.sourceIdentity) ? "scene" : root.sourceIdentity);
  }
  public static StoryLinkedContent Create(CompiledStoryProgram root,StoryContentBindings rootContent,string rootId="scene") {
   if(rootContent==null)throw new InvalidOperationException("Assign typed story content bindings.");
   var catalog=GlobalStoryCardCatalog.Require();
   var units=new List<StoryLinkUnit>();var bindings=new Dictionary<string,StoryContentBindings>(StringComparer.Ordinal) {{rootId,rootContent}};
   foreach(var asset in catalog.sources) {
    if(asset==null || !asset.IsCompiled || asset.content==null || string.IsNullOrEmpty(asset.sourceIdentity))
     throw new InvalidOperationException("The generated global card index contains an invalid source. Reimport/reindex published content.");
    if(bindings.ContainsKey(asset.sourceIdentity))throw new InvalidOperationException("Duplicate story source identity: "+asset.sourceIdentity);
    units.Add(new StoryLinkUnit(asset.sourceIdentity,asset.program));bindings.Add(asset.sourceIdentity,asset.content);
   }
   return new StoryLinkedContent(StoryProgramLinker.Link(root,units,rootId),rootContent,bindings);
  }
  public StoryContentBindings ContentFor(StorySource source) {
   if(source==null || string.IsNullOrEmpty(source.unit))return RootContent;
   return content.TryGetValue(source.unit,out var value) ? value : throw new StorySourceException(source,"Missing content owner '"+source.unit+"'.");
  }
  public string[] Diagnostics(CompiledStoryProgram view,StorySceneBindings scene=null) {
   var errors=new List<string>();
   foreach(var owner in content) {
    var local=new CompiledStoryProgram {version=view.version,bindings=view.bindings.Where(b=>b.source?.unit==owner.Key).ToArray(),
     dialogue=view.dialogue.Where(d=>d.source?.unit==owner.Key).ToArray()};
    errors.AddRange(owner.Value.Diagnostics(local,scene));
   }
   return errors.Distinct().ToArray();
  }
  public string[] MandatoryDiagnostics(StorySceneBindings scene,string sequence,string stopBeforePhase=null) {
   var view=string.IsNullOrEmpty(stopBeforePhase) ? StoryProgramReachability.ForSequence(Program,sequence) : StoryPlaybackWindow.BeforePhase(Program,sequence,stopBeforePhase);
   return Diagnostics(view,scene);
  }
  public string ResourceIssue(CardDefinition card,StorySceneBindings scene) {
   foreach(var use in StoryProgramReachability.ForCard(Program,card).bindings) {
    if(use.kind!=StoryBindingKind.Location && use.kind!=StoryBindingKind.Performer && use.kind!=StoryBindingKind.PlayerBody && use.kind!=StoryBindingKind.ContactTarget)continue;
    try {scene.ValidateSymbol(use);}
    catch(Exception error) {return use.source+"\nUnavailable scene resource '"+use.symbol+"': "+error.Message;}
   }
   return null;
  }
 }
}
