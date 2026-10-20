using System;
using UnityEngine;
namespace DazPose.Performer {
 public sealed partial class SuccubusPerformer {
  internal bool IsPresentationSeatingTransitioning=>IsSeatingTransitionActive();
  internal PerformerPresentationSnapshot CapturePresentation() {
   if(!IsRuntimeReady || IsTransitioning || IsLocomoting || IsTeleporting || IsDissolving || IsExpressionTransitioning || IsPerforming || IsSeatingTransitionActive() || (CurrentWardrobe?.IsChanging??false))throw new InvalidOperationException("Performer capture requires a stable presentation after the entrance barrier.");
   var wardrobe=GetComponent<PerformerWardrobe>();
   return new PerformerPresentationSnapshot(this,DesiredPose,SettledPose,_seating?.CapturePresentation(),IsHidden,wardrobe?.CaptureSelection(),GetComponent<PerformerMagicShaderPresentation>()?.CurrentProfile,_lastDesiredExpression,_lastDesiredExpressionIntensity,_gaze?.CapturePresentation(),_desiredAura,_desiredAuraTarget,_handGripController?.CapturePresentation());
  }
 }
}
