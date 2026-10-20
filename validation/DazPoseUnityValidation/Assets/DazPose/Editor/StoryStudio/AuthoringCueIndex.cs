using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DazPose.Story.Language;
namespace DazPose.Editor.Studio {
 public sealed class AuthoringCueState {
  public StoryAuthoringNode Node {get;} public string Declaration {get;} public string Block {get;}
  public AuthoringVisualState Before {get;} public AuthoringVisualState After {get;}
  public AuthoringCueState(StoryAuthoringNode node,string declaration,string block,AuthoringVisualState before,AuthoringVisualState after){Node=node;Declaration=declaration;Block=block;Before=before;After=after;}
 }
 public sealed class AuthoringCueIndex {
  sealed class Summary {public StoryAuthoringNode Declaration;public AuthoringVisualState Output;public readonly List<AuthoringCueState> Cues=new List<AuthoringCueState>();public readonly List<(StoryAuthoringNode target,AuthoringVisualState input)> Calls=new List<(StoryAuthoringNode,AuthoringVisualState)>();}
  readonly Dictionary<string,StoryAuthoringNode> declarations;
  readonly Dictionary<string,Summary> summaries=new Dictionary<string,Summary>();
  readonly HashSet<string> building=new HashSet<string>();
  readonly Dictionary<string,AuthoringCueState> lookup=new Dictionary<string,AuthoringCueState>();
  readonly CancellationToken token;readonly bool catalogComplete;readonly string root;
  public IReadOnlyList<AuthoringCueState> Cues {get;private set;}
  public int StatementsAnalyzed {get;private set;} public double BuildMilliseconds {get;private set;}
  public AuthoringCueState Find(string id)=>id!=null&&lookup.TryGetValue(id,out var cue)?cue:null;
  public AuthoringCueIndex(IEnumerable<StoryAuthoringNode> declarations,string root,string sequence,bool catalogComplete=true,CancellationToken token=default){
   var timer=System.Diagnostics.Stopwatch.StartNew();this.token=token;this.root=root;this.catalogComplete=catalogComplete;
   this.declarations=declarations.ToDictionary(d=>d.Id,StringComparer.Ordinal);
   foreach(var d in this.declarations.Values)Build(d,0);
   var contexts=new Dictionary<string,AuthoringVisualState>();var pending=new Queue<string>();
   void Context(StoryAuthoringNode d,AuthoringVisualState state){if(d==null)return;if(contexts.TryGetValue(d.Id,out var old)){state=AuthoringVisualState.Join(old,state,d.Source);if(old.Key==state.Key)return;}contexts[d.Id]=state;pending.Enqueue(d.Id);}
   var setup=this.declarations.Values.FirstOrDefault(d=>d.Source.unit==root&&d.Kind=="setup"&&d.Name==sequence);
   var entry=this.declarations.Values.FirstOrDefault(d=>d.Source.unit==root&&d.Kind=="sequence"&&d.Name==sequence);
   var baseline=new AuthoringVisualState();Context(setup,baseline);Context(entry,setup==null?baseline:summaries[setup.Id].Output.Substitute(baseline));
   // Each field can only lose precision at joins. A bounded worklist propagates explicit call contexts.
   int budget=Math.Max(1024,this.declarations.Count*128);
   while(pending.Count>0&&budget-->0){token.ThrowIfCancellationRequested();string id=pending.Dequeue();foreach(var call in summaries[id].Calls)Context(call.target,call.input.Substitute(contexts[id]));}
   bool exhausted=pending.Count>0;
   var rows=new List<AuthoringCueState>();
   foreach(var d in this.declarations.Values){
    var context=contexts.TryGetValue(d.Id,out var known)&&!exhausted?known:baseline.Unknown(255,d.Source,exhausted?"Card context work limit reached":"Card has no unique explicit incoming context");
    foreach(var c in summaries[d.Id].Cues){var cue=new AuthoringCueState(c.Node,c.Declaration,c.Block,c.Before.Substitute(context),c.After.Substitute(context));rows.Add(cue);lookup.Add(c.Node.Id,cue);}
   }
   Cues=rows.AsReadOnly();BuildMilliseconds=timer.Elapsed.TotalMilliseconds;
  }
  StoryAuthoringNode Target(StoryAuthoringNode n){
   string name=n.Kind=="play"?n.Name:n.Literals.TryGetValue("card",out var c)?c:null;
   var local=declarations.Values.Where(d=>d.Kind=="card"&&d.Source.unit==n.Source.unit&&d.Name==name).ToArray();
   var global=declarations.Values.Where(d=>d.Kind=="card"&&d.Source.unit!=root&&d.Tags.Count>0&&d.Name==name).ToArray();
   var matches=local.Concat(global).Distinct().ToArray();return matches.Length==1?matches[0]:null;
  }
  bool IsCall(StoryAuthoringNode n)=>n.Kind=="play"||n.Name=="Director.Execute";
  int DynamicMask(StoryAuthoringNode n,int depth){
   if(!catalogComplete)return 255;
   string[] tags=n.Literals.TryGetValue("tags",out var t)?t.Split('\u001f'):Array.Empty<string>();int mask=0;
   foreach(var d in declarations.Values.Where(d=>d.Kind=="card"&&d.Source.unit!=root&&d.Tags.Count>0&&tags.All(d.Tags.Contains)))mask|=OutputMask(Build(d,depth+1).Output);
   return mask;
  }
  int Mask(IEnumerable<StoryAuthoringNode> nodes,int depth){
   if(depth>48)return 255;int mask=0;
   foreach(var n in nodes){token.ThrowIfCancellationRequested();mask|=AuthoringVisualReducer.Writes(n);
    if(IsCall(n)){var target=Target(n);mask|=target==null?255:OutputMask(Build(target,depth+1).Output);}
    if(n.Name=="Director.DrawAndExecute")mask|=DynamicMask(n,depth+1);
    mask|=Mask(n.Body,depth+1)|Mask(n.Otherwise,depth+1);
   }return mask;
  }
  static int OutputMask(AuthoringVisualState output){int mask=0;for(int i=0;i<8;i++)if(output[(AuthoringField)i].Knowledge!=AuthoringKnowledge.Input)mask|=1<<i;return mask;}
  Summary Build(StoryAuthoringNode d,int depth){
   if(summaries.TryGetValue(d.Id,out var existing))return existing;
   if(depth>48||!building.Add(d.Id))return new Summary{Declaration=d,Output=new AuthoringVisualState().Unknown(255,d.Source,"Recursive/deep card call")};
   var result=new Summary{Declaration=d};
   AuthoringVisualState Walk(IEnumerable<StoryAuthoringNode> nodes,AuthoringVisualState state,string block){
    foreach(var n in nodes){token.ThrowIfCancellationRequested();StatementsAnalyzed++;var before=state;
     if(n.Kind=="phase"){
      result.Cues.Add(new AuthoringCueState(n,d.Name,block,before,before));state=Walk(n.Body,state,block);continue;
     }
     if(n.Kind=="if"||n.Kind=="while"||n.Kind=="repeat"){
      int mask=Mask(n.Body,depth+1)|Mask(n.Otherwise,depth+1);
      state=state.Unknown(mask,n.Source,n.Kind=="if"?"Persistent visual write inside skipped choice (authoring-rule violation)":"Persistent visual write inside skipped loop");
      result.Cues.Add(new AuthoringCueState(n,d.Name,block,before,state));
      Walk(n.Body,before,"Inside skipped "+n.Kind);Walk(n.Otherwise,before,"Inside skipped else");continue;
     }
     if(IsCall(n)){
      var target=Target(n);if(target==null)state=state.Unknown(255,n.Source,"Unknown/dynamic/ambiguous card call");
      else{result.Calls.Add((target,before));state=Build(target,depth+1).Output.Substitute(state);}
     }else if(n.Name=="Director.DrawAndExecute")state=state.Unknown(DynamicMask(n,depth+1),n.Source,"Dynamic Director card may change visual state");
     else state=AuthoringVisualReducer.Apply(state,n);
     result.Cues.Add(new AuthoringCueState(n,d.Name,block,before,state));
    }return state;
   }
   result.Output=Walk(d.Body,new AuthoringVisualState(true),"");building.Remove(d.Id);summaries[d.Id]=result;return result;
  }
 }
}
