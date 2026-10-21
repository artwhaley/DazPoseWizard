using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
namespace DazPose.Player {
 /// <summary>
 /// Fade to black drawn by the camera, two ways at once so it cannot silently fail to show: a runtime global post-process
 /// volume whose Color Adjustments colour filter goes from white to black (this multiplies the whole finished frame,
 /// particles included), and a camera-parented quad whose shader ignores depth. Not a Screen Space - Overlay canvas, and
 /// it never touches HeadPose. Nothing has to be saved in any scene or prefab.
 /// </summary>
 [DisallowMultipleComponent]
 public sealed class PlayerScreenFade : MonoBehaviour, IScreenFade {
  public const string ShaderResource="DazPose/PlayerScreenFade";
  static readonly int ColorId=Shader.PropertyToID("_FadeColor");
  Renderer quad;
  Volume volume;
  VolumeProfile profile;
  ColorAdjustments adjustments;
  Mesh mesh;
  Material material;
  MaterialPropertyBlock block;
  float alpha;
  bool reported;
  public float Alpha {
   get => alpha;
   set {
    alpha=Mathf.Clamp01(value);
    if(quad==null) { if(alpha<=0f) return; Build(); }
    Apply();
    if(alpha>=1f && !reported) { reported=true; Debug.Log("Screen fade reached full black: post-process volume "+(volume!=null && volume.enabled ? "on" : "missing")+", quad "+(quad!=null && quad.enabled ? "on" : "missing")+".",this); }
   }
  }
  /// <summary>The colour filter the fade volume applies to the whole frame (white = clear, black = full fade).</summary>
  public Color FilterColor => adjustments!=null ? adjustments.colorFilter.value : Color.white;
  public bool VolumeActive => volume!=null && volume.enabled && volume.isGlobal;
  public bool QuadActive => quad!=null && quad.enabled;
  /// <summary>The fade on this camera, added the first time it is needed.</summary>
  public static PlayerScreenFade For(Camera camera) {
   if(camera==null) throw new ArgumentNullException(nameof(camera));
   var fade=camera.GetComponent<PlayerScreenFade>();
   return fade!=null ? fade : camera.gameObject.AddComponent<PlayerScreenFade>();
  }
  void Build() {
   var shader=Resources.Load<Shader>(ShaderResource);
   if(shader==null) throw new InvalidOperationException("Missing the screen fade shader Resources/"+ShaderResource+".");
   var camera=GetComponent<Camera>();
   var holder=new GameObject("Screen Fade") { hideFlags=HideFlags.DontSave, layer=gameObject.layer };
   holder.transform.SetParent(transform,false);
   // Small and centred in front of the camera so it is never culled; the shader stretches it over the whole screen.
   float distance=(camera!=null ? camera.nearClipPlane : 0.1f)+0.05f;
   holder.transform.localPosition=new Vector3(0f,0f,distance);
   holder.transform.localRotation=Quaternion.identity;
   mesh=new Mesh { name="Screen Fade Quad", hideFlags=HideFlags.DontSave };
   mesh.vertices=new[]{new Vector3(-.01f,-.01f,0f),new Vector3(.01f,-.01f,0f),new Vector3(.01f,.01f,0f),new Vector3(-.01f,.01f,0f)};
   mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
   mesh.triangles=new[]{0,1,2,0,2,3};
   mesh.RecalculateBounds();
   holder.AddComponent<MeshFilter>().sharedMesh=mesh;
   var renderer=holder.AddComponent<MeshRenderer>();
   material=new Material(shader){ name="Screen Fade", hideFlags=HideFlags.DontSave };
   renderer.sharedMaterial=material;
   renderer.shadowCastingMode=ShadowCastingMode.Off;
   renderer.receiveShadows=false;
   renderer.lightProbeUsage=LightProbeUsage.Off;
   renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
   renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
   renderer.allowOcclusionWhenDynamic=false;
   quad=renderer;
   block=new MaterialPropertyBlock();
   BuildVolume();
  }
  void BuildVolume() {
   var go=new GameObject("Screen Fade Volume"){ hideFlags=HideFlags.DontSave, layer=0 }; // layer 0: the camera's volume mask
   go.transform.SetParent(transform,false);
   volume=go.AddComponent<Volume>();
   volume.isGlobal=true; volume.priority=10000f; volume.weight=1f;
   profile=ScriptableObject.CreateInstance<VolumeProfile>();
   profile.hideFlags=HideFlags.DontSave;
   adjustments=profile.Add<ColorAdjustments>(true);
   adjustments.colorFilter.Override(Color.white);
   volume.sharedProfile=profile;
   volume.enabled=false;
  }
  void Apply() {
   if(quad==null) return;
   quad.enabled=alpha>0f;
   if(volume!=null) {
    volume.enabled=alpha>0f;
    adjustments.colorFilter.value=Color.Lerp(Color.white,Color.black,alpha);
   }
   quad.GetPropertyBlock(block);
   block.SetColor(ColorId,new Color(0f,0f,0f,alpha));
   quad.SetPropertyBlock(block);
  }
  void OnDisable() { alpha=0f; Apply(); }
  void OnDestroy() {
   if(quad!=null) Destroy(quad.gameObject);
   if(mesh!=null) Destroy(mesh);
   if(material!=null) Destroy(material);
   if(volume!=null) Destroy(volume.gameObject);
   if(profile!=null) Destroy(profile);
  }
 }
}
