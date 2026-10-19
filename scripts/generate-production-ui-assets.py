#!/usr/bin/env python3
"""Generate deterministic U02 Unity YAML UI assets; writes only new Production/UI and ProductionUi assets."""
from pathlib import Path
import uuid
ROOT=Path(__file__).resolve().parents[1]/"validation"/"DazPoseUnityValidation"/"Assets"
UI=ROOT/"Production"/"UI"; RES=ROOT/"Resources"/"DazPose"
def g(s): return uuid.uuid5(uuid.NAMESPACE_URL,"dazpose-u02:"+s).hex
def folder(p):
 p.mkdir(parents=True,exist_ok=True)
 sidecar=p.with_name(p.name+".meta")
 if sidecar.exists(): return
 sidecar.write_text(f"fileFormatVersion: 2\nguid: {g(p.relative_to(ROOT).as_posix())}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",encoding="utf-8")
def meta(p,kind):
 sidecar=p.with_name(p.name+".meta")
 if sidecar.exists(): return
 sidecar.write_text(f"fileFormatVersion: 2\nguid: {g(p.relative_to(ROOT).as_posix())}\n{kind}:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",encoding="utf-8")
folder(UI); folder(RES.parent)
G={"Modal":"91fe0f5ecf7948918628bae0d500314d","Profile":"43b73423783344dbaf6d0587aab52553","Interest":"0b1c0d22de724303bd93e44d4c0cebcf","Equipment":"eef954b5a69d41cba54827104baeb787","Label":"b19d962527854b03bc1690c7efcfb369","Card":"37e6d005c3b145f88d074a383f2c440c","Browser":"94bd888792ee4d2f931b0b00682a434c","MenuController":"3c61954f9071c6e4287e518688ba0c5a","List":"c78c1440346a4900ad39e7411ea43d1d","ThemeGraphic":"c5d8bb2123fb43448bf6b416d5543472","Theme":"7bd837806d254bc38ebf96685c62be02","Library":"12a4fedd685e411991ed4a3f524deb27","Image":"fe87c0e1cc204ed48ad3b37840f39efc","Button":"4e29b1a8efbd4b44bb3f3716e73f07ff","Toggle":"9085046f02f69544eb97fd06b6048fe2","ToggleGroup":"2fafe2cfe61f6974895a912c3755e8f1","Ray":"dc42784cf147c0c48a680349fa168899","Scaler":"0cd44c1031e13a943bb63640046fad76","Scroll":"1aa08ab6e0800fa44ae55d278d1423e3","Mask":"31a19414c41e5ae4aae2af33fee712f6","Input":"2da0c512f12947e489f739169773d7ca","TMP":"f4688fdb7df04437aeb418b961361dc5"}
class Prefab:
 def __init__(self,name,canvas=False):
  self.name=name; self.i=10000; self.os=[]; self.root=self.obj(name,None,False)
  if canvas:
   self.comp(self.root,"Canvas",{"m_RenderMode":0,"m_TargetDisplay":0},None,223)
   self.comp(self.root,"Scaler",{"m_UiScaleMode":1,"m_ReferenceResolution":"{x: 1920, y: 1080}","m_ScreenMatchMode":0,"m_MatchWidthOrHeight":1})
   self.comp(self.root,"Ray",{},None)
 def id(self): self.i+=1; return self.i
 def obj(self,name,parent,active=True):
  o={"name":name,"go":self.id(),"tr":self.id(),"parent":parent,"active":active,"children":[],"cs":[],"rect":None}
  if parent: parent["children"].append(o)
  self.os.append(o); return o
 def rect(self,o,amin,amax,pos=(0,0),size=(0,0),pivot=(0.5,0.5)):
  o["rect"]=(amin,amax,pos,size,pivot)
 def comp(self,o,k,fields=None,g=None,classid=114,enabled=1):
  c={"id":self.id(),"kind":k,"fields":fields or {},"guid":g or G.get(k),"classid":classid,"enabled":enabled};o["cs"].append(c);return c["id"]
 def script(self,o,g,fields):return self.comp(o,"Script",fields,g)
 def canvas_renderer(self,o):
  if not any(c["classid"]==222 for c in o["cs"]):self.comp(o,"CanvasRenderer",{"m_CullTransparentMesh":1},None,222)
 def image(self,o,role="Panel",ray=False,enabled=1):
  self.canvas_renderer(o)
  i=self.comp(o,"Image",{"m_Color":"{r: 1, g: 1, b: 1, a: 1}","m_RaycastTarget":int(ray),"m_Sprite":"{fileID: 0}"},enabled=enabled)
  self.script(o,G["ThemeGraphic"],{"target":f"{{fileID: {i}}}","role":{"Background":0,"Panel":1,"Control":2,"Accent":3,"Text":4,"Muted":5,"Error":6}[role]})
  return i
 def text(self,o,label,size=18,role="Text"):
  self.canvas_renderer(o)
  i=self.comp(o,"TMP",{"m_text":label,"m_fontAsset":"{fileID: 0}","m_fontSize":size,"m_fontSizeBase":size,"m_enableAutoSizing":0,"m_RaycastTarget":0})
  self.script(o,G["ThemeGraphic"],{"target":f"{{fileID: {i}}}","role":{"Background":0,"Panel":1,"Control":2,"Accent":3,"Text":4,"Muted":5,"Error":6}[role]})
  return i
 def button(self,o):
  im=self.image(o,"Control",True); return self.comp(o,"Button",{"m_Interactable":1,"m_TargetGraphic":f"{{fileID: {im}}}","m_OnClick":{"m_PersistentCalls":{"m_Calls":[]}}})
 def toggle(self,o,group=0):
  im=self.image(o,"Control",True);mark=self.obj("SelectedMark",o);markrect=(.03,.12),(.25,.88),(0,0),(0,0),(.5,.5);self.rect(mark,*markrect);graphic=self.image(mark,"Accent",False,enabled=0)
  return self.comp(o,"Toggle",{"m_Interactable":1,"m_TargetGraphic":f"{{fileID: {im}}}","m_IsOn":0,"m_Graphic":f"{{fileID: {graphic}}}","m_Group":f"{{fileID: {group}}}","m_OnValueChanged":{"m_PersistentCalls":{"m_Calls":[]}}})
 def scroll(self,parent,name):
  box=self.obj(name,parent);self.image(box)
  v=self.obj("Viewport",box);self.image(v,"Background");self.comp(v,"Mask",{"m_ShowMaskGraphic":0})
  c=self.obj("Content",v);self.rect(c,(0,1),(1,1),(0,0),(0,0),(.5,1))
  sr=self.comp(box,"Scroll",{"m_Content":f"{{fileID: {c['tr']}}}","m_Viewport":f"{{fileID: {v['tr']}}}","m_Horizontal":0,"m_Vertical":1,"m_MovementType":2,"m_Inertia":1,"m_ScrollSensitivity":30,"m_OnValueChanged":{"m_PersistentCalls":{"m_Calls":[]}}})
  li=self.script(box,G["List"],{"scroll":f"{{fileID: {sr}}}","content":f"{{fileID: {c['tr']}}}","rowHeight":72})
  return box,c,li
 def write(self):
  p=UI/(self.name+".prefab");out=["%YAML 1.1","%TAG !u! tag:unity3d.com,2011:"]
  for o in self.os:
   out += [f"--- !u!1 &{o['go']}","GameObject:","  m_ObjectHideFlags: 0","  m_CorrespondingSourceObject: {fileID: 0}","  m_PrefabInstance: {fileID: 0}","  m_PrefabAsset: {fileID: 0}","  serializedVersion: 6","  m_Component:",f"  - component: {{fileID: {o['tr']}}}"]
   out += [f"  - component: {{fileID: {c['id']}}}" for c in o["cs"]]
   out += ["  m_Layer: 5",f"  m_Name: {o['name']}","  m_TagString: Untagged","  m_Icon: {fileID: 0}","  m_NavMeshLayer: 0","  m_StaticEditorFlags: 0",f"  m_IsActive: {int(o['active'])}",f"--- !u!224 &{o['tr']}","RectTransform:","  m_ObjectHideFlags: 0","  m_CorrespondingSourceObject: {fileID: 0}","  m_PrefabInstance: {fileID: 0}","  m_PrefabAsset: {fileID: 0}",f"  m_GameObject: {{fileID: {o['go']}}}","  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}","  m_LocalPosition: {x: 0, y: 0, z: 0}","  m_LocalScale: {x: 1, y: 1, z: 1}","  m_ConstrainProportionsScale: 0","  m_Children:"]
   out += [f"  - {{fileID: {x['tr']}}}" for x in o["children"]]
   if not o["children"]:out[-1]="  m_Children: []"
   amin,amax,pos,size,pivot=o["rect"] or ((0,0),(1,1),(0,0),(0,0),(.5,.5))
   out += [f"  m_Father: {{fileID: {o['parent']['tr'] if o['parent'] else 0}}}","  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",f"  m_AnchorMin: {{x: {amin[0]}, y: {amin[1]}}}",f"  m_AnchorMax: {{x: {amax[0]}, y: {amax[1]}}}",f"  m_AnchoredPosition: {{x: {pos[0]}, y: {pos[1]}}}",f"  m_SizeDelta: {{x: {size[0]}, y: {size[1]}}}",f"  m_Pivot: {{x: {pivot[0]}, y: {pivot[1]}}}"]
   for c in o["cs"]:
    if c["classid"]==222:
     out += [f"--- !u!222 &{c['id']}","CanvasRenderer:","  m_ObjectHideFlags: 0","  m_CorrespondingSourceObject: {fileID: 0}","  m_PrefabInstance: {fileID: 0}","  m_PrefabAsset: {fileID: 0}",f"  m_GameObject: {{fileID: {o['go']}}}","  m_CullTransparentMesh: 1"]
     continue
    out += [f"--- !u!{c['classid']} &{c['id']}",("Canvas:" if c['classid']==223 else "MonoBehaviour:"),"  m_ObjectHideFlags: 0","  m_CorrespondingSourceObject: {fileID: 0}","  m_PrefabInstance: {fileID: 0}","  m_PrefabAsset: {fileID: 0}",f"  m_GameObject: {{fileID: {o['go']}}}",f"  m_Enabled: {c['enabled']}","  m_EditorHideFlags: 0"]
    if c["guid"]:out.append(f"  m_Script: {{fileID: 11500000, guid: {c['guid']}, type: 3}}")
    for k,v in c["fields"].items():
     if isinstance(v,list):
      out.append(f"  {k}:")
      out += [f"  - {{fileID: {x}}}" if isinstance(x,int) else f"  - {x}" for x in v]
     elif isinstance(v,dict):
      out.append(f"  {k}:")
      for a,b in v.items():
       out.append(f"    {a}:")
       if isinstance(b,dict):
        for x,y in b.items():out.append(f"      {x}: {y}")
       elif isinstance(b,list):out.append("      []")
       else:out.append(f"      {b}")
     else:out.append(f"  {k}: {v}")
  p.write_text("\n".join(line.rstrip() for line in out).rstrip()+"\n",encoding="utf-8");meta(p,"PrefabImporter")
