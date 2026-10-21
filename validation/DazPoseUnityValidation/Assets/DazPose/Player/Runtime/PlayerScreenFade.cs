using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace DazPose.Player {
 /// <summary>
 /// Fade to black drawn by the camera itself: a camera-parented quad whose shader ignores depth and draws last among
 /// transparent objects, so it covers transparent dissolve particles too. Not a Screen Space - Overlay canvas, and it
 /// never touches HeadPose. The shader lives in Resources so no scene or prefab has to reference it.
 /// </summary>
 [DisallowMultipleComponent]
 public sealed class PlayerScreenFade : MonoBehaviour, IScreenFade {
  public const string ShaderResource="DazPose/PlayerScreenFade";
  static readonly int ColorId=Shader.PropertyToID("_FadeColor");
  Renderer quad;
  Mesh mesh;
  Material material;
  MaterialPropertyBlock block;
  float alpha;
  public float Alpha {
   get => alpha;
   set {
    alpha=Mathf.Clamp01(value);
    if(quad==null) { if(alpha<=0f) return; Build(); }
    Apply();
   }
  }
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
  }
  void Apply() {
   if(quad==null) return;
   quad.enabled=alpha>0f;
   quad.GetPropertyBlock(block);
   block.SetColor(ColorId,new Color(0f,0f,0f,alpha));
   quad.SetPropertyBlock(block);
  }
  void OnDisable() { alpha=0f; Apply(); }
  void OnDestroy() {
   if(quad!=null) Destroy(quad.gameObject);
   if(mesh!=null) Destroy(mesh);
   if(material!=null) Destroy(material);
  }
 }
}
