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
    [InitializeOnLoad]
    public static class LaraCandidateLiveDiagnostics
    {
        private const string Pending = "Lara.LiveDiagnostics.Pending";
        private static int frame, phase;
        private static Camera camera;
        private static RenderTexture target;
        private static SkinnedMeshRenderer body;
        private static Color32[] combinedPixels;
        private static Color32[] hiddenPixels;
        private static bool Wardrobe=>SessionState.GetBool("Lara.LiveDiagnostics.Wardrobe",false);
        private static string Output=>"TestOutput/appearance-evidence/"+(Wardrobe?"first-outfit-live":"live");
        static LaraCandidateLiveDiagnostics()
        {
            if (SessionState.GetBool(Pending,false)) EditorApplication.update += Tick;
        }
        public static void Run()
        {
            SessionState.SetBool("Lara.LiveDiagnostics.Wardrobe",false);Begin(LaraCandidateInstaller.ValidationScene);
        }
        public static void RunFirstOutfit()
        {
            SessionState.SetBool("Lara.LiveDiagnostics.Wardrobe",true);Begin(LaraFirstOutfitBuilder.ScenePath);
        }
        private static void Begin(string scene)
        {
            EditorSceneManager.OpenScene(scene,OpenSceneMode.Single);
            // Keep the imported rig; prevent choreography from owning visibility or morph weights.
            foreach (var item in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (item is SuccubusPerformer || item is LaraAnatomyControls || item.GetType().Name.Contains("Harness")) item.enabled = false;
            SessionState.SetBool(Pending,true);
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (body == null)
                {
                    body = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>().Single(r=>r.sharedMesh.GetBlendShapeIndex("CapturedOpening")>=0);
                    body.enabled = true; body.forceRenderingOff = false;
                    body.GetComponentInParent<Animator>().enabled = false;
                    // Animated perimeter smoke makes pixel restoration comparisons non-repeatable.
                    foreach (var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>())
                        particles.gameObject.SetActive(false);
                    foreach (var effect in UnityEngine.Object.FindObjectsByType<UnityEngine.VFX.VisualEffect>())
                        effect.gameObject.SetActive(false);
                    camera = new GameObject("Live Lara diagnostic camera").AddComponent<Camera>();
                    camera.gameObject.AddComponent<HDAdditionalCameraData>().antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
                    var bounds = body.bounds;
                    camera.transform.position = bounds.center + new Vector3(0,0,bounds.size.y*1.8f);
                    camera.transform.LookAt(bounds.center); camera.nearClipPlane = .01f; camera.farClipPlane = 100;
                    camera.fieldOfView = 35; camera.aspect = .8f;
                    target = new RenderTexture(640,800,24,RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
                    var volume = camera.gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10000;
                    volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                    var exposure = volume.sharedProfile.Add<Exposure>(true); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(8);
                    Directory.CreateDirectory(Output);
                    frame = Time.frameCount;
                    SetPhase();
                }
                if (Time.frameCount < frame+20) return;
                var previous = RenderTexture.active; RenderTexture.active = target;
                var pixels = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0); pixels.Apply();
                File.WriteAllBytes(Output+"/phase-" + phase + ".png",pixels.EncodeToPNG());
                if (phase == 8) combinedPixels = pixels.GetPixels32();
                if (phase == 9) hiddenPixels = pixels.GetPixels32();
                if (phase == 10)
                {
                    var restored = pixels.GetPixels32();
                    int changed = restored.Where((p,i) => Mathf.Max(Mathf.Abs(p.r-combinedPixels[i].r),
                        Mathf.Abs(p.g-combinedPixels[i].g),Mathf.Abs(p.b-combinedPixels[i].b)) > 3).Count();
                    File.WriteAllText(Output+"/restore.json","{\"changedPixels\":" + changed + "}");
                    if (changed > 20) throw new IOException("Live restored appearance changed by " + changed + " pixels.");
                }
                if (Wardrobe && phase == 11)
                {
                    var background=pixels.GetPixels32();
                    int remaining=background.Where((p,i)=>Mathf.Max(Mathf.Abs(p.r-hiddenPixels[i].r),Mathf.Abs(p.g-hiddenPixels[i].g),Mathf.Abs(p.b-hiddenPixels[i].b))>3).Count();
                    File.WriteAllText(Output+"/dissolve.json","{\"remainingPixels\":"+remaining+"}");
                    if(remaining>20)throw new IOException("Live wardrobe dissolve left "+remaining+" pixels.");
                }
                RenderTexture.active = previous; UnityEngine.Object.Destroy(pixels);
                Debug.Log("LARA_LIVE_PHASE " + phase + " captured with " + body.sharedMesh.vertexCount + " vertices, " + body.sharedMaterials.Length + " materials");
                phase++;
                if (phase == (Wardrobe?14:11))
                {
                    SessionState.SetBool(Pending,false); EditorApplication.update -= Tick; EditorApplication.Exit(0); return;
                }
                frame = Time.frameCount; SetPhase();
            }
            catch (Exception error)
            {
                Debug.LogException(error); SessionState.SetBool(Pending,false); EditorApplication.update -= Tick; EditorApplication.Exit(1);
            }
        }
        private static void SetPhase()
        {
            for (int i=0;i<body.sharedMesh.blendShapeCount;i++) body.SetBlendShapeWeight(i,0);
            if (phase == 1) body.SetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("Genesis8Female__PBMNipples"),100);
            if (phase >= 2 && phase <= 4)
                foreach (string name in new[] { "Genesis8Female__EX_Breathe", "Genesis8Female__EX_BreatheBelly" })
                    body.SetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex(name),phase == 2 ? 100 : phase == 3 ? 300 : 600);
            if (phase == 5 || phase == 6 || phase >= 8)
                body.SetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("LaraBreastsAdjustment"),
                    ((phase == 5 ? 0 : 1)-LaraCandidateBuilder.ReadManifest().breastsBakedValue)*100);
            if (phase >= 7)
            {
                body.SetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("Genesis8Female__eCTRLEyesClosedL"),100);
                body.SetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("Genesis8Female__eCTRLvAA"),100);
            }
            if (phase >= 8)
            {
                body.SetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("Genesis8Female__PBMNipples"),100);
                body.SetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("CapturedOpening"),100);
            }
            var block = new MaterialPropertyBlock(); body.GetPropertyBlock(block);
            block.SetFloat("_DissolveEnabled",phase == 9 ? 1 : 0); block.SetFloat("_DissolveProgress",phase == 9 ? 1 : 0);
            block.SetVector("_DissolveBoundsMin",body.localBounds.min); block.SetVector("_DissolveBoundsSize",body.localBounds.size);
            block.SetVector("_DissolveFieldParams",new Vector4(3.5f,.85f,17,1.15f)); body.SetPropertyBlock(block);
            if(Wardrobe)
            {
                var outfit=body.GetComponentInParent<LaraFirstOutfit>();body.forceRenderingOff=phase==11;
                outfit.ShowPanties=phase!=12;outfit.ShowDress=phase!=12;outfit.Apply();
                var coverage=new MaterialPropertyBlock();body.GetPropertyBlock(coverage);
                if(coverage.GetFloat("_WardrobeCoverageEnabled")!=(phase==12?0:1))throw new IOException("Clothing coverage toggle mismatch.");
                if(coverage.GetFloat("_WardrobeDressCoverageEnabled")!=(phase==12?0:1))throw new IOException("Dress coverage toggle mismatch.");
                if(!outfit.Validate(out string reason))throw new IOException(reason);
                foreach(var renderer in new[] {outfit.Hair,outfit.Dress,outfit.Panties})
                {
                    if(renderer.forceRenderingOff!=body.forceRenderingOff)throw new IOException("Attachment visibility mismatch.");
                    var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                    if(properties.GetFloat("_DissolveProgress")!=block.GetFloat("_DissolveProgress"))throw new IOException("Attachment dissolve progress mismatch.");
                    if(properties.GetFloat("_WardrobeCoverageEnabled")!=0)throw new IOException("Body coverage leaked onto an attachment.");
                    if(properties.GetFloat("_WardrobeDressCoverageEnabled")!=0)throw new IOException("Dress coverage leaked onto an attachment.");
                    for(int s=0;s<renderer.sharedMesh.blendShapeCount;s++)
                    { int b=body.sharedMesh.GetBlendShapeIndex(renderer.sharedMesh.GetBlendShapeName(s));if(b>=0 && renderer.GetBlendShapeWeight(s)!=body.GetBlendShapeWeight(b))throw new IOException("Attachment morph mismatch."); }
                }
            }
        }
    }
}
