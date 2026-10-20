using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor.Studio {
 // Unity input capture is main-thread only. The captured strings are safe for pure worker analysis.
 internal sealed class AuthoringSourceCatalog {
  internal sealed class Unit {public string Owner,Path,Text;public DateTime Write;public CompiledStoryAsset Asset;public StoryContentBindings Content;public string ContentIssue;}
  public readonly List<Unit> Units=new List<Unit>();public readonly List<string> Issues=new List<string>();
  public bool Complete=true;public string Root;
  public static AuthoringSourceCatalog Capture(CompiledStoryAsset root){
   var result=new AuthoringSourceCatalog();var assets=new List<CompiledStoryAsset>{root};
   var catalog=Resources.Load<GlobalStoryCardCatalog>("DazPose/GlobalStoryCards");
   if(catalog==null){result.Complete=false;result.Issues.Add("Global card index unavailable; dynamic incoming visual state is unknown.");}
   else{assets.AddRange(catalog.sources??Array.Empty<CompiledStoryAsset>());if(catalog.diagnostics.Length>0){result.Issues.AddRange(catalog.diagnostics);}}
   foreach(var asset in assets.Distinct()){
    if(asset==null){result.Complete=false;continue;}string path=AssetDatabase.GetAssetPath(asset),owner=AssetDatabase.AssetPathToGUID(path);
    if(asset==root)result.Root=owner;
    try{
     var unit=new Unit{Owner=owner,Path=path,Text=File.ReadAllText(path),Write=File.GetLastWriteTimeUtc(path),Asset=asset};
     try{unit.Content=SourceBindingReconciler.FindOwner(owner,SourceBindingReconciler.CompanionPath(path));}catch(Exception e){unit.ContentIssue=e.Message;result.Issues.Add(path+": "+e.Message);}
     result.Units.Add(unit);
    }catch(Exception e){result.Complete=false;result.Issues.Add(path+": "+e.Message);}
   }
   return result;
  }
  public StoryContentBindings ContentFor(StorySource source){var unit=Units.FirstOrDefault(u=>u.Owner==source?.unit);if(unit==null)throw new InvalidOperationException("Missing source owner "+source?.unit);if(unit.Content==null)throw new InvalidOperationException(unit.ContentIssue??"Create missing asset slots for "+unit.Path);return unit.Content;}
 }
}
