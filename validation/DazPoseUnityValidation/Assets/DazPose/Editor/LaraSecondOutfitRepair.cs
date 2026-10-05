using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.UnityValidation
{
    // Migration of this proof's owned assets. Never save an interactive user's scene.
    [InitializeOnLoad]
    public static class LaraSecondOutfitRepair
    {
        public const string ProfilePath=LaraSecondOutfitBuilder.Folder+"/BD Shoes Footwear.asset";
        static LaraSecondOutfitRepair()
        {
            EditorApplication.delayCall+=BindLoaded;
            EditorSceneManager.sceneOpened+=OnSceneOpened;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=BindLoaded;};
        }
        private static void OnSceneOpened(Scene scene,OpenSceneMode mode)=>BindLoaded();
        public static void BindLoaded()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var profile=AssetDatabase.LoadAssetAtPath<PerformerFootwearProfile>(ProfilePath);
            if(profile==null)return;
            foreach(var wardrobe in UnityEngine.Object.FindObjectsByType<LaraWardrobe>(FindObjectsInactive.Include))
                if(wardrobe.gameObject.scene.path==LaraSecondOutfitBuilder.ScenePath)
                {
                    if(AlignMaterials(wardrobe))EditorSceneManager.MarkSceneDirty(wardrobe.gameObject.scene);
                    var materials=wardrobe.Pieces.SelectMany(p=>p.renderer.sharedMaterials).Distinct().ToArray();
                    foreach(var material in materials)UpgradeMaterial(material);
                    EditorApplication.delayCall+=()=>{foreach(var material in materials)if(material!=null)AssetDatabase.SaveAssetIfDirty(material);};
                }
            foreach(var heel in UnityEngine.Object.FindObjectsByType<LaraHeelReview>(FindObjectsInactive.Include))
                if(heel.gameObject.scene.path==LaraSecondOutfitBuilder.ScenePath && heel.Footwear!=profile)
                {heel.SetFootwear(profile);EditorUtility.SetDirty(heel);EditorSceneManager.MarkSceneDirty(heel.gameObject.scene);}
        }
        public static void RepairAndRender()
        {
            if(!Application.isBatchMode)throw new IOException("Use the isolated batch project for asset migration; preserve interactive scene edits.");
            EditorSceneManager.OpenScene(LaraSecondOutfitBuilder.ScenePath,OpenSceneMode.Single);
            RepairLoaded();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            LaraSecondOutfitValidation.Run();
        }
        public static void VerifyExistingAssets()
        {
            if(!Application.isBatchMode)throw new IOException("Run asset verification in the isolated batch project.");
            EditorSceneManager.OpenScene(LaraSecondOutfitBuilder.ScenePath,OpenSceneMode.Single);
            BindLoaded();var wardrobe=UnityEngine.Object.FindAnyObjectByType<LaraWardrobe>();
            var donor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestCharacter/larasecondoutfit.fbx");int surfaces=0;
            foreach(var piece in wardrobe.Pieces)
            {
                var source=donor.GetComponentsInChildren<Renderer>(true).Single(r=>r.name==piece.name+".Shape");
                var materials=piece.renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var material=materials[i];
                    if(!material.name.EndsWith("_"+source.sharedMaterials[i].name,StringComparison.Ordinal)
                        || !AssetDatabase.GetAssetPath(material).StartsWith(LaraSecondOutfitBuilder.Folder+"/"+piece.name+"/RuntimeMaterials/",StringComparison.Ordinal))
                        throw new IOException("Node/surface assignment mismatch: "+piece.name+" / "+i);
                    if(material.HasProperty("_SurfaceType") && material.GetFloat("_SurfaceType")>.5f && !material.shader.name.Contains("Transparent"))
                        throw new IOException("Opacity surface uses an opaque shader graph: "+material.name);
                    if(ShaderUtil.ShaderHasError(material.shader))throw new IOException("Shader error: "+material.shader.name);
                    surfaces++;
                }
            }
            var heel=wardrobe.GetComponent<LaraHeelReview>();heel.Apply();
            int fit=wardrobe.Body.sharedMesh.GetBlendShapeIndex("WardrobeFootFit");
            if(heel.Footwear==null || fit<0 || Mathf.Abs(wardrobe.Body.GetBlendShapeWeight(fit)-heel.FootShrink/.05f*100)>.001f)
                throw new IOException("Footwear profile/fit is not active.");
            Directory.CreateDirectory("TestOutput/appearance-evidence/second-outfit");
            File.WriteAllText("TestOutput/appearance-evidence/second-outfit/surface-validation.json","{\"parts\":"+wardrobe.Pieces.Length+",\"verifiedSurfaces\":"+surfaces+",\"footwearActive\":true,\"bodyShapes\":"+wardrobe.Body.sharedMesh.blendShapeCount+"}");
            Debug.Log("LARA_SECOND_SURFACES_AND_FOOTWEAR_VERIFIED: "+surfaces+" surfaces");
        }
        public static void RepairLoaded()
        {
            var wardrobe=UnityEngine.Object.FindAnyObjectByType<LaraWardrobe>();
            var heel=wardrobe.GetComponent<LaraHeelReview>();var body=wardrobe.Body;
            AlignMaterials(wardrobe);
            foreach(var material in wardrobe.Pieces.SelectMany(p=>p.renderer.sharedMaterials).Distinct())
                UpgradeMaterial(material);
            var shoe=wardrobe.Pieces.Single(p=>p.name=="shoe_6146").renderer;
            MakePumpsRigid(shoe);
            AddFootFit(body,shoe);
            var profile=AssetDatabase.LoadAssetAtPath<PerformerFootwearProfile>(ProfilePath);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<PerformerFootwearProfile>();profile.sourceName="BD Shoes / shoe_6146";
                profile.standingHeight=heel.RigHeight;profile.footShrink=.05f;
                var vertices=shoe.sharedMesh.vertices;
                for(int side=0;side<2;side++)
                {
                    int bone=Array.FindIndex(shoe.bones,b=>b.name==(side==0?"lFoot":"rFoot"));
                    var foot=shoe.sharedMesh.bindposes[bone].inverse.MultiplyPoint3x4(Vector3.zero);
                    int other=Array.FindIndex(shoe.bones,b=>b.name==(side==0?"rFoot":"lFoot"));
                    var otherFoot=shoe.sharedMesh.bindposes[other].inverse.MultiplyPoint3x4(Vector3.zero);
                    var points=vertices.Where(v=>Mathf.Abs(v.x-foot.x)<Mathf.Abs(v.x-otherFoot.x)).ToArray();
                    float floor=points.Min(v=>v.y);var bottom=points.Where(v=>v.y<floor+.003f).ToArray();
                    float back=bottom.Min(v=>v.z),front=bottom.Max(v=>v.z);
                    Vector3 Mean(Vector3[] p)=>p.Aggregate(Vector3.zero,(a,b)=>a+b)/p.Length;
                    var heelPoint=Mean(bottom.Where(v=>v.z<back+.015f).ToArray());
                    var toePoint=Mean(bottom.Where(v=>v.z>front-.015f).ToArray());
                    var h=shoe.sharedMesh.bindposes[bone].MultiplyPoint3x4(heelPoint);
                    var t=shoe.sharedMesh.bindposes[bone].MultiplyPoint3x4(toePoint);
                    if(side==0){profile.leftHeel=h;profile.leftToe=t;}else{profile.rightHeel=h;profile.rightToe=t;}
                }
                var baked=new Mesh();shoe.BakeMesh(baked);
                profile.groundOffset=baked.vertices.Min(v=>shoe.transform.TransformPoint(v).y)-wardrobe.transform.position.y;
                UnityEngine.Object.DestroyImmediate(baked);AssetDatabase.CreateAsset(profile,ProfilePath);
            }
            heel.SetFootwear(profile);wardrobe.Apply();
            LaraFirstOutfitBuilder.RebindParticleMesh(wardrobe.GetComponent<SuccubusPerformer>(),body,LaraSecondOutfitBuilder.Folder);
            EditorUtility.SetDirty(heel);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            Debug.Log("LARA_SECOND_REPAIRED: transparent lace/fishnet, body-only foot fit="+profile.footShrink+", support height="+profile.standingHeight+", ground="+profile.groundOffset);
        }
        private static void UpgradeMaterial(Material material)
        {
            LaraCandidateBuilder.RepairOpacityTexture(material);
            if(!material.HasProperty("_SurfaceType") || material.GetFloat("_SurfaceType")<.5f)return;
            string name=material.shader.name.Contains("Metallic")?"Metallic":material.shader.name.Contains("Specular")?"Specular":null;
            if(name==null)throw new IOException("Unsupported transparent wardrobe shader: "+material.shader.name);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/DazPose/Effects/Dissolve/Shaders/uDTU HDRP "+name+" Transparent Dissolve.shadergraph");
            if(shader==null)throw new IOException("Missing transparent dissolve shader.");
            if(material.shader==shader)return;
            material.shader=shader;material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.EnableKeyword("_ALPHATEST_ON");
            if(material.HasProperty("_AlphaCutoffEnable"))material.SetFloat("_AlphaCutoffEnable",1);
            material.renderQueue=3000;EditorUtility.SetDirty(material);
            UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(material);
            if(ShaderUtil.ShaderHasError(shader))throw new IOException("Transparent shader compile error: "+shader.name);
        }
        private static bool AlignMaterials(LaraWardrobe wardrobe)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestCharacter/larasecondoutfit.fbx");
            var sources=model.GetComponentsInChildren<Renderer>(true);bool changed=false;
            foreach(var piece in wardrobe.Pieces)
            {
                var source=sources.Single(r=>r.name==piece.name+".Shape");
                var existing=piece.renderer.sharedMaterials;
                // Unity sorts submeshes by first used polygon, not FBX material-table order.
                // Scope to this exact node and match the source surface identity.
                var aligned=LaraCandidateBuilder.MatchImportedSurfaces(source,existing);
                if(existing.SequenceEqual(aligned))continue;
                piece.renderer.sharedMaterials=aligned;EditorUtility.SetDirty(piece.renderer);changed=true;
                Debug.Log("LARA_SURFACE_SLOTS_REPAIRED: "+piece.name+" / "+string.Join(",",source.sharedMaterials.Select(m=>m.name)));
            }
            return changed;
        }
        private static void AddFootFit(SkinnedMeshRenderer body,SkinnedMeshRenderer shoe)
        {
            var source=body.sharedMesh;
            var mesh=LaraCandidateBuilder.ReadableCopy(source);LaraFirstOutfitBuilder.CopyFrames(source,mesh,"WardrobeFootFit");
            var vertices=mesh.vertices;var posed=(Vector3[])vertices.Clone();var heelDelta=new Vector3[vertices.Length];
            source.GetBlendShapeFrameVertices(source.GetBlendShapeIndex("CapturedHeelFootPose"),0,heelDelta,null,null);
            for(int i=0;i<posed.Length;i++)posed[i]+=heelDelta[i];
            var centers=new[]{"lFoot","rFoot"}.Select(name=>mesh.bindposes[Array.FindIndex(body.bones,b=>b.name==name)].inverse.MultiplyPoint3x4(Vector3.zero)).ToArray();
            var counts=mesh.GetBonesPerVertex();var weights=mesh.GetAllBoneWeights();var delta=new Vector3[vertices.Length];var influence=new float[vertices.Length];int offset=0;
            try
            {
                for(int i=0;i<vertices.Length;i++)
                {
                    for(int j=0;j<counts[i];j++)
                    {
                        var weight=weights[offset++];string name=body.bones[weight.boneIndex].name;
                        if(name=="lFoot" || name=="rFoot" || name.Contains("Toe") || name.Contains("Metatarsals"))influence[i]+=weight.weight;
                    }
                }
            }
            finally{counts.Dispose();weights.Dispose();}
            var manifest=LaraCandidateBuilder.ReadManifest();var raw=LaraFirstOutfitBuilder.MapPoints(vertices,manifest.points,Array.Empty<LaraCandidateBuilder.Frame>());
            var normals=LaraCandidateBuilder.SmoothPointNormals(posed,mesh.triangles,raw,manifest.points.Length);
            var widths=new float[2];
            for(int side=0;side<2;side++)
            {
                var points=posed.Where((v,i)=>influence[i]>.5f && (Mathf.Abs(v.x-centers[side].x)<Mathf.Abs(v.x-centers[1-side].x))).ToArray();
                widths[side]=points.Max(v=>v.x)-points.Min(v=>v.x);
            }
            for(int i=0;i<vertices.Length;i++)
            {
                int side=Mathf.Abs(posed[i].x-centers[0].x)<Mathf.Abs(posed[i].x-centers[1].x)?0:1;
                // An inward surface offset avoids raising the forefoot toward the ankle pivot.
                // Ratio is relative to measured foot width, faded by foot skin influence.
                delta[i]=-normals[i]*widths[side]*.05f*influence[i];
            }
            // The closed pump needs a local clearance correction as well as general
            // volume fit. Measure its actual toe-cap roof in the shared rest space.
            int capSlot=Array.FindIndex(shoe.sharedMaterials,m=>m.name.EndsWith("_Shoe",StringComparison.Ordinal));
            if(capSlot<0)throw new IOException("Closed-pump surface identity missing.");
            var sv=shoe.sharedMesh.vertices;var st=shoe.sharedMesh.GetIndices(capSlot);int corrected=0;
            for(int i=0;i<posed.Length;i++)
            {
                int side=Mathf.Abs(posed[i].x-centers[0].x)<Mathf.Abs(posed[i].x-centers[1].x)?0:1;
                if(influence[i]<.2f || posed[i].z<centers[side].z+.06f)continue;
                var point=posed[i]+delta[i];float roof=float.NegativeInfinity;
                for(int j=0;j<st.Length;j+=3)
                {
                    var a=sv[st[j]];var b=sv[st[j+1]];var c=sv[st[j+2]];
                    if(Vector3.Cross(b-a,c-a).normalized.y<.3f)continue;
                    float determinant=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                    if(Mathf.Abs(determinant)<1e-10f)continue;
                    float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/determinant;
                    float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/determinant;
                    if(u<0 || v<0 || u+v>1)continue;
                    roof=Mathf.Max(roof,u*a.y+v*b.y+(1-u-v)*c.y);
                }
                if(!float.IsNegativeInfinity(roof) && point.y>roof-.002f)
                {delta[i].y-=point.y-roof+.002f;corrected++;}
            }
            Debug.Log("LARA_PUMP_TOE_CLEARANCE: "+corrected+" vertices / 2 mm roof clearance at default fit.");
            LaraFirstOutfitBuilder.AddFrame(mesh,"WardrobeFootFit",delta,posed,mesh.triangles,raw,manifest.points.Length,normals);
            body.sharedMesh=LaraCandidateBuilder.Save(mesh,AssetDatabase.GetAssetPath(source));
        }
        private static void MakePumpsRigid(SkinnedMeshRenderer shoe)
        {
            var source=shoe.sharedMesh;var mesh=LaraCandidateBuilder.ReadableCopy(source);LaraFirstOutfitBuilder.CopyFrames(source,mesh);
            var bones=new[]{"lFoot","rFoot"}.Select(n=>Array.FindIndex(shoe.bones,b=>b.name==n)).ToArray();
            if(bones.Any(b=>b<0))throw new IOException("Pump foot bindings missing.");
            var centers=bones.Select(b=>mesh.bindposes[b].inverse.MultiplyPoint3x4(Vector3.zero)).ToArray();
            var counts=new NativeArray<byte>(Enumerable.Repeat((byte)1,mesh.vertexCount).ToArray(),Allocator.Temp);
            var weights=new NativeArray<BoneWeight1>(mesh.vertices.Select(v=>new BoneWeight1{weight=1,boneIndex=bones[Mathf.Abs(v.x-centers[0].x)<Mathf.Abs(v.x-centers[1].x)?0:1]}).ToArray(),Allocator.Temp);
            try{mesh.SetBoneWeights(counts,weights);}finally{counts.Dispose();weights.Dispose();}
            shoe.sharedMesh=LaraCandidateBuilder.Save(mesh,AssetDatabase.GetAssetPath(source));
        }
    }
    internal sealed class LaraFootwearMigrationPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            if(imported.Any(path=>path.StartsWith(LaraSecondOutfitBuilder.Folder+"/",StringComparison.Ordinal) && path.EndsWith(".asset",StringComparison.Ordinal)))
                EditorApplication.delayCall+=LaraSecondOutfitRepair.BindLoaded;
        }
    }
}
