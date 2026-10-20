using System;
using DazPose.Performer;
namespace DazPose.Player {
 public sealed partial class PlayerBodyPresence {
  internal void CommitPresentationFrame(PresentationFrame frame,bool localFrame=false){EnsureDriver();frame.Validate("Player body return");if(!IsStablyHidden || driver.IsDissolving)throw new InvalidOperationException("Exact body relocation requires concealed body.");transform.SetParent(frame.Parent,true);if(localFrame){transform.localPosition=frame.LocalPosition;transform.localRotation=frame.LocalRotation;}else transform.SetPositionAndRotation(frame.Position,frame.Rotation);}
  internal PlayerBodyPresentationSnapshot CapturePresentation(){if(driver!=null && driver.IsDissolving)throw new InvalidOperationException("Player body capture requires stable visibility.");return new PlayerBodyPresentationSnapshot(this);}
 }
}