def label(p,parent,n,t,size=18,role="Text"):
 o=p.obj(n,parent)
 if any(c["kind"]=="Toggle" for c in parent["cs"]):p.rect(o,(.28,.05),(.97,.95))
 return p.text(o,t,size,role)
def btn(p,parent,n,t,size=20):
 o=p.obj(n,parent);i=p.button(o);label(p,o,"Label",t,size);return i
def owner(p,cid): return next(o for o in p.os if any(c["id"]==cid for c in o["cs"]))
def label_component(p,parent_name):
 parent=next(o for o in p.os if o["name"]==parent_name)
 child=next(o for o in parent["children"] if o["name"]=="Label")
 return next(c["id"] for c in child["cs"] if c["kind"]=="TMP")
p=Prefab("ModalShell");dimmer=p.obj("Backdrop",p.root);p.image(dimmer,"Background");p.rect(dimmer,(0,0),(1,.94));blocker=p.obj("InputBlocker",p.root);p.canvas_renderer(blocker);p.comp(blocker,"Image",{"m_Color":"{r: 0, g: 0, b: 0, a: 0}","m_RaycastTarget":1,"m_Sprite":"{fileID: 0}"});p.rect(blocker,(0,0),(1,.94));h=p.obj("Header",p.root);p.image(h);ti=label(p,h,"Title","TITLE",24);bodyScroll=p.obj("BodyScroll",p.root);p.image(bodyScroll,"Panel");viewport=p.obj("Viewport",bodyScroll);p.image(viewport,"Background");p.comp(viewport,"Mask",{"m_ShowMaskGraphic":0});bo=p.obj("Body",viewport);p.rect(bo,(0,1),(1,1),(0,0),(0,0),(.5,1));p.comp(bodyScroll,"Scroll",{"m_Content":f"{{fileID: {bo['tr']}}}","m_Viewport":f"{{fileID: {viewport['tr']}}}","m_Horizontal":0,"m_Vertical":1,"m_MovementType":2,"m_Inertia":1,"m_ScrollSensitivity":30});er=label(p,p.root,"Error","",18,"Error");f=p.obj("FixedFooter",p.root);p.image(f);ap=btn(p,f,"Apply","Apply");ca=btn(p,f,"Cancel","Cancel");p.rect(h,(0,1),(1,1),(0,-64),(0,72),(0.5,1));p.rect(bodyScroll,(.04,0),(.96,1),(0,108),(0,-216));p.rect(owner(p,er),(0,0),(1,0),(0,184),(0,32),(0.5,0));p.rect(f,(0,0),(1,0),(0,96),(0,72),(0.5,0));
p.rect(owner(p,ti),(.04,.05),(.96,.95));p.rect(owner(p,ap),(.04,.05),(.48,.95));p.rect(owner(p,ca),(.52,.05),(.96,.95));modalView=p.script(p.root,G["Modal"],{"titleText":f"{{fileID: {ti}}}","errorText":f"{{fileID: {er}}}","body":f"{{fileID: {bo['tr']}}}","applyButton":f"{{fileID: {ap}}}","cancelButton":f"{{fileID: {ca}}}"});p.write()
p=Prefab("ProfileCatalogPanel");p.image(p.root,"Background");s=p.obj("Search",p.root);p.image(s,"Control",True);searchtext=label(p,s,"InputText","",18);placeholder=label(p,s,"Placeholder","Search by ID, name, or description",18,"Muted");inp=p.comp(s,"Input",{"m_textViewport":"{fileID: 0}","m_TextComponent":f"{{fileID: {searchtext}}}","m_Placeholder":f"{{fileID: {placeholder}}}","m_OnValueChanged":{"m_PersistentCalls":{"m_Calls":[]}}});su=label(p,p.root,"Summary","0 / 0",18,"Muted");fb=btn(p,p.root,"Filter","All",18);pop=p.obj("FilterPopup",p.root,False);_,_,fl=p.scroll(pop,"FilterScroll");_,_,li=p.scroll(p.root,"CatalogScroll");p.rect(s,(.04,.91),(.78,.98));p.rect(owner(p,searchtext),(.03,.08),(.97,.92));p.rect(owner(p,placeholder),(.03,.08),(.97,.92));p.rect(owner(p,su),(.04,.84),(.62,.90));p.rect(p.os[-5] if False else pop,(.80,.28),(.97,.82));
# root child rectangles: popup overlays on demand; main list reserves fixed bottom/head bars.
for obj in p.root["children"]:
 if obj["name"]=="CatalogScroll":p.rect(obj,(.04,.06),(.96,.82))
 if obj["name"]=="Filter":p.rect(obj,(.80,.84),(.97,.90))
