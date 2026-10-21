using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Performer;
using DazPose.UnityValidation.Partner;
using UnityEngine;
namespace DazPose.Player {
 /// <summary>
 /// Cheap programmatic breathing for the player body: a slow cycle of small bone rotations and offsets on the
 /// chest and abdomen, applied on top of each bone's rest pose in LateUpdate. It never touches the pelvis, hip or
 /// shaft bones: the contact target is measured from the pelvis and PartnerBody drives the shaft.
 /// </summary>
 [DisallowMultipleComponent]
 public sealed class PlayerBreathing : MonoBehaviour {
  [SerializeField] float breathsPerMinute=10f;
  [SerializeField,Range(0f,10f)] float strength=0.3f;
  [SerializeField] BreathingBoneChannel[] channels=DefaultChannels();
  Transform[] bones=Array.Empty<Transform>();
  Vector3[] restPositions=Array.Empty<Vector3>();
  Quaternion[] restRotations=Array.Empty<Quaternion>();
  PlayerBodyPresence presence;
  float phase;
  bool prepared,applied;
  public float BreathsPerMinute { get => breathsPerMinute; set => breathsPerMinute=Mathf.Max(0f,value); }
  public float Strength { get => strength; set => strength=Mathf.Max(0f,value); }
  public IReadOnlyList<BreathingBoneChannel> Channels => channels;
  public bool IsPrepared => prepared;
  public void SetChannels(IEnumerable<BreathingBoneChannel> value) { if(prepared) throw new InvalidOperationException("Disable the breathing before changing its channels."); channels=value.ToArray(); }

  /// <summary>Lara's tuned chest and abdomen values, the starting point for the player body.</summary>
  public static BreathingBoneChannel[] DefaultChannels() => new[]{
   Channel("abdomenLower",new Vector3(-0.2f,0,0),Vector3.zero),
   Channel("abdomenUpper",new Vector3(-0.3f,0,0),new Vector3(0,0.0003f,0)),
   Channel("chestLower",new Vector3(-0.4f,0,0),new Vector3(0,0.0005f,0)),
   Channel("chestUpper",new Vector3(-0.25f,0,0),new Vector3(0,0.0003f,0))};
  static BreathingBoneChannel Channel(string bone,Vector3 rotation,Vector3 position) =>
   new BreathingBoneChannel{ boneName=bone,fullInhaleLocalRotationDelta=rotation,fullInhaleLocalPositionDelta=position };

  /// <summary>0 at the start of a breath rising smoothly to 1 at the middle and back; phase is in cycles.</summary>
  public static float BreathValue(float cycles) => 0.5f-0.5f*Mathf.Cos(Mathf.Repeat(cycles,1f)*2f*Mathf.PI);

  /// <summary>Finds every channel's bone, rejects forbidden ones by name, and records the rest pose. Throws on any problem.</summary>
  public void Prepare() {
   if(prepared) return;
   var all=GetComponentsInChildren<Transform>(true);
   var forbidden=ForbiddenTransforms(all);
   // The player prefab holds more than one skeleton (the eyelashes carry a copy). Breathing must move the
   // skeleton that skins the body, so bones are looked up under the biggest skinned mesh's root bone.
   var skeleton=SkeletonRoot().GetComponentsInChildren<Transform>(true);
   var found=new List<Transform>();
   foreach(var channel in channels) {
    var bone=skeleton.FirstOrDefault(t=>t.name==channel.boneName);
    if(bone==null) throw new InvalidOperationException("Breathing bone '"+channel.boneName+"' does not exist on the body skeleton of "+name+".");
    string reason=ForbiddenReason(bone,forbidden);
    if(reason!=null) throw new InvalidOperationException("Breathing bone '"+channel.boneName+"' "+reason+"; breathing must not move it.");
    if(found.Contains(bone)) throw new InvalidOperationException("Breathing bone '"+channel.boneName+"' is listed twice.");
    found.Add(bone);
   }
   bones=found.ToArray();
   restPositions=bones.Select(b=>b.localPosition).ToArray();
   restRotations=bones.Select(b=>b.localRotation).ToArray();
   prepared=true;
  }
  Transform SkeletonRoot() {
   var main=GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.rootBone!=null)
    .OrderByDescending(r=>r.bones!=null ? r.bones.Length : 0).FirstOrDefault();
   return main!=null ? main.rootBone : transform;
  }
  List<Transform> ForbiddenTransforms(Transform[] all) {
   var list=new List<Transform>();
   var partner=GetComponent<PartnerBody>();
   if(partner!=null) {
    if(partner.MainPelvis!=null) list.Add(partner.MainPelvis);
    if(partner.DonorPelvis!=null) list.Add(partner.DonorPelvis);
    if(partner.ShaftBones!=null) list.AddRange(partner.ShaftBones.Where(b=>b!=null));
   }
   list.AddRange(all.Where(t=>IsForbiddenName(t.name)));
   return list;
  }
  static bool IsForbiddenName(string boneName) {
   string lower=boneName.ToLowerInvariant();
   return lower=="hip" || lower=="pelvis" || lower.Contains("penis") || lower.Contains("shaft");
  }
  static string ForbiddenReason(Transform bone,List<Transform> forbidden) {
   if(IsForbiddenName(bone.name)) return "is the pelvis, a hip bone or a shaft bone";
   foreach(var f in forbidden) {
    if(f==bone) return "is the pelvis or a shaft bone";
    if(f.IsChildOf(bone)) return "is an ancestor of the pelvis or a shaft bone ("+f.name+")";
   }
   return null;
  }

  /// <summary>Puts every breathing bone at its rest pose plus the scaled offset for this point in the cycle. No accumulation.</summary>
  public void ApplyAtCycles(float cycles) {
   Prepare();
   float inhale=BreathValue(cycles)*strength;
   for(int i=0;i<bones.Length;i++) {
    var channel=channels[i];
    bones[i].localPosition=restPositions[i]+channel.fullInhaleLocalPositionDelta*inhale;
    bones[i].localRotation=restRotations[i]*Quaternion.Euler(channel.fullInhaleLocalRotationDelta*inhale);
   }
   applied=true;
  }
  /// <summary>Returns every breathing bone to its recorded rest pose.</summary>
  public void RestoreRest() {
   if(!prepared) return;
   for(int i=0;i<bones.Length;i++) { if(bones[i]==null) continue; bones[i].localPosition=restPositions[i]; bones[i].localRotation=restRotations[i]; }
   applied=false;
  }
  void OnEnable() {
   presence=GetComponent<PlayerBodyPresence>();
   try { Prepare(); }
   catch(Exception error) { Debug.LogException(error,this); enabled=false; }
  }
  void OnDisable() { RestoreRest(); prepared=false; }
  void LateUpdate() => Tick(Time.deltaTime);
  /// <summary>One frame of breathing. Does nothing while the body is stably hidden, restoring the rest pose once.</summary>
  public void Tick(float deltaTime) {
   if(!prepared) return;
   if(presence==null) presence=GetComponent<PlayerBodyPresence>();
   if(presence!=null && presence.IsStablyHidden) { if(applied) RestoreRest(); return; }
   phase=Mathf.Repeat(phase+Mathf.Max(0f,deltaTime)*breathsPerMinute/60f,1f);
   ApplyAtCycles(phase);
  }
 }
}
