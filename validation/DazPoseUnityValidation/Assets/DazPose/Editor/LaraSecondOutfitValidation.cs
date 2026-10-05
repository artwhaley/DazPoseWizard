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
    public static class LaraSecondOutfitValidation
    {
        private const string Output="TestOutput/appearance-evidence/second-outfit";
        public static void InspectGeneratedShader()
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.ShaderGraph.ShaderGraphImporter")).First(t=>t!=null);
            var method=type.GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Single(m=>m.Name=="GetShaderText" && m.GetParameters().Length==4);
            var collection=Activator.CreateInstance(method.GetParameters()[2].ParameterType,true);
            var arguments=new object[]{"Assets/DazPose/Effects/Dissolve/Shaders/uDTU HDRP Metallic Transparent Dissolve.shadergraph",null,collection,null};
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/transparent-generated-shader.txt",(string)method.Invoke(null,arguments));
            Run();
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene(LaraSecondOutfitBuilder.ScenePath,OpenSceneMode.Single);
            var outfit=UnityEngine.Object.FindAnyObjectByType<LaraWardrobe>();var body=outfit.Body;
            var anatomy=outfit.GetComponent<LaraAnatomyControls>();
            var sources=new[]{body}.Concat(outfit.Pieces.Select(p=>p.renderer)).ToArray();
            if(!outfit.Validate(out string reason))throw new IOException(reason);
            foreach(var piece in outfit.Pieces.Where(p=>p.name=="BraTop_G8F_7446" || p.name=="Stocking L G8F_6421"))
            {
                string surface=piece.name=="BraTop_G8F_7446"?"Cup":"Stocking";
                var material=piece.renderer.sharedMaterials.Single(m=>m.name.EndsWith("_"+surface,StringComparison.Ordinal));var map=material.GetTexture("_AlphaMap");
                var probe=RenderTexture.GetTemporary(128,128,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                var old=RenderTexture.active;Graphics.Blit(map,probe);RenderTexture.active=probe;
                var read=new Texture2D(128,128,TextureFormat.RGBA32,false,true);read.ReadPixels(new Rect(0,0,128,128),0,0);read.Apply();
                var pixels=read.GetPixels();Debug.Log("LARA_OPACITY_AUDIT: "+piece.name+" format="+(map as Texture2D)?.format+" alpha min="+pixels.Min(p=>p.a)+" max="+pixels.Max(p=>p.a)+" mean="+pixels.Average(p=>p.a));
                RenderTexture.active=old;RenderTexture.ReleaseTemporary(probe);UnityEngine.Object.DestroyImmediate(read);
            }
            foreach(var effect in UnityEngine.Object.FindObjectsByType<UnityEngine.VFX.VisualEffect>())effect.gameObject.SetActive(false);
            foreach(var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>())particles.gameObject.SetActive(false);
            body.enabled=true;body.forceRenderingOff=false;outfit.Apply();
            var camera=new GameObject("Second outfit diagnostic camera").AddComponent<Camera>();camera.enabled=false;
            camera.gameObject.AddComponent<HDAdditionalCameraData>().antialiasing=HDAdditionalCameraData.AntialiasingMode.None;
            camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.fieldOfView=35;camera.aspect=.8f;
            var volume=camera.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10000;
            volume.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure=volume.sharedProfile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(8);
            var target=new RenderTexture(640,800,24,RenderTextureFormat.ARGB32);target.Create();Directory.CreateDirectory(Output);
            ShaderUtil.allowAsyncCompilation=false;var bounds=body.bounds;
            void View(float yaw){camera.transform.position=bounds.center+Quaternion.Euler(0,yaw,0)*new Vector3(0,0,bounds.size.y*1.85f);camera.transform.LookAt(bounds.center);}
            try
            {
                View(0);Capture(sources,camera,target,"front");
                outfit.ShowClothes=false;Capture(sources,camera,target,"body-only");outfit.ShowClothes=true;
                var bra=outfit.Pieces.Single(p=>p.name=="BraTop_G8F_7446").renderer.sharedMaterials.Single(m=>m.name.EndsWith("_Cup",StringComparison.Ordinal));float originalOpacity=bra.GetFloat("_Alpha");
                try{bra.SetFloat("_Alpha",.5f);Capture(sources,camera,target,"bra-opacity-control-50");}finally{bra.SetFloat("_Alpha",originalOpacity);}
                var shadows=sources.Select(s=>s.shadowCastingMode).ToArray();
                foreach(var r in sources.Skip(1))r.shadowCastingMode=ShadowCastingMode.Off;
                Capture(sources,camera,target,"front-no-cloth-shadows");
                for(int i=0;i<sources.Length;i++)sources[i].shadowCastingMode=shadows[i];
                View(180);Capture(sources,camera,target,"back");View(90);Capture(sources,camera,target,"side");
                var footCenter=new Vector3(bounds.center.x,bounds.min.y+.16f,bounds.center.z);
                camera.transform.position=footCenter+new Vector3(.7f,.1f,.8f);camera.transform.LookAt(footCenter);Capture(sources,camera,target,"heels");View(0);
                camera.transform.position=footCenter+new Vector3(.7f,.1f,.8f);camera.transform.LookAt(footCenter);
                var pump=outfit.Pieces.Single(p=>p.name=="shoe_6146").renderer;var sourceEnabled=sources.Select(r=>r.enabled).ToArray();
                foreach(var renderer in sources)renderer.enabled=renderer==pump;
                try{Capture(new[]{pump},camera,target,"shoes-only");}finally{for(int i=0;i<sources.Length;i++)sources[i].enabled=sourceEnabled[i];}
                View(0);
                outfit.ShowGlossShells=false;Capture(sources,camera,target,"without-gloss-shells");outfit.ShowGlossShells=true;
                anatomy.LaraBreastsMeshPreview=1;anatomy.Nipples=1;anatomy.CapturedOpening=1;anatomy.Apply();outfit.Apply();Capture(sources,camera,target,"breasts-nipples-opening");
                anatomy.LaraBreastsMeshPreview=0;anatomy.Apply();outfit.Apply();Capture(sources,camera,target,"breasts-small");
                anatomy.LaraBreastsMeshPreview=anatomy.BakedLaraBreasts;anatomy.Nipples=0;anatomy.CapturedOpening=0;anatomy.Apply();
                int breath=body.sharedMesh.GetBlendShapeIndex("Genesis8Female__EX_Breathe");body.SetBlendShapeWeight(breath,300);outfit.Apply();Capture(sources,camera,target,"breathing-300");
                body.SetBlendShapeWeight(breath,0);outfit.Apply();
                LaraCandidateRenderValidation.SetDissolve(body,0);outfit.Apply();var visible=Capture(sources,camera,target,"visible");
                LaraCandidateRenderValidation.SetDissolve(body,.5f);outfit.Apply();Capture(sources,camera,target,"dissolve-half");
                LaraCandidateRenderValidation.SetDissolve(body,1);outfit.Apply();var hidden=Capture(sources,camera,target,"dissolve-complete");
                foreach(var r in sources)r.forceRenderingOff=true;var background=Capture(sources,camera,target,"background");
                foreach(var r in sources)r.forceRenderingOff=false;
                LaraCandidateRenderValidation.SetDissolve(body,0);outfit.Apply();var restored=Capture(sources,camera,target,"restored");
                int remaining=Difference(hidden,background),changed=Difference(restored,visible);
                File.WriteAllText(Output+"/validation.json","{\"parts\":"+outfit.Pieces.Length+",\"shells\":"+outfit.Pieces.Count(p=>p.shell)+",\"remainingPixels\":"+remaining+",\"restoredPixels\":"+changed+"}");
                if(remaining>20 || changed>20)throw new IOException("Second outfit dissolve/restore mismatch: "+remaining+" / "+changed);
                Debug.Log("LARA_SECOND_OUTFIT_RENDER_VALIDATED");
            }
            finally{target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(volume.sharedProfile);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
        }
        private static Color32[] Capture(SkinnedMeshRenderer[] sources,Camera camera,RenderTexture target,string name)
        {
            var objects=new List<GameObject>();var meshes=new List<Mesh>();var enabled=sources.Select(r=>r.enabled).ToArray();
            try
            {
                foreach(var source in sources)
                {
                    if(!source.enabled || source.forceRenderingOff)continue;
                    var mesh=new Mesh();source.BakeMesh(mesh);meshes.Add(mesh);
                    var item=new GameObject("Evaluated "+source.name);objects.Add(item);
                    item.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);item.transform.localScale=source.transform.lossyScale;
                    item.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=item.AddComponent<MeshRenderer>();renderer.sharedMaterials=source.sharedMaterials;renderer.renderingLayerMask=source.renderingLayerMask;renderer.shadowCastingMode=source.shadowCastingMode;
                    var block=new MaterialPropertyBlock();source.GetPropertyBlock(block);renderer.SetPropertyBlock(block);
                }
                foreach(var source in sources)source.enabled=false;
                Color32[] result=null;
                for(int pass=0;pass<6;pass++)result=LaraCandidateRenderValidation.Capture(camera,target,Output+"/"+name+".png");
                return result;
            }
            finally
            {
                for(int i=0;i<sources.Length;i++)sources[i].enabled=enabled[i];
                foreach(var item in objects)UnityEngine.Object.DestroyImmediate(item);
                foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        private static int Difference(Color32[] a,Color32[] b)=>a.Where((p,i)=>Mathf.Max(Mathf.Abs(p.r-b[i].r),Mathf.Abs(p.g-b[i].g),Mathf.Abs(p.b-b[i].b))>3).Count();
    }
}