p.script(p.root,G["Profile"],{"search":f"{{fileID: {inp}}}","summary":f"{{fileID: {su}}}","filterLabel":f"{{fileID: {label_component(p,'Filter')}}}","filterButton":f"{{fileID: {fb}}}","filterPopup":f"{{fileID: {pop['go']}}}","list":f"{{fileID: {li}}}","filterList":f"{{fileID: {fl}}}"});p.write()
p=Prefab("InterestRow");p.image(p.root);n=label(p,p.root,"Name","Interest name",24);d=label(p,p.root,"Details","Tags and description",18,"Muted");fg=p.comp(p.root,"ToggleGroup",{"m_AllowSwitchOff":0});sg=p.comp(p.root,"ToggleGroup",{"m_AllowSwitchOff":0});fs=[];ss=[]
for x in ("NO","1x","2x","4x"):
 o=p.obj("Frequency-"+x,p.root);fs.append(p.toggle(o,fg));label(p,o,"Label",x)
for x in ("Loved","Neutral","Punishment"):
 o=p.obj("Sentiment-"+x,p.root);ss.append(p.toggle(o,sg));label(p,o,"Label",x)
p.rect(p.root,(0,0),(1,1));p.rect(next(o for o in p.os if o["name"]=="Name"),(.02,.48),(.34,.94));p.rect(next(o for o in p.os if o["name"]=="Details"),(.02,.06),(.38,.45));
for i,o in enumerate([x for x in p.os if x["name"].startswith("Frequency-")]):p.rect(o,(.38+i*.062,.05),(.44+i*.062,.95))
for i,o in enumerate([x for x in p.os if x["name"].startswith("Sentiment-")]):p.rect(o,(.65+i*.11,.05),(.75+i*.11,.95))
p.script(p.root,G["Interest"],{"nameText":f"{{fileID: {n}}}","detailText":f"{{fileID: {d}}}","frequencies":fs,"sentiments":ss});p.write()
p=Prefab("EquipmentRow");p.image(p.root);n=label(p,p.root,"Name","Equipment name",24);d=label(p,p.root,"Details","Identity and theme tags",18,"Muted");ts=[]
for x in ("Owned / willing","Reward","Punishment"):
 o=p.obj(x,p.root);ts.append(p.toggle(o));label(p,o,"Label",x)
