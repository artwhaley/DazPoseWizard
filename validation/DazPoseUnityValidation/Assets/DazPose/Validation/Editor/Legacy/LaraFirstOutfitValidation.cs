using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace DazPose.UnityValidation
{
    public static class LaraFirstOutfitValidation
    {
        private static string Output="TestOutput/appearance-evidence/first-outfit";
        public static void BuildAndRun() { LaraFirstOutfitBuilder.Build();Run(); }
        public static void UpdateAndRun() { LaraFirstOutfitBuilder.UpdateExisting();Run(); }
        public static void Run()
        {
            EditorSceneManager.OpenScene(LaraFirstOutfitBuilder.ScenePath,OpenSceneMode.Single);
            var outfit=UnityEngine.Object.FindAnyObjectByType<LaraFirstOutfit>();
            var anatomy=outfit.GetComponent<LaraAnatomyControls>();
            var body=outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.sharedMesh.GetBlendShapeIndex("CapturedOpening")>=0);
            body.enabled=true;body.forceRenderingOff=false;
            var renderers=new[] { body,outfit.Hair,outfit.Dress,outfit.Panties };
            foreach(var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>())particles.gameObject.SetActive(false);
            foreach(var effect in UnityEngine.Object.FindObjectsByType<UnityEngine.VFX.VisualEffect>())effect.gameObject.SetActive(false);
            var camera=new GameObject("First outfit diagnostic camera").AddComponent<Camera>();camera.enabled=false;
            camera.gameObject.AddComponent<HDAdditionalCameraData>().antialiasing=HDAdditionalCameraData.AntialiasingMode.None;
            var bounds=body.bounds;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.fieldOfView=35;camera.aspect=.8f;
            var volume=camera.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10000;volume.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure=volume.sharedProfile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(8);
            var target=new RenderTexture(640,800,24,RenderTextureFormat.ARGB32);target.Create();Directory.CreateDirectory(Output);
            ShaderUtil.allowAsyncCompilation=false;
            void View(float yaw) { camera.transform.position=bounds.center+Quaternion.Euler(0,yaw,0)*new Vector3(0,0,bounds.size.y*1.85f);camera.transform.LookAt(bounds.center); }
            try
            {
                outfit.ShowHair=true;outfit.ShowDress=true;outfit.ShowPanties=true;outfit.Apply();View(0);
                Capture(renderers,camera,target,"front");View(180);Capture(renderers,camera,target,"back");body.enabled=false;Capture(renderers,camera,target,"clothing-only-back");body.enabled=true;View(90);Capture(renderers,camera,target,"side");View(0);
                anatomy.Nipples=1;anatomy.LaraBreastsMeshPreview=1;anatomy.CapturedOpening=1;anatomy.Apply();outfit.Apply();
                Capture(renderers,camera,target,"breasts-nipples-opening");
                anatomy.LaraBreastsMeshPreview=0;anatomy.Apply();outfit.Apply();Capture(renderers,camera,target,"breasts-small");
                anatomy.LaraBreastsMeshPreview=anatomy.BakedLaraBreasts;anatomy.Nipples=0;anatomy.Apply();
                foreach(string name in new[] { "Genesis8Female__EX_Breathe","Genesis8Female__EX_BreatheBelly" })body.SetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex(name),100);
                outfit.Apply();Capture(renderers,camera,target,"breathing");
                foreach(string name in new[] { "Genesis8Female__EX_Breathe","Genesis8Female__EX_BreatheBelly" })body.SetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex(name),0);
                outfit.ShowDress=false;outfit.Apply();Capture(renderers,camera,target,"panties-opening");View(180);Capture(renderers,camera,target,"panties-opening-back");View(0);
                outfit.ShowPanties=false;outfit.Apply();Capture(renderers,camera,target,"uncovered-opening");
                outfit.ShowDress=true;outfit.ShowPanties=true;outfit.Apply();
                var head=body.bones.Single(b=>b.name=="head");var rotation=head.localRotation;head.localRotation=rotation*Quaternion.Euler(15,35,0);Capture(renderers,camera,target,"head-pose");head.localRotation=rotation;
                LaraCandidateRenderValidation.SetDissolve(body,0);outfit.Apply();var visible=Capture(renderers,camera,target,"dissolve-visible");
                LaraCandidateRenderValidation.SetDissolve(body,.5f);outfit.Apply();Capture(renderers,camera,target,"dissolve-half");
                LaraCandidateRenderValidation.SetDissolve(body,1);outfit.Apply();var hidden=Capture(renderers,camera,target,"dissolve-complete");
                foreach(var renderer in renderers)renderer.forceRenderingOff=true;
                var background=Capture(renderers,camera,target,"background");
                foreach(var renderer in renderers)renderer.forceRenderingOff=false;
                LaraCandidateRenderValidation.SetDissolve(body,0);outfit.Apply();var restored=Capture(renderers,camera,target,"dissolve-restored");
                int remaining=Difference(hidden,background),changed=Difference(restored,visible);
                File.WriteAllText(Output+"/validation.json","{\"remainingPixels\":"+remaining+",\"restoredPixels\":"+changed+",\"visiblePixels\":"+Difference(visible,background)+"}");
                if(remaining>20 || changed>20)throw new IOException("First outfit dissolve/restore failed: "+remaining+" / "+changed);
                Debug.Log("LARA_FIRST_OUTFIT_RENDER_VALIDATED");
            }
            finally { target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(volume.sharedProfile);UnityEngine.Object.DestroyImmediate(camera.gameObject); }
        }
        private static Color32[] Capture(SkinnedMeshRenderer[] sources,Camera camera,RenderTexture target,string name)
        {
            var temporary=new List<GameObject>();var meshes=new List<Mesh>();var states=sources.Select(r=>r.enabled).ToArray();
            try
            {
                foreach(var source in sources)
                {
                    if(!source.enabled || source.forceRenderingOff)continue;
                    var mesh=new Mesh();source.BakeMesh(mesh);meshes.Add(mesh);
                    var item=new GameObject("Evaluated "+source.name);temporary.Add(item);
                    item.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);item.transform.localScale=source.transform.lossyScale;
                    item.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=item.AddComponent<MeshRenderer>();renderer.sharedMaterials=source.sharedMaterials;renderer.renderingLayerMask=source.renderingLayerMask;
                    var block=new MaterialPropertyBlock();source.GetPropertyBlock(block);renderer.SetPropertyBlock(block);
                }
                foreach(var source in sources)source.enabled=false;
                // HDRP's indirect-light history needs to settle after bright cloth/hair
                // dissolve edges disappear; keep the pixel comparison equally strict.
                Color32[] pixels=null;
                for(int pass=0;pass<3;pass++)pixels=LaraCandidateRenderValidation.Capture(camera,target,Output+"/"+name+".png");
                return pixels;
            }
            finally { for(int i=0;i<sources.Length;i++)sources[i].enabled=states[i];foreach(var item in temporary)UnityEngine.Object.DestroyImmediate(item);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh); }
        }
        private static int Difference(Color32[] a,Color32[] b)=>a.Where((p,i)=>Mathf.Max(Mathf.Abs(p.r-b[i].r),Mathf.Abs(p.g-b[i].g),Mathf.Abs(p.b-b[i].b))>3).Count();
    }
}
