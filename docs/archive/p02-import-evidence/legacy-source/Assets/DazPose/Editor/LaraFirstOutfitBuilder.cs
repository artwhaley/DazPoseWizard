using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using UnityEngine.Rendering.HighDefinition;

namespace DazPose.UnityValidation
{
    public static class LaraFirstOutfitBuilder
    {
        public const string Folder=LaraCandidateBuilder.Folder + "/FirstOutfit";
        public const string ScenePath=Folder + "/FirstPerformanceVoidLaraFirstOutfit.unity";
        [InitializeOnLoadMethod]
        private static void RefreshOpenReviewScene()
        {
            EditorApplication.delayCall+=()=>
            {
                if(Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)return;
                var current=UnityEngine.Object.FindAnyObjectByType<LaraFirstOutfit>();if(current==null)return;
                ConfigurePresentation();current.Apply();EditorSceneManager.MarkSceneDirty(current.gameObject.scene);
            };
        }
        [Serializable] private class Manifest { public string asset,sha256,duf,dufSHA256; public Part[] parts; }
        [Serializable] private class Part
        {
            public string node,mesh,kind; public LaraCandidateBuilder.Point[] points;
            public LaraCandidateBuilder.Frame[] frames; public LaraCandidateBuilder.Surface[] materials;
        }
        [Serializable] private class Audit
        {
            public string node; public int vertices,canonicalBones,localBones,channels,materials;
            public float maximumRestError,maximumBreastTransfer;
        }
        [MenuItem("Tools/DAZ Pose/Development/Open Lara First Outfit")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if(File.Exists(ScenePath))EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);else Build();
            ConfigurePresentation();
            Selection.activeGameObject=UnityEngine.Object.FindAnyObjectByType<LaraFirstOutfit>().gameObject;
        }
        public static void Build()
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(LaraCandidateBuilder.Folder + "/wardrobe-manifest.json"));
            if(Hash(manifest.asset)!=manifest.sha256 || Hash(manifest.duf)!=manifest.dufSHA256) throw new IOException("Wardrobe inputs changed; prepare the wardrobe manifest again.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(LaraCandidateInstaller.ValidationScene,OpenSceneMode.Single);
            var performer=UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            var body=performer.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.sharedMesh!=null && r.sharedMesh.GetBlendShapeIndex("CapturedOpening")>=0);
            // Copy the approved body; coverage adds only a spare UV channel.
            var bodyMesh=LaraCandidateBuilder.ReadableCopy(body.sharedMesh);
            CopyFrames(body.sharedMesh,bodyMesh);
            body.sharedMesh=LaraCandidateBuilder.Save(bodyMesh,Folder + "/LaraBodyWithCoverage.asset");
            var donor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(manifest.asset));
            donor.name="Temporary attachment donor";
            var donorBody=LaraCandidateBuilder.Body(donor);
            var canonical=body.bones.Where(b=>b!=null).GroupBy(b=>b.name).ToDictionary(g=>g.Key,g=>g.Single());
            var renderers=new Dictionary<string,SkinnedMeshRenderer>(); var audits=new List<Audit>();
            foreach(var part in manifest.parts)
            {
                var source=donor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.sharedMesh.name==part.node || r.name==part.node+".Shape");
                var audit=new Audit { node=part.node,vertices=source.sharedMesh.vertexCount,materials=part.materials.Length };
                var item=body.transform.Find(part.node);
                if(item!=null) UnityEngine.Object.DestroyImmediate(item.gameObject);
                var attachment=new GameObject(part.node); attachment.transform.SetParent(body.transform,false);
                var renderer=attachment.AddComponent<SkinnedMeshRenderer>();
                renderer.rootBone=body.rootBone; renderer.quality=SkinQuality.Auto; renderer.renderingLayerMask=body.renderingLayerMask;
                var mesh=LaraCandidateBuilder.ReadableCopy(source.sharedMesh); mesh.ClearBlendShapes();
                var points=MapPoints(source.sharedMesh.vertices,part.points,part.frames);
                var toBody=donorBody.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;
                var vertexTransforms=SkinTransforms(source,toBody);
                mesh.vertices=source.sharedMesh.vertices.Select((v,i)=>vertexTransforms[i].MultiplyPoint3x4(v)).ToArray();
                mesh.normals=source.sharedMesh.normals.Select((v,i)=>vertexTransforms[i].inverse.transpose.MultiplyVector(v).normalized).ToArray();
                var sourceTangents=source.sharedMesh.tangents;
                mesh.tangents=sourceTangents.Select((v,i)=> { var t=vertexTransforms[i].MultiplyVector(new Vector3(v.x,v.y,v.z)).normalized;return new Vector4(t.x,t.y,t.z,v.w); }).ToArray();
                var bones=new Transform[source.bones.Length];
                for(int b=0;b<bones.Length;b++)
                {
                    if(canonical.TryGetValue(source.bones[b].name,out bones[b])) { audit.canonicalBones++;continue; }
                    if(part.kind!="hair" || !new[] { "leftpiggy","rightpiggy" }.Contains(source.bones[b].name)) throw new IOException("Unexpected attachment-local bone "+source.bones[b].name);
                    var parent=canonical["head"];
                    var child=parent.Find("Emiko_"+source.bones[b].name);
                    if(child==null) { child=new GameObject("Emiko_"+source.bones[b].name).transform; child.SetParent(parent,false); }
                    var local=parent.worldToLocalMatrix*body.transform.localToWorldMatrix*donorBody.transform.worldToLocalMatrix*source.bones[b].localToWorldMatrix;
                    child.localPosition=local.GetColumn(3);child.localRotation=local.rotation;child.localScale=local.lossyScale;
                    bones[b]=child;audit.localBones++;
                }
                mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*body.transform.localToWorldMatrix).ToArray();
                AddFrames(mesh,part,points,vertexTransforms,body.sharedMesh,audit);
                mesh.RecalculateBounds(); mesh.name=part.node+" on canonical Lara";
                renderer.sharedMesh=LaraCandidateBuilder.Save(mesh,Folder+"/"+part.node+".asset");renderer.bones=bones;renderer.localBounds=renderer.sharedMesh.bounds;
                renderer.sharedMaterials=LaraCandidateBuilder.ConvertMaterials(new LaraCandidateBuilder.Manifest { materials=part.materials },Folder+"/"+part.node,false);
                var evaluated=new Mesh();renderer.BakeMesh(evaluated);
                var rest=renderer.sharedMesh.vertices;var actual=evaluated.vertices;
                for(int i=0;i<rest.Length;i++) audit.maximumRestError=Mathf.Max(audit.maximumRestError,(rest[i]-actual[i]).magnitude);
                UnityEngine.Object.DestroyImmediate(evaluated);
                if(audit.maximumRestError>3e-5f) throw new IOException("Rest skinning mismatch for "+part.node+": "+audit.maximumRestError);
                audit.channels=renderer.sharedMesh.blendShapeCount;audits.Add(audit);renderers[part.kind=="hair"?"hair":part.node.StartsWith("Peeka")?"dress":"panties"]=renderer;
            }
            UnityEngine.Object.DestroyImmediate(donor);
            AddUnderwearCoverage(body.sharedMesh,renderers["panties"].sharedMesh);
            AddDressCoverage(body.sharedMesh,renderers["dress"].sharedMesh);
            EditorUtility.SetDirty(body.sharedMesh);
            var outfit=performer.GetComponent<LaraFirstOutfit>() ?? performer.gameObject.AddComponent<LaraFirstOutfit>();
            outfit.Configure(body,renderers["hair"],renderers["dress"],renderers["panties"]);
            ConfigurePresentation();
            if(!outfit.Validate(out string reason)) throw new IOException(reason);
            // Body vertex/triangle identities are unchanged by the spare UV; reuse exact binding data,
            // updating only the mesh identity in the copied profile/binding asset.
            RebindParticleMesh(performer,body);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("TestOutput/appearance-evidence");
            File.WriteAllText("TestOutput/appearance-evidence/first-outfit.integration.json","["+string.Join(",",audits.Select(a=>JsonUtility.ToJson(a,true)))+"]");
            Debug.Log("LARA_FIRST_OUTFIT_BUILT: "+ScenePath);
        }
        internal static void CopyFrames(Mesh source,Mesh target,string skip=null)
        {
            var p=new Vector3[source.vertexCount];var n=new Vector3[source.vertexCount];var t=new Vector3[source.vertexCount];
            for(int s=0;s<source.blendShapeCount;s++) if(source.GetBlendShapeName(s)!=skip)for(int f=0;f<source.GetBlendShapeFrameCount(s);f++)
            { source.GetBlendShapeFrameVertices(s,f,p,n,t);target.AddBlendShapeFrame(source.GetBlendShapeName(s),source.GetBlendShapeFrameWeight(s,f),p,n,t); }
        }
        public static void ConfigurePresentation()
        {
            // HDRP exposes Scene-view AA through an internal cached preference class.
            var sceneSettings=typeof(HDAdditionalCameraData).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalSceneViewSettings");
            sceneSettings?.GetProperty("sceneViewAntialiasing",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)?.SetValue(null,HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing);
            foreach(SceneView view in SceneView.sceneViews){view.sceneViewState.alwaysRefresh=true;view.Repaint();}
            foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>())
            {
                var hd=camera.GetComponent<HDAdditionalCameraData>();if(hd==null)continue;
                hd.antialiasing=HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                hd.TAAQuality=HDAdditionalCameraData.TAAQualityLevel.High;
                hd.taaSharpenStrength=.15f;hd.taaHistorySharpening=.2f;hd.taaAntiHistoryRinging=true;
                hd.allowDynamicResolution=false;hd.dithering=true;EditorUtility.SetDirty(hd);
            }
        }
        public static void UpdateExisting()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var outfit=UnityEngine.Object.FindAnyObjectByType<LaraFirstOutfit>();
            var body=outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.sharedMesh.GetBlendShapeIndex("CapturedOpening")>=0);
            AddDressCoverage(body.sharedMesh,outfit.Dress.sharedMesh);EditorUtility.SetDirty(body.sharedMesh);
            outfit.Apply();ConfigurePresentation();AssetDatabase.SaveAssets();
            // Do not save the scene here: the caller may be updating only owned assets.
        }
        internal static Matrix4x4[] SkinTransforms(SkinnedMeshRenderer source,Matrix4x4 toBody)
        {
            var mesh=source.sharedMesh;var bind=mesh.bindposes;
            var boneMatrices=source.bones.Select((b,i)=>toBody*source.transform.worldToLocalMatrix*b.localToWorldMatrix*bind[i]).ToArray();
            var counts=mesh.GetBonesPerVertex();var weights=mesh.GetAllBoneWeights();var result=new Matrix4x4[mesh.vertexCount];int offset=0;
            try { for(int i=0;i<result.Length;i++) { var matrix=Matrix4x4.zero;for(int j=0;j<counts[i];j++) { var w=weights[offset++];var m=boneMatrices[w.boneIndex];for(int a=0;a<16;a++) matrix[a]+=m[a]*w.weight; }result[i]=matrix; } }
            finally { counts.Dispose();weights.Dispose(); } return result;
        }
        internal static int[] MapPoints(Vector3[] vertices,LaraCandidateBuilder.Point[] points,LaraCandidateBuilder.Frame[] frames)
        {
            const float size=2e-6f;
            Vector3Int Cell(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/size),Mathf.FloorToInt(p.y/size),Mathf.FloorToInt(p.z/size));
            var grid=points.GroupBy(p=>Cell(p.position)).ToDictionary(g=>g.Key,g=>g.ToArray());
            var deltas=frames.Select(f=>f.entries.ToDictionary(e=>e.index,e=>e.delta)).ToArray();var result=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var candidates=new List<LaraCandidateBuilder.Point>();var cell=Cell(vertices[i]);
                for(int x=-1;x<=1;x++) for(int y=-1;y<=1;y++) for(int z=-1;z<=1;z++)
                    if(grid.TryGetValue(cell+new Vector3Int(x,y,z),out var group)) candidates.AddRange(group.Where(p=>(p.position-vertices[i]).sqrMagnitude<=size*size));
                if(candidates.Count==0) throw new IOException("Attachment raw point not found: "+i);
                foreach(var other in candidates.Skip(1)) foreach(var map in deltas)
                { map.TryGetValue(candidates[0].index,out var a);map.TryGetValue(other.index,out var b);if((a-b).sqrMagnitude>1e-12f) throw new IOException("Ambiguous attachment point morph data."); }
                result[i]=candidates[0].index;
            }
            return result;
        }
        private static void AddFrames(Mesh mesh,Part part,int[] points,Matrix4x4[] transforms,Mesh body,Audit audit)
        {
            var vertices=mesh.vertices;var triangles=mesh.triangles;
            var normals=LaraCandidateBuilder.SmoothPointNormals(vertices,triangles,points,part.points.Length);
            foreach(var frame in part.frames)
            {
                var entries=frame.entries.ToDictionary(e=>e.index,e=>e.delta);
                var delta=points.Select((p,i)=>entries.TryGetValue(p,out var value)?transforms[i].MultiplyVector(value):Vector3.zero).ToArray();
                string name="Genesis8Female__"+frame.name.Substring(frame.name.IndexOf("__",StringComparison.Ordinal)+2);
                AddFrame(mesh,name,delta,vertices,triangles,points,part.points.Length,normals);
            }
            if(part.kind=="hair") return;
            // The existing FBX lacks this native slider. Transfer its approved body displacement
            // with a nearest-surface barycentric binding; the fit remains a visual review checkpoint.
            var bodyDelta=new Vector3[body.vertexCount];body.GetBlendShapeFrameVertices(body.GetBlendShapeIndex("LaraBreastsAdjustment"),0,bodyDelta,null,null);
            var bodyVertices=body.vertices;var bodyTriangles=body.triangles;var breast=new Vector3[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                if(vertices[i].y<1.15f || vertices[i].y>1.65f) continue;
                float best=float.PositiveInfinity;
                for(int t=0;t<bodyTriangles.Length;t+=3)
                {
                    int a=bodyTriangles[t],b=bodyTriangles[t+1],c=bodyTriangles[t+2];
                    if(bodyVertices[a].y<1.05f || bodyVertices[a].y>1.7f) continue;
                    Vector3 bary=ClosestBarycentric(vertices[i],bodyVertices[a],bodyVertices[b],bodyVertices[c]);
                    Vector3 surface=bodyVertices[a]*bary.x+bodyVertices[b]*bary.y+bodyVertices[c]*bary.z;
                    float distance=(vertices[i]-surface).sqrMagnitude;
                    if(distance>=best) continue;best=distance;breast[i]=bodyDelta[a]*bary.x+bodyDelta[b]*bary.y+bodyDelta[c]*bary.z;
                }
                audit.maximumBreastTransfer=Mathf.Max(audit.maximumBreastTransfer,breast[i].magnitude);
            }
            AddFrame(mesh,"LaraBreastsAdjustment",breast,vertices,triangles,points,part.points.Length,normals);
        }
        internal static void AddFrame(Mesh mesh,string name,Vector3[] delta,Vector3[] vertices,int[] triangles,int[] points,int pointCount,Vector3[] baseNormals)
        {
            var target=LaraCandidateBuilder.SmoothPointNormals(vertices.Select((v,i)=>v+delta[i]).ToArray(),triangles,points,pointCount);
            var normals=target.Select((n,i)=>n-baseNormals[i]).ToArray();mesh.AddBlendShapeFrame(name,100,delta,normals,null);
        }
        internal static Vector3 ClosestBarycentric(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0 && d2<=0)return new Vector3(1,0,0);
            Vector3 bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);
            if(d3>=0 && d4<=d3)return new Vector3(0,1,0);
            float vc=d1*d4-d3*d2;if(vc<=0 && d1>=0 && d3<=0) { float v=d1/(d1-d3);return new Vector3(1-v,v,0); }
            Vector3 cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);
            if(d6>=0 && d5<=d6)return new Vector3(0,0,1);
            float vb=d5*d2-d1*d6;if(vb<=0 && d2>=0 && d6<=0) { float w=d2/(d2-d6);return new Vector3(1-w,0,w); }
            float va=d3*d6-d5*d4;if(va<=0 && d4-d3>=0 && d5-d6>=0) { float w=(d4-d3)/((d4-d3)+(d5-d6));return new Vector3(0,1-w,w); }
            float inverse=1/(va+vb+vc),y=vb*inverse,z=vc*inverse;return new Vector3(1-y-z,y,z);
        }
        internal static void AddUnderwearCoverage(Mesh mesh,Mesh panties)
        {
            var coverage=new Vector2[mesh.vertexCount];var vertices=mesh.vertices;
            var cloth=panties.vertices;var triangles=panties.triangles;
            // A front-facing ray must meet the actual opaque front panel. Never erase
            // the exposed rear of the graft simply because it belongs to the same surface.
            foreach(int i in mesh.GetIndices(16))
            {
                var p=vertices[i];
                if(Mathf.Abs(p.x)>.065f || p.y>.98f || mesh.normals[i].z<-.05f)continue;
                for(int t=0;t<triangles.Length;t+=3)
                {
                    var a=cloth[triangles[t]];var b=cloth[triangles[t+1]];var c=cloth[triangles[t+2]];
                    if(Vector3.Cross(b-a,c-a).z<=0)continue;
                    float denominator=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
                    if(Mathf.Abs(denominator)<1e-10f)continue;
                    float u=((b.y-c.y)*(p.x-c.x)+(c.x-b.x)*(p.y-c.y))/denominator;
                    float v=((c.y-a.y)*(p.x-c.x)+(a.x-c.x)*(p.y-c.y))/denominator;
                    if(u<0 || v<0 || u+v>1)continue;
                    float z=u*a.z+v*b.z+(1-u-v)*c.z;
                    if(z>=p.z-.003f) { coverage[i]=Vector2.right;break; }
                }
            }
            // These surfaces are internal to the covered opening. Preserve the anus
            // and rear torso, which may remain visible with a revealing garment.
            foreach(int slot in new[] {17,18})foreach(int i in mesh.GetIndices(slot))coverage[i]=Vector2.right;
            mesh.SetUVs(3,coverage);
        }
        internal static void AddDressCoverage(Mesh body,Mesh dress)
        {
            const float cellSize=.04f,edgeMargin=.01f;
            Vector3Int Cell(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/cellSize),Mathf.FloorToInt(p.y/cellSize),Mathf.FloorToInt(p.z/cellSize));
            var cloth=dress.vertices;var triangles=dress.triangles;
            var grid=new Dictionary<Vector3Int,List<int>>();
            var pointIds=new int[cloth.Length];var welded=new Dictionary<Vector3Int,int>();
            for(int i=0;i<cloth.Length;i++)
            { var key=Vector3Int.RoundToInt(cloth[i]*1000000);if(!welded.TryGetValue(key,out int id))welded[key]=id=welded.Count;pointIds[i]=id; }
            var edges=new Dictionary<(int,int),(int count,int a,int b)>();
            for(int t=0;t<triangles.Length;t+=3)
            {
                var a=cloth[triangles[t]];var b=cloth[triangles[t+1]];var c=cloth[triangles[t+2]];
                var low=Cell(Vector3.Min(a,Vector3.Min(b,c)));var high=Cell(Vector3.Max(a,Vector3.Max(b,c)));
                for(int x=low.x;x<=high.x;x++)for(int y=low.y;y<=high.y;y++)for(int z=low.z;z<=high.z;z++)
                { var key=new Vector3Int(x,y,z);if(!grid.TryGetValue(key,out var list))grid[key]=list=new List<int>();list.Add(t); }
                for(int e=0;e<3;e++)
                {
                    int ia=triangles[t+e],ib=triangles[t+(e+1)%3],pa=pointIds[ia],pb=pointIds[ib];var key=(Math.Min(pa,pb),Math.Max(pa,pb));
                    edges.TryGetValue(key,out var value);edges[key]=(value.count+1,ia,ib);
                }
            }
            var boundaries=edges.Values.Where(e=>e.count==1).ToArray();
            var coverage=new List<Vector2>();body.GetUVs(3,coverage);if(coverage.Count!=body.vertexCount)coverage=Enumerable.Repeat(Vector2.zero,body.vertexCount).ToList();
            var vertices=body.vertices;var normals=body.normals;int hidden=0;
            for(int i=0;i<vertices.Length;i++)
            {
                coverage[i]=new Vector2(coverage[i].x,0);var p=vertices[i];var direction=normals[i].normalized;var cell=Cell(p);var candidates=new HashSet<int>();
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                    if(grid.TryGetValue(cell+new Vector3Int(x,y,z),out var list))candidates.UnionWith(list);
                foreach(int t in candidates)
                {
                    var a=cloth[triangles[t]];var b=cloth[triangles[t+1]];var c=cloth[triangles[t+2]];var ab=b-a;var ac=c-a;
                    if(Vector3.Dot(Vector3.Cross(ab,ac).normalized,direction)<.3f)continue;
                    var h=Vector3.Cross(direction,ac);float determinant=Vector3.Dot(ab,h);if(Mathf.Abs(determinant)<1e-9f)continue;
                    float inverse=1/determinant;var s=p-a;float u=inverse*Vector3.Dot(s,h);if(u<0 || u>1)continue;
                    var q=Vector3.Cross(s,ab);float v=inverse*Vector3.Dot(direction,q);if(v<0 || u+v>1)continue;
                    float distance=inverse*Vector3.Dot(ac,q);if(distance<-.012f || distance>.035f)continue;
                    var hit=p+direction*distance;
                    bool nearEdge=boundaries.Any(e=>
                    { var start=cloth[e.a];var edge=cloth[e.b]-start;float f=Mathf.Clamp01(Vector3.Dot(hit-start,edge)/Mathf.Max(edge.sqrMagnitude,1e-12f));return (hit-start-edge*f).sqrMagnitude<edgeMargin*edgeMargin; });
                    if(nearEdge)continue;
                    coverage[i]=new Vector2(coverage[i].x,1);hidden++;break;
                }
            }
            body.SetUVs(3,coverage);Debug.Log("LARA_DRESS_COVERAGE: "+hidden+" vertices; preserve a 10 mm border at garment openings.");
        }
        internal static void RebindParticleMesh(SuccubusPerformer performer,SkinnedMeshRenderer body,string folder=Folder)
        {
            var bindings=DazPose.Editor.ParticleBody.PerformerSurfaceBindingBaker.Bake(body,PerformerSurfaceBindingAsset.RequiredBindingCount,0x504f3942,folder+"/SurfaceBindings.asset");
            var rig=performer.GetComponent<PerformerDissolveRig>();var particle=rig.ParticleBody;
            var particleData=new SerializedObject(particle);particleData.FindProperty("surfaceBindings").objectReferenceValue=bindings;particleData.ApplyModifiedPropertiesWithoutUndo();
            var performerData=new SerializedObject(performer);var old=performerData.FindProperty("dissolveProfile").objectReferenceValue as PerformerDissolveProfile;
            var profile=LaraCandidateBuilder.Save(UnityEngine.Object.Instantiate(old),folder+"/DissolveProfile.asset");profile.ConfigureParticleBodyAssets(particle.VisualEffectAsset,bindings);
            performerData.FindProperty("dissolveProfile").objectReferenceValue=profile;performerData.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(profile);
            if(!rig.IsReady(profile,out string reason))throw new IOException(reason);
        }
        private static string Hash(string path) { using var sha=SHA256.Create();using var file=File.OpenRead(path);return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant(); }
    }
}