p.rect(next(o for o in p.os if o["name"]=="Name"),(.02,.48),(.36,.94));p.rect(next(o for o in p.os if o["name"]=="Details"),(.02,.06),(.38,.45));
for i,o in enumerate([x for x in p.os if x["name"] in ("Owned / willing","Reward","Punishment")]):p.rect(o,(.40+i*.20,.05),(.60+i*.20,.95))
p.script(p.root,G["Equipment"],{"nameText":f"{{fileID: {n}}}","detailText":f"{{fileID: {d}}}","owned":f"{{fileID: {ts[0]}}}","reward":f"{{fileID: {ts[1]}}}","punishment":f"{{fileID: {ts[2]}}}"});p.write()
p=Prefab("ClothingRow");p.image(p.root);n=label(p,p.root,"Label","Clothing slot",24);o=p.obj("Enabled",p.root);t=p.toggle(o);label(p,o,"Label","On");p.rect(next(o for o in p.os if o["name"]=="Label"),(.04,.05),(.55,.95));p.rect(next(o for o in p.os if o["name"]=="Enabled"),(.58,.05),(.98,.95));p.script(p.root,G["Label"],{"label":f"{{fileID: {n}}}","choice":f"{{fileID: {t}}}"});p.write()
p=Prefab("SceneCard");p.image(p.root);n=label(p,p.root,"Title","First Contact",24);d=label(p,p.root,"Description","Published scene",18,"Muted");m=p.obj("Selected",p.root,False);p.image(m,"Accent");b=btn(p,p.root,"Select","Select");p.rect(next(o for o in p.os if o["name"]=="Title"),(.06,.55),(.72,.96));p.rect(next(o for o in p.os if o["name"]=="Description"),(.06,.06),(.75,.50));p.rect(next(o for o in p.os if o["name"]=="Select"),(.78,.25),(.98,.75));p.rect(m,(0,0),(.015,1));p.script(p.root,G["Card"],{"titleText":f"{{fileID: {n}}}","descriptionText":f"{{fileID: {d}}}","selectButton":f"{{fileID: {b}}}","selectedMark":f"{{fileID: {m['go']}}}"});p.write()
p=Prefab("ChoiceButton");p.button(p.root);label(p,p.root,"Label","Continue",20);p.write()
p=Prefab("MainMenuScreen",True);p.image(p.root,"Background");h=p.obj("Header",p.root);mt=label(p,h,"Title","DAZPOSE",32);ms=label(p,h,"Subtitle","Choose a scene",18,"Muted");l=p.obj("SceneListPanel",p.root);p.image(l);_,_,sc=p.scroll(l,"Scenes");r=p.obj("SceneDetails",p.root);p.image(r);t=label(p,r,"SelectedTitle","First Contact",28);d=label(p,r,"SelectedDescription","",18,"Muted");e=label(p,r,"NavigationError","",18,"Error");st=btn(p,r,"Start","Start");se=btn(p,p.root,"Settings","Settings");q=btn(p,p.root,"Quit","Quit");p.rect(h,(.05,.87),(.95,.99));p.rect(owner(p,mt),(.02,.45),(.70,.95));p.rect(owner(p,ms),(.02,.05),(.75,.42));p.rect(l,(.04,.14),(.38,.85));p.rect(r,(.41,.14),(.96,.85));p.rect(next(o for o in p.os if o["name"]=="Settings"),(.04,.03),(.19,.10));p.rect(next(o for o in p.os if o["name"]=="Quit"),(.81,.03),(.96,.10));
for o in p.os:
 if o["name"]=="Scenes":p.rect(o,(.03,.03),(.97,.97))
 if o["name"]=="Start":p.rect(o,(.70,.05),(.96,.17))
