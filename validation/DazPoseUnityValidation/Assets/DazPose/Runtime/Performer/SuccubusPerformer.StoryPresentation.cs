using System;
using UnityEngine;
namespace DazPose.Performer {
 public sealed partial class SuccubusPerformer {
  public PerformerMaterialMode MaterialMode => GetComponent<PerformerMagicShaderPresentation>()?.Mode ?? PerformerMaterialMode.Regular;
  public Awaitable<bool> MagicShaderAsync(PerformerMagicShaderProfile profile) {
   if(!IsRuntimeReady) throw new InvalidOperationException("Lara's performer must be active before MagicShader.");
   if(IsDissolving) throw new InvalidOperationException("MagicShader cannot change materials during a dissolve transition.");
   var presentation=GetComponent<PerformerMagicShaderPresentation>() ?? gameObject.AddComponent<PerformerMagicShaderPresentation>();
   if(_wardrobeEffectTransitionOwner!=null || presentation.IsRefreshing) throw new InvalidOperationException("MagicShader cannot take materials while a wardrobe transition owns them.");
   presentation.Apply(profile);
   var source=new AwaitableCompletionSource<bool>(); source.SetResult(true); return source.Awaitable;
  }
  internal Action CaptureMagicShaderRestoration() => GetComponent<PerformerMagicShaderPresentation>()?.CaptureRestoration();
  // Explicit authored setup only. Never lifecycle cleanup and never a tuning reset.
  public void StartHiddenAt(Transform location) {
   if(location==null) throw new ArgumentNullException(nameof(location));
   if(!IsRuntimeReady || IsDissolving || IsLocomoting || _activePoseRequest!=null) throw new InvalidOperationException("StartHiddenAt requires an idle prepared performer before sequence actions.");
   _seating?.Cancel();
   transform.SetPositionAndRotation(location.position,location.rotation);
   if(_dissolve!=null) _dissolve.SetStableVisibility(true); else { SetStableHidden(true); ApplyStableVisibility(true); }
  }
  public void StartConfiguredIdle() {
   if(!IsRuntimeReady || initialPose==null) throw new InvalidOperationException("Lara needs her configured normal InitialPose before StartIdle.");
   Pose(initialPose,PoseTransition.Snap);
  }
 }
}