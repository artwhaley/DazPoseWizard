using System; using System.IO; using System.Linq; using System.Collections.Generic; using System.Text.RegularExpressions; using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine; using DazPose.Editor.Performer; using DazPose.Performer;
public static class VoidCorrection {
 static string Normalize(Component c){var text=Regex.Replace(EditorJsonUtility.ToJson(c),"\"instanceID\":-?[0-9]+","\"instanceID\":0");using(var so=new SerializedObject(c)){var p=so.GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue!=null&&EditorUtility.IsPersistent(p.objectReferenceValue))text+="|"+p.propertyPath+"="+AssetDatabase.GetAssetPath(p.objectReferenceValue);}return text;}
 static Dictionary<string,string> Capture(GameObject root){var rows=new Dictionary<string,string>();foreach(var t in root.GetComponentsInChildren<Transform>(true)){string path=AnimationUtility.CalculateTransformPath(t,root.transform);rows[path+"/transform"]=JsonUtility.ToJson(new TR{p=t.localPosition,r=t.localRotation,s=t.localScale,active=t.gameObject.activeSelf});foreach(var c in t.GetComponents<Component>().Where(c=>c!=null&&!(c is Transform)))rows[path+"/"+c.GetType().FullName]=Normalize(c);}return rows;}
 [Serializable] class TR{public Vector3 p,s; public Quaternion r; public bool active;}
 [Serializable] class Report{public string exception;public List<string> checks=new List<string>();public int environmentRecords;}
 public static void Run(){var report=new Report();try{
 if(!Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/production-consolidation/"))throw new Exception("Disposable project required");
 var sourceHash=Hash(ProductionTestEnvironmentBuilder.AuthoredScene);
 var source=EditorSceneManager.OpenScene(ProductionTestEnvironmentBuilder.AuthoredScene);var stage=source.GetRootGameObjects().Single(x=>x.name=="FirstPerformanceVoid");
 var before=Capture(stage.transform.Find("Environment").gameObject);var view=Capture(stage.transform.Find("Player").gameObject);var volume=Capture(stage.transform.Find("SceneVolumes").gameObject);
 // Move the author's exact light hierarchy into the reusable prefab; keep all light values unchanged.
 var lights=stage.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Character Visibility Lights");var authoredLights=Capture(lights.gameObject);
 var root=PrefabUtility.LoadPrefabContents(ProductionPerformerBuilder.PrefabPath);
 try{var existing=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Character Visibility Lights");var current=Capture(existing.gameObject);
 if(!Same(authoredLights,current)){UnityEngine.Object.DestroyImmediate(existing.gameObject);var copy=UnityEngine.Object.Instantiate(lights.gameObject);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy,root.scene);copy.transform.SetParent(root.transform,false);copy.name="Character Visibility Lights";copy.transform.localPosition=lights.localPosition;copy.transform.localRotation=lights.localRotation;copy.transform.localScale=lights.localScale;}
 var owned=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Character Visibility Lights");Check(report,Same(authoredLights,Capture(owned.gameObject)),"prefab owns exact authored character light hierarchy/settings");ProductionPerformerBuilder.RejectExternalReferences(root);PrefabUtility.SaveAsPrefabAsset(root,ProductionPerformerBuilder.PrefabPath);
 }finally{PrefabUtility.UnloadPrefabContents(root);}
 ProductionTestEnvironmentBuilder.Compose(); var scene=EditorSceneManager.GetActiveScene();stage=scene.GetRootGameObjects().Single(x=>x.name=="FirstPerformanceVoid");
 Check(report,Same(before,Capture(stage.transform.Find("Environment").gameObject)),"glowing floor, smoke, fog, furniture and environment light records unchanged");
 Check(report,Same(view,Capture(stage.transform.Find("Player").gameObject)),"authored camera/view hierarchy and settings unchanged");Check(report,Same(volume,Capture(stage.transform.Find("SceneVolumes").gameObject)),"authored scene volume settings unchanged");
 Check(report,Hash(ProductionTestEnvironmentBuilder.AuthoredScene)==sourceHash,"original authored scene byte-identical");Check(report,!stage.GetComponents<MonoBehaviour>().Any(),"old demo sequence and environment-owned body effect removed");
 Check(report,scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SuccubusPerformer>(true)).Count()==2,"two production prefab actors only");report.environmentRecords=before.Count;AssetDatabase.SaveAssets();Write(report);Day2Validation.Run();
 }catch(Exception e){report.exception=e.ToString();Debug.LogException(e);Write(report);EditorApplication.Exit(1);}}
 static string Hash(string p){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p)));}
 static bool Same(Dictionary<string,string>a,Dictionary<string,string>b)=>a.Count==b.Count&&a.All(x=>b.ContainsKey(x.Key)&&b[x.Key]==x.Value);
 static void Check(Report r,bool ok,string label){if(!ok)throw new Exception(label);r.checks.Add(label);}
 static void Write(Report r)=>File.WriteAllText("TestOutput/day2/environment-correction.json",JsonUtility.ToJson(r,true));
}
