using System.IO;
using System.Linq;
using DazPose.Game;
using DazPose.Presentation;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace DazPose.Editor {
 public static class GameUiAssetBuilder {
  public const string SettingsPath="Assets/Production/Application/SettingsPanel.prefab";
  public const string ControlsPath="Assets/Production/Application/PerformanceControls.prefab";
  static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
  static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h) {
   var go=new GameObject(name,typeof(RectTransform)); var r=go.GetComponent<RectTransform>();
   r.SetParent(parent,false); r.anchorMin=r.anchorMax=new Vector2(.5f,1); r.pivot=new Vector2(.5f,1);
   r.anchoredPosition=new Vector2(x,y); r.sizeDelta=new Vector2(w,h); return r;
  }
  static Text Text(string value,Transform parent,float x,float y,float w=400,float h=32,int size=18) {
   var t=Rect(value,parent,x,y,w,h).gameObject.AddComponent<Text>();
   t.font=Font; t.fontSize=size; t.text=value; t.color=new Color(.92f,.93f,.97f); t.alignment=TextAnchor.MiddleLeft; return t;
  }
  static Image Image(RectTransform r,Color color) { var i=r.gameObject.AddComponent<Image>(); i.color=color; return i; }
  static Button Button(string title,Transform parent,float x,float y,float w=150) {
   var r=Rect(title,parent,x,y,w,40); var image=Image(r,new Color(.19f,.22f,.31f));
   var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=image;
   var label=Text(title,r,0,0,w,40); label.alignment=TextAnchor.MiddleCenter; return b;
  }
  static InputField Input(string title,Transform parent,float x,float y,float w=360) {
   var r=Rect(title,parent,x,y,w,38); var bg=Image(r,new Color(.12f,.14f,.2f));
   var text=Text("",r,0,0,w-16,38); var field=r.gameObject.AddComponent<InputField>();
   field.targetGraphic=bg; field.textComponent=text; return field;
  }
  static Toggle Toggle(string title,Transform parent,float x,float y) {
   var r=Rect(title,parent,x,y,140,30);
   var box=Rect("Box",r,-60,0,26,26); var bg=Image(box,new Color(.15f,.18f,.26f));
   var check=Rect("Check",box,0,-5,16,16); var graphic=Image(check,new Color(.55f,.65f,1));
   var toggle=r.gameObject.AddComponent<Toggle>(); toggle.targetGraphic=bg; toggle.graphic=graphic;
   Text(title,r,18,0,100,28,16); return toggle;
  }
  static Slider Slider(string title,Transform parent,float x,float y,float width=180) {
   var r=Rect(title,parent,x,y,width,48); Text(title,r,0,0,width,24,15);
   var track=Rect("Track",r,0,-32,width,8); Image(track,new Color(.16f,.18f,.24f));
   var fill=Rect("Fill",track,0,0,width,8); Image(fill,new Color(.48f,.59f,.9f));
   var handle=Rect("Handle",track,0,4,16,16); var image=Image(handle,new Color(.85f,.89f,1));
   var slider=r.gameObject.AddComponent<Slider>(); slider.fillRect=fill; slider.handleRect=handle; slider.targetGraphic=image;
   slider.minValue=0; slider.maxValue=1; return slider;
  }
  static GameObject Canvas(string name,int order) {
   var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
   var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=order; canvas.overrideSorting=true;
   var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,800); scaler.matchWidthOrHeight=1;
   return go;
  }
  static RectTransform FullPanel(GameObject canvas) {
   var r=Rect("Panel",canvas.transform,0,0,1280,800); Image(r,new Color(.055f,.065f,.095f,.98f)); return r;
  }
  static SettingsMenuController Settings(Transform parent) {
   var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPath));
   go.transform.SetParent(parent,false); go.GetComponent<SettingsMenuController>().FitToParentCanvas(); go.SetActive(false); return go.GetComponent<SettingsMenuController>();
  }
  static void MakeSettings() {
   if(File.Exists(SettingsPath)) return;
   var go=Canvas("Settings",100); Object.DestroyImmediate(go.GetComponent<CanvasScaler>()); go.transform.localScale=Vector3.one; var p=FullPanel(go);
   Text("Settings",p,-380,-30,400,48,32);
   Text("Intiface",p,-430,-98,160,32);
   var endpoint=Input("Endpoint",p,-120,-96,440);
   var connect=Button("Connect",p,200,-96,110); var disconnect=Button("Disconnect",p,330,-96,130); var scan=Button("Scan",p,470,-96,110);
   var status=Text("Disconnected",p,-140,-145,440);
   var master=Slider("Master volume",p,-330,-202,260); var speech=Slider("Speech volume",p,0,-202,260);
   var stop=Button("Stop all toy output",p,355,-208,260);
   Text("Toy routes · enable features and choose scene channels",p,-200,-282,840);
   var viewport=Rect("Routes viewport",p,0,-324,1120,366);
   Image(viewport,new Color(.075f,.085f,.12f)); viewport.gameObject.AddComponent<RectMask2D>();
   var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false;
   var content=Rect("Routes",viewport,0,0,1120,366); content.anchorMin=new Vector2(0,1); content.anchorMax=new Vector2(1,1); content.sizeDelta=new Vector2(0,366);
   var layout=content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=8; layout.childForceExpandHeight=false;
   var fitter=content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
   scroll.viewport=viewport; scroll.content=content;
   var row=Rect("Route template",content,0,0,1100,124); row.gameObject.AddComponent<LayoutElement>().preferredHeight=124;
   var label=Text("Feature",row,-230,-8,630,28,16);
   var enabled=Toggle("Enabled",row,-445,-52); var invert=Toggle("Invert",row,-290,-52);
   Text("Channel",row,-160,-45,80,26,14); var channel=Input("Channel",row,-145,-72,90);
   var low=Slider("Minimum",row,30,-40,180); var high=Slider("Maximum",row,230,-40,180); var test=Button("Test",row,425,-58,110);
   var route=row.gameObject.AddComponent<SettingsRouteRow>(); route.Bind(label,enabled,invert,channel,low,high,test); row.gameObject.SetActive(false);
   var close=Button("Close",p,440,-724,150);
   var controller=go.AddComponent<SettingsMenuController>(); controller.Configure(endpoint,connect,disconnect,scan,stop,close,master,speech,status,content,route);
   PrefabUtility.SaveAsPrefabAsset(go,SettingsPath); Object.DestroyImmediate(go);
  }
  static void MakeControls() {
   if(File.Exists(ControlsPath)) return;
   var go=Canvas("Performance Controls",10);
   var bar=Rect("Controls",go.transform,0,0,1280,56); Image(bar,new Color(.055f,.065f,.095f,.88f));
   var status=Text("Idle",bar,-510,-8,200,40,16);
   var start=Button("Start",bar,-310,-8,110); var stop=Button("Stop",bar,-180,-8,110);
   var restart=Button("Restart scene",bar,-20,-8,180); var options=Button("Settings",bar,160,-8,140); var menu=Button("Main menu",bar,340,-8,180);
   var settings=Settings(go.transform);
   var controller=go.AddComponent<PerformanceControlsController>(); controller.Configure(start,stop,restart,options,menu,status,settings);
   PrefabUtility.SaveAsPrefabAsset(go,ControlsPath); Object.DestroyImmediate(go);
  }
  public static void EnsureAssets() { ProductionApplicationSetup.EnsureAssets(); MakeSettings(); MakeControls(); AssetDatabase.SaveAssets(); }
  public static void Install(PerformanceHost host) {
   var scene=host.gameObject.scene;
   var controls=GameSessionSceneSetup.InScene<PerformanceControlsController>(scene);
   if(controls.Length==0) {
    var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ControlsPath),scene);
    Undo.RegisterCreatedObjectUndo(go,"Add performance controls"); controls=new[]{go.GetComponent<PerformanceControlsController>()};
   }
   if(controls.Length==1) { Undo.RecordObject(controls[0],"Bind performance host"); controls[0].BindHost(host); EditorUtility.SetDirty(controls[0]); PrefabUtility.RecordPrefabInstancePropertyModifications(controls[0]); }
   EnsureEventSystem();
  }
  static void EnsureEventSystem() {
   if(GameSessionSceneSetup.InScene<EventSystem>(UnityEngine.SceneManagement.SceneManager.GetActiveScene()).Length==0) {
    var go=new GameObject("Event System",typeof(EventSystem),typeof(StandaloneInputModule)); Undo.RegisterCreatedObjectUndo(go,"Add event system");
   }
  }
  public static void RepairUiSizing() {
   foreach(string path in new[]{SettingsPath,ControlsPath}) {
    var root=PrefabUtility.LoadPrefabContents(path);
    foreach(var scaler in root.GetComponentsInChildren<CanvasScaler>(true)) scaler.matchWidthOrHeight=1;
    foreach(var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.overrideSorting=true;
    foreach(var settings in root.GetComponentsInChildren<SettingsMenuController>(true)) {
     var scaler=settings.GetComponent<CanvasScaler>(); if(scaler!=null) Object.DestroyImmediate(scaler);
     settings.transform.localScale=Vector3.one; settings.FitToParentCanvas();
    }
    PrefabUtility.SaveAsPrefabAsset(root,path); PrefabUtility.UnloadPrefabContents(root);
   }
  }
  public static void BuildShell() {
   if(EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Validate the saved main menu in Edit Mode.");
   const string scenePath="Assets/Production/Scenes/MainMenu.unity";
   var library=AssetDatabase.LoadAssetAtPath<ProductionUiLibrary>("Assets/Resources/DazPose/ProductionUi.asset");
   var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Production/UI/MainMenuScreen.prefab");
   if(library==null || library.mainMenu==null || template==null) throw new System.InvalidOperationException("The ProductionUi library and MainMenuScreen template must be present.");
   var scene=EditorSceneManager.GetSceneByPath(scenePath);
   if(!scene.IsValid() || !scene.isLoaded) scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
   var menus=GameSessionSceneSetup.InScene<MainMenuController>(scene);
   if(menus.Length!=1 || menus[0].GetComponent<SceneBrowserView>()==null) throw new System.InvalidOperationException("MainMenu must contain exactly one wired MainMenuController and SceneBrowserView.");
   if(GameSessionSceneSetup.InScene<EventSystem>(scene).Length!=1) throw new System.InvalidOperationException("MainMenu must preserve exactly one EventSystem.");
   var catalog=AssetDatabase.LoadAssetAtPath<SceneCatalog>(ProductionApplicationSetup.CatalogPath);
   var expected=GamePlayerBuild.ComputeScenes(catalog);
   if(EditorBuildSettings.scenes.Length!=expected.Length || !EditorBuildSettings.scenes.Select(x=>x.path).SequenceEqual(expected))
    throw new System.InvalidOperationException("Synchronize Editor Build Settings with the public scene catalog.");
  }
 }
}
