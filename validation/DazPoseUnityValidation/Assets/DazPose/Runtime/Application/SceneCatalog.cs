using System;
using UnityEngine;
namespace DazPose.Game {
 [CreateAssetMenu(menuName="DazPose/Scene Catalog")]
 public sealed class SceneCatalog : ScriptableObject {
  [Serializable] public sealed class Entry {
   public string id, title, description, scenePath;
   public bool enabled = true;
  }
  public string mainMenuPath = "Assets/Production/Scenes/MainMenu.unity";
  public Entry[] entries = Array.Empty<Entry>();
 }
}