p.rect(next(o for o in p.os if o["name"]=="SelectedTitle"),(.05,.75),(.95,.94));p.rect(next(o for o in p.os if o["name"]=="SelectedDescription"),(.05,.34),(.95,.73));p.rect(next(o for o in p.os if o["name"]=="NavigationError"),(.05,.20),(.95,.32))
mainmenuView=p.script(p.root,G["Browser"],{"scenes":f"{{fileID: {sc}}}","selectedTitle":f"{{fileID: {t}}}","selectedDescription":f"{{fileID: {d}}}","errorText":f"{{fileID: {e}}}","startButton":f"{{fileID: {st}}}","settingsButton":f"{{fileID: {se}}}","quitButton":f"{{fileID: {q}}}"});p.script(p.root,G["MenuController"],{"view":f"{{fileID: {mainmenuView}}}"});p.write()
theme="""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: UiTheme
  m_EditorClassIdentifier:
  m_Script: {fileID: 11500000, guid: 7bd837806d254bc38ebf96685c62be02, type: 3}
  background: {r: 0.025, g: 0.031, b: 0.047, a: 1}
  panel: {r: 0.065, g: 0.073, b: 0.098, a: 1}
  control: {r: 0.12, g: 0.14, b: 0.19, a: 1}
  accent: {r: 0.75, g: 0.36, b: 0.88, a: 1}
  text: {r: 0.95, g: 0.94, b: 0.98, a: 1}
  muted: {r: 0.66, g: 0.67, b: 0.74, a: 1}
  error: {r: 1, g: 0.48, b: 0.51, a: 1}
  hover: {r: 0.24, g: 0.20, b: 0.31, a: 1}
  selected: {r: 0.38, g: 0.23, b: 0.47, a: 1}
  fontAsset: {fileID: 11400000, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}
  fontShader: {fileID: 4800000, guid: fe393ace9b354375a9cb14cdbbc28be4, type: 3}
"""
tp=UI/"UiTheme.asset";tp.write_text(theme,encoding="utf-8");meta(tp,"NativeFormatImporter")
def ref(fid,g):return f"{{fileID: {fid}, guid: {g}, type: 3}}"
mp={"modalShell":(modalView,"ModalShell.prefab"),"profilePanel":(10002,"ProfileCatalogPanel.prefab"),"interestRow":(10001,"InterestRow.prefab"),"equipmentRow":(10001,"EquipmentRow.prefab"),"clothingRow":(10001,"ClothingRow.prefab"),"sceneCard":(10001,"SceneCard.prefab"),"choiceButton":(10001,"ChoiceButton.prefab"),"mainMenu":(mainmenuView,"MainMenuScreen.prefab")}
lines=["%YAML 1.1","%TAG !u! tag:unity3d.com,2011:","--- !u!114 &11400000","MonoBehaviour:","  m_ObjectHideFlags: 0","  m_CorrespondingSourceObject: {fileID: 0}","  m_PrefabInstance: {fileID: 0}","  m_PrefabAsset: {fileID: 0}","  m_Name: ProductionUi","  m_Script: {fileID: 11500000, guid: 12a4fedd685e411991ed4a3f524deb27, type: 3}","  theme: "+ref(11400000,g("Production/UI/UiTheme.asset"))]
for field,(fid,file) in mp.items():lines.append(f"  {field}: "+ref(fid,g("Production/UI/"+file)))
lines.append("  settings: "+ref(3376075008117195258,"f6e3324f4b8c40543bd8b925e9bf95d1"))
lib=RES/"ProductionUi.asset";lib.write_text("\n".join(lines)+"\n",encoding="utf-8");meta(lib,"NativeFormatImporter")
print("generated 8 prefab templates, theme, and resource library")






