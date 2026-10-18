using System;
using System.IO;
using System.Linq;
using DazPose.Motion;
using DazPose.Game;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DazPose.Editor {
 public static class StoryFullSessionSetup {
  public static void Prepare()=>FirstContactRepair.Prepare();
  internal static FunscriptMotionProgram EnsureMotion(string id) {
   if(id!="Finisher1" && id!="FinishLoop")throw new InvalidOperationException("Assign authored motion "+id);
   const string folder="Assets/Production/Story/Motion/Engineering";Directory.CreateDirectory(folder);
   string path=folder+"/"+id+"_Engineering.funscript";
   if(!File.Exists(path)) {
    int duration=id=="Finisher1" ? 18000 : 4000;
    var actions=Enumerable.Range(0,duration/1000+1).Select(i=>"{\"at\":"+i*1000+",\"pos\":"+(i%2==0 ? 30 : 70)+"}");
    File.WriteAllText(path,"{\"version\":\"1.0\",\"inverted\":false,\"range\":100,\"actions\":["+string.Join(",",actions)+"]}");
    File.WriteAllText(folder+"/README.txt","Temporary engineering motion assets for First Contact. Finisher1 lasts 18 seconds; FinishLoop lasts 4 seconds. Imported through the normal FunscriptImporter. Replace with final authored programs in FirstContact.bindings.asset.");
   }
   AssetDatabase.Refresh();AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
   return AssetDatabase.LoadAssetAtPath<FunscriptMotionProgram>(path) ?? throw new InvalidOperationException("Normal funscript importer failed for "+path);
  }
 }
}
