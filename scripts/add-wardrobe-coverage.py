"""Add an optional UV3 coverage mask to owned graphs, preserving vendor graphs."""
import copy,json,uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]/'validation/DazPoseUnityValidation/Assets/DazPose/Effects/Dissolve/Shaders'
def data(w):return json.loads(w['JSONnodeData'])
def pack(w,n):w['JSONnodeData']=json.dumps(n,separators=(',',':'));return w
def upgrade(g):
 custom=next(w for w in g['m_SerializableNodes'] if data(w).get('m_FunctionName')=='PerformerDissolveEvaluate');c=data(custom)
 coverage=next(s for s in c['m_SerializableSlots'] if data(s)['m_Id']==14);s=data(coverage)
 s['m_Value']=s['m_DefaultValue']={'x':0,'y':0};s['m_Labels']=['X','Y'];coverage['typeInfo']['fullName']='UnityEditor.ShaderGraph.Vector2MaterialSlot';pack(coverage,s)
 if not any(data(p).get('m_OverrideReferenceName')=='_WardrobeDressCoverageEnabled' for p in g['m_SerializedProperties']):
  template=next(p for p in g['m_SerializedProperties'] if data(p).get('m_OverrideReferenceName')=='_DissolveEnabled')
  prop=copy.deepcopy(template);p=data(prop);pid=str(uuid.uuid5(uuid.NAMESPACE_DNS,'Lara.DressCoverage.Property'));p['m_Guid']['m_GuidSerialized']=pid;p['m_Name']='Dress Coverage Enabled';p['m_OverrideReferenceName']='_WardrobeDressCoverageEnabled';p['m_Value']=0;pack(prop,p);g['m_SerializedProperties'].append(prop)
  source=next(n for n in g['m_SerializableNodes'] if data(n).get('m_PropertyGuidSerialized')==data(template)['m_Guid']['m_GuidSerialized'])
  node=copy.deepcopy(source);n=data(node);nid=str(uuid.uuid5(uuid.NAMESPACE_DNS,'Lara.DressCoverage.Node'));n['m_GuidSerialized']=nid;n['m_PropertyGuidSerialized']=pid;pack(node,n);g['m_SerializableNodes'].append(node)
  slot=copy.deepcopy(next(s for s in c['m_SerializableSlots'] if data(s)['m_Id']==4));s=data(slot);s['m_Id']=16;s['m_DisplayName']=s['m_ShaderOutputName']='DressCoverageEnabled';s['m_Value']=s['m_DefaultValue']=0;pack(slot,s);c['m_SerializableSlots'].append(slot)
  edge=copy.deepcopy(g['m_SerializableEdges'][0]);g['m_SerializableEdges'].append(pack(edge,{'m_OutputSlot':{'m_NodeGUIDSerialized':nid,'m_SlotId':0},'m_InputSlot':{'m_NodeGUIDSerialized':c['m_GuidSerialized'],'m_SlotId':16}}))
 pack(custom,c)
 for node in g['m_SerializableNodes']:
  if 'HairMaster' not in node['typeInfo']['fullName']:continue
  n=data(node);n['m_SpecularAA']=True
  for index,name,value in [(16,'Specular AA Variance',.1),(17,'Specular AA Threshold',.2)]:
   if any(data(s)['m_Id']==index for s in n['m_SerializableSlots']):continue
   slot=copy.deepcopy(next(s for s in c['m_SerializableSlots'] if data(s)['m_Id']==4));s=data(slot);s['m_Id']=index;s['m_DisplayName']=name;s['m_ShaderOutputName']=name;s['m_Value']=s['m_DefaultValue']=value;pack(slot,s);n['m_SerializableSlots'].append(slot)
  pack(node,n)
def main():
 for file in ROOT.glob('*.shadergraph'):
  g=json.loads(file.read_text());custom=next((w for w in g['m_SerializableNodes'] if 'CustomFunction' in w['typeInfo']['fullName'] and data(w).get('m_FunctionName')=='PerformerDissolveEvaluate'),None)
  if not custom:continue
  if any(data(p).get('m_OverrideReferenceName')=='_WardrobeCoverageEnabled' for p in g['m_SerializedProperties']):
   upgrade(g);file.write_text(json.dumps(g,indent=2)+'\n');print('Coverage upgraded:',file.name);continue
  template=next(p for p in g['m_SerializedProperties'] if data(p).get('m_OverrideReferenceName')=='_DissolveEnabled')
  prop=copy.deepcopy(template);p=data(prop);pid=str(uuid.uuid5(uuid.NAMESPACE_DNS,'Lara.WardrobeCoverage.Property'))
  p['m_Guid']['m_GuidSerialized']=pid;p['m_Name']='Wardrobe Coverage Enabled';p['m_OverrideReferenceName']='_WardrobeCoverageEnabled';p['m_Value']=0;pack(prop,p);g['m_SerializedProperties'].append(prop)
  source=next(n for n in g['m_SerializableNodes'] if data(n).get('m_PropertyGuidSerialized')==data(template)['m_Guid']['m_GuidSerialized'])
  node=copy.deepcopy(source);n=data(node);enabledid=str(uuid.uuid5(uuid.NAMESPACE_DNS,'Lara.WardrobeCoverage.EnabledNode'));n['m_GuidSerialized']=enabledid;n['m_PropertyGuidSerialized']=pid;pack(node,n);g['m_SerializableNodes'].append(node)
  # UV node is built using the same serializable slot format as ordinary graph nodes.
  uv=copy.deepcopy(node);u=data(uv);u.pop('m_PropertyGuidSerialized',None);uvid=str(uuid.uuid5(uuid.NAMESPACE_DNS,'Lara.WardrobeCoverage.UVNode'))
  u['m_GuidSerialized']=uvid;u['m_Name']='UV';u['m_OutputChannel']=3
  uv['typeInfo']['fullName']='UnityEditor.ShaderGraph.UVNode'
  slot=u['m_SerializableSlots'][0];s=data(slot);s['m_DisplayName']='Out';s['m_Value']={'x':0,'y':0,'z':0,'w':0};s['m_DefaultValue']={'x':0,'y':0,'z':0,'w':0};s['m_Labels']=['X','Y','Z','W'];slot['typeInfo']['fullName']='UnityEditor.ShaderGraph.Vector4MaterialSlot';pack(slot,s);pack(uv,u);g['m_SerializableNodes'].append(uv)
  c=data(custom);cid=c['m_GuidSerialized'];input_template=next(s for s in c['m_SerializableSlots'] if data(s)['m_Id']==4)
  for index,name in [(14,'Coverage'),(15,'CoverageEnabled')]:
   slot=copy.deepcopy(input_template);s=data(slot);s['m_Id']=index;s['m_DisplayName']=name;s['m_ShaderOutputName']=name;s['m_Value']=s['m_DefaultValue']=0;pack(slot,s);c['m_SerializableSlots'].append(slot)
  pack(custom,c)
  edge=copy.deepcopy(g['m_SerializableEdges'][0])
  for src,dst in [(uvid,14),(enabledid,15)]:
   g['m_SerializableEdges'].append(pack(copy.deepcopy(edge),{'m_OutputSlot':{'m_NodeGUIDSerialized':src,'m_SlotId':0},'m_InputSlot':{'m_NodeGUIDSerialized':cid,'m_SlotId':dst}}))
  upgrade(g);file.write_text(json.dumps(g,indent=2)+'\n');print('Coverage added:',file.name)
if __name__=='__main__':main()
