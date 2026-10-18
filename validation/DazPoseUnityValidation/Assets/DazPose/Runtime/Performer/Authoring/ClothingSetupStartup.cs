using System;
using UnityEngine;

namespace DazPose.Performer {
 [DisallowMultipleComponent]
 public sealed class ClothingSetupStartup : MonoBehaviour {
  [SerializeField] private SuccubusPerformer actor;
  [SerializeField] private string initialOutfitId="first-outfit";
  public string Status {get;private set;}="Waiting for preview";
#if UNITY_EDITOR
  public void Configure(SuccubusPerformer performer,string outfitId="first-outfit") {actor=performer;initialOutfitId=outfitId;}
#endif
  async void Start() {
   var cancellation=destroyCancellationToken;
   try {
    for(int frame=0;frame<180 && actor!=null && !actor.IsRuntimeReady;frame++) await Awaitable.NextFrameAsync(cancellation);
    if(actor==null || !actor.IsRuntimeReady) throw new InvalidOperationException("Clothing Setup's saved Lara performer did not initialize.");
    var wardrobe=actor.GetComponent<PerformerWardrobe>();
    if(wardrobe==null) throw new InvalidOperationException("Clothing Setup needs its saved wardrobe binding.");
    // Preserve an already selected outfit; use the saved authoring default only for a fresh preview.
    if(string.IsNullOrEmpty(actor.CurrentWardrobe.OutfitId) && !string.IsNullOrEmpty(initialOutfitId)) {
     var preset=Array.Find(wardrobe.Catalog.Presets,p=>p.PresetId==initialOutfitId);
     if(preset==null) throw new InvalidOperationException("Clothing Setup's saved initial outfit is missing: "+initialOutfitId);
     var result=await actor.OutfitAsync(preset);
     if(result.Status!=WardrobeChangeStatus.Applied && result.Status!=WardrobeChangeStatus.AlreadyEquipped)
      throw new InvalidOperationException(result.Status+": "+result.Message);
    }
    if(actor.IsHidden) await actor.DissolveInAsync(.25f);
    cancellation.ThrowIfCancellationRequested();
    Status="Ready";
   } catch(OperationCanceledException) when(cancellation.IsCancellationRequested) {Status="Closed";}
   catch(Exception error) {Status="Error: "+error.Message;Debug.LogException(error,this);}
  }
 }
}
