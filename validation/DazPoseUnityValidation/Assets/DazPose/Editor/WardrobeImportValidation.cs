using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public static class WardrobeImportValidation
    {
        [Serializable] private sealed class Check
        {public int parts,surfaces,localBones,characterChannels;public float shoeHeightMeters,footFit;public bool canonicalChannelsRetained,coverageToggles,footwearReference,materialMerge,capturedPoseMatches;}
        [Serializable] private sealed class Identity {public string path,guid;}
        [Serializable] private sealed class Reimport
        {public Identity[] assets;public string material;public float alpha,height,fit;public bool hasFootwear;}
        private static WardrobeOutfitDefinition Definition(WardrobeImportRecipe recipe)=>AssetDatabase.LoadAssetAtPath<WardrobeOutfitDefinition>(recipe.destination+"/Outfit.asset");
        public static void Run()
        {
            var recipe=WardrobeImportRecipe.Read();EditorSceneManager.OpenScene(recipe.Scene,OpenSceneMode.Single);
            var definition=Definition(recipe);var wardrobe=UnityEngine.Object.FindAnyObjectByType<LaraWardrobe>();
            if(definition==null || definition.sourceFbxHash!=WardrobeImporter.Hash(recipe.fbx) || definition.sourceDufHash!=WardrobeImporter.Hash(recipe.duf) || definition.recipeHash!=WardrobeImporter.Hash(WardrobeImportRecipe.Argument("-wardrobeRecipe")))throw new IOException("Source/recipe changed since import; run All.");
            if(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Any(t=>t.name=="Wardrobe Import Temporary Donor"))throw new IOException("Temporary source body/rig leaked into the review scene.");
            if(definition==null || definition.id!=recipe.id || definition.pieces.Length!=wardrobe.Pieces.Length)throw new IOException("Outfit definition/host inventory differs.");
            if(!wardrobe.Validate(out string reason))throw new IOException(reason);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(recipe.fbx).GetComponentsInChildren<Renderer>(true);
            var check=new Check{parts=definition.pieces.Length,characterChannels=wardrobe.Body.sharedMesh.blendShapeCount};
            foreach(var piece in definition.pieces)
            {
                var renderer=wardrobe.Pieces.Single(p=>p.name==piece.id).renderer;
                if(renderer.sharedMesh!=piece.mesh || !renderer.sharedMaterials.SequenceEqual(piece.materials) || !renderer.bones.Select(b=>b.name).SequenceEqual(piece.boneNames))
                    throw new IOException("Equipment/host bindings differ: "+piece.id);
                var imported=source.Single(r=>r.name==piece.id+".Shape");
                var aligned=LaraCandidateBuilder.MatchImportedSurfaces(imported,piece.materials);
                if(!piece.materials.SequenceEqual(aligned))throw new IOException("Surface order differs: "+piece.id);
                foreach(var material in piece.materials)
                {
                    if(!AssetDatabase.GetAssetPath(material).StartsWith(recipe.destination+"/Pieces/"+WardrobeImporter.Safe(piece.id)+"/RuntimeMaterials/",StringComparison.Ordinal))throw new IOException("Cross-piece material ownership: "+piece.id);
                    if(material.HasProperty("_SurfaceType") && material.GetFloat("_SurfaceType")>.5f && material.shader.name.Contains("Metallic") && !material.shader.name.Contains("Transparent"))throw new IOException("Transparent surface uses an opaque graph: "+material.name);
                    if(ShaderUtil.ShaderHasError(material.shader))throw new IOException("Shader compilation failed: "+material.shader.name);
                    check.surfaces++;
                }
                foreach(var local in piece.localBones)
                {
                    var bone=wardrobe.GetComponentsInChildren<Transform>(true).Single(b=>b.name==local.name);
                    if(bone.parent.name!=local.parent || Vector3.Distance(bone.localPosition,local.position)>.00001f)throw new IOException("Attachment bone reference differs: "+local.name);
                    check.localBones++;
                }
            }
            var canonical=definition.characterReference;var body=wardrobe.Body.sharedMesh;
            if(body.vertexCount!=canonical.vertexCount || !body.triangles.SequenceEqual(canonical.triangles) || !body.vertices.SequenceEqual(canonical.vertices))throw new IOException("Canonical body base geometry changed.");
            for(int i=0;i<canonical.blendShapeCount;i++)
                if(body.GetBlendShapeName(i)!=canonical.GetBlendShapeName(i) || body.GetBlendShapeFrameCount(i)!=canonical.GetBlendShapeFrameCount(i))throw new IOException("Canonical channel order changed.");
            check.canonicalChannelsRetained=true;
            var block=new MaterialPropertyBlock();
            foreach(var piece in wardrobe.Pieces.Where(p=>p.coverageChannel>=0))
            {
                string property=piece.coverageChannel==0?"_WardrobeCoverageEnabled":"_WardrobeDressCoverageEnabled";
                piece.visible=true;wardrobe.Apply();wardrobe.Body.GetPropertyBlock(block);if(block.GetFloat(property)!=1)throw new IOException("Coverage did not enable: "+piece.name);
                piece.visible=false;wardrobe.Apply();wardrobe.Body.GetPropertyBlock(block);if(block.GetFloat(property)!=0)throw new IOException("Coverage did not clear: "+piece.name);
                piece.visible=true;
            }
            wardrobe.Apply();
            foreach(var piece in wardrobe.Pieces){piece.renderer.GetPropertyBlock(block);if(block.GetFloat("_WardrobeCoverageEnabled")!=0 || block.GetFloat("_WardrobeDressCoverageEnabled")!=0)throw new IOException("Coverage leaked onto attachment.");}
            check.coverageToggles=true;
            CheckMaterialMerge(definition);check.materialMerge=true;
            if(definition.footwear!=null)
            {
                var heel=wardrobe.GetComponent<LaraHeelReview>();heel.Apply();
                if(heel.Footwear!=definition.footwear || definition.footwear.referenceBody!=body || definition.footwear.referenceToeBones.Length==0)throw new IOException("Portable footwear reference is incomplete.");
                if(body.GetBlendShapeIndex("CapturedHeelFootPose")<0 || body.GetBlendShapeIndex("WardrobeFootFit")<0)throw new IOException("Footwear body channels missing.");
                var points=JsonUtility.FromJson<LaraCandidateBuilder.Manifest>(File.ReadAllText(recipe.characterPoints));
                var raw=LaraFirstOutfitBuilder.MapPoints(body.vertices,points.points,Array.Empty<LaraCandidateBuilder.Frame>());
                var reference=JsonUtility.FromJson<WardrobeImportRecipe.Heel>(File.ReadAllText(recipe.destination+"/heel-reference.json"));
                var expected=reference.entries.ToDictionary(e=>e.index,e=>e.delta);
                var deltas=new Vector3[body.vertexCount];body.GetBlendShapeFrameVertices(body.GetBlendShapeIndex("CapturedHeelFootPose"),0,deltas,null,null);
                for(int i=0;i<deltas.Length;i++)if(Vector3.Distance(deltas[i],expected.TryGetValue(raw[i],out var value)?value:Vector3.zero)>.000002f)throw new IOException("Captured footwear pose differs from classified source geometry.");
                check.capturedPoseMatches=true;
                check.footwearReference=true;
                check.shoeHeightMeters=definition.footwear.standingHeight;check.footFit=definition.footwear.footShrink;
            }
            Directory.CreateDirectory(recipe.Output+"/assets");File.WriteAllText(recipe.Output+"/assets/validation.json",JsonUtility.ToJson(check,true));Debug.Log("WARDROBE_ASSETS_VALIDATED: "+check.surfaces+" surfaces");
        }
        private static void CheckMaterialMerge(WardrobeOutfitDefinition definition)
        {
            var material=definition.pieces.SelectMany(p=>p.materials).First(m=>m.HasProperty("_Alpha") && m.HasProperty("_AlphaMap"));
            var baseline=UnityEngine.Object.Instantiate(material);var revised=UnityEngine.Object.Instantiate(material);var artist=UnityEngine.Object.Instantiate(material);var map=new Texture2D(2,2);
            try
            {
                baseline.SetFloat("_Alpha",1);artist.SetFloat("_Alpha",.314159f);revised.SetFloat("_Alpha",.6f);
                revised.SetTexture("_AlphaMap",map);
                LaraCandidateBuilder.MergeUneditedMaterialProperties(baseline,revised,artist);
                if(Mathf.Abs(artist.GetFloat("_Alpha")-.314159f)>.000001f || artist.GetTexture("_AlphaMap")!=map)throw new IOException("Material baseline merge lost an artist override or failed to adopt an unedited source map.");
            }
            finally{UnityEngine.Object.DestroyImmediate(baseline);UnityEngine.Object.DestroyImmediate(revised);UnityEngine.Object.DestroyImmediate(artist);UnityEngine.Object.DestroyImmediate(map);}
        }
        public static void SeedReimport()
        {
            var recipe=WardrobeImportRecipe.Read();var definition=Definition(recipe);
            string path=recipe.Output+"/reimport-snapshot.json";if(File.Exists(path))throw new IOException("Restore the previous reimport fixture first.");
            var material=definition.pieces.SelectMany(p=>p.materials).First(m=>m.HasProperty("_Alpha"));
            var snapshot=new Reimport{material=AssetDatabase.GetAssetPath(material),alpha=material.GetFloat("_Alpha"),hasFootwear=definition.footwear!=null,
                height=definition.footwear!=null?definition.footwear.standingHeight:0,fit=definition.footwear!=null?definition.footwear.footShrink:0,
                assets=AssetDatabase.FindAssets("",new[]{recipe.destination}).Select(g=>new Identity{guid=g,path=AssetDatabase.GUIDToAssetPath(g)}).Where(a=>!AssetDatabase.IsValidFolder(a.path)).ToArray()};
            File.WriteAllText(path,JsonUtility.ToJson(snapshot,true));material.SetFloat("_Alpha",.314159f);EditorUtility.SetDirty(material);
            if(snapshot.hasFootwear){definition.footwear.standingHeight=snapshot.height+.001f;definition.footwear.footShrink=.0375f;EditorUtility.SetDirty(definition.footwear);}
            AssetDatabase.SaveAssets();Debug.Log("WARDROBE_REIMPORT_FIXTURE_SEEDED");
        }
        public static void CheckReimport()
        {
            var recipe=WardrobeImportRecipe.Read();var snapshot=JsonUtility.FromJson<Reimport>(File.ReadAllText(recipe.Output+"/reimport-snapshot.json"));
            foreach(var identity in snapshot.assets)if(AssetDatabase.AssetPathToGUID(identity.path)!=identity.guid)throw new IOException("Asset GUID changed on reimport: "+identity.path);
            var material=AssetDatabase.LoadAssetAtPath<Material>(snapshot.material);if(Mathf.Abs(material.GetFloat("_Alpha")-.314159f)>.000001f)throw new IOException("Artist opacity was lost on reimport.");
            var definition=Definition(recipe);
            if(snapshot.hasFootwear && (Mathf.Abs(definition.footwear.standingHeight-snapshot.height-.001f)>.000001f || Mathf.Abs(definition.footwear.footShrink-.0375f)>.000001f))throw new IOException("Artist footwear tuning was lost on reimport.");
            File.WriteAllText(recipe.Output+"/reimport-validation.json","{\"assetGuidsPreserved\":true,\"artistOpacityPreserved\":true,\"artistFootwearPreserved\":true}");Debug.Log("WARDROBE_REIMPORT_VALIDATED");
        }
        public static void RestoreReimport()
        {
            var recipe=WardrobeImportRecipe.Read();string path=recipe.Output+"/reimport-snapshot.json";
            if(!File.Exists(path))return;var snapshot=JsonUtility.FromJson<Reimport>(File.ReadAllText(path));
            var material=AssetDatabase.LoadAssetAtPath<Material>(snapshot.material);material.SetFloat("_Alpha",snapshot.alpha);EditorUtility.SetDirty(material);
            var definition=Definition(recipe);if(snapshot.hasFootwear){definition.footwear.standingHeight=snapshot.height;definition.footwear.footShrink=snapshot.fit;EditorUtility.SetDirty(definition.footwear);}
            AssetDatabase.SaveAssets();File.Delete(path);Debug.Log("WARDROBE_REIMPORT_FIXTURE_RESTORED");
        }
    }
}
