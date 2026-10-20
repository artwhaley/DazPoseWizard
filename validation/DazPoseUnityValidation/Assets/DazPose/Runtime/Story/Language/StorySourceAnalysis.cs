using System;
using System.Collections.Generic;
using System.Linq;
namespace DazPose.Story.Language {
 public sealed class StoryAnalysisOptions {
  public string SourceGuid;
  public IReadOnlyDictionary<string,StoryBindingKind> AssignedSymbols;
  public bool AllowExternalCards=true;
 }
 public sealed class StoryBindingRequirement {
  public string Id,Alias,ExpectedType,Transcript;
  public StoryBindingKind? Kind;
  public StorySource[] Occurrences=Array.Empty<StorySource>();
  public bool IsDialogue;
 }
 public sealed class StorySourceAnalysis {
  public CompiledStoryProgram Program;
  public IReadOnlyList<StoryAuthoringNode> Authoring=Array.Empty<StoryAuthoringNode>();
  public StoryBindingRequirement[] Requirements=Array.Empty<StoryBindingRequirement>();
  public string[] SyntaxDiagnostics=Array.Empty<string>();
  public string[] UnresolvedAssignments=Array.Empty<string>();
  public bool SyntaxValid=>SyntaxDiagnostics.Length==0 && Program!=null;
  public bool BindingsIncomplete=>SyntaxValid && UnresolvedAssignments.Length!=0;
 }
 public static partial class DazStoryCompiler {
  // Source-first analysis deliberately compiles without requiring assigned object references.
  public static StorySourceAnalysis Analyze(string source,string path,StoryAnalysisOptions options=null) {
   options=options ?? new StoryAnalysisOptions();
   string owner=string.IsNullOrWhiteSpace(options.SourceGuid) ? "path:"+(path ?? "") : options.SourceGuid.Trim();
   var result=new StorySourceAnalysis();
   try {
    var ast=new StoryParser(source ?? "",path ?? "<source>").Parse();
    result.Program=new Builder(path ?? "<source>",null,options.AllowExternalCards).Compile(ast);
    result.Authoring=Array.AsReadOnly(StoryAuthoringStructure.Export(ast,path ?? "<source>",owner,source ?? "",result.Program));
    var byKey=new Dictionary<string,StoryBindingRequirement>(StringComparer.Ordinal);
    bool collision=false;
    var aliasKinds=new Dictionary<string,StoryBindingKind>(StringComparer.Ordinal);
    foreach(var use in result.Program.bindings) {
     if(aliasKinds.TryGetValue(use.symbol,out var prior) && prior!=use.kind) {
      result.SyntaxDiagnostics=result.SyntaxDiagnostics.Append(use.source+"\nBinding alias '"+use.symbol+"' is used as both "+prior+" and "+use.kind+".").ToArray();
      collision=true; continue;
     }
     aliasKinds[use.symbol]=use.kind;
     string key=use.kind+"\u001f"+use.symbol;
     if(!byKey.TryGetValue(key,out var requirement)) {
      requirement=new StoryBindingRequirement {Id=owner+"|"+use.kind+"|"+use.symbol,Alias=use.symbol,Kind=use.kind,ExpectedType=use.kind.ToString()};
      byKey.Add(key,requirement);
     }
     requirement.Occurrences=requirement.Occurrences.Concat(new[]{use.source}).ToArray();
    }
    foreach(var line in result.Program.dialogue) {
     var dialogue=new StoryBindingRequirement {Id=owner+"|dialogue|"+line.id,Alias=line.transcript,Kind=null,ExpectedType="AudioClip",Transcript=line.transcript,IsDialogue=true,Occurrences=result.Program.instructions.Where(i=>i.code==StoryCode.Call && i.name=="Lara.Say" && i.arguments.Any(a=>a.name=="text" && result.Program.expressions[a.expression].atoms.Length==1 && result.Program.expressions[a.expression].atoms[0].text==line.transcript)).Select(i=>i.source).ToArray()};
     byKey.Add("dialogue\u001f"+line.id,dialogue);
    }
    result.Requirements=byKey.Values.ToArray();
    if(collision) result.Program=null;
    var missing=new List<string>();
    foreach(var requirement in result.Requirements) {
     if(requirement.IsDialogue || options.AssignedSymbols==null || !options.AssignedSymbols.TryGetValue(requirement.Alias,out var actual) || actual!=requirement.Kind)
      missing.Add(requirement.Id+" ("+requirement.ExpectedType+")");
    }
    result.UnresolvedAssignments=missing.ToArray();
   }
   catch(StorySourceException error) { result.Program=null;result.SyntaxDiagnostics=new[]{error.Message}; }
   return result;
  }
 }
}
