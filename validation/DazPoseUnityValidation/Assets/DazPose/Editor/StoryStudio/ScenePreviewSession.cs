using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DazPose.Performer;
using DazPose.Story;
using DazPose.UnityValidation.Partner;
namespace DazPose.Editor.Studio
{
    // Never Instantiate gameplay prefabs: only explicitly whitelisted visual component data is copied.
    public sealed class ScenePreviewSession : IDisposable
    {
        readonly Dictionary<Transform,Transform> map=new Dictionary<Transform,Transform>();
        readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
        readonly Dictionary<Renderer,Material[]> regular=new Dictionary<Renderer,Material[]>();
        readonly List<Material> ownedMaterials=new List<Material>();
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        readonly List<PreviewMarker> markers=new List<PreviewMarker>();
        readonly List<string> diagnostics=new List<string>();
        readonly struct PreviewMarker { public readonly Vector3 Position; public readonly string Label; public readonly Color Color; public PreviewMarker(Vector3 position,string label,Color color){Position=position;Label=label;Color=color;} }
        PreviewRenderUtility utility;
        GameObject root;
        bool animationMode;
        public Transform Lara {get;private set;}
        public Transform PlayerBody {get;private set;}
        public Camera SourceCamera {get;private set;}
        public string Diagnostic {get;private set;}
        public Vector3 ViewPosition {get;set;}=new Vector3(0,1.5f,-4);
        public Quaternion ViewRotation {get;set;}=Quaternion.identity;
        public bool PlayerCamera {get;set;}
        public float Yaw,Pitch=10,Distance=4;
        Vector3 focus=Vector3.up;
        bool customCamera,trackingLara,laraFocusIsChild;
        Vector3 laraFocusOffset;
        public Transform LaraFocus {get;private set;}
        public Vector3 CameraPosition=>shotPosition;
        public Quaternion CameraRotation=>shotRotation;
        Vector3 shotPosition;
        Quaternion shotRotation;
        public void PreviewCameraAt(Vector3 position,Quaternion rotation){trackingLara=false;shotPosition=position;shotRotation=rotation;customCamera=true;}
        public void TrackLara(){
            if(Lara==null)return;
            if(!customCamera){var camera=SourceCamera!=null?CloneFor(SourceCamera.transform)?.GetComponent<Camera>():null;var transform=camera!=null?camera.transform:SourceCamera!=null?SourceCamera.transform:utility.camera.transform;shotPosition=transform.position;shotRotation=transform.rotation;customCamera=true;}
            trackingLara=true;UpdateTrackedCamera();
            if(LaraFocus==null){const string message="Camera tracking uses Lara's upper renderer bounds because no mapped laraFocus is available.";if(!diagnostics.Contains(message))diagnostics.Add(message);Diagnostic=string.Join(" ",diagnostics.Distinct());}
        }
        public void UpdateTrackedCamera(){
            if(!trackingLara||Lara==null)return;
            Vector3 target;
            if(LaraFocus!=null)target=laraFocusIsChild?LaraFocus.position:Lara.TransformPoint(laraFocusOffset);
            else {var renderers=Lara.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)target=Lara.position+Lara.up*1.6f;else{var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);target=bounds.center+Vector3.up*bounds.extents.y*.65f;}}
            var direction=target-shotPosition;if(direction.sqrMagnitude>.0001f)shotRotation=Quaternion.LookRotation(direction,Vector3.up);
        }
        public Vector3 Project(Vector3 world,Rect rect){if(utility==null)return new Vector3(-10000,-10000,-1);var v=utility.camera.WorldToViewportPoint(world);return new Vector3(rect.x+v.x*rect.width,rect.y+(1-v.y)*rect.height,v.z);}
        public bool DragOnGround(Vector2 mouse,Rect rect,Vector3 current,out Vector3 next){
            next=current;if(utility==null)return false;
            var ray=utility.camera.ViewportPointToRay(new Vector3((mouse.x-rect.x)/rect.width,1-(mouse.y-rect.y)/rect.height,0));
            var plane=new Plane(Vector3.up,current);if(!plane.Raycast(ray,out var distance))return false;next=ray.GetPoint(distance);return true;
        }
        void DrawFrustum(Rect rect,Camera orbit) {
            if(SourceCamera==null||PlayerCamera)return;
            var points=new Vector3[4];float near=Mathf.Max(.02f,SourceCamera.nearClipPlane),far=Mathf.Min(5,SourceCamera.farClipPlane);
            var transform=CloneFor(SourceCamera.transform)??SourceCamera.transform;
            var position=customCamera?shotPosition:transform.position;var rotation=customCamera?shotRotation:transform.rotation;
            Vector3[] Plane(float distance){SourceCamera.CalculateFrustumCorners(new Rect(0,0,1,1),distance,Camera.MonoOrStereoscopicEye.Mono,points);return points.Select(p=>position+rotation*p).ToArray();}
            var n=Plane(near);var f=Plane(far);var old=Handles.color;Handles.BeginGUI();Handles.color=Color.yellow;
            void Edge(Vector3 a,Vector3 b){var pa=Project(a,rect);var pb=Project(b,rect);if(pa.z>0&&pb.z>0)Handles.DrawAAPolyLine(1.5f,new Vector3(pa.x,pa.y),new Vector3(pb.x,pb.y));}
            for(int i=0;i<4;i++){Edge(n[i],n[(i+1)%4]);Edge(f[i],f[(i+1)%4]);Edge(n[i],f[i]);}
            Handles.color=old;Handles.EndGUI();
        }
        public bool Ready=>root!=null;
        public Transform CloneFor(Transform source)=>source!=null&&map.TryGetValue(source,out var clone)?clone:null;
        public void Build(StorySceneBindings scene,GameObject performerPrefab,GameObject playerPrefab)
        {
            Dispose();Diagnostic=null;
            try {
                utility=new PreviewRenderUtility();utility.camera.nearClipPlane=.01f;utility.camera.farClipPlane=1000;
                root=new GameObject("Studio visual stage"){hideFlags=HideFlags.HideAndDontSave};
                utility.AddSingleGO(root);
                if(scene!=null) {
                    foreach(var source in scene.gameObject.scene.GetRootGameObjects()) {
                        if(source.GetComponent<Canvas>()!=null)continue;
                        Copy(source.transform,root.transform);
                    }
                    Lara=CloneFor(scene.lara!=null?scene.lara.transform:null);
                    LaraFocus=CloneFor(scene.laraFocus);laraFocusIsChild=LaraFocus!=null&&Lara!=null&&LaraFocus.IsChildOf(Lara);
                    if(LaraFocus!=null&&Lara!=null)laraFocusOffset=Lara.InverseTransformPoint(LaraFocus.position);
                    PlayerBody=CloneFor(scene.playerBody!=null?scene.playerBody.transform:null);
                    SourceCamera=scene.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c=>c.CompareTag("MainCamera"));
                    AddSceneMarkers(scene);
                    AddMeasuredContactPreview(scene);
                    if(scene.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Any(c=>c!=null&&c.GetType().FullName=="UnityEngine.Rendering.Volume"))
                        diagnostics.Add("Post-processing Volume profiles are not evaluated in the isolated preview.");
                } else {
                    if(performerPrefab!=null)Lara=Copy(performerPrefab.transform,root.transform);
                    if(playerPrefab!=null)PlayerBody=Copy(playerPrefab.transform,root.transform);
                    diagnostics.Add("No open story scene: showing selected production prefab visuals. Open a saved story scene to preview its authored environment and marks.");
                }
                if(Lara!=null)Lara.gameObject.SetActive(true);
                if(PlayerBody!=null)PlayerBody.gameObject.SetActive(true);
                // The hierarchy is fully mapped before any skinned renderer references are copied.
                foreach(var pair in map)CopyVisuals(pair.Key,pair.Value);
                Frame(Lara!=null?Lara:PlayerBody);
                if(diagnostics.Count>0)Diagnostic=string.Join(" ",diagnostics.Distinct());
            } catch(Exception e){Dispose();Diagnostic=e.Message;}
        }
        Transform Copy(Transform source,Transform parent)
        {
            var go=new GameObject(source.name){hideFlags=HideFlags.HideAndDontSave};
            go.layer=source.gameObject.layer;var t=go.transform;t.SetParent(parent,false);
            t.localPosition=source.localPosition;t.localRotation=source.localRotation;t.localScale=source.localScale;
            // Renderer-only copies may show actors hidden by runtime startup; no lifecycle scripts are evaluated.
            go.SetActive(source.gameObject.activeSelf);map.Add(source,t);
            for(int i=0;i<source.childCount;i++)Copy(source.GetChild(i),t);
            return t;
        }
        Material[] Materials(Material[] values)=>values.Select(value=>{
            if(value==null)return null;
            if(!materials.TryGetValue(value,out var copy)) {
                copy=new Material(value){hideFlags=HideFlags.HideAndDontSave};materials.Add(value,copy);ownedMaterials.Add(copy);
            }
            return copy;
        }).ToArray();
        void CopyVisuals(Transform source,Transform target)
        {
            var filter=source.GetComponent<MeshFilter>();if(filter!=null)target.gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            foreach(var renderer in source.GetComponents<MeshRenderer>()) {
                var copy=target.gameObject.AddComponent<MeshRenderer>();EditorUtility.CopySerialized(renderer,copy);
                copy.sharedMaterials=Materials(renderer.sharedMaterials);regular[copy]=copy.sharedMaterials;
            }
            foreach(var renderer in source.GetComponents<SkinnedMeshRenderer>()) {
                var copy=target.gameObject.AddComponent<SkinnedMeshRenderer>();EditorUtility.CopySerialized(renderer,copy);
                copy.rootBone=CloneFor(renderer.rootBone);copy.bones=renderer.bones.Select(CloneFor).ToArray();
                copy.sharedMaterials=Materials(renderer.sharedMaterials);regular[copy]=copy.sharedMaterials;copy.updateWhenOffscreen=true;
            }
            foreach(var light in source.GetComponents<Light>()) {
                var copy=target.gameObject.AddComponent<Light>();EditorUtility.CopySerialized(light,copy);
            }
            foreach(var sourceCamera in source.GetComponents<Camera>()) {
                var copy=target.gameObject.AddComponent<Camera>();copy.CopyFrom(sourceCamera);copy.enabled=false;copy.targetTexture=null;
            }
            var particle=source.GetComponent<ParticleSystem>();
            if(particle!=null) {
                var copy=target.gameObject.AddComponent<ParticleSystem>();EditorUtility.CopySerialized(particle,copy);
                var main=copy.main;main.playOnAwake=false;
                var external=copy.externalForces;external.enabled=false;
                var collision=copy.collision;collision.enabled=false;
                var trigger=copy.trigger;trigger.enabled=false;
                copy.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                copy.Simulate(Mathf.Clamp(main.duration*.35f,.2f,2f),true,true,false);copy.Pause(true);
                var sourceRenderer=source.GetComponent<ParticleSystemRenderer>();
                var particleRenderer=target.GetComponent<ParticleSystemRenderer>();
                if(sourceRenderer!=null&&particleRenderer!=null) {
                    EditorUtility.CopySerialized(sourceRenderer,particleRenderer);
                    particleRenderer.sharedMaterials=Materials(sourceRenderer.sharedMaterials);
                    particleRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    particleRenderer.receiveShadows=false;
                }
                diagnostics.Add("Particle systems use a fixed isolated snapshot; they do not simulate continuously.");
            }
        }
        void AddSceneMarkers(StorySceneBindings scene) {
            foreach(var location in scene.locations??Array.Empty<StoryLocationBinding>()) {
                if(location==null)continue;
                AddMarker(location.placement,location.id+" - placement",new Color(.2f,.85f,1f));
                AddMarker(location.focus,location.id+" - focus",new Color(.65f,1f,.25f));
                if(location.seat!=null) {
                    AddMarker(location.seat.ApproachAnchor,location.id+" - approach",new Color(1f,.7f,.15f));
                    AddMarker(location.seat.SeatAnchor,location.id+" - seat pelvis",new Color(1f,.25f,.8f));
                }
            }
            if(scene.laraFocus!=null)AddMarker(scene.laraFocus,"Lara focus",new Color(.65f,1f,.25f));
        }
        void AddMarker(Transform source,string label,Color color) {
            if(source==null)return;
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.name="Studio marker - "+label;sphere.hideFlags=HideFlags.HideAndDontSave;
            var collider=sphere.GetComponent<Collider>();if(collider!=null)UnityEngine.Object.DestroyImmediate(collider);
            sphere.transform.SetParent(root.transform,false);sphere.transform.position=source.position;sphere.transform.localScale=Vector3.one*.09f;
            var arrow=GameObject.CreatePrimitive(PrimitiveType.Cube);arrow.name="Studio facing - "+label;arrow.hideFlags=HideFlags.HideAndDontSave;
            collider=arrow.GetComponent<Collider>();if(collider!=null)UnityEngine.Object.DestroyImmediate(collider);
            arrow.transform.SetParent(root.transform,false);arrow.transform.SetPositionAndRotation(source.position+source.forward*.20f,source.rotation);arrow.transform.localScale=new Vector3(.025f,.025f,.38f);
            var material=NewMarkerMaterial(color);
            foreach(var renderer in sphere.GetComponents<Renderer>().Concat(arrow.GetComponents<Renderer>())){renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}
            markers.Add(new PreviewMarker(source.position,label,color));
        }
        Material NewMarkerMaterial(Color color) {
            var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");
            if(shader==null){diagnostics.Add("Preview anchor markers are unavailable because no supported unlit shader is loaded.");return null;}
            var material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            ownedMaterials.Add(material);return material;
        }
        void AddMeasuredContactPreview(StorySceneBindings scene) {
            var target=scene.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PerformanceSceneBindings>(true)).Select(b=>b.ContactTarget as PartnerContactTarget).FirstOrDefault(c=>c!=null);
            if(target==null||target.Body==null||target.Profile==null){diagnostics.Add("Prepared measured contact profile is unavailable; contact geometry is not previewed.");return;}
            var body=target.Body;var profile=target.Profile;var bodyClone=CloneFor(body.transform);
            if(bodyClone==null){diagnostics.Add("Measured contact profile exists but its prepared body transform is outside the cloned scene.");return;}
            var mesh=BuildMeasuredContactMesh(profile);if(mesh==null){diagnostics.Add("Measured contact profile has no usable center/radius samples.");return;}
            ownedMeshes.Add(mesh);var geometry=new GameObject("Studio measured contact radius preview"){hideFlags=HideFlags.HideAndDontSave};geometry.transform.SetParent(bodyClone,false);
            geometry.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=geometry.AddComponent<MeshRenderer>();renderer.sharedMaterial=NewMarkerMaterial(new Color(.1f,.85f,1f,.24f));
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            diagnostics.Add("Contact overlay shows measured center/radius samples; the evaluated irregular collision surface is not rendered.");
        }
        static Mesh BuildMeasuredContactMesh(PartnerContactProfile profile) {
            var centers=profile.CentersRoot;int rings=centers.Length,segments=12;if(rings<2)return null;
            Vector3 axis=profile.SectionTangentRoot.normalized;Vector3 side=Vector3.Cross(axis,Vector3.up);if(side.sqrMagnitude<.0001f)side=Vector3.Cross(axis,Vector3.right);side.Normalize();Vector3 up=Vector3.Cross(axis,side).normalized;
            var vertices=new Vector3[rings*segments];var triangles=new int[(rings-1)*segments*6];
            for(int r=0;r<rings;r++)for(int i=0;i<segments;i++){float angle=2*Mathf.PI*i/segments;vertices[r*segments+i]=centers[r]+profile.Radius(r)*(side*Mathf.Cos(angle)+up*Mathf.Sin(angle));}
            int at=0;for(int r=0;r<rings-1;r++)for(int i=0;i<segments;i++){int a=r*segments+i,b=r*segments+(i+1)%segments,c=(r+1)*segments+i,d=(r+1)*segments+(i+1)%segments;triangles[at++]=a;triangles[at++]=c;triangles[at++]=b;triangles[at++]=b;triangles[at++]=c;triangles[at++]=d;}
            var mesh=new Mesh{name="Studio measured contact radius preview"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public void Frame(Transform target) {
            if(target==null)return;
            var renderers=target.GetComponentsInChildren<Renderer>(true);
            var bounds=new Bounds(target.position+Vector3.up,Vector3.one);
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            focus=bounds.center;Distance=Mathf.Max(2,bounds.extents.magnitude*2.3f);UpdateTrackedCamera();
        }
        public void Arrival(Transform placement) {
            if(Lara==null||placement==null)throw new InvalidOperationException("Preview requires Lara and a placement.");
            Lara.gameObject.SetActive(true);Lara.SetPositionAndRotation(placement.position,placement.rotation);Frame(Lara);
        }
        public void SeatedArrival(PerformerSeat seat,AnimationClip pose=null) {
            if(seat==null||seat.SeatAnchor==null||seat.SeatingProfile?.SitStart==null)throw new InvalidOperationException("Seated preview needs the authored seat, pelvis anchor and seating profile.");
            Arrival(seat.SeatAnchor);
            var motion=seat.SeatingProfile.SitStart;var clip=pose??motion.BodyClip;
            Sample(clip,pose!=null?0:clip.length);
            Lara.position=seat.SeatAnchor.position-Lara.TransformVector(motion.PelvisOffsetAt(1));
            Frame(Lara);
        }
        public void Sample(AnimationClip clip,float time) {
            if(Lara==null||clip==null)throw new InvalidOperationException("Choose the generated pose/gesture wrapper and a Lara preview.");
            if(AnimationMode.InAnimationMode()&&!animationMode)throw new InvalidOperationException("Another editor owns Animation Mode. Close that preview first.");
            if(!animationMode){AnimationMode.StartAnimationMode();animationMode=true;}
            var placement=Lara.position;var facing=Lara.rotation;
            AnimationMode.BeginSampling();
            try{AnimationMode.SampleAnimationClip(Lara.gameObject,clip,Mathf.Clamp(time,0,clip.length));}
            finally{AnimationMode.EndSampling();Lara.SetPositionAndRotation(placement,facing);UpdateTrackedCamera();}
        }
        public void Magic(PerformerMagicShaderProfile profile) {
            if(profile==null)throw new ArgumentNullException(nameof(profile));profile.ValidateConfiguration();
            foreach(var row in regular)if(row.Key!=null&&Lara!=null&&row.Key.transform.IsChildOf(Lara))
                row.Key.sharedMaterials=profile.mode==PerformerMaterialMode.Regular?row.Value:
                    Materials(Enumerable.Repeat(profile.wireframeMaterial,row.Value.Length).ToArray());
        }
        public void Draw(Rect rect) {
            if(utility==null||root==null){EditorGUI.HelpBox(rect,Diagnostic??"Select a source and build the isolated preview.",MessageType.Info);return;}
            if(Event.current.type==EventType.MouseDrag&&rect.Contains(Event.current.mousePosition)){
                Yaw+=Event.current.delta.x*.5f;Pitch=Mathf.Clamp(Pitch+Event.current.delta.y*.5f,-80,80);Event.current.Use();
            }
            if(Event.current.type==EventType.ScrollWheel&&rect.Contains(Event.current.mousePosition)){
                Distance=Mathf.Clamp(Distance+Event.current.delta.y*.2f,.2f,100);Event.current.Use();
            }
            var camera=utility.camera;
            var cameraClone=SourceCamera!=null?CloneFor(SourceCamera.transform)?.GetComponent<Camera>():null;
            if(PlayerCamera&&customCamera){UpdateTrackedCamera();camera.transform.SetPositionAndRotation(shotPosition,shotRotation);camera.fieldOfView=SourceCamera!=null?SourceCamera.fieldOfView:45;}
            else if(PlayerCamera&&cameraClone!=null){camera.CopyFrom(cameraClone);camera.enabled=true;camera.targetTexture=null;camera.transform.SetPositionAndRotation(cameraClone.transform.position,cameraClone.transform.rotation);}
            else if(PlayerCamera&&SourceCamera!=null){camera.transform.SetPositionAndRotation(SourceCamera.transform.position,SourceCamera.transform.rotation);camera.fieldOfView=SourceCamera.fieldOfView;}
            else {ViewRotation=Quaternion.Euler(Pitch,Yaw,0);ViewPosition=focus-ViewRotation*Vector3.forward*Distance;
                camera.transform.SetPositionAndRotation(ViewPosition,ViewRotation);camera.fieldOfView=45;}
            if(Event.current.type!=EventType.Repaint)return;
            bool begun=false;
            try {utility.BeginPreview(rect,GUIStyle.none);begun=true;utility.Render(true);
                var texture=utility.EndPreview();begun=false;GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill,false);DrawFrustum(rect,camera);
                foreach(var marker in markers){var point=camera.WorldToViewportPoint(marker.Position);if(point.z<=0)continue;float x=rect.x+point.x*rect.width,y=rect.y+(1-point.y)*rect.height;var tint=GUI.color;GUI.color=marker.Color;GUI.Label(new Rect(x+5,y-9,Mathf.Min(150,rect.width*.45f),18),marker.Label,EditorStyles.whiteMiniLabel);GUI.color=tint;}
            }
            catch(Exception e){if(begun)try{utility.EndPreview();}catch{}Diagnostic="Preview render unsupported: "+e.Message;EditorGUI.HelpBox(rect,Diagnostic,MessageType.Warning);}
            if(Diagnostic!=null)GUI.Label(new Rect(rect.x+8,rect.y+8,rect.width-16,50),Diagnostic,EditorStyles.wordWrappedMiniLabel);
        }
        public void Dispose() {
            if(animationMode){if(AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();animationMode=false;}
            if(root!=null)UnityEngine.Object.DestroyImmediate(root);root=null;
            utility?.Cleanup();utility=null;
            foreach(var material in ownedMaterials)if(material!=null)UnityEngine.Object.DestroyImmediate(material);
            foreach(var mesh in ownedMeshes)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
            ownedMaterials.Clear();ownedMeshes.Clear();markers.Clear();diagnostics.Clear();materials.Clear();regular.Clear();map.Clear();customCamera=false;trackingLara=false;laraFocusIsChild=false;laraFocusOffset=Vector3.zero;LaraFocus=null;Lara=null;PlayerBody=null;SourceCamera=null;Diagnostic=null;
        }
    }
}
