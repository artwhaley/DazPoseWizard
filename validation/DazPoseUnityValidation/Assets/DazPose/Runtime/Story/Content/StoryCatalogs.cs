using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
namespace DazPose.Story {
 public sealed class StoryCatalogs : ScriptableObject {
  public TextAsset kinkCatalog, equipmentCatalog;
  public KinkCatalog Kinks { get; private set; }
  public EquipmentCatalog Equipment { get; private set; }
  public void ValidateAndLoad() {
   if(kinkCatalog==null || equipmentCatalog==null) throw new InvalidOperationException("Assign both versioned story catalogs.");
   Kinks=KinkCatalog.Parse(kinkCatalog.text); Equipment=EquipmentCatalog.Parse(equipmentCatalog.text);
  }
  public static StoryCatalogs Load() {
   var config=Resources.Load<StoryCatalogs>("DazPose/StoryCatalogs");
   if(config==null) throw new InvalidOperationException("Install the saved DazPose/StoryCatalogs configuration.");
   config.ValidateAndLoad(); return config;
  }
 }
 internal static class CatalogValidation {
  [Serializable] sealed class Header { public int version; }
  public static void RequireVersion(string json) {
   if(string.IsNullOrWhiteSpace(json) || JsonUtility.FromJson<Header>(json)?.version!=1) throw new ArgumentException("Expected catalog version 1.");
  }
  public static void Entries<T>(T[] entries,Func<T,string> id,Func<T,string> name) where T:class {
   if(entries==null) throw new ArgumentException("Catalog entries must be an array.");
   var ids=new HashSet<string>(StringComparer.Ordinal);
   foreach(var e in entries) if(e==null || string.IsNullOrWhiteSpace(id(e)) || !ids.Add(id(e)) || string.IsNullOrWhiteSpace(name(e)))
    throw new ArgumentException("Catalog requires unique nonblank IDs and display names.");
  }
  public static void Tags(string[] tags) {
   if(tags==null) throw new ArgumentException("Tag list must be an array.");
   var found=new HashSet<string>(StringComparer.Ordinal);
   foreach(var tag in tags) if(tag==null || !Regex.IsMatch(tag,@"^[a-z][a-z0-9-]*:[a-z0-9][a-z0-9-]*$") || !found.Add(tag))
    throw new ArgumentException("Tags must be unique, lower-case, trimmed, namespace-prefixed: "+tag);
  }
 }
}