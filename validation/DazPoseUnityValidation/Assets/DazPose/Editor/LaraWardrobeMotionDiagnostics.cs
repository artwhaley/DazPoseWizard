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
    public static class LaraWardrobeMotionDiagnostics
    {
        private const string Pending="Lara.WardrobeMotion.Pending",Second="Lara.WardrobeMotion.Second";
        private const string RecipeOutput="Lara.WardrobeMotion.RecipeOutput",RecipeShoe="Lara.WardrobeMotion.RecipeShoe";
        private static string Output=>SessionState.GetString(RecipeOutput,"")!=""?SessionState.GetString(RecipeOutput,""):SessionState.GetBool(Second,false)?"TestOutput/appearance-evidence/second-outfit-motion":"TestOutput/appearance-evidence/wardrobe-motion";
        private static LaraFirstOutfit outfit;
        private static LaraWardrobe wardrobe;
        private static SkinnedMeshRenderer[] followers;
        private static SkinnedMeshRenderer body;
        private static Transform owner;
        private static AnimationClip clip;
        private static Camera camera,orbit;
        private static RenderTexture target;
        private static Mesh baked;
        private static Vector3 position;
        private static Quaternion rotation;
        private static int frame,badLegacyBounds,badFixedBounds,badFrusta;
        private static string worst="";
        private static SkinnedMeshRenderer shoes;
        private static Transform[] feet;
        private static Vector3[] shoeReference;
        private static int[] shoeSides;
        private static float maximumShoeShapeError,maximumSupportError,maximumFloorPenetration;
        private static int supportedSamples;
        private static bool rigidShoes;
        static LaraWardrobeMotionDiagnostics(){if(SessionState.GetBool(Pending,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            SessionState.SetString(RecipeOutput,"");
            SessionState.SetBool(Second,false);
            EditorSceneManager.OpenScene(LaraFirstOutfitBuilder.ScenePath,OpenSceneMode.Single);
            Begin();
        }
        public static void RunSecondOutfit()
        {
            SessionState.SetString(RecipeOutput,"");
            SessionState.SetBool(Second,true);
            EditorSceneManager.OpenScene(LaraSecondOutfitBuilder.ScenePath,OpenSceneMode.Single);
            Begin();
        }
        public static void RunRecipe()
        {
            var recipe=WardrobeImportRecipe.Read();SessionState.SetBool(Second,true);
            SessionState.SetString(RecipeOutput,recipe.Output+"/motion");
            SessionState.SetString(RecipeShoe,recipe.shoes!=null?recipe.shoes.node:"");
            SessionState.SetBool("Lara.WardrobeMotion.RigidShoes",recipe.shoes!=null && recipe.shoes.articulation=="rigid-pair");
            EditorSceneManager.OpenScene(recipe.Scene,OpenSceneMode.Single);Begin();
        }
        private static void Begin()
        {
            foreach(var item in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if(item is SuccubusPerformer || item is LaraAnatomyControls || item.GetType().Name.Contains("Harness"))item.enabled=false;
            SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            try
            {
                if(body==null)
                {
                    Transform actor;
                    if(SessionState.GetBool(Second,false))
                    {
                        wardrobe=UnityEngine.Object.FindAnyObjectByType<LaraWardrobe>();body=wardrobe.Body;actor=wardrobe.transform;
                        followers=wardrobe.Pieces.Select(p=>p.renderer).ToArray();
                        string shoeName=SessionState.GetString(RecipeOutput,"")==""?"shoe_6146":SessionState.GetString(RecipeShoe,"");
                        shoes=wardrobe.Pieces.SingleOrDefault(p=>p.name==shoeName)?.renderer;
                        if(shoes!=null)
                        {
                        rigidShoes=SessionState.GetString(RecipeOutput,"")=="" || SessionState.GetBool("Lara.WardrobeMotion.RigidShoes",false);
                        feet=new[]{"lFoot","rFoot"}.Select(n=>body.bones.Single(b=>b.name==n)).ToArray();
                        shoeReference=shoes.sharedMesh.vertices;
                        shoeSides=shoes.sharedMesh.boneWeights.Select(w=>shoes.bones[w.boneIndex0].name=="lFoot"?0:1).ToArray();
                        for(int i=0;i<shoeReference.Length;i++)
                        {
                            int bone=Array.IndexOf(shoes.bones,feet[shoeSides[i]]);
                            shoeReference[i]=shoes.sharedMesh.bindposes[bone].MultiplyPoint3x4(shoeReference[i]);
                        }
                        }
                    }
                    else
                    {
                        outfit=UnityEngine.Object.FindAnyObjectByType<LaraFirstOutfit>();actor=outfit.transform;
                        body=outfit.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.sharedMesh.GetBlendShapeIndex("CapturedOpening")>=0);
                        followers=new[]{outfit.Hair,outfit.Dress,outfit.Panties};
                    }
                    var animator=body.GetComponentInParent<Animator>();owner=animator.transform;animator.enabled=false;
                    position=owner.position;rotation=owner.rotation;
                    clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Performer/Locomotion/Walk01_Loop.anim");
                    if(clip==null)throw new IOException("Walking fixture not found.");
                    var paths=AnimationUtility.GetCurveBindings(clip).Select(b=>b.path).Distinct().ToArray();
                    owner=new[] {animator.transform,animator.transform.parent,body.rootBone.parent,actor}.Where(r=>r!=null).Distinct().OrderByDescending(r=>paths.Count(p=>p=="" || r.Find(p)!=null)).First();
                    int matches=paths.Count(p=>p=="" || owner.Find(p)!=null);if(matches!=paths.Length)throw new IOException("Walking fixture bindings unresolved: "+matches+" / "+paths.Length);
                    position=owner.position;rotation=owner.rotation;Debug.Log("LARA_WALK_BINDINGS: "+owner.name+" / "+matches);
                    foreach(var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>())particles.gameObject.SetActive(false);
                    foreach(var effect in UnityEngine.Object.FindObjectsByType<UnityEngine.VFX.VisualEffect>())effect.gameObject.SetActive(false);
                    foreach(var existing in UnityEngine.Object.FindObjectsByType<Camera>())existing.enabled=false;
                    camera=MakeCamera("Walking review",true);orbit=MakeCamera("Scene-view frustum probe",false);
                    target=new RenderTexture(960,1200,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
                    baked=new Mesh();Directory.CreateDirectory(Output);
                }
                float t=frame/60f;clip.SampleAnimation(owner.gameObject,t%clip.length);
                owner.position=position+new Vector3(Mathf.Sin(t*2)*.5f,0,0);owner.rotation=rotation*Quaternion.Euler(0,Mathf.Sin(t)*25,0);
                body.enabled=true;body.forceRenderingOff=false;
                if(wardrobe!=null){wardrobe.GetComponent<LaraHeelReview>()?.Apply();wardrobe.Apply();}else outfit.Apply();
                var focus=body.bounds.center;
                camera.transform.position=focus+new Vector3(0,.1f,3.3f);camera.transform.LookAt(focus);
                orbit.transform.position=focus+Quaternion.Euler(0,120+t*20,0)*new Vector3(0,.1f,3.3f);orbit.transform.LookAt(focus);
                foreach(var renderer in followers)
                {
                    renderer.BakeMesh(baked);baked.RecalculateBounds();var actual=WorldBounds(baked.bounds,renderer.transform.localToWorldMatrix);
                    var legacy=WorldBounds(renderer.sharedMesh.bounds,renderer.rootBone.localToWorldMatrix);
                    if(!Contains(legacy,actual))badLegacyBounds++;
                    if(!Contains(renderer.bounds,actual)){badFixedBounds++;worst=renderer.name+" actual="+actual+" envelope="+renderer.bounds;}
                    foreach(var view in new[] {camera,orbit})
                    {
                        var planes=GeometryUtility.CalculateFrustumPlanes(view);
                        if(GeometryUtility.TestPlanesAABB(planes,actual) && !GeometryUtility.TestPlanesAABB(planes,renderer.bounds))badFrusta++;
                    }
                    if(!renderer.enabled || renderer.forceRenderingOff || !renderer.updateWhenOffscreen)throw new IOException("Unexpected wardrobe visibility state during walking.");
                }
                if(shoes!=null)
                {
                    shoes.BakeMesh(baked);var vertices=baked.vertices;var heel=wardrobe.GetComponent<LaraHeelReview>();
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var world=shoes.transform.TransformPoint(vertices[i]);
                        if(rigidShoes)maximumShoeShapeError=Mathf.Max(maximumShoeShapeError,Vector3.Distance(feet[shoeSides[i]].InverseTransformPoint(world),shoeReference[i]));
                        maximumFloorPenetration=Mathf.Max(maximumFloorPenetration,heel.GroundY-world.y);
                    }
                    for(int side=0;side<2;side++)if((side==0?heel.LeftStance:heel.RightStance)>.99f)
                    {
                        var profile=heel.Footwear;
                        var h=feet[side].TransformPoint(side==0?profile.leftHeel:profile.rightHeel);
                        var tip=feet[side].TransformPoint(side==0?profile.leftToe:profile.rightToe);
                        maximumSupportError=Mathf.Max(maximumSupportError,Mathf.Abs(Mathf.Min(h.y,tip.y)-heel.GroundY));supportedSamples++;
                    }
                }
                if(frame==90)Capture("taa-walking");
                if(frame++<180)return;
                Capture("taa-walking-final");
                string Number(float value)=>value.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                File.WriteAllText(Output+"/validation.json","{\"frames\":"+frame+",\"legacyBoundsMisses\":"+badLegacyBounds+",\"fixedBoundsMisses\":"+badFixedBounds+",\"frustumFalseNegatives\":"+badFrusta+",\"maximumShoeShapeErrorMeters\":"+Number(maximumShoeShapeError)+",\"maximumSupportErrorMeters\":"+Number(maximumSupportError)+",\"maximumFloorPenetrationMeters\":"+Number(maximumFloorPenetration)+",\"supportedSamples\":"+supportedSamples+"}");
                if(badFixedBounds!=0 || badFrusta!=0)throw new IOException("Wardrobe motion bounds failed: "+badFixedBounds+" / "+badFrusta+" "+worst);
                if(shoes!=null && (supportedSamples==0 || (rigidShoes && maximumShoeShapeError>.0001f) || maximumSupportError>.003f || maximumFloorPenetration>.003f))
                    throw new IOException("Footwear motion failed: shape="+maximumShoeShapeError+", support="+maximumSupportError+", floor penetration="+maximumFloorPenetration);
                Debug.Log("LARA_WARDROBE_WALKING_VALIDATED");Finish(0);
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        private static Camera MakeCamera(string name,bool enabled)
        {
            var result=new GameObject(name).AddComponent<Camera>();result.enabled=enabled;result.nearClipPlane=.01f;result.farClipPlane=100;result.fieldOfView=35;result.aspect=.8f;
            var hd=result.gameObject.AddComponent<HDAdditionalCameraData>();hd.antialiasing=HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;hd.TAAQuality=HDAdditionalCameraData.TAAQualityLevel.High;hd.taaSharpenStrength=.15f;
            var volume=result.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10000;volume.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure=volume.sharedProfile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(8);
            return result;
        }
        private static bool Contains(Bounds envelope,Bounds actual)
        { envelope.Expand(.002f);return envelope.Contains(actual.min)&&envelope.Contains(actual.max); }
        private static Bounds WorldBounds(Bounds local,Matrix4x4 matrix)
        {
            var result=new Bounds(matrix.MultiplyPoint3x4(local.center),Vector3.zero);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                result.Encapsulate(matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z))));
            return result;
        }
        private static void Capture(string name)
        {
            var old=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
            RenderTexture.active=old;UnityEngine.Object.Destroy(pixels);
        }
        private static void Finish(int code){SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
