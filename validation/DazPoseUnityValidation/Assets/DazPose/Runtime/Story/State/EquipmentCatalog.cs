using System;
using System.Linq;
using UnityEngine;
namespace DazPose.Story {
 [Serializable] public sealed class EquipmentCatalog {
  public int version=1;
  public EquipmentDefinition[] entries=Array.Empty<EquipmentDefinition>();
  public EquipmentDefinition Require(string id) => entries.SingleOrDefault(e=>e.id==id) ?? throw new ArgumentException("Equipment is absent from catalog: "+id);
  public static EquipmentCatalog Parse(string json) {
   CatalogValidation.RequireVersion(json);
   var catalog=JsonUtility.FromJson<EquipmentCatalog>(json);
   CatalogValidation.Entries(catalog.entries,e=>e.id,e=>e.displayName);
   foreach(var e in catalog.entries) { CatalogValidation.Tags(e.identityTags); CatalogValidation.Tags(e.themeTags); CatalogValidation.Tags(e.excludesTags); }
   return catalog;
  }
 }
}