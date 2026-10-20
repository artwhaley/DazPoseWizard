using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Story.Language;
using UnityEngine;
namespace DazPose.Story {
 [Serializable] public sealed class StorySceneSymbol { public string id; public StoryBindingKind kind; }
 [Serializable] public sealed class StoryPoseContent { public string id; public PerformerPose value; public PlayerPosture posture=PlayerPosture.Standing; }
 [Serializable] public sealed class StoryGestureContent { public string id; public PerformerGesture value; }
 [Serializable] public sealed class StoryShaderContent { public string id; public PerformerMagicShaderProfile value; }
 [Serializable] public sealed class StoryWardrobeContent { public string id; public WardrobePreset value; }
 [Serializable] public sealed class StoryHairContent { public string id; public GameObject value; }
 [Serializable] public sealed class StoryMotionContent { public string id; public FunscriptMotionProgram value; }
 [Serializable] public sealed class StoryDialogueContent {
  [HideInInspector] public string id;
  [TextArea(2,5)] public string transcript;
  public AudioClip clip;
  public bool engineeringPlaceholder;
 }
 [CreateAssetMenu(menuName="DazPose/Story/Content Bindings")]
 public sealed class StoryContentBindings : ScriptableObject {
  [HideInInspector] public string ownerSourceGuid;
  public StorySceneSymbol[] sceneSymbols=Array.Empty<StorySceneSymbol>();
  public StoryPoseContent[] poses=Array.Empty<StoryPoseContent>();
  public StoryGestureContent[] gestures=Array.Empty<StoryGestureContent>();
  public StoryShaderContent[] shaders=Array.Empty<StoryShaderContent>();
  public StoryWardrobeContent[] wardrobe=Array.Empty<StoryWardrobeContent>();
  public StoryHairContent[] hair=Array.Empty<StoryHairContent>();
  public StoryMotionContent[] motion=Array.Empty<StoryMotionContent>();
  public StoryDialogueContent[] dialogue=Array.Empty<StoryDialogueContent>();
  public Dictionary<string,StoryBindingKind> Symbols() {
   var result=new Dictionary<string,StoryBindingKind>(StringComparer.Ordinal);
   foreach(var row in sceneSymbols) Add(result,row.id,row.kind);
   foreach(var row in poses) Add(result,row.id,StoryBindingKind.Pose);
   foreach(var row in gestures) Add(result,row.id,StoryBindingKind.Gesture);
   foreach(var row in shaders) Add(result,row.id,StoryBindingKind.MagicShaderProfile);
   foreach(var row in motion) Add(result,row.id,StoryBindingKind.Funscript);
   foreach(var row in wardrobe) Add(result,row.id,StoryBindingKind.WardrobePreset);
   foreach(var row in hair) Add(result,row.id,StoryBindingKind.Hair);
   return result;
  }
  static void Add(Dictionary<string,StoryBindingKind> symbols,string id,StoryBindingKind kind) {
   if(string.IsNullOrWhiteSpace(id) || symbols.ContainsKey(id)) throw new InvalidOperationException("Duplicate or empty story symbol '"+id+"'."); symbols.Add(id,kind);
  }
  public StoryPoseContent Pose(string id)=>poses.SingleOrDefault(p=>p.id==id) ?? throw new InvalidOperationException("Unknown Pose symbol '"+id+"'.");
  public PerformerGesture Gesture(string id)=>gestures.SingleOrDefault(p=>p.id==id)?.value ?? throw new InvalidOperationException("Assign the PerformerGesture wrapper for '"+id+"'.");
  public PerformerMagicShaderProfile Shader(string id)=>shaders.SingleOrDefault(p=>p.id==id)?.value ?? throw new InvalidOperationException("Assign the MagicShaderProfile for '"+id+"'.");
  public FunscriptMotionProgram Motion(string id)=>motion.SingleOrDefault(p=>p.id==id)?.value ?? throw new InvalidOperationException("Assign the imported Funscript for '"+id+"'.");
  public WardrobePreset Wardrobe(string id)=>wardrobe.SingleOrDefault(p=>p.id==id)?.value ?? throw new InvalidOperationException("Assign the released wardrobe preset for '"+id+"'.");
  public WardrobeAttachmentDescriptor Hair(string id) {
   var prefab=hair.SingleOrDefault(p=>p.id==id)?.value;
   if(prefab==null || prefab.scene.IsValid() || prefab.transform.parent!=null)throw new InvalidOperationException("Assign a Project hair prefab root for '"+id+"'.");
   var descriptor=prefab.GetComponent<WardrobeAttachmentDescriptor>();
   if(descriptor==null || descriptor.AttachmentRole!=WardrobeAttachmentRole.Hair)throw new InvalidOperationException("Hair slot requires a generated Hair descriptor on the prefab root.");
   if(!descriptor.Validate(out var error))throw new InvalidOperationException(error);
   return descriptor;
  }
  public void ValidateAppearance(StoryBindingUse use,StorySceneBindings scene) {
   var owner=scene?.lara!=null ? scene.lara.GetComponent<PerformerWardrobe>() : null;
   if(scene!=null && owner==null)throw new InvalidOperationException("Lara requires PerformerWardrobe.");
   if(use.kind==StoryBindingKind.WardrobePreset) {
    var preset=Wardrobe(use.symbol);
    if(!preset.Validate(out var error))throw new InvalidOperationException(error);
    if(preset.FitStates.Length==0 || preset.FitStates.Any(f=>f==null || f.Status!=WardrobeFitStatus.Validated || f.BodyMesh==null || f.SurfaceBindings==null || f.DissolveProfile==null))throw new InvalidOperationException("Wardrobe preset requires validated released fits.");
    if(owner!=null && !owner.TryValidateReadiness(Application.isPlaying,new[]{preset},out var readiness))throw new InvalidOperationException(readiness);
   } else {
    var descriptor=Hair(use.symbol);
    if(owner!=null) {
     var rig=owner.RigBinding?.Manifest;
     if(rig==null || owner.Catalog==null || rig.GenerationId!=owner.Catalog.ReleasedGenerationId || descriptor.GenerationId!=rig.GenerationId || descriptor.CharacterSignature!=rig.CharacterSignature || !rig.TryGetHair(descriptor.StableKey,out var expected) || expected!=descriptor)throw new InvalidOperationException("Hair prefab must match Lara's configured rig manifest, generation and character signature.");
    }
   }
  }
  public AudioClip Dialogue(string transcript) {
   string id=StoryDialogue.IdFor(transcript); var row=dialogue.SingleOrDefault(p=>p.id==id);
   if(row==null || row.transcript!=transcript || row.id!=StoryDialogue.IdFor(row.transcript) || row.clip==null)
    throw new InvalidOperationException("No current AudioClip binding for transcript: "+transcript+". Changing a transcript invalidates its old audio binding.");
   return row.clip;
  }
  public string[] Diagnostics(CompiledStoryProgram program,StorySceneBindings scene=null) {
   var errors=new List<string>(); Symbols();
   foreach(var use in program.bindings) {
    try {
     switch(use.kind) {
      case StoryBindingKind.Pose:var pose=Pose(use.symbol); if(pose.value==null || pose.value.Clip==null) throw new InvalidOperationException("Assign the generated PerformerPose wrapper"); break;
      case StoryBindingKind.Gesture:var gesture=Gesture(use.symbol); if(!gesture.IsReady(out string reason))throw new InvalidOperationException(reason);break;
      case StoryBindingKind.MagicShaderProfile:Shader(use.symbol).ValidateConfiguration();break;
      case StoryBindingKind.Funscript:Motion(use.symbol);break;
      case StoryBindingKind.WardrobePreset:case StoryBindingKind.Hair:ValidateAppearance(use,scene);break;
      default:if(scene!=null) scene.ValidateSymbol(use);break;
     }
    } catch(Exception error) { errors.Add(use.source+"\nMissing/invalid "+use.kind+" binding '"+use.symbol+"': "+error.Message); }
   }
   foreach(var line in program.dialogue) { try { Dialogue(line.transcript); } catch(Exception error) { errors.Add(line.source+"\n"+error.Message); } }
   return errors.Distinct().ToArray();
  }
 }
}