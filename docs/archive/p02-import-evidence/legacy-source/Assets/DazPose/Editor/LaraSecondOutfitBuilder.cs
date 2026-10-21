using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DazPose.Performer;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace DazPose.UnityValidation
{
    public static class LaraSecondOutfitBuilder
    {
        public const string Folder=LaraCandidateBuilder.Folder+"/SecondOutfit";
        public const string ScenePath=Folder+"/FirstPerformanceVoidLaraSecondOutfit.unity";
        [Serializable] private class Manifest
        { public string asset,sha256,duf,dufSHA256;public Part[] parts;public Node[] sourceNodes; }
        [Serializable] private class Node {public string id,parent;}
        [Serializable] private class Part
        {
            public string node,sourceNode,kind;public bool shell;
            public LaraCandidateBuilder.Point[] points;public LaraCandidateBuilder.Frame[] frames;
            public LaraCandidateBuilder.Surface[] materials;
        }
        [Serializable] private class Heel {public string sourceSHA256,referenceSHA256;public LaraCandidateBuilder.Entry[] entries;}
        [Serializable] private class Audit
        { public string node;public int vertices,bones,materials;public bool shell;public float maximumRestError; }
        [MenuItem("Tools/DAZ Pose/Development/Open Lara Second Outfit")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(ScenePath))throw new IOException("Build the second outfit before opening its review scene.");
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);LaraSecondOutfitRepair.BindLoaded();LaraFirstOutfitBuilder.ConfigurePresentation();
            Selection.activeGameObject=UnityEngine.Object.FindAnyObjectByType<LaraWardrobe>().gameObject;
            LaraSecondOutfitReviewWindow.ShowFor(Selection.activeGameObject.GetComponent<LaraWardrobe>());
        }
        public static void BuildAndRun(){Build();LaraSecondOutfitValidation.Run();}
        public static void Build()
        {
            Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder+"/wardrobe-manifest.json"));
            if(Hash(manifest.asset)!=manifest.sha256 || Hash(manifest.duf)!=manifest.dufSHA256)
                throw new IOException("Second outfit sources changed; prepare the inventory again.");
            if(File.Exists(ScenePath))throw new IOException("Second review scene already exists; preserve user edits before rebuilding.");
            EditorSceneManager.OpenScene(LaraCandidateInstaller.ValidationScene,OpenSceneMode.Single);
            var performer=UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            var body=performer.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.sharedMesh!=null && r.sharedMesh.GetBlendShapeIndex("CapturedOpening")>=0);
            var donor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(manifest.asset));
            try
            {
                var donorBody=LaraCandidateBuilder.Body(donor);
                var canonical=body.bones.ToDictionary(b=>b.name);
                var donorBones=donorBody.bones.ToDictionary(b=>b.name);
                var rigRoot=body.rootBone.parent;
                var bare=new Mesh();body.BakeMesh(bare);
                float referenceFloor=bare.vertices.Min(v=>body.transform.TransformPoint(v).y);UnityEngine.Object.DestroyImmediate(bare);
                var bodyMesh=LaraCandidateBuilder.ReadableCopy(body.sharedMesh);LaraFirstOutfitBuilder.CopyFrames(body.sharedMesh,bodyMesh);
                var baseManifest=LaraCandidateBuilder.ReadManifest();
                var bodyPoints=LaraFirstOutfitBuilder.MapPoints(bodyMesh.vertices,baseManifest.points,Array.Empty<LaraCandidateBuilder.Frame>());
                var heel=JsonUtility.FromJson<Heel>(File.ReadAllText(Folder+"/heel-reference.json"));
                if(heel.sourceSHA256!=manifest.sha256 || heel.referenceSHA256!=Hash("Assets/TestCharacter/larafirstoutfit.fbx"))
                    throw new IOException("Heel reference inputs changed; run prepare-lara-heel-reference.py again.");
                var deltas=heel.entries.Where(e=>baseManifest.points[e.index].position.y<.4f).ToDictionary(e=>e.index,e=>e.delta);
                var footDelta=bodyPoints.Select(p=>deltas.TryGetValue(p,out var d)?d:Vector3.zero).ToArray();
                var normals=LaraCandidateBuilder.SmoothPointNormals(bodyMesh.vertices,bodyMesh.triangles,bodyPoints,baseManifest.points.Length);
                LaraFirstOutfitBuilder.AddFrame(bodyMesh,"CapturedHeelFootPose",footDelta,bodyMesh.vertices,bodyMesh.triangles,bodyPoints,baseManifest.points.Length,normals);
                // FBX flattened the Daz pose into mesh positions and toe joint centers.
                // Retain those centers, then rebuild rest bind matrices on the one rig.
                foreach(var pair in canonical)if(pair.Key.Contains("Toe") && donorBones.TryGetValue(pair.Key,out var source))
                    pair.Value.localPosition=source.localPosition;
                bodyMesh.bindposes=body.bones.Select(b=>b.worldToLocalMatrix*body.transform.localToWorldMatrix).ToArray();
                body.sharedMesh=LaraCandidateBuilder.Save(bodyMesh,Folder+"/LaraBodyHeelReference.asset");
                body.SetBlendShapeWeight(bodyMesh.GetBlendShapeIndex("CapturedHeelFootPose"),100);
                var inventory=new List<LaraWardrobe.Piece>();var audits=new List<Audit>();
                var allSources=donor.GetComponentsInChildren<Renderer>(true);
                foreach(var part in manifest.parts)
                {
                    var source=allSources.Single(r=>r.name==part.node+".Shape");
                    var skin=source as SkinnedMeshRenderer;
                    var original=skin!=null?skin.sharedMesh:source.GetComponent<MeshFilter>().sharedMesh;
                    var rawMap=LaraFirstOutfitBuilder.MapPoints(original.vertices,part.points,part.frames);
                    var toBody=donorBody.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;
                    var mesh=LaraCandidateBuilder.ReadableCopy(original);mesh.ClearBlendShapes();
                    Matrix4x4[] transforms;
                    SkinnedMeshRenderer skinSource=skin;
                    if(part.shell)
                    {
                        string owner=Uri.UnescapeDataString(manifest.sourceNodes.Single(n=>n.id==part.sourceNode).parent.TrimStart('#'));
                        var parentPart=manifest.parts.Single(p=>p.sourceNode==owner);
                        skinSource=allSources.OfType<SkinnedMeshRenderer>().Single(r=>r.name==parentPart.node+".Shape");
                        if(part.points.Length!=parentPart.points.Length)throw new IOException("Shell control-point topology differs: "+part.node);
                        InheritWeights(mesh,rawMap,skinSource,parentPart);
                        transforms=Enumerable.Repeat(toBody,mesh.vertexCount).ToArray();
                    }
                    else transforms=LaraFirstOutfitBuilder.SkinTransforms(skin,toBody);
                    var vertices=original.vertices;var importedNormals=original.normals;var tangents=original.tangents;
                    mesh.vertices=vertices.Select((v,i)=>transforms[i].MultiplyPoint3x4(v)).ToArray();
                    mesh.normals=importedNormals.Select((n,i)=>transforms[i].inverse.transpose.MultiplyVector(n).normalized).ToArray();
                    mesh.tangents=tangents.Select((t,i)=>{var v=transforms[i].MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;return new Vector4(v.x,v.y,v.z,t.w);}).ToArray();
                    var bones=skinSource.bones.Select(b=>canonical.TryGetValue(b.name,out var target)?target:throw new IOException("Unknown wardrobe bone: "+b.name)).ToArray();
                    mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*body.transform.localToWorldMatrix).ToArray();
                    if(part.frames.Length!=0)throw new IOException("Second outfit acquired nonempty source morphs; implement their transfer explicitly.");
                    TransferBodyShapes(mesh,rawMap,part.points.Length,body.sharedMesh);
                    mesh.RecalculateBounds();mesh.name=part.node+" on canonical Lara";
                    var attachment=new GameObject(part.node);attachment.transform.SetParent(body.transform,false);
                    var renderer=attachment.AddComponent<SkinnedMeshRenderer>();renderer.rootBone=body.rootBone;
                    renderer.renderingLayerMask=body.renderingLayerMask;renderer.bones=bones;
                    renderer.sharedMesh=LaraCandidateBuilder.Save(mesh,Folder+"/Meshes/"+part.node+".asset");
                    renderer.sharedMaterials=LaraCandidateBuilder.MatchImportedSurfaces(source,LaraCandidateBuilder.ConvertMaterials(new LaraCandidateBuilder.Manifest{materials=part.materials},Folder+"/"+part.node,false));
                    var audit=new Audit{node=part.node,vertices=mesh.vertexCount,bones=bones.Length,materials=part.materials.Length,shell=part.shell};
                    var evaluated=new Mesh();renderer.BakeMesh(evaluated);
                    var actual=evaluated.vertices;var rest=mesh.vertices;
                    for(int i=0;i<rest.Length;i++)audit.maximumRestError=Mathf.Max(audit.maximumRestError,(actual[i]-rest[i]).magnitude);
                    UnityEngine.Object.DestroyImmediate(evaluated);
                    if(audit.maximumRestError>3e-5f)throw new IOException("Wardrobe rest skin mismatch: "+part.node+" "+audit.maximumRestError);
                    audits.Add(audit);inventory.Add(new LaraWardrobe.Piece{name=part.node,renderer=renderer,shell=part.shell});
                    Debug.Log("LARA_SECOND_PART: "+part.node+" / "+mesh.vertexCount+" vertices");
                }
                var outfit=performer.gameObject.AddComponent<LaraWardrobe>();outfit.Configure(body,inventory.ToArray());
                var shoes=inventory.Single(p=>p.name=="shoe_6146").renderer;var shoeMesh=new Mesh();shoes.BakeMesh(shoeMesh);
                float shoeFloor=shoeMesh.vertices.Min(v=>shoes.transform.TransformPoint(v).y);UnityEngine.Object.DestroyImmediate(shoeMesh);
                float height=Mathf.Max(0,(referenceFloor-shoeFloor)/rigRoot.lossyScale.y);
                var review=performer.gameObject.AddComponent<LaraHeelReview>();
                review.Configure(body,rigRoot,canonical["lFoot"],canonical["rFoot"],canonical["lToe"],canonical["rToe"],height);
                LaraSecondOutfitRepair.RepairLoaded();
                outfit.Apply();LaraFirstOutfitBuilder.RebindParticleMesh(performer,body,Folder);
                if(!outfit.Validate(out string reason))throw new IOException(reason);
                EnsureInspectionLight(performer.transform);
                LaraFirstOutfitBuilder.ConfigurePresentation();
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);AssetDatabase.SaveAssets();
                Directory.CreateDirectory("TestOutput/appearance-evidence/second-outfit");
                File.WriteAllText("TestOutput/appearance-evidence/second-outfit/integration.json","{\"standingHeightMeters\":"+height.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"parts\":["+string.Join(",",audits.Select(a=>JsonUtility.ToJson(a)))+"]}");
                Debug.Log("LARA_SECOND_OUTFIT_BUILT: "+ScenePath+"; standing height "+height);
            }
            finally{UnityEngine.Object.DestroyImmediate(donor);}
        }
        public static void FinishReviewAndRun()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            EnsureInspectionLight(UnityEngine.Object.FindAnyObjectByType<LaraWardrobe>().transform,true);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            LaraSecondOutfitValidation.Run();
        }
        internal static void EnsureInspectionLight(Transform owner,bool resetNewReview=false)
        {
            var existing=owner.Find("Outfit Inspection Light");
            if(existing!=null && !resetNewReview)return;
            var item=existing!=null?existing.gameObject:new GameObject("Outfit Inspection Light");item.transform.SetParent(owner,false);
            item.transform.rotation=Quaternion.LookRotation(new Vector3(-2,-3,-4));
            var light=item.GetComponent<Light>();if(light==null)light=item.AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;
            var hd=item.GetComponent<HDAdditionalLightData>();if(hd==null)hd=item.AddComponent<HDAdditionalLightData>();light.lightUnit=LightUnit.Lux;light.intensity=2000;hd.UpdateAllLightValues();
            typeof(HDAdditionalLightData).GetMethod("UpdateRenderEntity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(hd,null);
        }
        private static void InheritWeights(Mesh shell,int[] shellRaw,SkinnedMeshRenderer parent,Part parentPart)
        {
            var parentMap=LaraFirstOutfitBuilder.MapPoints(parent.sharedMesh.vertices,parentPart.points,parentPart.frames);
            var counts=parent.sharedMesh.GetBonesPerVertex();var weights=parent.sharedMesh.GetAllBoneWeights();
            var perPoint=new Dictionary<int,BoneWeight1[]>();int offset=0;
            try
            {
                for(int i=0;i<parentMap.Length;i++)
                {
                    var influence=new BoneWeight1[counts[i]];
                    for(int w=0;w<influence.Length;w++)influence[w]=weights[offset++];
                    if(perPoint.TryGetValue(parentMap[i],out var existing))
                    {
                        if(existing.Length!=influence.Length || existing.Where((w,j)=>w.boneIndex!=influence[j].boneIndex || Mathf.Abs(w.weight-influence[j].weight)>1e-6f).Any())
                            throw new IOException("Split parent point has inconsistent skin weights.");
                    }
                    else perPoint[parentMap[i]]=influence;
                }
            }
            finally{counts.Dispose();weights.Dispose();}
            var resultCounts=new NativeArray<byte>(shellRaw.Select(p=>(byte)perPoint[p].Length).ToArray(),Allocator.Temp);
            var resultWeights=new NativeArray<BoneWeight1>(shellRaw.SelectMany(p=>perPoint[p]).ToArray(),Allocator.Temp);
            try{shell.SetBoneWeights(resultCounts,resultWeights);}finally{resultCounts.Dispose();resultWeights.Dispose();}
        }
        private static void TransferBodyShapes(Mesh mesh,int[] raw,int pointCount,Mesh body)
        {
            var vertices=mesh.vertices;var bv=body.vertices;var bt=body.triangles;
            const float cellSize=.05f;
            Vector3Int Cell(Vector3 v)=>new Vector3Int(Mathf.FloorToInt(v.x/cellSize),Mathf.FloorToInt(v.y/cellSize),Mathf.FloorToInt(v.z/cellSize));
            var grid=new Dictionary<Vector3Int,List<int>>();
            for(int t=0;t<bt.Length;t+=3)
            {
                var a=bv[bt[t]];var b=bv[bt[t+1]];var c=bv[bt[t+2]];
                if(Mathf.Max(a.y,Mathf.Max(b.y,c.y))<.8f || Mathf.Min(a.y,Mathf.Min(b.y,c.y))>1.8f)continue;
                var low=Cell(Vector3.Min(a,Vector3.Min(b,c)));var high=Cell(Vector3.Max(a,Vector3.Max(b,c)));
                for(int x=low.x;x<=high.x;x++)for(int y=low.y;y<=high.y;y++)for(int z=low.z;z<=high.z;z++)
                {var key=new Vector3Int(x,y,z);if(!grid.TryGetValue(key,out var list))grid[key]=list=new List<int>();list.Add(t);}
            }
            var binding=new int[vertices.Length];Array.Fill(binding,-1);var barycentric=new Vector3[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                if(vertices[i].y<.85f || vertices[i].y>1.75f)continue;
                var cell=Cell(vertices[i]);float best=.01f;
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                    if(grid.TryGetValue(cell+new Vector3Int(x,y,z),out var list))foreach(int t in list)
                    {
                        var a=bv[bt[t]];var b=bv[bt[t+1]];var c=bv[bt[t+2]];
                        var bary=LaraFirstOutfitBuilder.ClosestBarycentric(vertices[i],a,b,c);
                        float distance=(vertices[i]-a*bary.x-b*bary.y-c*bary.z).sqrMagnitude;
                        if(distance>=best)continue;best=distance;binding[i]=t;barycentric[i]=bary;
                    }
            }
            var normals=LaraCandidateBuilder.SmoothPointNormals(vertices,mesh.triangles,raw,pointCount);
            foreach(string name in new[]{"LaraBreastsAdjustment","Genesis8Female__PBMNipples","Genesis8Female__EX_Breathe","Genesis8Female__EX_BreatheBelly"})
            {
                int index=body.GetBlendShapeIndex(name);if(index<0)throw new IOException("Approved body shape missing: "+name);
                var source=new Vector3[body.vertexCount];body.GetBlendShapeFrameVertices(index,0,source,null,null);
                var delta=new Vector3[vertices.Length];
                for(int i=0;i<vertices.Length;i++)if(binding[i]>=0)
                {int t=binding[i];var b=barycentric[i];delta[i]=source[bt[t]]*b.x+source[bt[t+1]]*b.y+source[bt[t+2]]*b.z;}
                if(delta.Any(v=>v.sqrMagnitude>1e-12f))LaraFirstOutfitBuilder.AddFrame(mesh,name,delta,vertices,mesh.triangles,raw,pointCount,normals);
            }
        }
        private static string Hash(string path)
        {using var sha=SHA256.Create();using var file=File.OpenRead(path);return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
    }
}
