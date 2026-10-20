using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Performer;
using DazPose.Story;
using UnityEngine;
using UnityEditor;
namespace DazPose.Editor.Studio {
 // Appearance-only surfaces share the authored skeleton. No runtime controller or source MPB writes.
 internal sealed class NativeWardrobeProjection : IDisposable {
  readonly NativeStageContext context;
  readonly HashSet<Renderer> replaced=new HashSet<Renderer>();
  readonly List<Renderer> effective=new List<Renderer>();
  readonly List<WardrobeAttachmentLease> attachments=new List<WardrobeAttachmentLease>();
  readonly Dictionary<Renderer,Renderer> blocksFrom=new Dictionary<Renderer,Renderer>();
  readonly Dictionary<Renderer,float[]> opacities=new Dictionary<Renderer,float[]>();
  readonly Dictionary<Renderer,Material[]> normal=new Dictionary<Renderer,Material[]>();
  readonly Material[] bodyMaterials;readonly float baselineFootRootY;readonly bool baselineHidden;
  LaraHeelReview.WardrobeEndpoint footwear;
  GameObject bodyNode;SkinnedMeshRenderer source,body;
  WardrobePreset preset;WardrobeFitState fit;WardrobeSelection selection;
  public int PrepareCount {get;private set;} public int AttachmentBuildCount {get;private set;}
  public int CommitCount {get;private set;} public int CacheHits {get;private set;}
  public double ColdPrepareMilliseconds {get;private set;}
  public bool Active=>body!=null;
  public string VisibilityKey=>context.Lara.GetComponent<LaraWardrobe>()?.ShowClothes+"/"+context.Lara.GetComponent<LaraWardrobe>()?.ShowGlossShells;
  public string Description {get;private set;}="Saved appearance";
  public NativeWardrobeProjection(NativeStageContext context){this.context=context;bodyMaterials=context.Lara.GetComponent<PerformerWardrobe>()?.Body?.sharedMaterials;var original=context.Lara.GetComponent<PerformerWardrobe>()?.Body;baselineHidden=original!=null&&(original.forceRenderingOff||!original.enabled);baselineFootRootY=context.Lara.GetComponent<LaraHeelReview>()!=null?context.Lara.GetComponent<LaraHeelReview>().BaseRigY:original!=null&&original.rootBone!=null&&original.rootBone.parent!=null?original.rootBone.parent.localPosition.y:0;}
  public bool Replaces(Renderer renderer)=>Active&&replaced.Contains(renderer);
  public IEnumerable<Renderer> Effective=>effective;
  public void Prepare(AuthoringVisualState state,Func<AuthoringValue,StoryContentBindings> content,Action<UnityEngine.Object> dependency,int revision){
   var outfit=state[AuthoringField.Outfit];var hair=state[AuthoringField.Hair];var layers=state[AuthoringField.Layers];
   if(!outfit.IsKnown && !hair.IsKnown && !layers.IsKnown){Clear();return;}
   if(outfit.Knowledge==AuthoringKnowledge.Unknown||hair.Knowledge==AuthoringKnowledge.Unknown||layers.Knowledge==AuthoringKnowledge.Unknown){Clear();throw new InvalidOperationException("Wardrobe is ambiguous after skipped logic; saved appearance shown.");}
   var wardrobe=context.Lara.GetComponent<PerformerWardrobe>();source=wardrobe?.Body;
   var catalog=wardrobe?.Catalog;var manifest=wardrobe?.RigBinding?.Manifest;
   if(source==null||catalog==null||manifest==null)throw new InvalidOperationException("Lara needs a released wardrobe catalog and rig manifest.");
   dependency(catalog);dependency(manifest);dependency(wardrobe.Configuration);
   var owner=outfit.IsKnown?content(outfit):null;dependency(owner);
   var inventory=context.Lara.GetComponent<LaraWardrobe>();dependency(inventory);
   var target=outfit.IsKnown?owner.wardrobe.SingleOrDefault(r=>r.id==outfit.Symbol)?.value:
    catalog.Presets.FirstOrDefault(p=>inventory!=null&&inventory.Pieces.Any(piece=>!piece.persistentHair&&piece.visible&&(piece.stableKey==p.PresetId||piece.name.StartsWith(p.PresetId+"/",StringComparison.Ordinal))));
   bool savedNaked=!outfit.IsKnown&&target==null;
   if(savedNaked)target=catalog.Presets.FirstOrDefault();
   if(target==null){Clear();throw new InvalidOperationException("Assign an explicit outfit before wardrobe preview; saved appearance shown.");}
   if(!catalog.Presets.Contains(target))throw new InvalidOperationException("Outfit slot belongs to a different wardrobe catalog.");
   dependency(target);dependency(target.Package);
   int mask=target.PopulatedMask;
   int ceiling=AuthoringVisualReducer.LayerCeiling(layers,mask,savedNaked?-1:2);
   string hairKey="";
   if(hair.IsKnown&&hair.Op=="hair"){
    var hairOwner=content(hair);dependency(hairOwner);var asset=hairOwner.hair.SingleOrDefault(r=>r.id==hair.Symbol)?.value;
    var descriptor=asset!=null?asset.GetComponent<WardrobeAttachmentDescriptor>():null;
    if(descriptor==null||descriptor.AttachmentRole!=WardrobeAttachmentRole.Hair)throw new InvalidOperationException("Assign a generated hair prefab to "+hair.Symbol);
    hairKey=descriptor.StableKey;dependency(asset);
   }else if(!hair.IsKnown){
    // Preserve the actual saved visible hair, rather than imposing the outfit's legacy default.
    hairKey=inventory?.Pieces.FirstOrDefault(p=>p.persistentHair&&p.visible)?.stableKey??"";
   }
   var next=new WardrobeSelection(catalog.ReleasedGenerationId,target.PresetId,ceiling,hairKey);
   var fits=target.FitStates;
   var prepared=WardrobeSelectionPreparer.Prepare(next,catalog.ReleasedGenerationId,mask,fits.Select(f=>new WardrobeFitSelectionFact(f.VisibleMask,f.Status==WardrobeFitStatus.Validated,f.BodyMesh!=null&&f.SurfaceBindings!=null&&f.DissolveProfile!=null)).ToArray());
   if(!prepared.IsValid)throw new InvalidOperationException(prepared.Error);
   var targetFit=fits[prepared.FitIndex];dependency(targetFit);dependency(targetFit.BodyMesh);dependency(target.Footwear);
   if(targetFit.CharacterSignature!=manifest.CharacterSignature||manifest.GenerationId!=catalog.ReleasedGenerationId||targetFit.SurfaceBindings.SourceMesh!=targetFit.BodyMesh||targetFit.DissolveProfile.SurfaceBindings!=targetFit.SurfaceBindings)
    throw new InvalidOperationException("Wardrobe fit, rig and catalog do not identify the same released endpoint.");
   if(manifest.TryGetClothing(target.PresetId,out var expectedClothing))dependency(expectedClothing);
   if(hairKey.Length>0&&manifest.TryGetHair(hairKey,out var expectedHair))dependency(expectedHair);
   revision=HashCode.Combine(EditorUtility.GetDirtyCount(target),EditorUtility.GetDirtyCount(targetFit),EditorUtility.GetDirtyCount(targetFit.BodyMesh),EditorUtility.GetDirtyCount(manifest),wardrobe.Configuration!=null?EditorUtility.GetDirtyCount(wardrobe.Configuration):0);
   if(selection!=null&&selection.Equals(next)&&appearanceRevision==revision){CacheHits++;return;}
   var watch=System.Diagnostics.Stopwatch.StartNew();PrepareCount++;Clear();preset=target;fit=targetFit;selection=next;appearanceRevision=revision;
   try{
    bodyNode=new GameObject("Wardrobe preview body"){hideFlags=HideFlags.HideAndDontSave};
    bodyNode.transform.SetParent(source.transform,false);body=bodyNode.AddComponent<SkinnedMeshRenderer>();
    body.sharedMesh=fit.BodyMesh;body.bones=source.bones;body.rootBone=source.rootBone;body.sharedMaterials=bodyMaterials;
    CopyFlags(source,body);body.enabled=false;effective.Add(body);normal[body]=body.sharedMaterials;blocksFrom[body]=source;replaced.Add(source);
    foreach(var piece in context.Lara.GetComponent<LaraWardrobe>()?.Pieces??Array.Empty<LaraWardrobe.Piece>())if(piece?.renderer!=null)replaced.Add(piece.renderer);
    if(!manifest.TryGetClothing(preset.PresetId,out var clothes))throw new InvalidOperationException("Released clothing prefab missing: "+preset.PresetId);
    Bind(clothes,manifest,prepared.VisibleMask,false,dependency);
    if(hairKey.Length>0){if(!manifest.TryGetHair(hairKey,out var hairPrefab))throw new InvalidOperationException("Released hairstyle missing: "+hairKey);Bind(hairPrefab,manifest,prepared.VisibleMask,true,dependency);}
    Description=preset.DisplayName+" / "+(hairKey.Length==0?"no hair":hairKey)+" / "+(prepared.VisibleMask==0?"all layers removed":string.Join(", ",preset.Parts.Where(p=>(prepared.VisibleMask&(1<<p.layer))!=0).Select(p=>"layer "+p.layer+": "+p.sourcePieceId)));
    footwear=new LaraHeelReview.WardrobeEndpoint(body,preset.Footwear,fit.Footwear,context.Lara.GetComponent<LaraHeelReview>(),baselineFootRootY);
    ColdPrepareMilliseconds=watch.Elapsed.TotalMilliseconds;
   }catch{Clear();throw;}
  }
  int appearanceRevision;
  void Bind(WardrobeAttachmentDescriptor descriptor,WardrobeRigManifest manifest,int mask,bool hair,Action<UnityEngine.Object> dependency){
   dependency(descriptor);var lease=WardrobeAttachmentBinder.Bind(descriptor,source,null,manifest.GenerationId,manifest.CharacterSignature);attachments.Add(lease);AttachmentBuildCount++;
   lease.MarkTransient();
   foreach(var piece in lease.Pieces){
    var renderer=piece.renderer;CopyFlags(source,renderer);
    var materials=renderer.sharedMaterials;
    var template=descriptor.Parts.First(p=>piece.name.EndsWith("/"+p.stablePieceId,StringComparison.Ordinal)).renderer;
    blocksFrom[renderer]=template;
    var alpha=Enumerable.Repeat(-1f,materials.Length).ToArray();
    foreach(var row in context.Lara.GetComponent<PerformerWardrobe>().Configuration?.MaterialOverrides??Array.Empty<WardrobeConfiguration.MaterialOverride>())
     if(row.presetId==descriptor.PresetId&&piece.name.EndsWith("/"+row.sourcePieceId,StringComparison.Ordinal)&&int.TryParse(row.materialSlotId,out int slot)&&slot>=0&&slot<materials.Length ){
      if(row.material!=null)materials[slot]=row.material;
      if(row.opacityOverride)alpha[slot]=row.opacity;
     }
    renderer.sharedMaterials=materials;normal[renderer]=materials;opacities[renderer]=alpha;effective.Add(renderer);
    visible[renderer]=hair||(mask&(1<<piece.layer))!=0;
    coverage[renderer]=piece.coverageChannel;pieces[renderer]=piece;
    foreach(var material in materials)dependency(material);
   }
  }
  readonly Dictionary<Renderer,LaraWardrobe.Piece> pieces=new Dictionary<Renderer,LaraWardrobe.Piece>();
  readonly Dictionary<Renderer,bool> visible=new Dictionary<Renderer,bool>();
  readonly Dictionary<Renderer,int> coverage=new Dictionary<Renderer,int>();
  static void CopyFlags(SkinnedMeshRenderer from,SkinnedMeshRenderer to){
   to.renderingLayerMask=from.renderingLayerMask;to.shadowCastingMode=from.shadowCastingMode;to.receiveShadows=from.receiveShadows;to.lightProbeUsage=from.lightProbeUsage;to.reflectionProbeUsage=from.reflectionProbeUsage;to.probeAnchor=from.probeAnchor;to.sortingLayerID=from.sortingLayerID;to.sortingOrder=from.sortingOrder;to.localBounds=from.localBounds;to.updateWhenOffscreen=true;to.allowOcclusionWhenDynamic=false;
  }
  public void CaptureFootwearPose()=>footwear?.CapturePose();
  public void ApplyFootwear(bool seated)=>footwear?.Apply(seated);
  public void Hide(){foreach(var renderer in effective)if(renderer!=null)renderer.enabled=false;}
  bool Shows(Renderer renderer){var wardrobe=context.Lara.GetComponent<LaraWardrobe>();var piece=pieces[renderer];return visible[renderer]&&(piece.persistentHair||wardrobe==null||wardrobe.ShowClothes)&&(!piece.shell||wardrobe==null||wardrobe.ShowGlossShells);}
  public void Present(bool? hidden,PerformerMagicShaderProfile material){
   if(!Active)return;CommitCount++;
   foreach(var renderer in replaced)if(renderer!=null)renderer.forceRenderingOff=true;
   bool front=coverage.Any(p=>p.Value==0&&Shows(p.Key)),dress=coverage.Any(p=>p.Value==1&&Shows(p.Key));
   foreach(var renderer in effective){
    if(renderer==null)continue;renderer.enabled=renderer==body||Shows(renderer);renderer.forceRenderingOff=hidden??baselineHidden;
    renderer.sharedMaterials=material!=null&&material.mode==PerformerMaterialMode.GlowyWireframe?normal[renderer].Select(_=>material.wireframeMaterial).ToArray():normal[renderer];
    var block=new MaterialPropertyBlock();var original=blocksFrom[renderer];original.GetPropertyBlock(block);block.SetFloat("_WardrobeCoverageEnabled",renderer==body&&front?1:0);block.SetFloat("_WardrobeDressCoverageEnabled",renderer==body&&dress?1:0);renderer.SetPropertyBlock(block);
    var materials=normal[renderer];
    for(int slot=0;slot<materials.Length;slot++){
     original.GetPropertyBlock(block,slot);
     if(block.isEmpty)original.GetPropertyBlock(block);
     block.SetFloat("_WardrobeCoverageEnabled",renderer==body&&front?1:0);block.SetFloat("_WardrobeDressCoverageEnabled",renderer==body&&dress?1:0);
     if(opacities.TryGetValue(renderer,out var alpha)&&alpha[slot]>=0)WardrobeMaterialOpacity.Apply(block,materials[slot],alpha[slot]);
     renderer.SetPropertyBlock(block,slot);
    }
   }
   CopyMorphs();
  }
  public void CopyMorphs(){
   if(!Active)return;
   foreach(var renderer in effective.OfType<SkinnedMeshRenderer>()){
    var mesh=renderer.sharedMesh;if(mesh==null)continue;
    for(int index=0;index<mesh.blendShapeCount;index++){int original=source.sharedMesh.GetBlendShapeIndex(mesh.GetBlendShapeName(index));if(original>=0)renderer.SetBlendShapeWeight(index,source.GetBlendShapeWeight(original));}
   }
   var anatomy=context.Lara.GetComponent<LaraAnatomyControls>();
   if(anatomy!=null){SetMorph("CapturedOpening",anatomy.CapturedOpening*100);SetMorph("Genesis8Female__PBMNipples",anatomy.Nipples*100);SetMorph("LaraBreastsAdjustment",(anatomy.LaraBreastsMeshPreview-anatomy.BakedLaraBreasts)*100);}
   SetMorph("CapturedHeelFootPose",fit.Footwear.BentFootPoseActive?100:0);SetMorph("WardrobeFootFit",fit.Footwear.BentFootPoseActive?fit.Footwear.FootShrink/.05f*100:0);
  }
  void SetMorph(string name,float value){foreach(var renderer in effective.OfType<SkinnedMeshRenderer>()){int index=renderer.sharedMesh.GetBlendShapeIndex(name);if(index>=0)renderer.SetBlendShapeWeight(index,value);}}
  void Clear(){foreach(var lease in attachments)lease.Dispose();attachments.Clear();if(bodyNode!=null)UnityEngine.Object.DestroyImmediate(bodyNode);bodyNode=null;body=null;footwear=null;effective.Clear();replaced.Clear();normal.Clear();blocksFrom.Clear();opacities.Clear();pieces.Clear();visible.Clear();coverage.Clear();selection=null;Description="Saved appearance";}
  public void Dispose()=>Clear();
 }
}
