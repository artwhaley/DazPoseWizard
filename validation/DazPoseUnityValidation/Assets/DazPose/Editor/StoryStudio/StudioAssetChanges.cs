using System;
using System.Linq;
using UnityEditor;
namespace DazPose.Editor.Studio {
 // Import paths invalidate only the active shot/source. No recursive dependency polling.
 internal sealed class StudioAssetChanges : AssetPostprocessor {
  internal static event Action<string[]> Changed;
  static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] from){Changed?.Invoke(imported.Concat(deleted).Concat(moved).Concat(from).Distinct().ToArray());}
 }
}
