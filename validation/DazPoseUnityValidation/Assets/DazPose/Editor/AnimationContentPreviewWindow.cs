using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor
{
    /// <summary>Scene-free animation inspection on a render-only clone of explicit asset inputs.</summary>
    public sealed class AnimationContentPreviewWindow : EditorWindow
    {
        private GameObject modelAsset, previewRoot;
        private AnimationClip clipAsset;
        private PreviewRenderUtility preview;
        private readonly List<Material> previewMaterials = new List<Material>();
        private readonly Dictionary<Material, Material> materialCopies = new Dictionary<Material, Material>();
        private bool playing, loop = true, ownsAnimationMode;
        private float previewTime, playbackSpeed = 1f, yaw, pitch = 8f;
        private double lastTick;
        private Bounds bounds;
        private string error;

        [MenuItem("DazPose/Content/Animation Asset Preview")]
        public static void Open()
        {
            var w=GetWindow<AnimationContentPreviewWindow>();
            w.titleContent=new GUIContent("Animation Preview");
            if(Selection.activeObject is AnimationClip c) w.clipAsset=c;
            else if(Selection.activeObject is GameObject g) w.modelAsset=g;
            w.CreatePreview();
            w.Show();
        }
        public static void Open(GameObject model, AnimationClip clip)
        {
            var w=GetWindow<AnimationContentPreviewWindow>();
            w.titleContent=new GUIContent("Animation Preview");
            w.modelAsset=model; w.clipAsset=clip; w.CreatePreview(); w.Show();
        }
        private void OnEnable()
        {
            titleContent=new GUIContent("Animation Preview");
            lastTick=EditorApplication.timeSinceStartup;
            EditorApplication.update+=Tick;
            AssemblyReloadEvents.beforeAssemblyReload+=OnBeforeAssemblyReload;
            EditorSceneManager.activeSceneChangedInEditMode+=OnActiveSceneChanged;
        }
        private void OnDisable()
        {
            EditorApplication.update-=Tick;
            AssemblyReloadEvents.beforeAssemblyReload-=OnBeforeAssemblyReload;
            EditorSceneManager.activeSceneChangedInEditMode-=OnActiveSceneChanged;
            playing=false; Cleanup();
        }
        private void OnBeforeAssemblyReload() { playing=false; Cleanup(); }
        private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        { playing=false; Cleanup(); error="Preview released because the active scene changed. Reopen it to continue."; Repaint(); }
        private void Tick()
        {
            double now=EditorApplication.timeSinceStartup;
            float dt=Mathf.Clamp((float)(now-lastTick),0f,.1f); lastTick=now;
            if(playing && clipAsset!=null && clipAsset.length>0f)
            {
                previewTime+=dt*playbackSpeed;
                if(loop) previewTime%=clipAsset.length;
                else if(previewTime>=clipAsset.length) { previewTime=clipAsset.length; playing=false; }
                Repaint();
            }
        }
        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            modelAsset=(GameObject)EditorGUILayout.ObjectField("Model asset",modelAsset,typeof(GameObject),false);
            clipAsset=(AnimationClip)EditorGUILayout.ObjectField("Animation clip",clipAsset,typeof(AnimationClip),false);
            bool changed=EditorGUI.EndChangeCheck();
            if(changed) { playing=false; previewTime=0f; yaw=0f; CreatePreview(); }
            using(new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled=previewRoot!=null && clipAsset!=null && clipAsset.length>0f;
                if(GUILayout.Button(playing?"Pause":"Play preview",GUILayout.Width(105))) playing=!playing;
                if(GUILayout.Button("Restart",GUILayout.Width(70))) { previewTime=0f; TrySample(); }
                GUI.enabled=true;
                loop=GUILayout.Toggle(loop,"Loop",GUILayout.Width(55));
                playbackSpeed=EditorGUILayout.Slider("Speed",playbackSpeed,.1f,2f);
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                yaw=EditorGUILayout.Slider("View",yaw,-180f,180f);
                pitch=EditorGUILayout.Slider("Pitch",pitch,-20f,40f);
            }
            if(clipAsset!=null && clipAsset.length>0f)
            {
                float next=EditorGUILayout.Slider("Time",previewTime,0f,clipAsset.length);
                if(!Mathf.Approximately(next,previewTime)) { previewTime=next; TrySample(); }
                EditorGUILayout.LabelField("Clip length",clipAsset.length.ToString("0.000")+" s");
            }
            Rect rect=GUILayoutUtility.GetRect(100,280,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            if(previewRoot==null || preview==null)
            {
                EditorGUI.HelpBox(rect,error??"Assign a model asset and animation clip.",MessageType.Info);
                return;
            }
            bool began=false;
            try
            {
                if(!TrySample() || previewRoot==null || preview==null)return;
                preview.BeginPreview(rect,GUIStyle.none); began=true;
                ConfigureCamera(); preview.Render();
                Texture image=preview.EndPreview(); began=false;
                GUI.DrawTexture(rect,image,ScaleMode.StretchToFill,false);
            }
            catch(Exception e)
            {
                if(began && preview!=null) { try { preview.EndPreview(); } catch { } }
                FailPreview(e);
                return;
            }
            if(Event.current.type==EventType.Repaint)
                GUI.Label(new Rect(rect.x+8,rect.y+8,rect.width-16,20),
                    modelAsset.name+" - "+clipAsset.name+"  "+previewTime.ToString("0.00")+" s",EditorStyles.whiteMiniLabel);
        }

        private void CreatePreview()
        {
            Cleanup(); error=null;
            if(modelAsset==null || clipAsset==null) return;
            if(!EditorUtility.IsPersistent(modelAsset) || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(modelAsset)))
            { error="Choose a model asset from the Project window; scene objects cannot be previewed."; return; }
            if(!EditorUtility.IsPersistent(clipAsset) || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(clipAsset)))
            { error="Choose an animation clip asset from the Project window."; return; }
            if(AnimationMode.InAnimationMode())
            { error="Another editor currently owns Animation Mode. Stop or close that preview, then reopen this window."; return; }
            try
            {
                var map=new Dictionary<Transform,Transform>();
                var sourceRoot=modelAsset.transform;
                var root=CloneTransforms(sourceRoot,null,map);
                previewRoot=root.gameObject;
                previewRoot.name=modelAsset.name+" (Preview)";
                previewRoot.hideFlags=HideFlags.HideAndDontSave;
                foreach(var pair in map)
                {
                    var src=pair.Key.gameObject; var dst=pair.Value.gameObject;
                    foreach(var filter in src.GetComponents<MeshFilter>())
                        dst.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                    foreach(var renderer in src.GetComponents<MeshRenderer>())
                    {
                        var copy=dst.AddComponent<MeshRenderer>();
                        EditorUtility.CopySerialized(renderer,copy);
                        copy.sharedMaterials=CloneMaterials(renderer.sharedMaterials);
                    }
                    foreach(var sourceSkin in src.GetComponents<SkinnedMeshRenderer>())
                    {
                        var skin=dst.AddComponent<SkinnedMeshRenderer>();
                        EditorUtility.CopySerialized(sourceSkin,skin);
                        skin.sharedMesh=sourceSkin.sharedMesh;
                        skin.sharedMaterials=CloneMaterials(sourceSkin.sharedMaterials);
                        skin.rootBone=Remap(sourceRoot,map,sourceSkin.rootBone);
                        skin.bones=(sourceSkin.bones??Array.Empty<Transform>()).Select(b=>Remap(sourceRoot,map,b)).ToArray();
                        skin.updateWhenOffscreen=true;
                    }
                }
                var sourceAnimator=modelAsset.GetComponentInChildren<Animator>(true);
                var avatar=sourceAnimator!=null?sourceAnimator.avatar:FindAvatar(modelAsset);
                if(avatar!=null)
                {
                    var animatorHost=sourceAnimator!=null && map.ContainsKey(sourceAnimator.transform)?map[sourceAnimator.transform]:root;
                    var animator=animatorHost.gameObject.AddComponent<Animator>();
                    animator.avatar=avatar;
                    animator.runtimeAnimatorController=null;
                    animator.applyRootMotion=false;
                    animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                }
                preview=new PreviewRenderUtility();
                preview.cameraFieldOfView=30f;
                preview.camera.nearClipPlane=.01f; preview.camera.farClipPlane=1000f;
                preview.camera.clearFlags=CameraClearFlags.Color;
                preview.camera.backgroundColor=new Color(.16f,.18f,.21f,0f);
                if(preview.lights!=null && preview.lights.Length>0)
                { preview.lights[0].intensity=1.3f; preview.lights[0].transform.rotation=Quaternion.Euler(35,25,0); }
                if(preview.lights!=null && preview.lights.Length>1)
                { preview.lights[1].intensity=.7f; preview.lights[1].transform.rotation=Quaternion.Euler(25,-135,0); }
                preview.AddSingleGO(previewRoot);
                UpdateBounds(); Sample();
            }
            catch(Exception e) { FailPreview(e); }
        }

        private static Transform CloneTransforms(Transform source,Transform parent,Dictionary<Transform,Transform> map)
        {
            var go=new GameObject(source.name){hideFlags=HideFlags.HideAndDontSave};
            var t=go.transform;
            if(parent!=null)t.SetParent(parent,false);
            t.localPosition=source.localPosition; t.localRotation=source.localRotation; t.localScale=source.localScale;
            map.Add(source,t);
            for(int i=0;i<source.childCount;i++)CloneTransforms(source.GetChild(i),t,map);
            return t;
        }
        private static Transform Remap(Transform root,Dictionary<Transform,Transform> map,Transform source)
        {
            if(source==null)return null;
            if(map.TryGetValue(source,out var mapped))return mapped;
            if(!source.IsChildOf(root))return null;
            string path=AnimationUtility.CalculateTransformPath(source,root);
            var found=map.Keys.FirstOrDefault(t=>AnimationUtility.CalculateTransformPath(t,root)==path);
            return found!=null?map[found]:null;
        }
        private static Avatar FindAvatar(GameObject model)
        {
            string path=AssetDatabase.GetAssetPath(model);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        }
        private void Sample()
        {
            if(previewRoot==null || clipAsset==null)return;
            if(AnimationMode.InAnimationMode())
            {
                if(!ownsAnimationMode) throw new InvalidOperationException("Another editor began Animation Mode. Stop or close that preview before sampling this clip.");
            }
            else { AnimationMode.StartAnimationMode(); ownsAnimationMode=true; }
            AnimationMode.BeginSampling();
            try { AnimationMode.SampleAnimationClip(previewRoot,clipAsset,Mathf.Clamp(previewTime,0f,clipAsset.length)); }
            finally { AnimationMode.EndSampling(); }
        }
        private Material[] CloneMaterials(Material[] originals)
        {
            if(originals==null)return Array.Empty<Material>();
            var result=new Material[originals.Length];
            for(int i=0;i<originals.Length;i++)
            {
                var source=originals[i]; if(source==null)continue;
                if(!materialCopies.TryGetValue(source,out var copy))
                {
                    copy=new Material(source){name=source.name+" (Preview)",hideFlags=HideFlags.HideAndDontSave};
                    materialCopies.Add(source,copy); previewMaterials.Add(copy);
                }
                result[i]=copy;
            }
            return result;
        }
        private bool TrySample()
        {
            try { Sample(); return true; }
            catch(Exception e) { FailPreview(e); return false; }
        }
        private void FailPreview(Exception e)
        {
            playing=false; error="Preview stopped and released after an error: "+e.GetBaseException().Message;
            Cleanup(); Repaint();
        }
        private void UpdateBounds()
        {
            var renderers=previewRoot.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0) { bounds=new Bounds(Vector3.up,Vector3.one); return; }
            bounds=renderers[0].bounds;
            for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
            if(bounds.size.sqrMagnitude<.0001f)bounds.Expand(1f);
        }
        private void ConfigureCamera()
        {
            float radius=Mathf.Max(.5f,bounds.extents.magnitude);
            float distance=radius/Mathf.Tan(preview.camera.fieldOfView*Mathf.Deg2Rad*.5f)*1.15f;
            Vector3 direction=Quaternion.Euler(pitch,yaw,0f)*Vector3.forward;
            Vector3 center=bounds.center;
            preview.camera.transform.position=center-direction*distance;
            preview.camera.transform.rotation=Quaternion.LookRotation(center-preview.camera.transform.position,Vector3.up);
            preview.camera.nearClipPlane=Mathf.Max(.01f,distance-radius*2f);
            preview.camera.farClipPlane=distance+radius*3f+1f;
        }
        private void Cleanup()
        {
            if(ownsAnimationMode)
            {
                try { if(AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode(); } catch { }
                ownsAnimationMode=false;
            }
            if(previewRoot!=null) { try { DestroyImmediate(previewRoot); } catch { } }
            previewRoot=null;
            if(preview!=null) { try { preview.Cleanup(); } catch { } preview=null; }
            foreach(var material in previewMaterials) if(material!=null) { try { DestroyImmediate(material); } catch { } }
            previewMaterials.Clear(); materialCopies.Clear();
        }
    }
}

