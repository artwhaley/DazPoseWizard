using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
namespace DazPose.UnityValidation
{
    internal static class WardrobeFootwearGeometry
    {
        internal static void AddFootFit(SkinnedMeshRenderer body,SkinnedMeshRenderer shoe,string capSurface,string pointManifest)
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
            var manifest=UnityEngine.JsonUtility.FromJson<LaraCandidateBuilder.Manifest>(System.IO.File.ReadAllText(pointManifest));var raw=LaraFirstOutfitBuilder.MapPoints(vertices,manifest.points,Array.Empty<LaraCandidateBuilder.Frame>());
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
            int capSlot=Array.FindIndex(shoe.sharedMaterials,m=>!string.IsNullOrEmpty(capSurface) && m.name.EndsWith("_"+capSurface,StringComparison.Ordinal));
            if(!string.IsNullOrEmpty(capSurface) && capSlot<0)throw new IOException("Closed-pump surface identity missing: "+capSurface);
            var sv=shoe.sharedMesh.vertices;var st=capSlot<0?Array.Empty<int>():shoe.sharedMesh.GetIndices(capSlot);int corrected=0;
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
        internal static void MakePumpsRigid(SkinnedMeshRenderer shoe)
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
}
