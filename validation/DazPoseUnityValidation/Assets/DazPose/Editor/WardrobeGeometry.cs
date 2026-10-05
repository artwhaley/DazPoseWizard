using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEngine;
namespace DazPose.UnityValidation
{
    internal static class WardrobeGeometry
    {
        internal static void InheritWeights(Mesh shell,int[] shellRaw,SkinnedMeshRenderer parent,WardrobeImportRecipe.Part parentPart)
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
        internal static void TransferBodyShapes(Mesh mesh,int[] raw,int pointCount,Mesh body)
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
                if(mesh.GetBlendShapeIndex(name)>=0)continue;
                int index=body.GetBlendShapeIndex(name);if(index<0)throw new IOException("Approved body shape missing: "+name);
                var source=new Vector3[body.vertexCount];body.GetBlendShapeFrameVertices(index,0,source,null,null);
                var delta=new Vector3[vertices.Length];
                for(int i=0;i<vertices.Length;i++)if(binding[i]>=0)
                {int t=binding[i];var b=barycentric[i];delta[i]=source[bt[t]]*b.x+source[bt[t+1]]*b.y+source[bt[t+2]]*b.z;}
                if(delta.Any(v=>v.sqrMagnitude>1e-12f))LaraFirstOutfitBuilder.AddFrame(mesh,name,delta,vertices,mesh.triangles,raw,pointCount,normals);
            }
        }
    }
}
