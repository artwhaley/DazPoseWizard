using System;using System.IO;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using DazPose.Performer;using DazPose.Performer.HandGrip;using DazPose.UnityValidation.Partner;using DazPose.UnityValidation.Editor.Partner;using DazPose.UnityValidation;
public static class Day4SceneBuilder
{
 public const string ScenePath="Assets/Production/Scenes/Day4PartnerContactValidation.unity";
 public const string PartnerPath="Assets/Production/Partners/Partner.prefab";
 public const string ProfilePath="Assets/Production/Partners/PreparedContact.asset";
 [Serializable]public class Report{public string exception;public float minimumBackHeight,radialDeviation;public Vector2 span;public string scene;public PartnerContactProfileBuilder.Measurement measurement;}
 public static void Create(){var report=new Report();try{
 if(!Application.dataPath.Replace('\\','/').Contains("/.dazposewizard/production-consolidation/"))throw new Exception("Disposable project required");
 var scene=EditorSceneManager.OpenScene("Assets/Production/Scenes/Day2ActorValidation.unity");
 var actors=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<SuccubusPerformer>(true)).OrderBy(x=>x.name).ToArray();var actor=actors[0];foreach(var other in actors.Skip(1))UnityEngine.Object.DestroyImmediate(other.gameObject);
 var source=EditorSceneManager.OpenScene("Assets/Scenes/PartnerRigAcceptance.unity",OpenSceneMode.Additive);
 var original=source.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<PartnerAnatomyTestController>(true)).Single();
 var instance=UnityEngine.Object.Instantiate(original.gameObject);SceneManager.MoveGameObjectToScene(instance,scene);instance.name="Partner";
 var diagnostic=instance.GetComponent<PartnerAnatomyTestController>();if(!diagnostic.ConfigureRuntimeBody(out string reason))throw new Exception(reason);var body=diagnostic.RuntimeBody;UnityEngine.Object.DestroyImmediate(diagnostic);
 foreach(var animator in instance.GetComponentsInChildren<Animator>(true))animator.enabled=false;
 PoseArm(instance.transform,"rShldrBend","rForearmBend",1f);PoseArm(instance.transform,"lShldrBend","lForearmBend",-1f);
 instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(-90f,0f,0f));var bounds=BodyBounds(body);instance.transform.position=Vector3.up*(1f-bounds.min.y);report.minimumBackHeight=BodyBounds(body).min.y;
 EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(scene);
 Folder("Assets/Production/Partners");
 report.span=new Vector2(.2f,.75f);report.measurement=PartnerContactProfileBuilder.Measure(instance.transform,body.StructuralBody,body.ShellRenderer,new[]{body.ErectCenterlineRoot[0],body.ErectCenterlineRoot[body.ErectCenterlineRoot.Length-1]},17,report.span.x,report.span.y);
 if(report.measurement.errors.Length>0)throw new Exception(string.Join("; ",report.measurement.errors));
 var sections=report.measurement.sections;report.radialDeviation=sections.Max(x=>(x.maximumSurfaceRadiusRoot-x.minimumSurfaceRadiusRoot)*.5f);
 var surface=BuildSurface(body); var profile=AssetDatabase.LoadAssetAtPath<PartnerContactProfile>(ProfilePath);if(profile==null){profile=ScriptableObject.CreateInstance<PartnerContactProfile>();AssetDatabase.CreateAsset(profile,ProfilePath);}
 profile.Configure(body.StructuralBody.sharedMesh,body.ShellRenderer.sharedMesh,sections.Select(x=>x.centerRoot).ToArray(),sections.Select(x=>(x.maximumSurfaceRadiusRoot+x.minimumSurfaceRadiusRoot)*.5f).ToArray(),report.span,report.radialDeviation,"evaluated-prepared-endpoint; triangle-plane sections; rigid/unit; "+DateTime.UtcNow.ToString("o"),(body.ErectCenterlineRoot[body.ErectCenterlineRoot.Length-1]-body.ErectCenterlineRoot[0]).normalized,surface);EditorUtility.SetDirty(profile);
 var target=instance.AddComponent<PartnerContactTarget>();target.Configure(body,profile);if(!target.TryEvaluate(.5f,out var frame,out reason))throw new Exception(reason);
 PrefabUtility.SaveAsPrefabAssetAndConnect(instance,PartnerPath,InteractionMode.AutomatedAction);
 var support=GameObject.CreatePrimitive(PrimitiveType.Cube);support.name="One Metre Test Table";support.transform.SetPositionAndRotation(new Vector3(0f,.95f,-.95f),Quaternion.identity);support.transform.localScale=new Vector3(.8f,.1f,2.05f);UnityEngine.Object.DestroyImmediate(support.GetComponent<Collider>());
 var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Production/Partners/Table.mat");if(material==null){material=new Material(Shader.Find("HDRP/Lit"));material.SetColor("_BaseColor",new Color(.05f,.05f,.055f));AssetDatabase.CreateAsset(material,"Assets/Production/Partners/Table.mat");}support.GetComponent<Renderer>().sharedMaterial=material;
 actor.transform.SetPositionAndRotation(new Vector3(.65f,0f,-.95f),Quaternion.LookRotation(Vector3.left,Vector3.up));
 var camera=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>(true)).Single(x=>x.CompareTag("MainCamera"));camera.fieldOfView=50f;
 var root=new GameObject("Day 4 Scene Runner");var overview=new GameObject("Overview Camera Mark").transform;overview.SetParent(root.transform);overview.position=new Vector3(4.6f,3.1f,3.6f);overview.rotation=Quaternion.LookRotation(new Vector3(.05f,1f,-.85f)-overview.position,Vector3.up);
 var closeup=new GameObject("Contact Camera Mark").transform;closeup.SetParent(root.transform);closeup.position=frame.Center+new Vector3(.65f,.35f,.55f);closeup.rotation=Quaternion.LookRotation(frame.Center-closeup.position,Vector3.up);
 camera.transform.SetPositionAndRotation(overview.position,overview.rotation);
 var runner=root.AddComponent<Day4RegressionRunner>();Set(runner,"actor",actor);Set(runner,"partner",body);Set(runner,"target",target);Set(runner,"driver",actor.MotionSource);Set(runner,"view",camera);Set(runner,"overviewCameraMark",overview);Set(runner,"closeupCameraMark",closeup);Set(runner,"support",support.transform);
 EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();report.scene=ScenePath;Write(report);Debug.Log("DAY4_SAVED_SCENE_CREATED");Day4Validation.Run();
 }catch(Exception e){report.exception=e.ToString();Debug.LogException(e);Write(report);EditorApplication.Exit(1);}}
 static GripContactSurfaceData BuildSurface(PartnerBody body){
  var rows=new System.Collections.Generic.List<GripContactSurfaceData.Triangle>();
  AddSurface(body.StructuralBody,body.transform,new[]{16,17},rows);AddSurface(body.ShellRenderer,body.transform,new[]{0,1},rows);
  const string path="Assets/Production/Partners/PreparedSurface.asset";
  var data=AssetDatabase.LoadAssetAtPath<GripContactSurfaceData>(path);if(data==null){data=ScriptableObject.CreateInstance<GripContactSurfaceData>();AssetDatabase.CreateAsset(data,path);}
  data.Configure(rows.ToArray());EditorUtility.SetDirty(data);return data;
 }
 static void AddSurface(SkinnedMeshRenderer renderer,Transform root,int[] slots,System.Collections.Generic.List<GripContactSurfaceData.Triangle> rows){
  var mesh=new Mesh();try{renderer.BakeMesh(mesh);var vertices=mesh.vertices;var normals=mesh.normals;
  foreach(var slot in slots){var indices=mesh.GetIndices(slot);for(int i=0;i<indices.Length;i+=3){int a=indices[i],b=indices[i+1],c=indices[i+2];
  rows.Add(new GripContactSurfaceData.Triangle{A=root.InverseTransformPoint(renderer.transform.TransformPoint(vertices[a])),B=root.InverseTransformPoint(renderer.transform.TransformPoint(vertices[b])),C=root.InverseTransformPoint(renderer.transform.TransformPoint(vertices[c])),NormalA=root.InverseTransformDirection(renderer.transform.TransformDirection(normals[a])),NormalB=root.InverseTransformDirection(renderer.transform.TransformDirection(normals[b])),NormalC=root.InverseTransformDirection(renderer.transform.TransformDirection(normals[c]))});}}
  }finally{UnityEngine.Object.DestroyImmediate(mesh);}
 }
 static Bounds BodyBounds(PartnerBody body){var mesh=new Mesh();body.StructuralBody.BakeMesh(mesh);var v=mesh.vertices;var b=new Bounds(body.StructuralBody.transform.TransformPoint(v[0]),Vector3.zero);foreach(var p in v)b.Encapsulate(body.StructuralBody.transform.TransformPoint(p));UnityEngine.Object.DestroyImmediate(mesh);return b;}
 static void PoseArm(Transform root,string upperName,string lowerName,float side){var all=root.GetComponentsInChildren<Transform>(true);var upper=all.Single(t=>t.name==upperName);var lower=all.Single(t=>t.name==lowerName);upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,root.TransformDirection(new Vector3(side*.12f,-1f,0f)).normalized)*upper.rotation;}
 static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
 static void Set(UnityEngine.Object owner,string key,UnityEngine.Object value){using(var so=new SerializedObject(owner)){var p=so.FindProperty(key);if(p==null)throw new Exception("Missing runner binding "+key);p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}}
 static void Write(Report r){Directory.CreateDirectory("TestOutput/day4");File.WriteAllText("TestOutput/day4/scene-build.json",JsonUtility.ToJson(r,true));}
}







