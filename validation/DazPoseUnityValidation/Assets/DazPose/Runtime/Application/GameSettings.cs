using System;
using DazPose.Toys;
namespace DazPose.Game {
 [Serializable] public sealed class ToyRouteSettings {
  public string deviceName, signature;
  public uint featureIndex;
  public ToyOutputCapability capability;
  public bool enabled;
  public int channel;
  public float minimum, maximum = 1;
  public bool inverted;
  [NonSerialized] public uint? sessionDeviceIndex;
 }
 [Serializable] public sealed class GameSettings {
  public int version = 1;
  public string endpoint = ToyControlService.DefaultServerAddress;
  public float masterVolume = 1, speechVolume = 1;
  public ToyRouteSettings[] routes = Array.Empty<ToyRouteSettings>();
 }
}