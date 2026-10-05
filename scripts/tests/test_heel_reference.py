"""Guard tests with synthetic FBX parser data; no Blender or user exports needed."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('heel', Path(__file__).parents[1]/'prepare-lara-heel-reference.py')
heel = importlib.util.module_from_spec(spec)
spec.loader.exec_module(heel)

def element(key, props=(), elems=()):
    return SimpleNamespace(id=key, props=props, elems=elems)

class HeelReferenceGuards(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.base = [0.,10.,0., 0.,45.,0., 0.,150.,0.]
        self.posed = [0.,20.,0., 0.,45.23265,0., 0.,150.,0.]
        self.reference = self.root/'reference.fbx'; self.reference.write_bytes(b'approved reference')
        self.source = self.root/'source.fbx'; self.source.write_bytes(b'posed source')
        self.duf = self.root/'source.duf'
        self.controls = [.75,.75]
        self.calibration = self.root/'calibration.json'
        self.calibration.write_text(json.dumps({'schemaVersion':1,
            'referenceSHA256':hashlib.sha256(self.reference.read_bytes()).hexdigest(),
            'minimumControl':0,'maximumControl':1,'maximumResidualMeters':2e-6,
            'entries':[{'index':1,'deltaPerUnit':[0,.0023265/.75,0]}]}))
        self.out = self.root/'out.json'

    def parse(self, path):
        vertices = self.base if Path(path)==self.reference else self.posed
        mesh = element(b'Geometry', (1,b'Genesis8Female\x00\x01Geometry',b'Mesh'),
            [element(b'Vertices',(vertices,)),element(b'PolygonVertexIndex',([0,1,-3],))])
        return element(b'Root',elems=[element(b'Objects',elems=[mesh])]),7400

    def run_helper(self, calibrated=True):
        self.duf.write_text(json.dumps({'scene':{'modifiers':[
            {'id':'pCTRL'+side+'FootTipToes','parent':'#Genesis8Female','channel':{'current_value':value}}
            for side,value in zip(['l','r'],self.controls)]}}))
        argv = ['heel','--fbx',str(self.source),'--reference',str(self.reference),'--out',str(self.out)]
        if calibrated: argv += ['--duf',str(self.duf),'--calibration',str(self.calibration)]
        with patch.object(heel.sys,'argv',argv),patch.object(heel.importlib,'import_module',return_value=SimpleNamespace(parse=self.parse)):
            heel.main()

    def test_classified_leg_is_transferred(self):
        self.run_helper()
        report=json.loads(self.out.read_text())
        self.assertEqual([e['index'] for e in report['entries']],[0,1])
        self.assertGreater(report['maximumMeters']['upper'],.0015)
        self.assertEqual(report['maximumUnexplainedUpperMeters'],0)

    def test_original_guard_still_rejects_without_calibration(self):
        with self.assertRaisesRegex(ValueError,'Unexplained body change'): self.run_helper(False)

    def test_wrong_leg_pattern_rejected(self):
        self.posed[4]+=.01
        with self.assertRaisesRegex(ValueError,'differs from the known tiptoe'): self.run_helper()

    def test_unrelated_body_change_rejected(self):
        self.posed[7]+=.2
        with self.assertRaisesRegex(ValueError,'Unexplained body change'): self.run_helper()

    def test_asymmetric_pose_rejected(self):
        self.controls[1]=.5
        with self.assertRaisesRegex(ValueError,'paired equal controls'): self.run_helper()

if __name__=='__main__': unittest.main()
