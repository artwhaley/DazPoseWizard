using System.IO;
using DazPose.Game;
using DazPose.Toys;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor {
 public static class ProductionApplicationSetup {
  public const string CatalogPath="Assets/Production/Application/SceneCatalog.asset";
  public const string ApplicationPath="Assets/Resources/DazPose/GameApplication.prefab";
  public static void EnsureAssets() {
   Directory.CreateDirectory("Assets/Production/Application");
   Directory.CreateDirectory("Assets/Resources/DazPose");
   AssetDatabase.Refresh();
   var catalog=AssetDatabase.LoadAssetAtPath<SceneCatalog>(CatalogPath);
   if(catalog==null) { catalog=ScriptableObject.CreateInstance<SceneCatalog>(); AssetDatabase.CreateAsset(catalog,CatalogPath); }
   if(AssetDatabase.LoadAssetAtPath<GameObject>(ApplicationPath)==null) {
    var go=new GameObject("DazPose Application");
    var toys=go.AddComponent<ToyControlService>();
    var app=go.AddComponent<GameApplication>(); app.Configure(toys,catalog);
    PrefabUtility.SaveAsPrefabAsset(go,ApplicationPath);
    Object.DestroyImmediate(go);
   }
   AssetDatabase.SaveAssets();
  }
 }
}