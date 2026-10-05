"""Create explicitly transparent wardrobe dissolve graphs; preserve vendor/base graphs."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]/'validation/DazPoseUnityValidation/Assets/DazPose/Effects/Dissolve/Shaders'
for family in ('Metallic','Specular'):
 source=ROOT/f'uDTU HDRP {family} Dissolve.shadergraph'
 graph=json.loads(source.read_text())
 wrapper=next(w for w in graph['m_SerializableNodes'] if json.loads(w['JSONnodeData'])['m_GuidSerialized']==graph['m_ActiveOutputNodeGuidSerialized'])
 master=json.loads(wrapper['JSONnodeData']);master['m_SurfaceType']=1;master['m_AlphaTest']=True
 for slot in master['m_SerializableSlots']:
  data=json.loads(slot['JSONnodeData'])
  if data['m_Id']==18:
   data['m_Value']=.0001;data['m_DefaultValue']=.0001
   slot['JSONnodeData']=json.dumps(data,separators=(',',':'))
 wrapper['JSONnodeData']=json.dumps(master,separators=(',',':'))
 output=ROOT/f'uDTU HDRP {family} Transparent Dissolve.shadergraph'
 output.write_text(json.dumps(graph,indent=2)+'\n')
 print(output.name)
