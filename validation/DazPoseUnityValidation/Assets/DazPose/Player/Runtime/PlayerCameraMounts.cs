using System;
using UnityEngine;
namespace DazPose.Player {
 /// <summary>Fixed viewpoints above the player body's head. Story names: High and Low.</summary>
 public enum PlayerCameraHeight { High, Low }
 /// <summary>Two authored camera mounts that travel with the player body root.</summary>
 [DisallowMultipleComponent]
 public sealed class PlayerCameraMounts : MonoBehaviour {
  /// <summary>A forward direction this close to straight up or down cannot define a view yaw.</summary>
  public const float MaxAbsoluteForwardY = 0.99f;
  [SerializeField] Transform high;
  [SerializeField] Transform low;
  public Transform High => high;
  public Transform Low => low;
  public void Configure(Transform highMount,Transform lowMount) { high=highMount; low=lowMount; }
  public static bool TryParse(string name,out PlayerCameraHeight height) {
   switch(name) {
    case "High":height=PlayerCameraHeight.High;return true;
    case "Low":height=PlayerCameraHeight.Low;return true;
    default:height=PlayerCameraHeight.High;return false;
   }
  }
  public Transform Mount(PlayerCameraHeight height) {
   Validate();
   return height==PlayerCameraHeight.High ? high : low;
  }
  /// <summary>Fails clearly when a mount is missing, outside the body, or has no usable look direction.</summary>
  public void Validate() {
   ValidateMount(PlayerCameraHeight.High,high);
   ValidateMount(PlayerCameraHeight.Low,low);
   if(ReferenceEquals(high,low)) throw new InvalidOperationException("The High and Low camera mounts must be different transforms.");
  }
  void ValidateMount(PlayerCameraHeight height,Transform mount) {
   if(mount==null) throw new InvalidOperationException("Assign the "+height+" camera mount on "+name+" (DazPose > Player > Add Camera Mounts to PlayerBody).");
   if(mount==transform || !mount.IsChildOf(transform)) throw new InvalidOperationException("The "+height+" camera mount '"+mount.name+"' must be a child of the player body root '"+name+"'.");
   Vector3 forward=mount.forward;
   if(float.IsNaN(forward.x) || float.IsNaN(forward.y) || float.IsNaN(forward.z) || Mathf.Abs(forward.y)>MaxAbsoluteForwardY)
    throw new InvalidOperationException("The "+height+" camera mount '"+mount.name+"' looks straight up or down; rotate it toward the body so the view has a heading.");
  }
  /// <summary>Instantly puts the Player root and view rig on the mount and cancels move and tracking.</summary>
  public void PlaceView(PlayerView view,PlayerCameraHeight height) {
   if(view==null) throw new ArgumentNullException(nameof(view));
   var mount=Mount(height);
   view.ResetPerformanceView(mount.position,mount.rotation);
  }
 }
}
