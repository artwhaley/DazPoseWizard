using System;
using DazPose.Performer;
namespace DazPose.Player {
 public sealed partial class PlayerBodyPresence {
  internal PlayerBodyPresentationSnapshot CapturePresentation(){if(driver!=null && driver.IsDissolving)throw new InvalidOperationException("Player body capture requires stable visibility.");return new PlayerBodyPresentationSnapshot(this);}
 }
}