# Offline, deterministic replacement of only the obsolete main-menu UI tree.
# Keep the saved EventSystem and the existing DazPose application object intact.
def integrate_saved_menu():
 import re
 scene=ROOT/"Production"/"Scenes"/"MainMenu.unity"
 original=scene.read_text(encoding="utf-8-sig")
 docs=re.split(r"(?=^--- !u!\d+ &\d+\s*$)",original,flags=re.M)
 def header(doc):
  m=re.match(r"--- !u!(\d+) &(\d+)",doc)
  return (int(m.group(1)),int(m.group(2))) if m else None
 go_docs={}
 all_go_docs=[]
 trans={}
 trans_parent={}
 for doc in docs:
  h=header(doc)
  if not h: continue
  cls,fid=h
  if cls==1:
   nm=re.search(r"^  m_Name: (.*)$",doc,re.M)
   if nm:
    go_docs[nm.group(1)]=(fid,doc)
    all_go_docs.append((nm.group(1),fid,doc))
  if cls in (4,224):
   gm=re.search(r"^  m_GameObject: \{fileID: (\d+)\}$",doc,re.M)
   if gm:
    trans[gm.group(1)]=fid
    fa=re.search(r"^  m_Father: \{fileID: (\d+)\}$",doc,re.M)
    trans_parent[fid]=int(fa.group(1)) if fa else 0
 if "Main Menu" not in go_docs or "DazPose" not in go_docs or "Event System" not in go_docs:
  raise RuntimeError("Expected Main Menu, DazPose application object, and Event System in saved scene.")
 old_root,rootdoc=go_docs["Main Menu"]; app_go,appdoc=go_docs["DazPose"]
 def compids(doc): return [int(x) for x in re.findall(r"^  - component: \{fileID: (\d+)\}$",doc,re.M)]
 old_root_tr=compids(rootdoc)[0]; app_tr=compids(appdoc)[0]
 # Identify all old menu descendants by local transform parent edges.
 descendant={old_root_tr}; changed=True
 while changed:
  changed=False
  for child,parent in list(trans_parent.items()):
   if child not in descendant and parent in descendant:
    descendant.add(child);changed=True
 app_trans=int(trans[str(app_go)]); doomed_gos={}
 for name,gid,doc in all_go_docs:
  if name not in ("Main Menu","DazPose","Event System"):
   doomed_gos[gid]=doc
 # Replace the original root component set as well, retaining the same root GO and Transform IDs.
 doomed_gos[old_root]=rootdoc
 doomed_ids=set(doomed_gos)
 doomed_trans={trans[str(x)] for x in doomed_ids if str(x) in trans}
 doomed_comps={c for d in doomed_gos.values() for c in compids(d)}|doomed_trans|doomed_ids
 # MainMenu has only one prefab instance: the old Settings overlay, now loaded from ProductionUi at runtime.
 removed_instances=set()
 for doc in docs:
  h=header(doc)
  if h and h[0]==1001:
   removed_instances.add(h[1]);doomed_ids.add(h[1]);doomed_comps.add(h[1])
 kept=[]
 for doc in docs:
  h=header(doc)
  if h and h[1] in doomed_comps: continue
  if h and re.search(r"m_PrefabInstance: \{fileID: (\d+)\}",doc) and int(re.search(r"m_PrefabInstance: \{fileID: (\d+)\}",doc).group(1)) in removed_instances: continue
  kept.append(doc)
 # Clone generated menu objects/components into the saved scene, reusing the original scene root IDs.
 prefab=(UI/"MainMenuScreen.prefab").read_text(encoding="utf-8-sig")
 pd=[x for x in re.split(r"(?=^--- !u!\d+ &\d+\s*$)",prefab,flags=re.M) if header(x)]
 pgo=None;ptr=None
 for doc in pd:
  cls,fid=header(doc)
  if cls==1 and re.search(r"^  m_Name: MainMenuScreen$",doc,re.M):pgo=fid
  if cls==224 and re.search(r"^  m_Father: \{fileID: 0\}$",doc,re.M):ptr=fid
 if pgo is None or ptr is None:raise RuntimeError("Cannot identify generated MainMenuScreen prefab root documents.")
 # Map every prefab document anchor to a globally unique scene local ID.
 local_ids=[header(d)[1] for d in pd]
 mapping={pgo:old_root,ptr:old_root_tr}
 nextid=7000000000000000000
 for fid in local_ids:
  if fid not in mapping:
   mapping[fid]=nextid;nextid+=1
 cloned=[]
 for doc in pd:
  cls,fid=header(doc)
  def replace_ref(m):
   n=int(m.group(1));return "fileID: "+str(mapping.get(n,n))
  d=re.sub(r"fileID: (\d+)",replace_ref,doc)
  d=re.sub(r"(?<=&)\d+",lambda m:str(mapping.get(int(m.group(0)),int(m.group(0)))),d,count=1)
  if cls==1 and fid==pgo:
   d=re.sub(r"^  m_Name: MainMenuScreen$","  m_Name: Main Menu",d,flags=re.M)
   d=re.sub(r"^  m_IsActive: 0$","  m_IsActive: 1",d,flags=re.M)
  if cls==224 and fid==ptr:
   # Preserve the existing app bootstrap object under the new menu canvas.
   d=re.sub(r"(  m_Children:\n(?:  - \{fileID: \d+\}\n)*)",lambda m:m.group(1)+f"  - {{fileID: {app_trans}}}\n",d,count=1)
  cloned.append(d)
 # Reparent preserved app object from the deleted legacy panel onto the new root.
 for i,doc in enumerate(kept):
  h=header(doc)
  if h and h[0] in (4,224) and h[1]==app_trans:
   kept[i]=re.sub(r"^  m_Father: \{fileID: \d+\}$",f"  m_Father: {{fileID: {old_root_tr}}}",doc,flags=re.M)
   break
 else: raise RuntimeError("Could not preserve the DazPose application object's Transform.")
 root_index=next((i for i,d in enumerate(kept) if d.startswith("--- !u!1660057539 &")),len(kept))
 kept[root_index:root_index]=cloned
 # Re-running is safe: stable generated IDs may repeat and identical YAML docs are collapsed.
 unique=[];by_id={}
 for doc in kept:
  h=header(doc)
  if not h: unique.append(doc);continue
  if h[1] in by_id:
   if by_id[h[1]]!=doc:raise RuntimeError(f"Conflicting MainMenu YAML document ID {h[1]}.")
   continue
  by_id[h[1]]=doc;unique.append(doc)
 scene.write_text("\n".join(line.rstrip() for line in "".join(unique).splitlines()).rstrip()+"\n",encoding="utf-8")

integrate_saved_menu()
