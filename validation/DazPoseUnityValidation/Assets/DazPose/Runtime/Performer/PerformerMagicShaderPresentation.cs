using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DazPose.Performer {
 [DisallowMultipleComponent]
 public sealed class PerformerMagicShaderPresentation : MonoBehaviour {
  sealed class Surface { public Renderer renderer; public Material[] normal,effect; }
  sealed class RefreshLease : IDisposable {
   Action complete;
   public RefreshLease(Action complete) { this.complete=complete; }
   public void Dispose() { var action=complete; complete=null; action?.Invoke(); }
  }
  readonly List<Surface> surfaces=new List<Surface>();
  PerformerMagicShaderProfile active;
  int generation,refreshDepth;
  public bool IsRefreshing => refreshDepth>0;
  public PerformerMaterialMode Mode => active==null || IsRefreshing ? PerformerMaterialMode.Regular : active.mode;
  public void Apply(PerformerMagicShaderProfile profile) {
   if(profile==null) throw new ArgumentNullException(nameof(profile)); profile.ValidateConfiguration();
   RestoreRegular();
   if(profile.mode==PerformerMaterialMode.Regular) return;
   active=profile;
   try { ApplyCurrentRenderers(); } catch { RestoreRegular(); throw; }
  }
  void ApplyCurrentRenderers() {
   foreach(var renderer in GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled && r.gameObject.activeInHierarchy && (r is SkinnedMeshRenderer || r is MeshRenderer))) {
    var normal=renderer.sharedMaterials;
    var effect=normal.Select(_=>new Material(active.wireframeMaterial) { name="Lara wireframe (runtime)",hideFlags=HideFlags.DontSave }).ToArray();
    var surface=new Surface {renderer=renderer,normal=normal,effect=effect}; surfaces.Add(surface); renderer.sharedMaterials=effect;
   }
  }
  void RestoreSurfaces() {
   foreach(var surface in surfaces) {
    if(surface.renderer!=null) {
     var current=surface.renderer.sharedMaterials; bool changed=false;
     for(int i=0;i<current.Length;i++) {
      int owned=Array.IndexOf(surface.effect,current[i]);
      if(owned>=0) {current[i]=surface.normal[owned];changed=true;}
     }
     if(changed) surface.renderer.sharedMaterials=current;
    }
    foreach(var material in surface.effect) if(material!=null) { if(Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
   }
   surfaces.Clear();
  }
  public void RestoreRegular() { ++generation; RestoreSurfaces(); active=null; }
  public Action CaptureRestoration() {
   int owned=generation; return ()=> { if(owned==generation) RestoreRegular(); };
  }
  public IDisposable BeginWardrobeRefresh() {
   var requested=active; int owned=generation; if(refreshDepth++==0)RestoreSurfaces();
   return new RefreshLease(()=> {
    if(--refreshDepth!=0)return;
    if(owned!=generation || requested==null || !isActiveAndEnabled) return;
    active=requested;
    try { ApplyCurrentRenderers(); } catch { RestoreRegular(); throw; }
   });
  }
  public void RefreshRenderers() { using(BeginWardrobeRefresh()) { } }
  void OnDisable() { RestoreRegular(); }
  void OnDestroy() { RestoreRegular(); }
 }
}