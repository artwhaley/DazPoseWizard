using System;
using UnityEngine;
namespace DazPose.Story {
 [Serializable] public sealed class KinkCatalog {
  public int version=1;
  public KinkDefinition[] entries=Array.Empty<KinkDefinition>();
  public static KinkCatalog Parse(string json) {
   CatalogValidation.RequireVersion(json);
   var catalog=JsonUtility.FromJson<KinkCatalog>(json);
   CatalogValidation.Entries(catalog.entries,e=>e.id,e=>e.displayName);
   foreach(var e in catalog.entries) CatalogValidation.Tags(e.themeTags);
   return catalog;
  }
 }
}