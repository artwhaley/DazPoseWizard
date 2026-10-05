using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Recipe-driven import into an isolated project; equipment assets are separate from review scenes.</summary>
    public static class WardrobeImporter
    {
        [Serializable] public sealed class Audit
        {
            public string node,role,coverage;
            public int vertices,bones,localBones,materials,sourceMorphs,channels;
            public bool shell;public float restErrorMeters;
        }
        [Serializable] public sealed class Report
        {
            public string id,status,recipeHash,fbxHash,dufHash,definition,reviewScene;
            public int characterChannels; public float shoeHeightMeters;
            public Audit[] pieces;public string[] warnings;
        }
        public static string Hash(string path)
        {using var sha=SHA256.Create();using var file=File.OpenRead(path);return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
        public static void Build()
        {
            if(!Application.isBatchMode)throw new IOException("Use scripts/import-lara-outfit.ps1 so the user's scene remains untouched.");
            var recipe=WardrobeImportRecipe.Read();Directory.CreateDirectory(recipe.Output);
            try{Build(recipe);}catch(Exception error)
            {
                File.WriteAllText(recipe.Output+"/failure.txt",error.ToString());
                Debug.LogException(error);throw;
            }
        }
        private static void Build(WardrobeImportRecipe recipe)
        {
            var manifest=JsonUtility.FromJson<WardrobeImportRecipe.Manifest>(File.ReadAllText(recipe.destination+"/wardrobe-manifest.json"));
            if(Hash(manifest.asset)!=manifest.sha256 || Hash(manifest.duf)!=manifest.dufSHA256)throw new IOException("Source hashes changed; regenerate the manifest.");
            if(manifest.asset!=recipe.fbx)throw new IOException("Recipe/manifest FBX differs.");
            foreach(var policy in recipe.policies)
                if(!manifest.parts.Any(p=>p.node==policy.node))throw new IOException("Recipe piece is absent: "+policy.node);
            Directory.CreateDirectory(recipe.destination+"/Meshes");AssetDatabase.Refresh();
            // Always regenerate the disposable isolated host from the approved character.
            // Artist materials/profile live in persistent assets, never in this disposable scene.
            EditorSceneManager.OpenScene(recipe.characterScene,OpenSceneMode.Single);
            var performer=UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            var body=performer.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.sharedMesh!=null && r.sharedMesh.GetBlendShapeIndex("CapturedOpening")>=0);
            var characterReference=body.sharedMesh;
            var points=JsonUtility.FromJson<LaraCandidateBuilder.Manifest>(File.ReadAllText(recipe.characterPoints));
            var bodyMesh=LaraCandidateBuilder.ReadableCopy(body.sharedMesh);LaraFirstOutfitBuilder.CopyFrames(body.sharedMesh,bodyMesh);
            var rawBody=LaraFirstOutfitBuilder.MapPoints(bodyMesh.vertices,points.points,Array.Empty<LaraCandidateBuilder.Frame>());
            var canonical=body.bones.ToDictionary(b=>b.name);
            var rigRoot=body.rootBone.parent;
            float referenceFloor=Floor(body);
            body.sharedMesh=LaraCandidateBuilder.Save(bodyMesh,recipe.destination+"/ReviewBody.asset");
            var donor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(manifest.asset));
            donor.name="Wardrobe Import Temporary Donor";
            try
            {
                var donorBody=LaraCandidateBuilder.Body(donor);
                if(recipe.shoes!=null && !string.IsNullOrEmpty(recipe.shoes.node))
                    CaptureHeel(recipe,manifest,body,donorBody,canonical,rawBody,points.points.Length);
                var sources=donor.GetComponentsInChildren<Renderer>(true);
                var inventory=new List<LaraWardrobe.Piece>();var equipment=new List<WardrobeOutfitDefinition.Piece>();var audits=new List<Audit>();var warnings=new List<string>();
                foreach(var part in manifest.parts)
                {
                    var policy=recipe.policies.SingleOrDefault(p=>p.node==part.node)??new WardrobeImportRecipe.Policy{node=part.node,role=part.kind};
                    if(policy.coverage!="none" && policy.coverage!="opaque-front" && policy.coverage!="opaque-envelope")throw new IOException("Unknown coverage policy: "+part.node);
                    if(policy.coverage!="none" && part.shell)throw new IOException("Shell cannot own body coverage: "+part.node);
                    var source=sources.Single(r=>r.name==part.node+".Shape");
                    var skin=source as SkinnedMeshRenderer;var original=skin!=null?skin.sharedMesh:source.GetComponent<MeshFilter>().sharedMesh;
                    var raw=LaraFirstOutfitBuilder.MapPoints(original.vertices,part.points,part.frames);
                    var toBody=donorBody.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;
                    var mesh=LaraCandidateBuilder.ReadableCopy(original);mesh.ClearBlendShapes();
                    var skinSource=skin;Matrix4x4[] transforms;
                    if(part.shell)
                    {
                        string owner=Uri.UnescapeDataString(manifest.sourceNodes.Single(n=>n.id==part.sourceNode).parent.TrimStart('#'));
                        var parentPart=manifest.parts.Single(p=>p.sourceNode==owner);
                        skinSource=sources.OfType<SkinnedMeshRenderer>().Single(r=>r.name==parentPart.node+".Shape");
                        if(part.points.Length!=parentPart.points.Length)throw new IOException("Shell control-point topology differs: "+part.node);
                        WardrobeGeometry.InheritWeights(mesh,raw,skinSource,parentPart);transforms=Enumerable.Repeat(toBody,mesh.vertexCount).ToArray();
                    }
                    else
                    {
                        if(skin==null)throw new IOException("Unskinned non-shell needs an explicit binding policy: "+part.node);
                        transforms=LaraFirstOutfitBuilder.SkinTransforms(skin,toBody);
                    }
                    mesh.vertices=original.vertices.Select((v,i)=>transforms[i].MultiplyPoint3x4(v)).ToArray();
                    mesh.normals=original.normals.Select((n,i)=>transforms[i].inverse.transpose.MultiplyVector(n).normalized).ToArray();
                    mesh.tangents=original.tangents.Select((t,i)=>{var v=transforms[i].MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;return new Vector4(v.x,v.y,v.z,t.w);}).ToArray();
                    var local=new Dictionary<Transform,Transform>();var localData=new List<WardrobeOutfitDefinition.LocalBone>();
                    Transform Resolve(Transform bone)
                    {
                        if(canonical.TryGetValue(bone.name,out var target))return target;
                        if(local.TryGetValue(bone,out target))return target;
                        if(!policy.allowLocalBones || bone.parent==null)throw new IOException("Unresolved local bone parent: "+part.node+" / "+bone.name);
                        var parent=Resolve(bone.parent);
                        target=new GameObject(part.node+"::"+bone.name).transform;target.SetParent(parent,false);
                        var matrix=parent.worldToLocalMatrix*body.transform.localToWorldMatrix*donorBody.transform.worldToLocalMatrix*bone.localToWorldMatrix;
                        target.localPosition=matrix.GetColumn(3);target.localRotation=matrix.rotation;target.localScale=matrix.lossyScale;
                        local[bone]=target;localData.Add(new WardrobeOutfitDefinition.LocalBone{name=target.name,parent=parent.name,position=target.localPosition,rotation=target.localRotation,scale=target.localScale});
                        return target;
                    }
                    var bones=skinSource.bones.Select(Resolve).ToArray();
                    mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*body.transform.localToWorldMatrix).ToArray();
                    AddSourceShapes(recipe,part,mesh,raw,transforms,body.sharedMesh,warnings);
                    bool rigidFootwear=recipe.shoes!=null && recipe.shoes.node==part.node && recipe.shoes.articulation=="rigid-pair";
                    if(policy.transferBodyMorphs && part.kind!="hair" && !rigidFootwear)WardrobeGeometry.TransferBodyShapes(mesh,raw,part.points.Length,body.sharedMesh);
                    mesh.RecalculateBounds();mesh.name=part.node+" on canonical Lara";
                    var attachment=new GameObject(part.node);attachment.transform.SetParent(body.transform,false);
                    var renderer=attachment.AddComponent<SkinnedMeshRenderer>();renderer.rootBone=body.rootBone;renderer.bones=bones;renderer.renderingLayerMask=body.renderingLayerMask;
                    renderer.sharedMesh=LaraCandidateBuilder.Save(mesh,recipe.destination+"/Meshes/"+Safe(part.node)+".asset");
                    mesh=renderer.sharedMesh;
                    renderer.sharedMaterials=LaraCandidateBuilder.MatchImportedSurfaces(source,LaraCandidateBuilder.ConvertMaterials(new LaraCandidateBuilder.Manifest{materials=part.materials},recipe.destination+"/Pieces/"+Safe(part.node),false,true));
                    var audit=new Audit{node=part.node,role=policy.role??part.kind,coverage=policy.coverage,vertices=mesh.vertexCount,bones=bones.Length,localBones=local.Count,materials=part.materials.Length,sourceMorphs=part.frames.Length,channels=mesh.blendShapeCount,shell=part.shell};
                    var baked=new Mesh();renderer.BakeMesh(baked);var actual=baked.vertices;var rest=mesh.vertices;
                    for(int i=0;i<actual.Length;i++)audit.restErrorMeters=Mathf.Max(audit.restErrorMeters,(actual[i]-rest[i]).magnitude);
                    UnityEngine.Object.DestroyImmediate(baked);
                    if(audit.restErrorMeters>3e-5f)throw new IOException("Rest skinning mismatch: "+part.node+" / "+audit.restErrorMeters);
                    int channel=policy.coverage=="opaque-front"?0:policy.coverage=="opaque-envelope"?1:-1;
                    if(channel>=0 && inventory.Any(p=>p.coverageChannel==channel))throw new IOException("Only one coverage owner per channel is supported; combine or review policies explicitly.");
                    inventory.Add(new LaraWardrobe.Piece{name=part.node,renderer=renderer,shell=part.shell,coverageChannel=channel});audits.Add(audit);
                    bool bentFootDependency = policy.requiresBentFootPose ||
                        string.Equals(audit.role,"footwear",StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(audit.role,"hosiery",StringComparison.OrdinalIgnoreCase);
                    equipment.Add(new WardrobeOutfitDefinition.Piece{id=part.node,sourceNode=part.sourceNode,role=audit.role,shell=part.shell,requiresBentFootPose=bentFootDependency,coverageChannel=channel,mesh=renderer.sharedMesh,materials=renderer.sharedMaterials,boneNames=bones.Select(b=>b.name).ToArray(),localBones=localData.ToArray()});
                    if(part.coveragePolicy=="review-transparency")warnings.Add("Review saved opacity/layering: "+part.node);
                }
                var wardrobe=performer.gameObject.AddComponent<LaraWardrobe>();wardrobe.Configure(body,inventory.ToArray());
                foreach(var piece in inventory)
                {
                    if(piece.coverageChannel==0)LaraFirstOutfitBuilder.AddUnderwearCoverage(body.sharedMesh,piece.renderer.sharedMesh);
                    if(piece.coverageChannel==1)LaraFirstOutfitBuilder.AddDressCoverage(body.sharedMesh,piece.renderer.sharedMesh);
                }
                EditorUtility.SetDirty(body.sharedMesh);
                PerformerFootwearProfile footwear=null;
                if(recipe.shoes!=null && !string.IsNullOrEmpty(recipe.shoes.node))
                    footwear=BuildFootwear(recipe,wardrobe,referenceFloor,canonical,rigRoot);
                wardrobe.Apply();if(!wardrobe.Validate(out string reason))throw new IOException(reason);
                LaraFirstOutfitBuilder.RebindParticleMesh(performer,body,recipe.destination);
                var definition=ScriptableObject.CreateInstance<WardrobeOutfitDefinition>();definition.id=recipe.id;
                definition.sourceFbxHash=manifest.sha256;definition.sourceDufHash=manifest.dufSHA256;definition.recipeHash=Hash(WardrobeImportRecipe.Argument("-wardrobeRecipe"));
                definition.characterReference=characterReference;definition.reviewBody=body.sharedMesh;definition.footwear=footwear;definition.pieces=equipment.ToArray();
                LaraCandidateBuilder.Save(definition,recipe.destination+"/Outfit.asset");
                // The source body/rig is import evidence only. It must never be serialized
                // into the review host or render beneath the approved character.
                UnityEngine.Object.DestroyImmediate(donor);donor=null;
                LaraFirstOutfitBuilder.ConfigurePresentation();LaraSecondOutfitBuilder.EnsureInspectionLight(performer.transform);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),recipe.Scene);AssetDatabase.SaveAssets();
                var report=new Report{id=recipe.id,status="built-awaiting-validation",recipeHash=Hash(WardrobeImportRecipe.Argument("-wardrobeRecipe")),fbxHash=manifest.sha256,dufHash=manifest.dufSHA256,definition=recipe.destination+"/Outfit.asset",reviewScene=recipe.Scene,characterChannels=body.sharedMesh.blendShapeCount,shoeHeightMeters=footwear!=null?footwear.standingHeight:0,pieces=audits.ToArray(),warnings=warnings.Distinct().ToArray()};
                File.WriteAllText(recipe.Output+"/import.json",JsonUtility.ToJson(report,true));Debug.Log("WARDROBE_IMPORTED: "+recipe.id+" / "+audits.Count+" pieces");
            }
            finally{if(donor!=null)UnityEngine.Object.DestroyImmediate(donor);}
        }
        public static string Safe(string name)=>string.Concat(name.Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_'))+"_"+ShortHash(name);
        private static string ShortHash(string value){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").Substring(0,8).ToLowerInvariant();}
        private static float Floor(SkinnedMeshRenderer renderer)
        {var baked=new Mesh();try{renderer.BakeMesh(baked);return baked.vertices.Min(v=>renderer.transform.TransformPoint(v).y);}finally{UnityEngine.Object.DestroyImmediate(baked);}}
        private static void AddSourceShapes(WardrobeImportRecipe recipe,WardrobeImportRecipe.Part part,Mesh mesh,int[] raw,Matrix4x4[] transforms,Mesh body,List<string> warnings)
        {
            var vertices=mesh.vertices;var normals=LaraCandidateBuilder.SmoothPointNormals(vertices,mesh.triangles,raw,part.points.Length);
            foreach(var frame in part.frames)
            {
                var mapping=recipe.shapeNames.SingleOrDefault(m=>m.from==frame.name);
                string semantic=frame.name.Contains("__")?"Genesis8Female__"+frame.name.Substring(frame.name.IndexOf("__",StringComparison.Ordinal)+2):frame.name;
                string name=mapping!=null?mapping.to:body.GetBlendShapeIndex(semantic)>=0?semantic:frame.name;
                if(body.GetBlendShapeIndex(name)<0)warnings.Add("Attachment-local morph retained (not driven by body): "+part.node+" / "+name);
                var entries=frame.entries.ToDictionary(e=>e.index,e=>e.delta);
                var delta=raw.Select((p,i)=>entries.TryGetValue(p,out var d)?transforms[i].MultiplyVector(d):Vector3.zero).ToArray();
                if(mesh.GetBlendShapeIndex(name)>=0)throw new IOException("Duplicate semantic morph: "+part.node+" / "+name);
                LaraFirstOutfitBuilder.AddFrame(mesh,name,delta,vertices,mesh.triangles,raw,part.points.Length,normals);
            }
        }
        private static void CaptureHeel(WardrobeImportRecipe recipe,WardrobeImportRecipe.Manifest manifest,SkinnedMeshRenderer body,SkinnedMeshRenderer donor,Dictionary<string,Transform> canonical,int[] raw,int pointCount)
        {
            var heel=JsonUtility.FromJson<WardrobeImportRecipe.Heel>(File.ReadAllText(recipe.destination+"/heel-reference.json"));
            if(heel.sourceSHA256!=manifest.sha256 || heel.referenceSHA256!=Hash(recipe.referenceFbx))throw new IOException("Heel reference hash mismatch.");
            var source=donor.bones.ToDictionary(b=>b.name);
            foreach(string name in new[]{"lFoot","rFoot"})
                if(Quaternion.Angle(source[name].localRotation,canonical[name].localRotation)>.1f)throw new IOException("Exported foot rotation differs from the supported baked-pose reference; inspect export convention: "+name);
            var entries=heel.entries.ToDictionary(e=>e.index,e=>e.delta);var mesh=body.sharedMesh;
            var delta=raw.Select(p=>entries.TryGetValue(p,out var d)?d:Vector3.zero).ToArray();
            var normals=LaraCandidateBuilder.SmoothPointNormals(mesh.vertices,mesh.triangles,raw,pointCount);
            LaraFirstOutfitBuilder.AddFrame(mesh,"CapturedHeelFootPose",delta,mesh.vertices,mesh.triangles,raw,pointCount,normals);
            foreach(var pair in canonical)if(pair.Key.Contains("Toe") && source.TryGetValue(pair.Key,out var bone))pair.Value.localPosition=bone.localPosition;
            mesh.bindposes=body.bones.Select(b=>b.worldToLocalMatrix*body.transform.localToWorldMatrix).ToArray();EditorUtility.SetDirty(mesh);
            body.SetBlendShapeWeight(mesh.GetBlendShapeIndex("CapturedHeelFootPose"),100);
        }
        private static PerformerFootwearProfile BuildFootwear(WardrobeImportRecipe recipe,LaraWardrobe wardrobe,float referenceFloor,Dictionary<string,Transform> bones,Transform root)
        {
            var shoe=wardrobe.Pieces.Single(p=>p.name==recipe.shoes.node).renderer;
            if(recipe.shoes.articulation!="rigid-pair" && recipe.shoes.articulation!="skinned")throw new IOException("Choose shoe articulation: rigid-pair or skinned.");
            if(recipe.shoes.articulation=="rigid-pair")WardrobeFootwearGeometry.MakePumpsRigid(shoe);
            WardrobeFootwearGeometry.AddFootFit(wardrobe.Body,shoe,recipe.shoes.capSurface,recipe.characterPoints);
            float height=Mathf.Max(0,(referenceFloor-Floor(shoe))/root.lossyScale.y);
            string path=recipe.destination+"/Footwear.asset";var profile=AssetDatabase.LoadAssetAtPath<PerformerFootwearProfile>(path);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<PerformerFootwearProfile>();profile.sourceName=recipe.shoes.node;
                profile.standingHeight=height;profile.footShrink=recipe.shoes.defaultFootFit;profile.rigidShoe=recipe.shoes.articulation=="rigid-pair";
                AssetDatabase.CreateAsset(profile,path);
            }
            // Contact geometry is mechanical source data; regenerate it while retaining artist height/fit.
            {
                var vertices=shoe.sharedMesh.vertices;
                for(int side=0;side<2;side++)
                {
                    int index=Array.FindIndex(shoe.bones,b=>b.name==(side==0?"lFoot":"rFoot"));
                    int other=Array.FindIndex(shoe.bones,b=>b.name==(side==0?"rFoot":"lFoot"));
                    if(index<0 || other<0)throw new IOException("Combined left/right foot bindings required for footwear contact measurement.");
                    var center=shoe.sharedMesh.bindposes[index].inverse.MultiplyPoint3x4(Vector3.zero);
                    var otherCenter=shoe.sharedMesh.bindposes[other].inverse.MultiplyPoint3x4(Vector3.zero);
                    var points=vertices.Where(v=>Mathf.Abs(v.x-center.x)<Mathf.Abs(v.x-otherCenter.x)).ToArray();
                    float floor=points.Min(v=>v.y);var bottom=points.Where(v=>v.y<floor+.003f).ToArray();
                    float back=bottom.Min(v=>v.z),front=bottom.Max(v=>v.z);
                    Vector3 Mean(Vector3[] p)=>p.Aggregate(Vector3.zero,(a,b)=>a+b)/p.Length;
                    var h=shoe.sharedMesh.bindposes[index].MultiplyPoint3x4(Mean(bottom.Where(v=>v.z<back+.015f).ToArray()));
                    var t=shoe.sharedMesh.bindposes[index].MultiplyPoint3x4(Mean(bottom.Where(v=>v.z>front-.015f).ToArray()));
                    if(side==0){profile.leftHeel=h;profile.leftToe=t;}else{profile.rightHeel=h;profile.rightToe=t;}
                }
            }
            // Contact ground belongs to the barefoot floor. Measure before applying the
            // standing lift, so the imported heel does not establish a lower floor.
            profile.groundOffset=referenceFloor-wardrobe.transform.position.y;
            profile.rigidShoe=recipe.shoes.articulation=="rigid-pair";
            profile.referenceBody=wardrobe.Body.sharedMesh;
            profile.referenceToeBones=bones.Values.Where(b=>b.name.Contains("Toe")||b.name.Contains("Metatarsals")).Select(b=>new WardrobeOutfitDefinition.LocalBone{name=b.name,parent=b.parent.name,position=b.localPosition,rotation=b.localRotation,scale=b.localScale}).ToArray();EditorUtility.SetDirty(profile);
            var review=wardrobe.gameObject.AddComponent<LaraHeelReview>();review.Configure(wardrobe.Body,root,bones["lFoot"],bones["rFoot"],bones["lToe"],bones["rToe"],profile.standingHeight);review.SetFootwear(profile);
            return profile;
        }
    }
}
