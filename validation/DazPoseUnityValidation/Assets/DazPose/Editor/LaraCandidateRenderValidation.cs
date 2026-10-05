using System;
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
    // Uses the real HDRP camera path. Requires a graphics-enabled Unity batch process.
    public static class LaraCandidateRenderValidation
    {
        [Serializable] private class Audit
        {
            public int visiblePixels, halfwayPixels, remainingPixels, restoredPixels, backgroundWarmupPixels;
            public bool completeDissolveMatchesBackground, restoredMatchesVisible;
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene(LaraCandidateInstaller.ValidationScene,OpenSceneMode.Single);
            var performer = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            var anatomy = performer.GetComponent<LaraAnatomyControls>();
            var renderer = performer.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.sharedMesh != null);
            renderer.enabled = true; renderer.forceRenderingOff = false;
            var cameraObject = new GameObject("Lara render verification camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<HDAdditionalCameraData>().antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
            camera.enabled = false;
            Bounds bounds = renderer.bounds;
            camera.transform.position = bounds.center + new Vector3(0,0,bounds.size.y*1.8f);
            camera.transform.LookAt(bounds.center);
            camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            camera.fieldOfView = 35; camera.aspect = .8f;
            var verificationVolume = cameraObject.AddComponent<Volume>();
            verificationVolume.isGlobal = true; verificationVolume.priority = 10000;
            verificationVolume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure = verificationVolume.sharedProfile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(8);
            var target = new RenderTexture(640,800,24,RenderTextureFormat.ARGB32);
            target.Create();
            string output = "TestOutput/appearance-evidence/render";
            Directory.CreateDirectory(output);
            bool asynchronous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                renderer.enabled = false;
                var initialBackground = Capture(camera,target,output + "/background-initial.png");
                renderer.enabled = true;
                SetDissolve(renderer,0);
                var visible = Capture(camera,target,output + "/lara-visible.png");
                SetDissolve(renderer,.5f);
                var halfway = Capture(camera,target,output + "/lara-dissolve-half.png");
                SetDissolve(renderer,1);
                var hidden = Capture(camera,target,output + "/lara-dissolve-complete.png");
                renderer.enabled = false;
                // Compare against an adjacent disabled-renderer control, after HDRP initialization.
                var background = Capture(camera,target,output + "/background.png");
                renderer.enabled = true;
                SetDissolve(renderer,0);
                var restored = Capture(camera,target,output + "/lara-restored.png");
                var audit = new Audit { visiblePixels = Difference(visible,background), halfwayPixels = Difference(halfway,background),
                    remainingPixels = Difference(hidden,background), restoredPixels = Difference(restored,visible),
                    backgroundWarmupPixels = Difference(initialBackground,background) };
                audit.completeDissolveMatchesBackground = audit.remainingPixels <= 20;
                audit.restoredMatchesVisible = audit.restoredPixels <= 20;
                File.WriteAllText(output + "/validation.json",JsonUtility.ToJson(audit,true));
                if (audit.visiblePixels < 1000) throw new IOException("Rendered body is absent or camera verification is inconclusive.");
                if (!audit.completeDissolveMatchesBackground) throw new IOException("Complete dissolve leaves rendered pixels: " + audit.remainingPixels);
                if (!audit.restoredMatchesVisible) throw new IOException("Restored appearance changed: " + audit.restoredPixels);
                if (audit.halfwayPixels == 0 || audit.halfwayPixels == audit.visiblePixels) throw new IOException("Half dissolve did not visibly change coverage.");
                anatomy.CapturedOpening = 1; anatomy.Nipples = 1; anatomy.Apply();
                renderer.SetBlendShapeWeight(renderer.sharedMesh.GetBlendShapeIndex("Genesis8Female__eCTRLEyesClosedL"),100);
                Capture(camera,target,output + "/lara-opening-nipples-blink.png");
                var keyObject = new GameObject("Neutral material verification key");
                var key = keyObject.AddComponent<Light>(); key.type = LightType.Directional; key.color = Color.white;
                keyObject.transform.rotation = Quaternion.LookRotation(new Vector3(-2,-3,-4));
                var keyData = keyObject.AddComponent<HDAdditionalLightData>();
                key.lightUnit = LightUnit.Lux;
                key.intensity = 200;
                keyData.UpdateAllLightValues();
                // SubmitRenderRequest does not tick HDRP's LateUpdate. Match the installed
                // HDLightEditor synchronization so the diagnostic uses the authored intensity.
                var synchronize = typeof(HDAdditionalLightData).GetMethod("UpdateRenderEntity",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (synchronize == null) throw new MissingMethodException("Installed HDRP light synchronization changed.");
                synchronize.Invoke(keyData,null);
                verificationVolume.sharedProfile.Add<Bloom>(true).intensity.Override(0);
                verificationVolume.sharedProfile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                Capture(camera,target,output + "/lara-neutral-opening-nipples-blink.png");
                anatomy.CapturedOpening = 0; anatomy.Nipples = 0; anatomy.Apply();
                renderer.SetBlendShapeWeight(renderer.sharedMesh.GetBlendShapeIndex("Genesis8Female__eCTRLEyesClosedL"),0);
                Capture(camera,target,output + "/lara-neutral.png");
                foreach (var item in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    Debug.Log("LARA_RENDERER_INVENTORY: " + item.name + " / " + item.GetType().Name + " / " + item.enabled
                        + " / " + string.Join(",",item.sharedMaterials.Select(m => m == null ? "null" : m.name)));
                foreach (float weight in new[] { 100f, 300f, 600f })
                {
                    foreach (string name in new[] { "Genesis8Female__EX_Breathe", "Genesis8Female__EX_BreatheBelly" })
                        renderer.SetBlendShapeWeight(renderer.sharedMesh.GetBlendShapeIndex(name),weight);
                    CaptureBaked(renderer,camera,target,output + "/lara-breath-" + weight + ".png");
                }
                foreach (string name in new[] { "Genesis8Female__EX_Breathe", "Genesis8Female__EX_BreatheBelly" })
                    renderer.SetBlendShapeWeight(renderer.sharedMesh.GetBlendShapeIndex(name),0);
                anatomy.Nipples = 1; anatomy.Apply();
                camera.transform.position = bounds.center + new Vector3(0,bounds.size.y*.18f,bounds.size.y*.55f);
                camera.transform.LookAt(bounds.center + new Vector3(0,bounds.size.y*.2f,0));
                CaptureBaked(renderer,camera,target,output + "/lara-nipples-close.png");
                anatomy.Nipples = 0; anatomy.Apply();
                CaptureBaked(renderer,camera,target,output + "/lara-nipples-zero-close.png");
                UnityEngine.Object.DestroyImmediate(keyObject);
                Debug.Log("LARA_RENDER_DISSOLVE_VALIDATED: " + JsonUtility.ToJson(audit));
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asynchronous;
                UnityEngine.Object.DestroyImmediate(verificationVolume.sharedProfile);
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
        public static void BuildAndRun()
        {
            LaraCandidateInstaller.BuildAll();
            Run();
        }
        public static void TuneAndRun()
        {
            LaraCandidateBuilder.CopyAcceptedSkinResponse();
            Run();
        }
        private static void CaptureBaked(SkinnedMeshRenderer body,Camera camera,RenderTexture target,string path)
        {
            // A synchronous render request can reuse the GPU's skinning cache for this frame.
            // BakeMesh evaluates current shape weights immediately for the deformation comparison.
            var mesh = new Mesh(); body.BakeMesh(mesh);
            var item = new GameObject("Evaluated deformation diagnostic");
            item.transform.SetPositionAndRotation(body.transform.position,body.transform.rotation);
            item.transform.localScale = body.transform.lossyScale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = body.sharedMaterials; renderer.renderingLayerMask = body.renderingLayerMask;
            var block = new MaterialPropertyBlock(); body.GetPropertyBlock(block); renderer.SetPropertyBlock(block);
            body.enabled = false;
            try { Capture(camera,target,path); }
            finally { body.enabled = true; UnityEngine.Object.DestroyImmediate(item); UnityEngine.Object.DestroyImmediate(mesh); }
        }
        internal static Color32[] Capture(Camera camera, RenderTexture target, string path)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            // Warm several frames so shader compilation/history does not invalidate comparisons.
            for (int i = 0; i < 6; i++) RenderPipeline.SubmitRenderRequest(camera,request);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try
            {
                pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0); pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
                return pixels.GetPixels32();
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixels); }
        }
        private static int Difference(Color32[] a, Color32[] b)
        {
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Max(Mathf.Abs(a[i].r-b[i].r),Mathf.Abs(a[i].g-b[i].g),Mathf.Abs(a[i].b-b[i].b)) > 3) changed++;
            return changed;
        }
        internal static void SetDissolve(SkinnedMeshRenderer body,float progress)
        {
            var block = new MaterialPropertyBlock(); body.GetPropertyBlock(block);
            block.SetFloat("_DissolveEnabled",progress > 0 ? 1 : 0);
            block.SetFloat("_DissolveProgress",progress);
            block.SetVector("_DissolveBoundsMin",body.localBounds.min);
            block.SetVector("_DissolveBoundsSize",body.localBounds.size);
            block.SetVector("_DissolveFieldParams",new Vector4(3.5f,.85f,17,1.15f));
            block.SetFloat("_DissolveEdgeWidth",.035f); block.SetColor("_DissolveEdgeColor",new Color(3,.06f,4,1));
            block.SetFloat("_DissolveEdgeEmission",4); body.SetPropertyBlock(block);
        }
    }
}
