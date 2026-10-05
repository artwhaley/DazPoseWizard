"""Derive owned Specular/Metallic graphs using the existing SSS dissolve wiring.

Retains every family-specific node/property and replaces only active alpha and
emission inputs. Refuses unexpected graph layouts; vendor files stay untouched.
"""
import copy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'validation/DazPoseUnityValidation/Assets'
OUTPUT = ROOT / 'DazPose/Effects/Dissolve/Shaders'
SOURCE = ROOT / 'Daz3D/Shaders/uDTU'
def unpack(w): return json.loads(w['JSONnodeData'])
def pack(w, data):
    w['JSONnodeData'] = json.dumps(data, separators=(',', ':'))
    return w
def node_id(w): return unpack(w)['m_GuidSerialized']
def main():
    base = json.loads((SOURCE / 'uDTU HDRP.SSS.shadergraph').read_text())
    owned = json.loads((OUTPUT / 'uDTU HDRP SSS Dissolve.shadergraph').read_text())
    ids = {node_id(w) for w in base['m_SerializableNodes']}
    extra_nodes = [w for w in owned['m_SerializableNodes'] if node_id(w) not in ids]
    extras = {node_id(w) for w in extra_nodes}
    extra_props = owned['m_SerializedProperties'][len(base['m_SerializedProperties']):]
    assert len(extra_props) >= 8
    active = owned['m_ActiveOutputNodeGuidSerialized']
    custom = next(node_id(w) for w in extra_nodes if 'CustomFunction' in w['typeInfo']['fullName'])
    master_owned = unpack(next(w for w in owned['m_SerializableNodes'] if node_id(w) == active))
    clip = next(s for s in master_owned['m_SerializableSlots'] if unpack(s)['m_Id'] == 18)
    for family in ['Specular', 'Metallic', 'Hair']:
        graph = json.loads((SOURCE / f'uDTU HDRP.{family}.shadergraph').read_text())
        family_active=graph['m_ActiveOutputNodeGuidSerialized']
        old_properties = copy.deepcopy(graph['m_SerializedProperties'])
        master_wrapper = next(w for w in graph['m_SerializableNodes'] if node_id(w) == family_active)
        master = unpack(master_wrapper)
        master['m_AlphaTest'] = True
        alpha_slot,emission_slot=(12,11) if family=='Hair' else (17,13)
        if family!='Hair':master['m_SerializableSlots'].append(copy.deepcopy(clip))
        if family=='Hair':
            emission=copy.deepcopy(next(s for s in master_owned['m_SerializableSlots'] if unpack(s)['m_Id']==13))
            data=unpack(emission);data['m_Id']=11;pack(emission,data);master['m_SerializableSlots'].append(emission)
        pack(master_wrapper, master)
        source_edges = {}
        kept = []
        for w in graph['m_SerializableEdges']:
            e = unpack(w)
            if e['m_InputSlot']['m_NodeGUIDSerialized'] == family_active and e['m_InputSlot']['m_SlotId'] in (alpha_slot,emission_slot):
                source_edges[e['m_InputSlot']['m_SlotId']] = e['m_OutputSlot']
            else: kept.append(w)
        assert alpha_slot in source_edges
        for w in owned['m_SerializableEdges']:
            e = unpack(w)
            if e['m_InputSlot']['m_NodeGUIDSerialized'] in extras or e['m_OutputSlot']['m_NodeGUIDSerialized'] in extras:
                e = copy.deepcopy(e)
                if e['m_InputSlot']['m_NodeGUIDSerialized'] == custom and e['m_InputSlot']['m_SlotId'] in (9,10):
                    slot=alpha_slot if e['m_InputSlot']['m_SlotId']==9 else emission_slot
                    if slot not in source_edges:continue
                    e['m_OutputSlot']=source_edges[slot]
                if e['m_InputSlot']['m_NodeGUIDSerialized']==active:
                    slot=e['m_InputSlot']['m_SlotId']
                    if slot==18 and family=='Hair':continue
                    e['m_InputSlot']={'m_NodeGUIDSerialized':family_active,'m_SlotId':alpha_slot if slot==17 else emission_slot if slot==13 else slot}
                kept.append(pack(copy.deepcopy(w),e))
        graph['m_SerializableEdges'] = kept
        graph['m_SerializableNodes'] += copy.deepcopy(extra_nodes)
        graph['m_SerializedProperties'] += copy.deepcopy(extra_props)
        graph['m_Path'] = 'DazPose/Performer'
        assert graph['m_SerializedProperties'][:len(old_properties)] == old_properties
        path = OUTPUT / f'uDTU HDRP {family} Dissolve.shadergraph'
        path.write_text(json.dumps(graph,indent=2)+'\n')
        print(f'{family}: preserved {len(old_properties)} properties, added eight controls and alpha/emission field wiring')

if __name__ == '__main__': main()
