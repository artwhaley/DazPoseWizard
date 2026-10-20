using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor.Studio {
 public sealed partial class SceneDirectorStudioWindow {
  sealed class IndexResult {public AuthoringCueIndex Index;public Dictionary<string,SourceBindingManifest> Manifests;public Dictionary<string,string> Text;public string[] Issues;}
  AuthoringSourceCatalog authorCatalog;AuthoringCueIndex cueIndex;AuthoringCueState selectedCue;
  Task<IndexResult> indexTask;CancellationTokenSource indexCancellation;
  readonly AuthoringSelectionQueue projectionQueue=new AuthoringSelectionQueue();
  bool projectionPending=>projectionQueue.Pending;int generation=>projectionQueue.Generation;int staleRequests=>projectionQueue.Discarded;
  bool indexStale;int indexBuilds,poseRevision,presentationRevision;
  double nextRevisionPoll,requestedAt,lookupMilliseconds,requestMilliseconds;int lookupCount;
  readonly Dictionary<string,string> authorText=new Dictionary<string,string>();
  readonly Dictionary<string,StoryBindingRequirement[]> directRequirements=new Dictionary<string,StoryBindingRequirement[]>();
  UnityEngine.Object[] activeDependencies=Array.Empty<UnityEngine.Object>();string dependenciesStamp;
  void CancelIndex(){indexCancellation?.Cancel();indexCancellation?.Dispose();indexCancellation=null;indexTask=null;}
  void BeginAuthoringIndex(){
   CancelIndex();projectionQueue.Cancel();indexStale=true;if(source==null){cueIndex=null;manifest=null;return;}
   try{
    authorCatalog=AuthoringSourceCatalog.Capture(source);var captured=authorCatalog;string chosen=nativeSequence!=null&&nativeSequence.CompiledSource==source?nativeSequence.EntrySequence:sequence;indexCancellation=new CancellationTokenSource();var token=indexCancellation.Token;
    indexTask=Task.Run(()=>{
     var manifests=new Dictionary<string,SourceBindingManifest>();var text=new Dictionary<string,string>();var nodes=new List<StoryAuthoringNode>();var issues=new List<string>(captured.Issues);bool complete=captured.Complete;
     foreach(var unit in captured.Units){token.ThrowIfCancellationRequested();var analysis=DazStoryCompiler.Analyze(unit.Text,unit.Path,new StoryAnalysisOptions{SourceGuid=unit.Owner});var m=new SourceBindingManifest{SourceGuid=unit.Owner,SourcePath=unit.Path,Analysis=analysis};manifests[unit.Path]=m;
      if(!analysis.SyntaxValid){complete=false;issues.AddRange(analysis.SyntaxDiagnostics);continue;}
      nodes.AddRange(analysis.Authoring);var lines=unit.Text.Split('\n');
      void Labels(IEnumerable<StoryAuthoringNode> body){foreach(var n in body){text[n.Id]=n.Source.line>0&&n.Source.line<=lines.Length?lines[n.Source.line-1].Trim():n.Name??n.Kind;Labels(n.Body);Labels(n.Otherwise);}}
      Labels(analysis.Authoring);
     }
     var roots=nodes.Where(n=>n.Source.unit==captured.Root&&n.Kind=="sequence").ToArray();
     string setupName=nodes.FirstOrDefault(n=>n.Source.unit==captured.Root&&n.Kind=="setup")?.Name;
     string seq=roots.FirstOrDefault(n=>n.Name==chosen)?.Name??roots.FirstOrDefault(n=>n.Name==setupName)?.Name??(roots.Length==1?roots[0].Name:null);
     if(seq==null)issues.Add("No explicit root sequence is selected. Connect the saved performance sequence in Scene / script.");
     return new IndexResult{Index=new AuthoringCueIndex(nodes,captured.Root,seq,complete,token),Manifests=manifests,Text=text,Issues=issues.ToArray()};
    },token);Repaint();
   }catch(Exception e){error=e.Message;indexStale=false;}
  }
  static string Signature(AuthoringCueState cue)=>cue==null?null:cue.Node.Source.unit+"/"+cue.Node.Source.declaration+"/"+cue.Node.Kind+"/"+cue.Node.Name+"/"+string.Join(";",cue.Node.Literals.Select(k=>k.Key+"="+k.Value));
  void CompleteIndex(){
   if(indexTask==null||!indexTask.IsCompleted)return;var task=indexTask;indexTask=null;
   if(task.IsCanceled)return;if(task.IsFaulted){error=task.Exception.GetBaseException().Message;indexStale=true;Repaint();return;}
   var result=task.Result;string signature=Signature(selectedCue);bool resume=stage!=null;cueIndex=result.Index;cueManifests.Clear();authorText.Clear();directRequirements.Clear();
   foreach(var m in result.Manifests)cueManifests[m.Key]=m.Value;foreach(var t in result.Text)authorText[t.Key]=t.Value;
   manifest=authorCatalog.Units.Count>0&&cueManifests.TryGetValue(authorCatalog.Units[0].Path,out var rootManifest)?rootManifest:null;
   content=authorCatalog.Units.FirstOrDefault(u=>u.Owner==authorCatalog.Root)?.Content;
   foreach(var row in cueIndex.Cues){var m=ManifestFor(row.Node.Source);var keys=new HashSet<string>();
    if(row.Node.Kind=="call"){
     var spec=StoryVocabulary.Require(row.Node.Name,row.Node.Source);foreach(var param in spec.bindingParameters)if(row.Node.Literals.TryGetValue(param.Key,out var alias))keys.Add(param.Value+"|"+alias);
    }
    directRequirements[row.Node.Id]=(m?.Requirements??Array.Empty<StoryBindingRequirement>()).Where(r=>r.IsDialogue?row.Node.Name=="Lara.Say"&&row.Node.Literals.TryGetValue("text",out var transcript)&&r.Transcript==transcript:keys.Contains(r.Kind+"|"+r.Alias)).ToArray();
   }
   selectedCue=null;selectedPc=-1;if(signature!=null){var matches=cueIndex.Cues.Where(c=>Signature(c)==signature).ToArray();if(matches.Length==1){selectedCue=matches[0];selectedPc=cueIndex.Cues.ToList().IndexOf(selectedCue);}else{Release();error="Selected cue changed or is duplicated. Select it again in the updated script.";}}
   indexBuilds++;indexStale=false;revisionDirty=false;dependenciesStamp=null;
   if(result.Issues.Length>0)error=string.Join("\n",result.Issues);
   if(selectedCue!=null){selectedSource=selectedCue.Node.Source;selectedEntry=selectedCue.Declaration;detailManifest=ManifestFor(selectedSource);if(resume)RequestProjection();}
   Repaint();
  }
  void RequestProjection(){if(selectedCue==null||indexStale)return;projectionQueue.Select(selectedCue,stageAfter);requestedAt=EditorApplication.timeSinceStartup;Repaint();}
  void ProjectSelected(){
   if(!projectionQueue.TryTake(out var request)||request.Cue!=selectedCue||request.After!=stageAfter||indexStale)return;
   try{
    BuildPreview();visualState=stageAfter?selectedCue.After:selectedCue.Before;
    stage.ApplyAuthoringState(visualState,v=>authorCatalog.ContentFor(v.Source),poseRevision,presentationRevision);
    activeDependencies=stage.AuthoringDependencies;RememberDependencies();anchorStamp=CaptureAnchorStamp();requestMilliseconds=(EditorApplication.timeSinceStartup-requestedAt)*1000;ObserveNative("cue");
   }catch(Exception e){HaltVisual(e);}Repaint();
  }
  void HaltVisual(Exception e){error=e.Message;ObserveNative("failure",e.ToString());stage?.Dispose();stage=null;nativeContext=null;projectionQueue.Cancel();Repaint();}
  string DependencyStamp(){
   return string.Join(";",activeDependencies.Where(o=>o!=null).Select(o=>o.GetEntityId()+":"+EditorUtility.GetDirtyCount(o)));
  }
  void QueueBindingRefresh(){RefreshOwnerReferences();if(stage!=null)RequestProjection();}
  void QueueCueRefresh(){RefreshOwnerReferences();if(stage!=null)RequestProjection();}
  void RefreshOwnerReferences(){if(authorCatalog==null)return;foreach(var unit in authorCatalog.Units){if(unit.Content!=null)continue;try{unit.Content=SourceBindingReconciler.FindOwner(unit.Owner,SourceBindingReconciler.CompanionPath(unit.Path));unit.ContentIssue=null;}catch(Exception e){unit.ContentIssue=e.Message;}}}
 }
}
