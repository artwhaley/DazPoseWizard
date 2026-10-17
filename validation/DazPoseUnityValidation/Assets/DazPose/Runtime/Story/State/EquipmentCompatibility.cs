using System;
using System.Collections.Generic;
using System.Linq;
namespace DazPose.Story {
 public static class EquipmentCompatibility {
  static IEnumerable<string> Tags(EquipmentDefinition e) => e.identityTags.Concat(e.themeTags);
  public static bool AreCompatible(EquipmentDefinition a,EquipmentDefinition b) {
   if(a==null || b==null) throw new ArgumentNullException("equipment");
   return !a.excludesTags.Intersect(Tags(b),StringComparer.Ordinal).Any()
    && !b.excludesTags.Intersect(Tags(a),StringComparer.Ordinal).Any();
  }
  public static bool CanAdd(IEnumerable<EquipmentDefinition> selected,EquipmentDefinition item) =>
   selected.All(other=>other.id!=item.id && AreCompatible(other,item));
  public static string[] Select(EquipmentCatalog catalog,EquipmentPreference[] preferences,int desiredCount,Random random,
   string[] requiredThemeTags=null,string[] requiredIdentityTags=null) {
   if(desiredCount<0 || desiredCount>1000) throw new ArgumentOutOfRangeException(nameof(desiredCount));
   if(random==null) throw new ArgumentNullException(nameof(random));
   var candidates=catalog.entries.Where(e=>(preferences ?? Array.Empty<EquipmentPreference>()).Any(p=>p.id==e.id && p.ownedAndWilling)
    && (requiredThemeTags ?? Array.Empty<string>()).All(tag=>e.themeTags.Contains(tag))
    && (requiredIdentityTags ?? Array.Empty<string>()).All(tag=>e.identityTags.Contains(tag))).ToList();
   // Stable catalog order + one session RNG. Selection consumes each candidate at most once.
   for(int i=candidates.Count-1;i>0;i--) { int j=random.Next(i+1); var temp=candidates[i]; candidates[i]=candidates[j]; candidates[j]=temp; }
   var selected=new List<EquipmentDefinition>();
   foreach(var candidate in candidates) { if(selected.Count>=desiredCount) break; if(CanAdd(selected,candidate)) selected.Add(candidate); }
   return selected.Select(e=>e.id).ToArray();
  }
 }
}