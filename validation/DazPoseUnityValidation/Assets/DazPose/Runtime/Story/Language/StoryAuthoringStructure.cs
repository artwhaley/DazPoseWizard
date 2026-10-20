using System;
using System.Collections.Generic;
using System.Linq;
namespace DazPose.Story.Language {
 // Analysis-only immutable view of the existing frontend; never serialized into executable data.
 public sealed class StoryAuthoringNode {
  public string Id {get;} public string Kind {get;} public string Name {get;} public StorySource Source {get;}
  public IReadOnlyDictionary<string,string> Literals {get;} public IReadOnlyList<StoryAuthoringNode> Body {get;} public IReadOnlyList<StoryAuthoringNode> Otherwise {get;}
  public IReadOnlyList<string> Tags {get;} public string Text {get;}
  internal StoryAuthoringNode(string id,string kind,string name,StorySource source,Dictionary<string,string> args,StoryAuthoringNode[] body,StoryAuthoringNode[] otherwise,string[] tags=null,string text=null){
   Text=text;Id=id;Kind=kind;Name=name;Source=source;Literals=new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(args);Body=Array.AsReadOnly(body);Otherwise=Array.AsReadOnly(otherwise);Tags=Array.AsReadOnly(tags??Array.Empty<string>());
  }
 }
 public static class StoryAuthoringStructure {
  internal static StoryAuthoringNode[] Export(List<StoryAstDeclaration> ast,string file,string owner,string text,CompiledStoryProgram compiled){
   var calls=compiled.instructions.Where(i=>i.code==StoryCode.Call).ToDictionary(i=>(i.source.line,i.source.column,i.name));
   var lines=text.Split('\n');var offsets=new int[lines.Length];for(int i=1;i<lines.Length;i++)offsets[i]=offsets[i-1]+lines[i-1].Length+1;
   int Offset(StoryToken t)=>Math.Min(text.Length,offsets[Math.Max(0,Math.Min(offsets.Length-1,t.line-1))]+t.column-1);
   string Display(StoryAstNode n){if(n.kind=="if"||n.kind=="while"||n.kind=="repeat"||n.kind=="phase")return lines[Math.Min(lines.Length-1,n.token.line-1)].Trim();int begin=Offset(n.token),end=n.end==null?begin:Offset(n.end);return text.Substring(begin,Math.Max(0,end-begin)).Trim().TrimEnd(';').Trim();}
   StorySource At(StoryToken t,string declaration)=>new StorySource(file,t.line,t.column,declaration){unit=owner};
   StoryAuthoringNode[] Nodes(IEnumerable<StoryAstNode> nodes,string declaration,string prefix){
    int ordinal=0;return nodes.Select(n=>{
     string id=prefix+"/"+(ordinal++);var args=new Dictionary<string,string>(StringComparer.Ordinal);
     if(n.kind=="call"){
      var spec=StoryVocabulary.Require(n.name,At(n.token,declaration));int positional=0;
      foreach(var a in n.arguments){string key=a.name??spec.parameters[positional++];var e=a.expression;
       if(calls.TryGetValue((n.token.line,n.token.column,n.name),out var call)){
        var argument=call.arguments.FirstOrDefault(x=>x.name==key);
        if(argument!=null&&compiled.expressions[argument.expression].atoms.Any(x=>x.code==StoryAtomCode.Read||x.code==StoryAtomCode.Function))continue;
       }
       if(e.op=="name"||e.op=="string"||e.op=="bool")args[key]=e.token.text;
       else if(e.op=="list"&&e.args.All(x=>x.op=="name"||x.op=="string"))args[key]=string.Join("\u001f",e.args.Select(x=>x.token.text));
      }
     }
     return new StoryAuthoringNode(id,n.kind,n.name,At(n.token,declaration),args,Nodes(n.body,declaration,id+"/body"),Nodes(n.otherwise,declaration,id+"/else"),text:Display(n));
    }).ToArray();
   }
   return ast.Select(d=>new StoryAuthoringNode(owner+"/"+d.kind+":"+d.name,d.kind,d.name,At(d.token,d.kind+" "+d.name),new Dictionary<string,string>(),Nodes(d.body,d.kind+" "+d.name,owner+"/"+d.kind+":"+d.name),Array.Empty<StoryAuthoringNode>(),d.tags.ToArray())).ToArray();
  }
 }
}
