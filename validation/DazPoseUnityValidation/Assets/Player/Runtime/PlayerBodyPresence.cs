using System;
using DazPose.Performer;
using UnityEngine;
namespace DazPose.Player {
 [Serializable] public sealed class PlayerBodyDissolvePart {
  public PerformerDissolveRig rig;
  public PerformerDissolveProfile profile;
  public SkinnedMeshRenderer renderer;
  public Material[] visibleMaterials;
  public void Validate() {
   if(rig==null || profile==null || renderer==null || visibleMaterials==null || visibleMaterials.Length!=renderer.sharedMaterials.Length)
    throw new InvalidOperationException("Assign each player body surface, its original materials, and its dissolve presentation.");
   var saved=renderer.sharedMaterials;
   try {
    renderer.sharedMaterials=profile.LaraRuntimeMaterials;
    if(!rig.IsReady(profile,out string reason)) throw new InvalidOperationException("Player body surface "+renderer.name+": "+reason);
   } finally { renderer.sharedMaterials=saved; }
  }
  internal void Prepare() => renderer.sharedMaterials=profile.LaraRuntimeMaterials;
  internal void Finish(bool hidden,bool restore) {
   try { if(restore) rig.Restore(hidden); else rig.Finish(hidden); }
   finally { renderer.sharedMaterials=visibleMaterials; }
  }
 }
 [DisallowMultipleComponent]
 [DefaultExecutionOrder(-70)]
 public sealed partial class PlayerBodyPresence : MonoBehaviour {
  [SerializeField] PlayerBodyDissolvePart[] parts=Array.Empty<PlayerBodyDissolvePart>();
  [SerializeField] bool startHidden=true;
  PerformerDissolve driver;
  bool stableHidden;
  public int SurfaceCount => parts.Length;
  public bool StartsHidden => startHidden;
  public PlayerBodyVisibilityState VisibilityState => driver==null
   ? (startHidden ? PlayerBodyVisibilityState.Hidden : PlayerBodyVisibilityState.Visible)
   : (PlayerBodyVisibilityState)(int)driver.VisibilityState;
  public bool IsStablyHidden => driver==null ? startHidden : stableHidden;
  public void Configure(PlayerBodyDissolvePart[] surfaces,bool hidden=true) {
   if(driver!=null) throw new InvalidOperationException("Cannot reconfigure an active player body.");
   parts=surfaces; startHidden=hidden;
  }
  public void ValidateConfiguration() {
   if(parts==null || parts.Length==0) throw new InvalidOperationException("Assign the prepared male player body's dissolve surfaces.");
   foreach(var part in parts) { if(part==null) throw new InvalidOperationException("Player body surface is missing."); part.Validate(); }
  }
  void EnsureDriver() {
   if(driver!=null) return;
   if(!isActiveAndEnabled) throw new InvalidOperationException("Player body is disabled.");
   ValidateConfiguration(); stableHidden=startHidden;
   // One existing transition owner controls all original meshes and one sound cue.
   driver=new PerformerDissolve(transform,parts[0].profile,new BodyPresentation(parts),_=>{},null,startHidden,hidden=>stableHidden=hidden);
   driver.SetStableVisibility(startHidden);
  }
  void Awake() { EnsureDriver(); }
  void Update() { driver?.Advance(Time.deltaTime); }
  public void StartHiddenAt(Transform location) {
   if(location==null) throw new ArgumentNullException(nameof(location)); EnsureDriver();
   if(driver.IsDissolving) throw new InvalidOperationException("Player-body setup cannot interrupt a transition.");
   transform.SetPositionAndRotation(location.position,location.rotation); driver.SetStableVisibility(true);
  }
  public Awaitable<VisibilityCompletion> DissolveInAsync(float seconds=3f) {
   EnsureDriver(); var source=new AwaitableCompletionSource<VisibilityCompletion>();
   driver.DissolveIn(seconds,source); return source.Awaitable;
  }
  public Awaitable<VisibilityCompletion> DissolveOutAsync(float seconds=3f) {
   EnsureDriver(); var source=new AwaitableCompletionSource<VisibilityCompletion>();
   driver.DissolveOut(seconds,source); return source.Awaitable;
  }
  internal Action CaptureCancellation() => driver?.CaptureCancellation();
  void OnDisable() { driver?.Cancel(); }
  void OnDestroy() { driver?.Dispose(); driver=null; }
  sealed class BodyPresentation : IPerformerDissolvePresentation {
   readonly PlayerBodyDissolvePart[] surfaces;
   public BodyPresentation(PlayerBodyDissolvePart[] parts) { surfaces=parts; }
   public Vector3 BodyCenter => surfaces[0].rig.BodyCenter;
   public float EvaluateEffectCurve(float time) => surfaces[0].rig.EvaluateEffectCurve(time);
   void Each(Action<PlayerBodyDissolvePart> action) {
    Exception first=null;
    foreach(var part in surfaces) { try { action(part); } catch(Exception error) { if(first==null) first=error; } }
    if(first!=null) throw first;
   }
   public void Begin(PerformerDissolveProfile _,PerformerDissolveTiming timing) => Each(p=> { p.Prepare(); p.rig.Begin(p.profile,timing); });
   public void BeginVisibilityIn(PerformerDissolveProfile _) => Each(p=> { p.Prepare(); p.rig.BeginVisibilityIn(p.profile); });
   public void BeginVisibilityOut(PerformerDissolveProfile _) => Each(p=> { p.Prepare(); p.rig.BeginVisibilityOut(p.profile); });
   public void SetDissolveProgress(float progress) => Each(p=>p.rig.SetDissolveProgress(progress));
   public void CompleteDeparture() => Each(p=>p.rig.CompleteDeparture());
   public void BeginTransit(Vector3 target) => Each(p=>p.rig.BeginTransit(target));
   public void RetargetTransit(Vector3 target) => Each(p=>p.rig.RetargetTransit(target));
   public void SetTransitProgress(float progress) => Each(p=>p.rig.SetTransitProgress(progress));
   public void BeginMaterialize() => Each(p=>p.rig.BeginMaterialize());
   public void SetMaterializeProgress(float progress) => Each(p=>p.rig.SetMaterializeProgress(progress));
   public void SetVisibilityInProgress(float progress) => Each(p=>p.rig.SetVisibilityInProgress(progress));
   public void SetVisibilityOutProgress(float progress) => Each(p=>p.rig.SetVisibilityOutProgress(progress));
   public void Finish(bool hidden) => Each(p=>p.Finish(hidden,false));
   public void Restore(bool hidden) => Each(p=>p.Finish(hidden,true));
  }
 }
}