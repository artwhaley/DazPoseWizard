using System;
using System.Collections.Generic;
namespace DazPose.Game {
 public static class CatalogViewportAnchor {
  // Prefer the old top item, then the nearest surviving old neighbour (next wins ties).
  public static int Resolve(IReadOnlyList<string> oldIds,IReadOnlyList<string> newIds,int oldIndex) {
   if(newIds.Count==0)return 0;
   if(oldIds.Count==0)return Math.Max(0,Math.Min(oldIndex,newIds.Count-1));
   oldIndex=Math.Max(0,Math.Min(oldIndex,oldIds.Count-1));
   var indices=new Dictionary<string,int>(StringComparer.Ordinal);for(int i=0;i<newIds.Count;i++)indices[newIds[i]]=i;
   for(int distance=0;distance<oldIds.Count;distance++) {
    int next=oldIndex+distance;if(next<oldIds.Count&&indices.TryGetValue(oldIds[next],out int found))return found;
    int previous=oldIndex-distance;if(distance>0&&previous>=0&&indices.TryGetValue(oldIds[previous],out found))return found;
   }
   return Math.Min(oldIndex,newIds.Count-1);
  }
 }
}
