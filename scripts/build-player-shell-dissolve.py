"""Add the existing dissolve field to the prepared shell graph without losing its root fade.
The source shader, material, mesh and contact measurements are never modified.
"""
import copy
import json
from pathlib import Path
from importlib.util import module_from_spec, spec_from_file_location
ROOT=Path(__file__).resolve().parents[1]/'validation/DazPoseUnityValidation/Assets'
OUTPUT=ROOT/'DazPose/Effects/Dissolve/Shaders'
def unpack(w): return json.loads(w['JSONnodeData'])
def pack(w,data):
    w['JSONnodeData']=json.dumps(data,separators=(',',':'))
    return w
def node_id(w): return unpack(w)['m_GuidSerialized']
base=json.loads((ROOT/'Daz3D/Shaders/uDTU/uDTU HDRP.SSS.shadergraph').read_text())
owned=json.loads((OUTPUT/'uDTU HDRP SSS Dissolve.shadergraph').read_text())
graph=json.loads((ROOT/'PartnerProof/PartnerShell.shadergraph').read_text())
original_properties=copy.deepcopy(graph['m_SerializedProperties'])
ids={node_id(w) for w in base['m_SerializableNodes']}
extra=[w for w in owned['m_SerializableNodes'] if node_id(w) not in ids]
extras={node_id(w) for w in extra}
custom=next(node_id(w) for w in extra if 'CustomFunction' in w['typeInfo']['fullName'])
active=owned['m_ActiveOutputNodeGuidSerialized']; target=graph['m_ActiveOutputNodeGuidSerialized']
master_owned=unpack(next(w for w in owned['m_SerializableNodes'] if node_id(w)==active))
wrapper=next(w for w in graph['m_SerializableNodes'] if node_id(w)==target)
master=unpack(wrapper)
assert master['m_SurfaceType']==1, 'Prepared shell must remain transparent'
master['m_AlphaTest']=True
clip=copy.deepcopy(next(s for s in master_owned['m_SerializableSlots'] if unpack(s)['m_Id']==18))
clip_data=unpack(clip);clip_data['m_Value']=.0001;clip_data['m_DefaultValue']=.0001;pack(clip,clip_data)
master['m_SerializableSlots']=[s for s in master['m_SerializableSlots'] if unpack(s)['m_Id']!=18]+[clip]
pack(wrapper,master)
source_edges={};kept=[]
for w in graph['m_SerializableEdges']:
    e=unpack(w)
    if e['m_InputSlot']['m_NodeGUIDSerialized']==target and e['m_InputSlot']['m_SlotId'] in (17,13):
        source_edges[e['m_InputSlot']['m_SlotId']]=e['m_OutputSlot']
    else:kept.append(w)
assert 17 in source_edges
for w in owned['m_SerializableEdges']:
    e=unpack(w)
    if e['m_InputSlot']['m_NodeGUIDSerialized'] in extras or e['m_OutputSlot']['m_NodeGUIDSerialized'] in extras:
        e=copy.deepcopy(e)
        if e['m_InputSlot']['m_NodeGUIDSerialized']==custom and e['m_InputSlot']['m_SlotId'] in (9,10):
            slot=17 if e['m_InputSlot']['m_SlotId']==9 else 13
            if slot not in source_edges:continue
            e['m_OutputSlot']=source_edges[slot]
        if e['m_InputSlot']['m_NodeGUIDSerialized']==active:e['m_InputSlot']['m_NodeGUIDSerialized']=target
        kept.append(pack(copy.deepcopy(w),e))
graph['m_SerializableEdges']=kept
graph['m_SerializableNodes']+=copy.deepcopy(extra)
graph['m_SerializedProperties']+=copy.deepcopy(owned['m_SerializedProperties'][len(base['m_SerializedProperties']):])
graph['m_Path']='DazPose/Player'
assert graph['m_SerializedProperties'][:len(original_properties)]==original_properties
(OUTPUT/'Player Shell Dissolve.shadergraph').write_text(json.dumps(graph,indent=2)+'\n')
print('Prepared shell properties, root-fade alpha input and transparent master preserved; dissolve field added.')